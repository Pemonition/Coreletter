using RadarPlural.Infrastructure;
using RadarPlural.Pipeline;
using RadarPlural.Pipeline.Comparison;

namespace RadarPlural.Tests;

// Contrato v1 (docs/contratos/comparacao-v1.md) e comparador simulado (B05). Fontes fictícias, as mesmas dos exemplos do contrato.
public static class ContractFixtures
{
    public static readonly Guid A = Guid.Parse("11111111-0000-0000-0000-00000000000a");
    public static readonly Guid B = Guid.Parse("11111111-0000-0000-0000-00000000000b");
    public static readonly Guid C = Guid.Parse("11111111-0000-0000-0000-00000000000c");
    public static readonly Guid Story = Guid.Parse("22222222-0000-0000-0000-000000000001");

    public static StoryItem Item(Guid id, string source, string title, string summary) =>
        new(id, source, source.ToUpperInvariant(), $"https://{source}.example.org/n", title, summary, new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero));

    public static StorySnapshot Concordante => new(Story, 1,
    [
        Item(A, "fonte-a", "Universidade lança chip nacional para sensores agrícolas", "O chip foi desenvolvido em parceria com uma cooperativa e começa a ser testado em 20 fazendas no primeiro semestre."),
        Item(B, "fonte-b", "Chip criado em universidade vai monitorar lavouras", "Testes começam em 20 fazendas no primeiro semestre, segundo a equipe responsável."),
    ]);

    public static StorySnapshot Divergente => new(Story, 1,
    [
        Item(A, "fonte-a", "Vazamento expõe dados de 2 milhões de clientes de operadora", "A empresa confirmou o incidente e diz que senhas não foram afetadas."),
        Item(B, "fonte-b", "Operadora admite incidente com dados de 200 mil clientes", "Segundo a operadora, senhas não foram afetadas e a investigação continua."),
        Item(C, "fonte-c", "Operadora investiga acesso indevido a sistema de cadastro", "Empresa não informou quantos clientes foram atingidos."),
    ]);

    public static StorySnapshot Insuficiente => new(Story, 1,
    [
        Item(A, "fonte-a", "O que esperar da inteligência artificial em 2027", "Especialistas comentam tendências para o próximo ano."),
        Item(B, "fonte-b", "IA nas empresas: o debate continua", "Opinião: o setor precisa discutir regras."),
    ]);

    public static ComparisonResult Result(StorySnapshot s, params Claim[] claims) => new(
        ContractValidator.Version, s.StoryId, "teste", true, DateTimeOffset.UnixEpoch,
        s.Items.Select(i => new ComparisonSource(i.ItemId, i.SourceId, i.SourceName, i.Url, i.Title, i.Summary, i.PublishedAt)).ToList(),
        claims, new ComparisonSummary(0, 0, 0, 0), []);
}

public class ContractValidatorTests
{
    private static Claim C(string status, Guid[] supported, params Evidence[] evidence) => new("k1", "Afirmação neutra", status, supported, [], evidence);

    [Fact]
    public void LiteralQuoteIsKeptIgnoringCaseAndSpaces()
    {
        var r = ContractValidator.Sanitize(ContractFixtures.Result(ContractFixtures.Concordante,
            C(ClaimStatus.Concordante, [ContractFixtures.A], new Evidence(ContractFixtures.A, "  CHIP   nacional para sensores "))));
        Assert.Single(r.Claims[0].Evidence);
        Assert.Equal(ClaimStatus.Concordante, r.Claims[0].Status);
    }

    [Fact]
    public void InventedQuoteIsRemovedAndClaimWithoutEvidenceBecomesInsufficient()
    {
        var r = ContractValidator.Sanitize(ContractFixtures.Result(ContractFixtures.Concordante,
            C(ClaimStatus.Concordante, [ContractFixtures.A, ContractFixtures.B], new Evidence(ContractFixtures.A, "o governo proibiu os chips"))));
        Assert.Empty(r.Claims[0].Evidence);
        Assert.Equal(ClaimStatus.EvidenciaInsuficiente, r.Claims[0].Status);
        Assert.Empty(r.Claims[0].SupportedBy);
    }

    [Fact]
    public void QuoteAttributedToTheWrongSourceIsRemoved()
    {
        var r = ContractValidator.Sanitize(ContractFixtures.Result(ContractFixtures.Concordante,
            C(ClaimStatus.Concordante, [ContractFixtures.B], new Evidence(ContractFixtures.B, "em parceria com uma cooperativa")))); // está na fonte A
        Assert.Empty(r.Claims[0].Evidence);
    }

