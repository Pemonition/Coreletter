using RadarPlural.Ingestion;

namespace RadarPlural.Tests;

// Triagem e agrupamento (docs/decisoes/0003-pipeline-auditoria-video.md). Manchetes reais coletadas em 12/11/2026.
public class TriageTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 15, 0, 0, TimeSpan.Zero);
    private static readonly FeedSource Tecnoblog = new("tecnoblog", "Tecnoblog", new Uri("https://tecnoblog.net/feed/"), "tecnologia", ["/achados/", "/guias/"]);
    private static readonly FeedSource Plain = new("fonte", "Fonte", new Uri("https://example.org/feed"), "tecnologia");

    private static FeedEntry Entry(string title, string url = "https://example.org/noticia", DateTimeOffset? at = null) => new(title, url, "", at ?? Now.AddHours(-1));

    [Fact]
    public void RegularNewsIsAccepted()
    {
        var r = Triage.Evaluate(Plain, Entry("Jensen Huang renuncia ao cargo de CEO da Nvidia"), Now);
        Assert.True(r.Accepted);
        Assert.Null(r.DiscardReason);
        Assert.Empty(r.Flags);
    }

    [Theory]
    [InlineData("https://tecnoblog.net/achados/galaxy-z-flip-7-recebe-desconto/")]
    [InlineData("https://tecnoblog.net/guias/melhor-tv-qled/")]
    public void SourceAffiliatePathsAreDiscarded(string url)
    {
        var r = Triage.Evaluate(Tecnoblog, Entry("Qualquer título", url), Now);
        Assert.False(r.Accepted);
        Assert.Equal(Triage.ReasonPromoUrl, r.DiscardReason);
    }

    [Fact]
    public void PathRuleDoesNotLeakToOtherSources() =>
        Assert.True(Triage.Evaluate(Plain, Entry("Notícia", "https://example.org/achados/x"), Now).Accepted);

    [Theory]
    [InlineData("Black November: Motorola Edge 70 (512 GB) com cristais Swarovski tem 54% OFF")]
    [InlineData("Galaxy S25 Ultra tem preço imbatível com cupom de R$ 500 OFF no 11.11")]
    [InlineData("Lenovo Idea Tab com 33% OFF em até 12x sem juros")]
    [InlineData("Ofertas do dia: Switch 2, TV Samsung 65″ + Soundbar e mais")]
    [InlineData("Hora de renovar a sala: Smart TVs 4K em promoção na Amazon")]
    public void PromotionalHeadlinesAreDiscarded(string title)
    {
        var r = Triage.Evaluate(Plain, Entry(title), Now);
        Assert.False(r.Accepted);
        Assert.Equal(Triage.ReasonPromoTitle, r.DiscardReason);
    }

    [Theory]
    [InlineData("Prodam São Paulo contrata AWS para ampliar oferta de serviços digitais")] // "oferta" de serviço, não promoção
    [InlineData("Inflação perde força e fica em 0,14% em outubro; conta de luz cai")]   // percentual sem OFF
    [InlineData("Wuchang e mais jogos de Xbox: o que muda na Game Pass")]
    public void NewsThatMerelyMentionsPricesOrOffersIsKept(string title) => Assert.True(Triage.Evaluate(Plain, Entry(title), Now).Accepted);

    [Theory]
    [InlineData("O impossível aconteceu: jogos de PS5 começam a rodar no PS4 via emulador")]
    [InlineData("Você não vai acreditar no que a Apple fez")]
    [InlineData("URGENTE: NOVA FALHA AFETA MILHÕES DE CELULARES")]
    [InlineData("Isso muda tudo!!")]
    public void ClickbaitIsFlaggedButNotDiscarded(string title)
    {
        var r = Triage.Evaluate(Plain, Entry(title), Now);
        Assert.True(r.Accepted);
        Assert.Contains(Triage.FlagClickbait, r.Flags);
    }

    [Theory]
    [InlineData("iPhone 17: até quanto vale a pena pagar por ele na Black Friday?")]
    [InlineData("Kindle Colorsoft: quanto vale pagar na Black Friday 2026?")]
    [InlineData("O que comprar na Black Friday 2026: passagens aéreas")]
    public void ShoppingGuidesAreDiscarded(string title) => Assert.Equal(Triage.ReasonPromoTitle, Triage.Evaluate(Plain, Entry(title), Now).DiscardReason);

    [Fact]
    public void DailyRoundupsAreDiscardedBecauseTheyMixManyStories() =>
        Assert.Equal(Triage.ReasonRoundup, Triage.Evaluate(Plain, Entry("Hoje no TecMundo (11): GTA 6 é recorde de pré-venda, Google lança nova IA e mais"), Now).DiscardReason);

    [Fact]
    public void OlderThanSevenDaysIsDiscarded()
    {
        var r = Triage.Evaluate(Plain, Entry("Notícia", at: Now.AddDays(-8)), Now);
        Assert.Equal(Triage.ReasonOld, r.DiscardReason);
        Assert.True(Triage.Evaluate(Plain, Entry("Notícia", at: Now.AddDays(-6)), Now).Accepted);
    }

    [Fact]
    public void MissingDateIsNotTreatedAsOld() =>
        Assert.True(Triage.Evaluate(Plain, new FeedEntry("Notícia", "https://example.org/n", "", null), Now).Accepted);
}

