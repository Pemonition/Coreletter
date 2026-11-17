using System.Text.Json;

namespace RadarPlural.Tests.Infra;

// Regra de rentabilidade da infraestrutura (docs/AULAS.md, decisoes/0004): o custo máximo mensal dos manifests
// (réplicas máximas × limites de CPU/memória × preço) não pode passar do teto de deploy/k8s/custo.json.
public class CostCeilingTests
{
    private static readonly CostPrices Prices = new(0.20m, 0.04m, 730m, 60m, 1000m);

    [Theory]
    [InlineData("500m", 0.5)]
    [InlineData("1", 1)]
    [InlineData("1.5", 1.5)]
    [InlineData("250m", 0.25)]
    public void CpuQuantities(string q, double expected) => Assert.Equal((decimal)expected, CostCeiling.ParseCpu(q));

    [Theory]
    [InlineData("256Mi", 0.25)]
    [InlineData("1Gi", 1)]
    [InlineData("512Mi", 0.5)]
    [InlineData("2G", 1.862645149230957)]
    public void MemoryQuantities(string q, double expected) => Assert.Equal(expected, (double)CostCeiling.ParseMemoryGib(q), 6);

    private const string Api = """
        apiVersion: apps/v1
        kind: Deployment
        metadata: { name: api }
        spec:
          replicas: 2
          template:
            spec:
              containers:
                - name: api
                  resources: { limits: { cpu: 500m, memory: 256Mi } }
        """;

    private const string Hpa = """
        apiVersion: autoscaling/v2
        kind: HorizontalPodAutoscaler
        metadata: { name: api }
        spec:
          scaleTargetRef: { kind: Deployment, name: api }
          minReplicas: 2
          maxReplicas: 4
        """;

    [Fact]
    public void DeploymentUsesAutoscalerMaximum()
    {
        var api = Assert.Single(CostCeiling.Calculate([Api, Hpa], Prices));
        Assert.Equal(4, api.MaxReplicas);
        Assert.Equal(4 * (0.5m * 0.20m + 0.25m * 0.04m) * 730m, api.Monthly); // 4 × 0,11 × 730 = 321,20
    }

    [Fact]
    public void WithoutAutoscalerUsesAnnotationThenReplicas()
    {
        var annotated = Api.Replace("metadata: { name: api }", "metadata: { name: api, annotations: { coreletter.dev/max-replicas: \"3\" } }");
        Assert.Equal(3, Assert.Single(CostCeiling.Calculate([annotated], Prices)).MaxReplicas);
        Assert.Equal(2, Assert.Single(CostCeiling.Calculate([Api], Prices)).MaxReplicas);
    }

    [Fact]
    public void CronJobCountsOnePodForItsMonthlyHours()
    {
        var cron = """
            apiVersion: batch/v1
            kind: CronJob
            metadata: { name: coletor }
            spec:
              jobTemplate:
                spec:
                  template:
                    spec:
                      containers:
                        - name: c
                          resources: { limits: { cpu: "1", memory: 1Gi } }
            """;
        var c = Assert.Single(CostCeiling.Calculate([cron], Prices));
        Assert.Equal((1m * 0.20m + 1m * 0.04m) * 60m, c.Monthly);
    }

    [Fact]
    public void ContainerWithoutLimitsMakesCostUnboundedAndIsRejected() =>
        Assert.Throws<InvalidOperationException>(() => CostCeiling.Calculate([Api.Replace("resources: { limits: { cpu: 500m, memory: 256Mi } }", "resources: {}")], Prices));

    [Fact]
    public void RealManifestsStayUnderTheMonthlyCeiling()
    {
        var dir = FindRepoDir("deploy/k8s");
        var cfg = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "custo.json"))).RootElement;
        var prices = new CostPrices(cfg.GetProperty("precoPorVcpuHora").GetDecimal(), cfg.GetProperty("precoPorGibHora").GetDecimal(),
            cfg.GetProperty("horasPorMes").GetDecimal(), cfg.GetProperty("coletorHorasPorMes").GetDecimal(), cfg.GetProperty("tetoMensal").GetDecimal());
        var docs = Directory.GetFiles(dir, "*.yaml").Where(f => !f.EndsWith("kustomization.yaml")).Select(File.ReadAllText);
        var costs = CostCeiling.Calculate(docs, prices);
        var total = costs.Sum(c => c.Monthly);
        var breakdown = string.Join("; ", costs.Select(c => $"{c.Kind}/{c.Name}: {c.MaxReplicas}× → {c.Monthly:0.00}"));
        Assert.True(total <= prices.MonthlyCeiling, $"Custo máximo mensal {total:0.00} passa do teto {prices.MonthlyCeiling:0.00}. {breakdown}");
        Assert.Contains(costs, c => c.Name == "coreletter-api");
    }

    private static string FindRepoDir(string relative)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            var candidate = Path.Combine(d.FullName, relative);
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new DirectoryNotFoundException(relative);
    }
}