    [Fact]
    public void UnknownItemIdsAreDropped()
    {
        var stranger = Guid.NewGuid();
        var r = ContractValidator.Sanitize(ContractFixtures.Result(ContractFixtures.Concordante,
            new Claim("k1", "Afirmação", ClaimStatus.Concordante, [ContractFixtures.A, stranger], [stranger],
                [new Evidence(ContractFixtures.A, "chip nacional"), new Evidence(stranger, "chip nacional")])));
        Assert.Equal([ContractFixtures.A], r.Claims[0].SupportedBy);
        Assert.Empty(r.Claims[0].ContradictedBy);
        Assert.Single(r.Claims[0].Evidence);
    }

    [Theory]
    [InlineData("Isso é falso")]
    [InlineData("A notícia é FAKE")]
    [InlineData("Fonte confiável confirma")]
    [InlineData("Informação verdadeira")]
    [InlineData("É mentira da operadora")]
    public void VerdictWordsAreNotAllowed(string text)
    {
        var r = ContractValidator.Sanitize(ContractFixtures.Result(ContractFixtures.Concordante,
            new Claim("k1", text, ClaimStatus.Concordante, [ContractFixtures.A], [], [new Evidence(ContractFixtures.A, "chip nacional")])));
        Assert.Empty(r.Claims);
        Assert.Contains(r.Limitations, l => l.Contains("removida"));
    }

    [Fact]
    public void SummaryIsRecountedAndVersionForced()
    {
        var r = ContractValidator.Sanitize(ContractFixtures.Result(ContractFixtures.Divergente,
            new Claim("k1", "a", ClaimStatus.Concordante, [ContractFixtures.A], [], [new Evidence(ContractFixtures.A, "senhas não foram afetadas")]),
            new Claim("k2", "b", ClaimStatus.Divergente, [ContractFixtures.A], [ContractFixtures.B], [new Evidence(ContractFixtures.A, "2 milhões"), new Evidence(ContractFixtures.B, "200 mil")])) with { ContractVersion = "9" });
        Assert.Equal(new ComparisonSummary(1, 1, 0, 0), r.Summary);
        Assert.Equal("1.0", r.ContractVersion);
    }

    [Fact]
    public void StandardLimitationsAreAlwaysPresent()
    {
        var r = ContractValidator.Sanitize(ContractFixtures.Result(ContractFixtures.Concordante));
        Assert.Contains(r.Limitations, l => l.Contains("manchetes e resumos"));
        Assert.Contains(r.Limitations, l => l.Contains("não prova"));
    }
}

public class SimulatedAuditorTests
{
    private static readonly SimulatedAuditor Auditor = new(TimeProvider.System);

    private static async Task<ComparisonResult> Audit(StorySnapshot s) => ContractValidator.Sanitize(await Auditor.AuditAsync(s, default));

    [Fact]
    public async Task SameEventAcrossSourcesBecomesAgreementQuotingEachTitle()
    {
        var r = await Audit(ContractFixtures.Concordante);
        var agreement = r.Claims.First(c => c.Status == ClaimStatus.Concordante);
        Assert.Equal([ContractFixtures.A, ContractFixtures.B], agreement.SupportedBy.Order());
        Assert.Equal(2, agreement.Evidence.Count);
        Assert.True(r.Simulated);
        Assert.Equal("simulado", r.Provider);
    }

    [Fact]
    public async Task DifferentNumbersForTheSameThingBecomeDivergence()
    {
        var r = await Audit(ContractFixtures.Divergente);
        var divergence = Assert.Single(r.Claims, c => c.Status == ClaimStatus.Divergente);
        Assert.Contains(divergence.Evidence, e => e.ItemId == ContractFixtures.A && e.Quote.Contains("2 milhões"));
        Assert.Contains(divergence.Evidence, e => e.ItemId == ContractFixtures.B && e.Quote.Contains("200 mil"));
    }

    [Fact]
    public async Task DetailOnlyOneSourceMentionsIsSingleSource()
    {
        var r = await Audit(ContractFixtures.Concordante);
        Assert.Contains(r.Claims, c => c.Status == ClaimStatus.UnicaFonte && c.SupportedBy.SequenceEqual([ContractFixtures.A]) && c.Evidence.Any(e => e.Quote.Contains("cooperativa")));
    }

    [Fact]
    public async Task VagueOpinionPiecesGiveInsufficientEvidence()
    {
        var r = await Audit(ContractFixtures.Insuficiente);
        Assert.All(r.Claims, c => Assert.NotEqual(ClaimStatus.Concordante, c.Status));
        Assert.Contains(r.Claims, c => c.Status == ClaimStatus.EvidenciaInsuficiente);
    }

    [Fact]
    public async Task EveryQuoteSurvivesTheValidator()
    {
        foreach (var s in new[] { ContractFixtures.Concordante, ContractFixtures.Divergente, ContractFixtures.Insuficiente })
        {
            var raw = await Auditor.AuditAsync(s, default);
            var clean = ContractValidator.Sanitize(raw);
            Assert.Equal(raw.Claims.Sum(c => c.Evidence.Count), clean.Claims.Sum(c => c.Evidence.Count)); // simulado nunca inventa citação
        }
    }

