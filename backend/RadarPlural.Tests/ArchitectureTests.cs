using System.Reflection;

namespace RadarPlural.Tests;

// Regra de dependência da arquitetura limpa (aula de 11/11, decisoes/0004): camadas internas não conhecem as externas.
// Quebrou este teste? A dependência está no lugar errado: crie uma porta (interface) na camada interna
// e implemente o adaptador em RadarPlural.Infrastructure.
public class ArchitectureTests
{
    private static readonly string[] Frameworks = ["Npgsql", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "YamlDotNet"];

    private static IReadOnlyList<string> References(Type marker) =>
        marker.Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

    [Fact]
    public void ApplicationLayerDependsOnlyOnTheDomain()
    {
        // Aplicação → Domínio é a direção certa (camada de fora conhece a de dentro). O resto é proibido.
        var refs = References(typeof(RadarPlural.Pipeline.StageRunner));
        Assert.DoesNotContain(refs, r => r.StartsWith("RadarPlural.") && r != "RadarPlural.Domain");
        Assert.DoesNotContain(refs, r => Frameworks.Any(f => r.StartsWith(f)));
    }

    [Fact]
    public void BusinessDomainDependsOnNothing()
    {
        var refs = References(typeof(RadarPlural.Domain.Billing.Profitability));
        Assert.DoesNotContain(refs, r => r.StartsWith("RadarPlural."));
        Assert.DoesNotContain(refs, r => Frameworks.Any(f => r.StartsWith(f)));
    }

    [Fact]
    public void DomainRulesDoNotDependOnPipelineInfrastructureOrWeb()
    {
        var refs = References(typeof(RadarPlural.Ingestion.Triage));
        Assert.DoesNotContain(refs, r => r is "RadarPlural.Pipeline" or "RadarPlural.Infrastructure" or "RadarPlural.Api");
        Assert.DoesNotContain(refs, r => Frameworks.Any(f => r.StartsWith(f)));
    }

    [Fact]
    public void InfrastructureImplementsPortsAndIsNotReferencedByInnerLayers()
    {
        var infra = typeof(RadarPlural.Infrastructure.PgStoryQueue);
        Assert.True(typeof(RadarPlural.Pipeline.IStoryQueue).IsAssignableFrom(infra));
        Assert.DoesNotContain("RadarPlural.Infrastructure", References(typeof(RadarPlural.Pipeline.StageRunner)));
        Assert.DoesNotContain("RadarPlural.Infrastructure", References(typeof(RadarPlural.Ingestion.Triage)));
    }
}
