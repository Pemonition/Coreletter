using System.Globalization;
using YamlDotNet.RepresentationModel;

namespace RadarPlural.Tests.Infra;

public sealed record CostPrices(decimal PricePerVcpuHour, decimal PricePerGibHour, decimal HoursPerMonth, decimal CollectorHoursPerMonth, decimal MonthlyCeiling);

public sealed record WorkloadCost(string Kind, string Name, int MaxReplicas, decimal Hours, decimal Monthly);

// Calcula o custo máximo mensal a partir dos manifests do Kubernetes (deploy/k8s).
// Deployment: réplicas máximas (HPA > anotação coreletter.dev/max-replicas > spec.replicas) × horas do mês.
// CronJob: 1 pod × horas estimadas de execução no mês. Job (migração) é pontual e não entra.
// Custo de cada pod = soma dos LIMITES dos contêineres (o máximo que ele pode consumir).
public static class CostCeiling
{
    public static decimal ParseCpu(string q) =>
        q.EndsWith('m') ? decimal.Parse(q[..^1], CultureInfo.InvariantCulture) / 1000m : decimal.Parse(q, CultureInfo.InvariantCulture);

    public static decimal ParseMemoryGib(string q)
    {
        (string suffix, decimal bytes)[] units = [("Ki", 1024m), ("Mi", 1024m * 1024), ("Gi", 1024m * 1024 * 1024), ("K", 1e3m), ("M", 1e6m), ("G", 1e9m)];
        foreach (var (suffix, bytes) in units)
            if (q.EndsWith(suffix)) return decimal.Parse(q[..^suffix.Length], CultureInfo.InvariantCulture) * bytes / (1024m * 1024 * 1024);
        return decimal.Parse(q, CultureInfo.InvariantCulture) / (1024m * 1024 * 1024);
    }

    public static IReadOnlyList<WorkloadCost> Calculate(IEnumerable<string> yamlDocuments, CostPrices prices)
    {
        var docs = new List<YamlMappingNode>();
        foreach (var text in yamlDocuments)
        {
            var stream = new YamlStream();
            stream.Load(new StringReader(text));
            docs.AddRange(stream.Documents.Select(d => d.RootNode).OfType<YamlMappingNode>());
        }

        var hpaMax = docs.Where(d => Str(d, "kind") == "HorizontalPodAutoscaler")
            .ToDictionary(d => Str(Map(Map(d, "spec"), "scaleTargetRef"), "name")!, d => int.Parse(Str(Map(d, "spec"), "maxReplicas")!));

        var result = new List<WorkloadCost>();
        foreach (var d in docs)
        {
            var kind = Str(d, "kind");
            var meta = Map(d, "metadata");
            var name = Str(meta, "name") ?? "?";
            switch (kind)
            {
                case "Deployment":
                {
                    var spec = Map(d, "spec");
                    var annotation = Str(Map(meta, "annotations"), "coreletter.dev/max-replicas");
                    var max = hpaMax.TryGetValue(name, out var h) ? h
                        : annotation is not null ? int.Parse(annotation)
                        : int.Parse(Str(spec, "replicas") ?? "1");
                    var pod = PodHourly(Map(Map(spec, "template"), "spec"), kind, name, prices);
                    result.Add(new WorkloadCost(kind, name, max, prices.HoursPerMonth, max * pod * prices.HoursPerMonth));
                    break;
                }
                case "CronJob":
                {
                    var podSpec = Map(Map(Map(Map(Map(d, "spec"), "jobTemplate"), "spec"), "template"), "spec");
                    var pod = PodHourly(podSpec, kind, name, prices);
                    result.Add(new WorkloadCost(kind, name, 1, prices.CollectorHoursPerMonth, pod * prices.CollectorHoursPerMonth));
                    break;
                }
            }
        }
        return result;
    }

    private static decimal PodHourly(YamlMappingNode? podSpec, string? kind, string name, CostPrices prices)
    {
        var containers = podSpec?.Children.TryGetValue(new YamlScalarNode("containers"), out var c) == true ? (YamlSequenceNode)c : null;
        if (containers is null || containers.Children.Count == 0) throw new InvalidOperationException($"{kind}/{name} sem contêineres.");
        decimal total = 0;
        foreach (var container in containers.Children.OfType<YamlMappingNode>())
        {
            var limits = Map(Map(container, "resources"), "limits");
            var cpu = Str(limits, "cpu");
            var memory = Str(limits, "memory");
            // Sem limite, o pod pode consumir o node inteiro: o custo não tem teto e a regra não se sustenta.
            if (cpu is null || memory is null)
                throw new InvalidOperationException($"{kind}/{name}: contêiner '{Str(container, "name")}' sem limits de cpu e memory.");
            total += ParseCpu(cpu) * prices.PricePerVcpuHour + ParseMemoryGib(memory) * prices.PricePerGibHour;
        }
        return total;
    }

    private static YamlMappingNode? Map(YamlMappingNode? node, string key) =>
        node is not null && node.Children.TryGetValue(new YamlScalarNode(key), out var v) ? v as YamlMappingNode : null;

    private static string? Str(YamlMappingNode? node, string key) =>
        node is not null && node.Children.TryGetValue(new YamlScalarNode(key), out var v) ? (v as YamlScalarNode)?.Value : null;
}