    [Fact]
    public async Task SameInputGivesSameOutput()
    {
        var a = await Audit(ContractFixtures.Divergente);
        var b = await Audit(ContractFixtures.Divergente);
        Assert.Equal(a.Claims.Select(c => (c.Id, c.Status, c.Text)), b.Claims.Select(c => (c.Id, c.Status, c.Text)));
    }
}

public class AuditStageHandlerTests
{
    private sealed class Reader(StorySnapshot? s) : IStoryReader
    {
        public Task<StorySnapshot?> ReadAsync(Guid storyId, CancellationToken ct) => Task.FromResult(s);
    }

    private sealed class Store : IAuditStore
    {
        public List<(ComparisonResult R, int V)> Saved { get; } = [];
        public Task SaveAsync(ComparisonResult result, int storyVersion, CancellationToken ct) { Saved.Add((result, storyVersion)); return Task.CompletedTask; }
        public Task<(ComparisonResult Result, int StoryVersion)?> LatestAsync(Guid storyId, CancellationToken ct) => Task.FromResult<(ComparisonResult, int)?>(null);
    }

    private sealed class LyingAuditor : IAuditor
    {
        public string Name => "mentiroso";
        public bool Simulated => false;
        public Task<ComparisonResult> AuditAsync(StorySnapshot story, CancellationToken ct) => Task.FromResult(ContractFixtures.Result(story,
            new Claim("k1", "Afirmação", ClaimStatus.Concordante, [ContractFixtures.A], [], [new Evidence(ContractFixtures.A, "citação inventada")])));
    }

    [Fact]
    public async Task AuditIsValidatedBeforeSaving()
    {
        var store = new Store();
        var outcome = await new AuditStageHandler(new Reader(ContractFixtures.Concordante), new LyingAuditor(), store).HandleAsync(new ClaimedStory(ContractFixtures.Story, 1, 7), default);
        Assert.IsType<StageOutcome.Done>(outcome);
        var saved = Assert.Single(store.Saved);
        Assert.Equal(7, saved.V); // grava com a versão reservada: auditoria de versão velha é detectável
        Assert.Equal(ClaimStatus.EvidenciaInsuficiente, saved.R.Claims[0].Status);
    }

    [Fact]
    public async Task MissingStoryIsRetriedNotSaved()
    {
        var store = new Store();
        var outcome = await new AuditStageHandler(new Reader(null), new LyingAuditor(), store).HandleAsync(new ClaimedStory(ContractFixtures.Story, 1, 1), default);
        Assert.IsType<StageOutcome.Retry>(outcome);
        Assert.Empty(store.Saved);
    }
}