public class HeadlineTokensTests
{
    [Fact]
    public void AccentsCaseStopwordsAndFillerAreRemoved() =>
        Assert.Equal(["cargo", "huang", "jensen", "nvidia", "renuncia", "traicao"],
            HeadlineTokens.Of("Jensen Huang renuncia ao cargo de CEO da Nvidia após traição; veja").Order());

    [Fact]
    public void WordAfterQuestionMarkIsNotAProperName() =>
        Assert.DoesNotContain("servicos", HeadlineTokens.Names("Gov.br caiu? Serviços online do governo apresentam instabilidade"));

    [Fact]
    public void PluralsMatchSingulars() =>
        Assert.Equal(HeadlineTokens.Of("pré-venda aplicativo"), HeadlineTokens.Of("pré-vendas aplicativos"));

    [Fact]
    public void ModelNumbersSurvive() =>
        Assert.Contains("h200", HeadlineTokens.Of("Universidade compra chips H200 da Nvidia"));
}

public class StoryClustererTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);
    private static int seq;

    private static ClusterCandidate C(string source, string title, double hoursAgo = 1) =>
        new(new Guid(Interlocked.Increment(ref seq), 0, 0, new byte[8]), source, title, T0.AddHours(-hoursAgo));

    // Recorte real de 12/11/2026: uma história em 6 veículos, duas em 2, e notícias soltas parecidas no vocabulário.
    private static readonly ClusterCandidate[] Jensen =
    [
        C("canaltech", "Jensen Huang renuncia ao cargo de CEO da Nvidia após admitir traição"),
        C("convergencia-digital", "Jensen Huang, executivo que deu protagonismo à IA, renuncia ao comando da Nvidia após escândalo extraconjugal e de espionagem"),
        C("canaltech", "NVIDIA prepara demissão de Jensen Huang após segundo caso de traição"),
        C("tecmundo", "Jensen Huang deixa Nvidia: confira linha do tempo da fundação até a saída da empresa"),
        C("olhar-digital", "Nvidia anuncia saída imediata de Jensen Huang do cargo de CEO"),
        C("tecnoblog", "Jensen Huang renuncia ao cargo de CEO da Nvidia após escândalos"),
    ];
    private static readonly ClusterCandidate[] Caloi =
    [
        C("olhar-digital", "Caloi relança Mobylette elétrica no Brasil com 60 km de autonomia"),
        C("canaltech", "Caloi Mobylette elétrica ganha nova geração no Brasil; veja o preço"),
    ];
    private static readonly ClusterCandidate[] Fust =
    [
        C("mobile-time", "Senado aprova projeto que protege recursos do Fust contra contingenciamento"),
        C("convergencia-digital", "Vai à sanção presidencial projeto de Lei que impede o contingenciamento do FUST"),
    ];
    private static readonly ClusterCandidate[] Loose =
    [
        C("olhar-digital", "Universidade chinesa fura sanções e compra chips H200 da Nvidia"),
        C("canaltech", "Nvidia teria engavetado RTX 5080 de 24 GB por custo alto de memória"),
        C("canaltech", "WhatsApp ganha nova aba de Contatos que mostra quem está online e aniversários"),
        C("tecmundo", "WhatsApp testa barra de navegação flutuante no Android; veja o novo visual"),
        C("tecmundo", "Caixa fora do ar? Pix do banco fica indisponível nesta quinta-feira (12)"),
        C("tecmundo", "Gov.br caiu? Serviços online do governo apresentam instabilidade nesta quinta (12)"),
        C("agencia-brasil", "Inflação perde força e fica em 0,14% em outubro; conta de luz cai"),
        C("agencia-brasil", "INPC, inflação que corrige salários, acumula 4,49% em 12 meses"),
        C("tecmundo", "Apple passa a aceitar Pix como forma de pagamento na App Store"),
        C("canaltech", "Galaxy S27 Ultra vai copiar a cor do iPhone 18 Pro? Veja novo vazamento"),
        C("tecnoblog", "Senado vai fornecer iPhones 18 Pro a senadores em contrato de R$ 6 milhões"),
    ];

    private static IReadOnlyList<IReadOnlyList<Guid>> All() => StoryClusterer.Cluster([.. Jensen, .. Caloi, .. Fust, .. Loose]);

    private static IReadOnlyList<Guid> ClusterOf(IReadOnlyList<IReadOnlyList<Guid>> clusters, ClusterCandidate c) => clusters.Single(x => x.Contains(c.Id));

    [Fact]
    public void SameStoryAcrossSixOutletsWithDifferentWordingIsOneCluster()
    {
        var clusters = All();
        Assert.Equal(Jensen.Select(x => x.Id).Order(), ClusterOf(clusters, Jensen[0]).Order());
    }

    [Fact]
    public void TwoOutletPairsAreGrouped()
    {
        var clusters = All();
        Assert.Equal(2, ClusterOf(clusters, Caloi[0]).Count);
        Assert.Equal(2, ClusterOf(clusters, Fust[0]).Count);
    }

    [Fact]
    public void SharingOneCommonWordIsNotEnough()
    {
        var clusters = All();
        Assert.All(Loose, item => Assert.Single(ClusterOf(clusters, item))); // Nvidia, WhatsApp, Pix, inflação, Senado, iPhone 18 Pro...
    }

    [Fact]
    public void EveryItemLandsInExactlyOneCluster()
    {
        var clusters = All();
        Assert.Equal(Jensen.Length + Caloi.Length + Fust.Length + Loose.Length, clusters.Sum(x => x.Count));
        Assert.Equal(clusters.Sum(x => x.Count), clusters.SelectMany(x => x).Distinct().Count());
    }

    [Fact]
    public void GenericRoleWordsCannotChainUnrelatedStories()
    {
        var bridge = C("tecmundo", "‘Seria uma honra’: Elon Musk diz que contrataria Jensen Huang, ex-CEO da Nvidia");
        var verizon = C("tecmundo", "CEO da Verizon corta 13 mil vagas e prepara empresa para enfrentar Musk");
        var clusters = StoryClusterer.Cluster([.. Jensen, bridge, verizon]);
        Assert.Contains(bridge.Id, ClusterOf(clusters, Jensen[0]));
        Assert.Single(ClusterOf(clusters, verizon));
    }

    [Fact]
    public void SameSubjectButDifferentEventsStaySeparate()
    {
        var cloud = C("tecnoblog", "GTA 6 chegará ao Xbox Cloud Gaming no lançamento");
        var record = C("canaltech", "GTA 6 quebra recorde de mais pré-vendas em todos os tempos no PlayStation e Xbox");
        var billion = C("canaltech", "Pré-venda de GTA 6 supera US$ 1 bilhão; veja números no PS5 e XBOX");
        var leak = C("canaltech", "Antes da hora: notas de GTA 6 vazam na PlayStation Store antes do embargo");
        var clusters = StoryClusterer.Cluster([cloud, record, billion, leak]);
        Assert.Equal([record.Id, billion.Id], ClusterOf(clusters, record).Order());
        Assert.Single(ClusterOf(clusters, cloud));
        Assert.Single(ClusterOf(clusters, leak));
    }

    [Fact]
    public void DifferentOutagesOnTheSameDayStaySeparate()
    {
        var govbr = C("tecmundo", "Gov.br caiu? Serviços online do governo apresentam instabilidade nesta quinta (12)");
        var caixa1 = C("tecmundo", "Caixa fora do ar? Pix do banco fica indisponível nesta quinta-feira (12)");
        var caixa2 = C("canaltech", "Pix na Caixa fora do ar? Serviços apresentam instabilidade nesta quinta-feira (12)");
        var clusters = StoryClusterer.Cluster([govbr, caixa1, caixa2]);
        Assert.Equal([caixa1.Id, caixa2.Id], ClusterOf(clusters, caixa1).Order());
        Assert.Single(ClusterOf(clusters, govbr));
    }

    [Fact]
    public void ItemsOutsideTheTimeWindowDoNotJoin()
    {
        var old = C("tecnoblog", "Jensen Huang renuncia ao cargo de CEO da Nvidia após escândalos", hoursAgo: 80);
        var clusters = StoryClusterer.Cluster([.. Jensen, old], window: TimeSpan.FromHours(48));
        Assert.Single(ClusterOf(clusters, old));
    }

    [Fact]
    public void ResultIsDeterministicRegardlessOfInputOrder()
    {
        var a = StoryClusterer.Cluster([.. Jensen, .. Caloi, .. Loose]).Select(x => string.Join(",", x.Order())).Order();
        var b = StoryClusterer.Cluster([.. Loose.Reverse(), .. Caloi, .. Jensen.Reverse()]).Select(x => string.Join(",", x.Order())).Order();
        Assert.Equal(a, b);
    }
}

