using System.Diagnostics;
using Npgsql;
using RadarPlural.Api;
using RadarPlural.Ingestion;
using RadarPlural.Infrastructure;
using RadarPlural.Pipeline;

// Coletor de notícias: roda uma vez, percorre as fontes aprovadas e termina.
// Agendado pelo GitHub Actions (.github/workflows/ingestao.yml); pode rodar à mão:
//   dotnet run --project backend/RadarPlural.Worker -- --dry-run            (não grava; mostra o que coletaria)
//   dotnet run --project backend/RadarPlural.Worker -- --fonte tecnoblog    (só uma fonte)
// Banco: variável ConnectionStrings__Default (mesma da API; aceita URL postgresql://).
//
// Modo etapa (docs/decisoes/0004): processa histórias de uma etapa do pipeline. Pode rodar em N réplicas ao mesmo tempo.
//   dotnet run --project backend/RadarPlural.Worker -- --etapa auditoria [--lote 10] [--continuo]

if (ArgValue("--etapa") is { } stageName) return await RunStageAsync(stageName);

var dryRun = args.Contains("--dry-run");
var only = ArgValue("--fonte");
var catalogPath = ArgValue("--fontes") ?? Path.Combine(AppContext.BaseDirectory, "fontes.json");
var policy = new IngestionPolicy();

IReadOnlyList<FeedSource> sources;
try
{
    sources = SourceCatalog.Parse(await File.ReadAllTextAsync(catalogPath));
}
catch (Exception e) when (e is IngestionException or IOException)
{
    Log($"ERRO lista de fontes ({catalogPath}): {e.Message}");
    return 2;
}
var allowed = SourceCatalog.AllowedHosts(sources); // a lista de permissão é sempre a lista inteira
var catalog = sources.ToDictionary(x => x.Id); // retriagem usa a lista inteira, mesmo com --fonte
if (only is not null) sources = sources.Where(s => s.Id == only).ToList();
if (sources.Count == 0)
{
    Log($"ERRO fonte '{only}' não existe em fontes.json.");
    return 2;
}

FeedItemStore? store = null;
NpgsqlDataSource? dataSource = null;
if (!dryRun)
{
    var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
    if (string.IsNullOrWhiteSpace(connection))
    {
        Log("ERRO defina ConnectionStrings__Default ou use --dry-run.");
        return 2;
    }
    dataSource = NpgsqlDataSource.Create(DatabaseUrl.ToNpgsql(connection));
    store = new FeedItemStore(dataSource);
}

using var http = SafeHttp.CreateClient(policy);
var fetcher = new SafeFeedFetcher(http, policy);
using var shutdown = new CancellationTokenSource(TimeSpan.FromMinutes(10));
Console.CancelKeyPress += (_, e) => { e.Cancel = true; shutdown.Cancel(); };

int failures = 0, totalNew = 0, totalRead = 0, totalDiscarded = 0;
var dryRunCandidates = new List<ClusterCandidate>();
foreach (var source in sources)
{
    var clock = Stopwatch.StartNew();
    try
    {
        var bytes = await fetcher.FetchAsync(source.Url, allowed, shutdown.Token);
        var entries = FeedParser.Parse(new MemoryStream(bytes), source.Url, policy);
        totalRead += entries.Count;
        var now = DateTimeOffset.UtcNow;
        var triaged = entries.Select(e => (Entry: e, Triage: Triage.Evaluate(source, e, now))).ToList();
        var discarded = triaged.Count(t => !t.Triage.Accepted);
        totalDiscarded += discarded;
        if (store is null)
        {
            Log($"OK   {source.Id}: {entries.Count} itens, {discarded} descartados na triagem ({bytes.Length / 1024} KB, {clock.ElapsedMilliseconds} ms) [dry-run]");
            foreach (var t in triaged.Where(t => !t.Triage.Accepted).Take(2)) Log($"       x {t.Triage.DiscardReason}: {t.Entry.Title}");
            dryRunCandidates.AddRange(triaged.Where(t => t.Triage.Accepted)
                .Select(t => new ClusterCandidate(Guid.NewGuid(), source.Id, t.Entry.Title, t.Entry.PublishedAt ?? now)));
        }
        else
        {
            var inserted = await store.SaveAsync(source, triaged, now, shutdown.Token);
            totalNew += inserted;
            Log($"OK   {source.Id}: {entries.Count} itens, {inserted} novos, {discarded} descartados na triagem ({clock.ElapsedMilliseconds} ms)");
        }
    }
    catch (IngestionException e)
    {
        failures++;
        Log($"FALHA {source.Id}: {e.Message}");
    }
    catch (NpgsqlException e)
    {
        Log($"ERRO banco: {e.Message}. A API já aplicou as migrações (tabela FeedItems)?");
        return 3;
    }
    await Task.Delay(TimeSpan.FromSeconds(1), shutdown.Token); // educação com os servidores das fontes
}