public class CompareStoryTests
{
    private static readonly RadarPlural.Domain.Billing.Plan Plan = new("plus", "Plus", 19.90m, 60);
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 20, 0, 0, TimeSpan.Zero);

    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Reader(StorySnapshot? s) : IStoryReader { public Task<StorySnapshot?> ReadAsync(Guid id, CancellationToken ct) => Task.FromResult(s); }
    private sealed class Store((ComparisonResult, int)? latest = null) : IAuditStore
    {
        public int Saves;
        public Task SaveAsync(ComparisonResult r, int v, CancellationToken ct) { Saves++; return Task.CompletedTask; }
        public Task<(ComparisonResult Result, int StoryVersion)?> LatestAsync(Guid id, CancellationToken ct) => Task.FromResult(latest);
    }
    private sealed class Ledger(RadarPlural.Domain.Billing.ReserveResult answer) : RadarPlural.Domain.Billing.IUsageLedger
    {
        public int Reserves, Settles, Releases;
        public Task<RadarPlural.Domain.Billing.ReserveResult> ReserveAsync(Guid m, RadarPlural.Domain.Billing.Plan p, Guid k, DateTimeOffset n, CancellationToken ct) { Reserves++; return Task.FromResult(answer); }
        public Task SettleAsync(Guid k, CancellationToken ct) { Settles++; return Task.CompletedTask; }
        public Task ReleaseAsync(Guid k, CancellationToken ct) { Releases++; return Task.CompletedTask; }
        public Task<int> UsedAsync(Guid m, DateTimeOffset n, CancellationToken ct) => Task.FromResult(0);
    }
    private sealed class BrokenAuditor : IAuditor
    {
        public string Name => "fora";
        public bool Simulated => false;
        public Task<ComparisonResult> AuditAsync(StorySnapshot s, CancellationToken ct) => throw new HttpRequestException("timeout");
    }

    private static CompareStory Sut(StorySnapshot? s, Store store, Ledger ledger, IAuditor? auditor = null) =>
        new(new Reader(s), auditor ?? new SimulatedAuditor(new Clock()), store, ledger, new Clock());

    private static ComparisonResult Cached(DateTimeOffset at) => ContractFixtures.Result(ContractFixtures.Concordante) with { CreatedAt = at };

    [Fact]
    public async Task SuccessChargesOneUnitAndSaves()
    {
        var store = new Store(); var ledger = new Ledger(RadarPlural.Domain.Billing.ReserveResult.Reserved);
        var r = await Sut(ContractFixtures.Concordante, store, ledger).RunAsync(Guid.NewGuid(), Plan, ContractFixtures.Story, Guid.NewGuid(), default);
        Assert.Null(r.ErrorCode); Assert.Equal(1, r.Units); Assert.False(r.FromCache);
        Assert.Equal((1, 1, 0, 1), (ledger.Reserves, ledger.Settles, ledger.Releases, store.Saves));
    }

    [Fact]
    public async Task RecentResultForSameVersionIsFreeAndSkipsTheAuditor()
    {
        var ledger = new Ledger(RadarPlural.Domain.Billing.ReserveResult.Reserved);
        var r = await Sut(ContractFixtures.Concordante, new Store((Cached(Now.AddHours(-23)), 1)), ledger, new BrokenAuditor()).RunAsync(Guid.NewGuid(), Plan, ContractFixtures.Story, Guid.NewGuid(), default);
        Assert.True(r.FromCache); Assert.Equal(0, r.Units); Assert.Equal(0, ledger.Reserves);
    }

    [Theory]
    [InlineData(25, 1)] // cache vencido (mais de 24 h)
    [InlineData(1, 2)]  // história mudou de versão desde a última comparação
    public async Task StaleCacheIsRecomputed(int hoursAgo, int cachedVersion)
    {
        var ledger = new Ledger(RadarPlural.Domain.Billing.ReserveResult.Reserved);
        var r = await Sut(ContractFixtures.Concordante, new Store((Cached(Now.AddHours(-hoursAgo)), cachedVersion)), ledger).RunAsync(Guid.NewGuid(), Plan, ContractFixtures.Story, Guid.NewGuid(), default);
        Assert.False(r.FromCache); Assert.Equal(1, ledger.Reserves);
    }

    [Fact]
    public async Task QuotaExceededStopsBeforeCallingTheAuditor()
    {
        var store = new Store();
        var r = await Sut(ContractFixtures.Concordante, store, new Ledger(RadarPlural.Domain.Billing.ReserveResult.QuotaExceeded), new BrokenAuditor()).RunAsync(Guid.NewGuid(), Plan, ContractFixtures.Story, Guid.NewGuid(), default);
        Assert.Equal("quota_esgotada", r.ErrorCode); Assert.Equal(0, store.Saves);
    }

    [Fact]
    public async Task ProviderFailureGivesTheUnitBack()
    {
        var ledger = new Ledger(RadarPlural.Domain.Billing.ReserveResult.Reserved);
        var r = await Sut(ContractFixtures.Concordante, new Store(), ledger, new BrokenAuditor()).RunAsync(Guid.NewGuid(), Plan, ContractFixtures.Story, Guid.NewGuid(), default);
        Assert.Equal("provedor_indisponivel", r.ErrorCode); Assert.Equal((1, 0, 1), (ledger.Reserves, ledger.Settles, ledger.Releases));
    }

    [Fact]
    public async Task RepeatedKeyDoesNotChargeAgain()
    {
        var ledger = new Ledger(RadarPlural.Domain.Billing.ReserveResult.AlreadyReserved);
        var r = await Sut(ContractFixtures.Concordante, new Store(), ledger).RunAsync(Guid.NewGuid(), Plan, ContractFixtures.Story, Guid.NewGuid(), default);
        Assert.Null(r.ErrorCode); Assert.Equal(0, r.Units); Assert.Equal(0, ledger.Settles);
    }

    [Fact]
    public async Task MissingStoryAndSingleSourceAreRejectedWithoutCharging()
    {
        var ledger = new Ledger(RadarPlural.Domain.Billing.ReserveResult.Reserved);
        Assert.Equal("historia_inexistente", (await Sut(null, new Store(), ledger).RunAsync(Guid.NewGuid(), Plan, Guid.NewGuid(), Guid.NewGuid(), default)).ErrorCode);
        var single = ContractFixtures.Concordante with { Items = [ContractFixtures.Concordante.Items[0]] };
        Assert.Equal("entrada_invalida", (await Sut(single, new Store(), ledger).RunAsync(Guid.NewGuid(), Plan, ContractFixtures.Story, Guid.NewGuid(), default)).ErrorCode);
        Assert.Equal(0, ledger.Reserves);
    }
}