public class StoryReconcilerTests
{
    private static readonly Guid I1 = Guid.Parse("00000000-0000-0000-0000-000000000001"), I2 = Guid.Parse("00000000-0000-0000-0000-000000000002"),
        I3 = Guid.Parse("00000000-0000-0000-0000-000000000003"), I4 = Guid.Parse("00000000-0000-0000-0000-000000000004");
    private static readonly Guid S1 = Guid.Parse("10000000-0000-0000-0000-000000000001"), S2 = Guid.Parse("10000000-0000-0000-0000-000000000002");

    private static Func<Guid> Ids(params Guid[] ids)
    {
        var q = new Queue<Guid>(ids);
        return q.Dequeue;
    }

    [Fact]
    public void NewClusterGetsNewStory()
    {
        var n = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var a = Assert.Single(StoryReconciler.Reconcile([[I1, I2]], new Dictionary<Guid, Guid>(), Ids(n)));
        Assert.Equal(new StoryAssignment(n, a.ItemIds, true), a);
        Assert.Equal([I1, I2], a.ItemIds.Order());
    }

    [Fact]
    public void NewItemJoiningExistingStoryKeepsItsIdSoAuditWorkIsNotLost()
    {
        var a = Assert.Single(StoryReconciler.Reconcile([[I1, I2, I3]], new Dictionary<Guid, Guid> { [I1] = S1, [I2] = S1 }, Ids()));
        Assert.Equal(S1, a.StoryId);
        Assert.False(a.IsNew);
    }