// Agrupamento: junta a mesma notícia publicada por veículos diferentes (decisoes/0003).
if (dataSource is not null && failures < sources.Count)
{
    try
    {
        var storyStore = new StoryStore(dataSource);
        var retriaged = await storyStore.RetriageAsync(catalog, DateTimeOffset.UtcNow, shutdown.Token);
        if (retriaged > 0) Log($"TRIA {retriaged} notícias já gravadas descartadas pelas regras atuais");
        var (stories, created, corroborated) = await storyStore.GroupAsync(DateTimeOffset.UtcNow, shutdown.Token);
        Log($"HIST {stories} histórias nas últimas 72 h ({created} novas), {corroborated} com 2+ fontes");
    }
    catch (NpgsqlException e)
    {
        Log($"ERRO banco no agrupamento: {e.Message}. A API já aplicou a migração TriagemHistorias?");
        return 3;
    }
}
else if (dataSource is null)
{
    var byId = dryRunCandidates.ToDictionary(c => c.Id);
    var groups = StoryClusterer.Cluster(dryRunCandidates).Where(g => g.Select(i => byId[i].SourceId).Distinct().Count() >= 2).OrderByDescending(g => g.Count).ToList();
    Log($"HIST {groups.Count} histórias com 2+ fontes [dry-run]");
    foreach (var g in groups.Take(5))
    {
        Log($"       {g.Count} itens:");
        foreach (var i in g) Log($"         {byId[i].SourceId}: {byId[i].Title}");
    }
}

Log($"FIM  {sources.Count} fontes, {failures} com falha, {totalRead} itens lidos, {totalDiscarded} descartados" + (store is null ? "" : $", {totalNew} novos"));
if (dataSource is not null) await dataSource.DisposeAsync();
// Uma fonte fora do ar não é erro da execução; todas falhando indica problema nosso (rede, política, parser).
return failures == sources.Count ? 1 : 0;

async Task<int> RunStageAsync(string name)
{
    var stage = Stages.Find(name);
    if (stage is null)
    {
        Log($"ERRO etapa '{name}' não existe. Etapas: {string.Join(", ", Stages.All.Select(s => s.Name))}.");
        return 2;
    }
    var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
    if (string.IsNullOrWhiteSpace(connection) && stage.Name == "auditoria")
    {
        Log("ERRO defina ConnectionStrings__Default.");
        return 2;
    }
    await using var db = NpgsqlDataSource.Create(DatabaseUrl.ToNpgsql(connection ?? "Host=localhost"));
    // Adaptadores de cada etapa (portas em RadarPlural.Pipeline/Ports.cs).
    // auditoria: simulado (B05). Gemini entra na B09 trocando o IAuditor. roteiro = #19; video = #20.
    IStageHandler? handler = stage.Name switch
    {
        "auditoria" => new AuditStageHandler(new PgStoryReader(db), new SimulatedAuditor(TimeProvider.System), new PgAuditStore(db)),
        _ => null
    };
    if (handler is null)
    {
        Log($"ERRO etapa '{stage.Name}' ainda sem adaptador configurado. Ver RadarPlural.Pipeline/Ports.cs e docs/decisoes/0004.");
        return 4;
    }
    var batch = 10;
    if (ArgValue("--lote") is { } loteArg && (!int.TryParse(loteArg, out batch) || batch is < 1 or > 100))
    {
        Log($"ERRO --lote deve ser um número de 1 a 100 (recebido: '{loteArg}').");
        return 2;
    }
    var continuous = args.Contains("--continuo");
    // Nome único por réplica: no Kubernetes, HOSTNAME é o nome do pod.
    var workerId = $"{Environment.GetEnvironmentVariable("HOSTNAME") ?? Environment.MachineName}-{Environment.ProcessId}";
    var runner = new StageRunner(new PgStoryQueue(db), handler, workerId, TimeProvider.System);

    // SIGTERM (Kubernetes) ou Ctrl+C: termina a história atual e sai; o lease das não iniciadas expira e outra réplica retoma.
    // Sem "using": ProcessExit dispara DEPOIS que este método termina; com o objeto já descartado,
    // Cancel() lançava ObjectDisposedException e o processo saía com erro mesmo tendo dado tudo certo.
    var stop = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
    AppDomain.CurrentDomain.ProcessExit += (_, _) => stop.Cancel();

    do
    {
        try
        {
            var r = await runner.RunOnceAsync(batch, stop.Token);
            if (r.Claimed > 0) Log($"ETAPA {stage.Name}: {r.Claimed} reservadas, {r.Completed} concluídas, {r.Retrying} para tentar de novo, {r.GaveUp} falharam, {r.Stale} desatualizadas, {r.LeaseLost} perdidas");
            if (continuous && r.Claimed == 0) await Task.Delay(TimeSpan.FromSeconds(10), stop.Token);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            break;
        }
    } while (continuous && !stop.IsCancellationRequested);
    Log($"FIM  etapa {stage.Name} ({workerId})");
    return 0;
}

string? ArgValue(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

static void Log(string message) => Console.WriteLine($"{DateTimeOffset.UtcNow:HH:mm:ss} {message}");
