using System.Diagnostics;
using Npgsql;
using RadarPlural.Infrastructure.Persistence;
using RadarPlural.Ingestion;
using RadarPlural.Worker;

// Coletor de notícias: roda uma vez, percorre as fontes aprovadas e termina.
// Agendado pelo GitHub Actions (.github/workflows/ingestao.yml); pode rodar à mão:
//   dotnet run --project backend/RadarPlural.Worker -- --dry-run            (não grava; mostra o que coletaria)
//   dotnet run --project backend/RadarPlural.Worker -- --fonte tecnoblog    (só uma fonte)
// Banco: variável ConnectionStrings__Default (mesma da API; aceita URL postgresql://).

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

int failures = 0, totalNew = 0, totalRead = 0;
foreach (var source in sources)
{
    var clock = Stopwatch.StartNew();
    try
    {
        var bytes = await fetcher.FetchAsync(source.Url, allowed, shutdown.Token);
        var entries = FeedParser.Parse(new MemoryStream(bytes), source.Url, policy);
        totalRead += entries.Count;
        if (store is null)
        {
            Log($"OK   {source.Id}: {entries.Count} itens ({bytes.Length / 1024} KB, {clock.ElapsedMilliseconds} ms) [dry-run]");
            foreach (var e in entries.Take(3)) Log($"       - {e.PublishedAt:yyyy-MM-dd HH:mm} | {e.Title}");
        }
        else
        {
            var inserted = await store.SaveAsync(source, entries, DateTimeOffset.UtcNow, shutdown.Token);
            totalNew += inserted;
            Log($"OK   {source.Id}: {entries.Count} itens, {inserted} novos ({clock.ElapsedMilliseconds} ms)");
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

Log($"FIM  {sources.Count} fontes, {failures} com falha, {totalRead} itens lidos" + (store is null ? "" : $", {totalNew} novos"));
if (dataSource is not null) await dataSource.DisposeAsync();
// Uma fonte fora do ar não é erro da execução; todas falhando indica problema nosso (rede, política, parser).
return failures == sources.Count ? 1 : 0;

string? ArgValue(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

static void Log(string message) => Console.WriteLine($"{DateTimeOffset.UtcNow:HH:mm:ss} {message}");