    [Fact]
    public void MergedStoriesKeepTheOneWithMoreItems()
    {
        var a = Assert.Single(StoryReconciler.Reconcile([[I1, I2, I3]], new Dictionary<Guid, Guid> { [I1] = S2, [I2] = S1, [I3] = S1 }, Ids()));
        Assert.Equal(S1, a.StoryId);
    }

    [Fact]
    public void SplitStoryKeepsIdOnLargerPartAndCreatesNewForTheRest()
    {
        var n = Guid.Parse("20000000-0000-0000-0000-000000000009");
        var result = StoryReconciler.Reconcile([[I4], [I1, I2]], new Dictionary<Guid, Guid> { [I1] = S1, [I2] = S1, [I4] = S1 }, Ids(n));
        Assert.Equal(S1, result.Single(x => x.ItemIds.Contains(I1)).StoryId);
        Assert.Equal(n, result.Single(x => x.ItemIds.Contains(I4)).StoryId);
    }
}

public class SourceCatalogExclusionTests
{
    [Fact]
    public void ExcludedPathsAreLoaded()
    {
        var s = Assert.Single(SourceCatalog.Parse("""[{ "id": "tecnoblog", "name": "Tecnoblog", "url": "https://tecnoblog.net/feed/", "topic": "tecnologia", "excludeUrlContains": ["/achados/", "/guias/"] }]"""));
        Assert.Equal(["/achados/", "/guias/"], s.ExcludeUrlContains);
    }

    [Theory]
    [InlineData("""[{ "id": "a", "name": "A", "url": "https://example.org/f", "topic": "t", "excludeUrlContains": [""] }]""")]
    [InlineData("""[{ "id": "a", "name": "A", "url": "https://example.org/f", "topic": "t", "excludeUrlContains": ["/"] }]""")]
    public void ExclusionThatWouldMatchEverythingIsRejected(string json) => Assert.Throws<IngestionException>(() => SourceCatalog.Parse(json));
}
