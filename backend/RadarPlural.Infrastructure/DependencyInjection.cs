using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RadarPlural.Domain.Ports;
using RadarPlural.Infrastructure.Persistence;
using RadarPlural.Infrastructure.Persistence.Repositories;
using RadarPlural.Infrastructure.Security;

namespace RadarPlural.Infrastructure;

public static class DependencyInjection
{
    // Liga cada porta do domínio ao adapter concreto. Trocar PostgreSQL por outro banco mexe só aqui e nos repositórios.
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Configure ConnectionStrings__Default para PostgreSQL.");
        services.AddDbContext<AppDb>(o => o.UseNpgsql(DatabaseUrl.ToNpgsql(connection)));

        // Chaves que protegem o cookie ficam no banco: reinício, novo deploy ou várias réplicas no Kubernetes
        // compartilham as mesmas chaves e não derrubam sessões.
        services.AddDataProtection().PersistKeysToDbContext<AppDb>().SetApplicationName("coreletter");

        services.AddScoped<IMemberRepository, MemberRepository>();
        services.AddScoped<IPostRepository, PostRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<INewsRepository, NewsRepository>();
        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();

        // Prontidão = consegue falar com o banco. Usado pelo readinessProbe do Kubernetes.
        services.AddHealthChecks().AddDbContextCheck<AppDb>("database", tags: ["ready"]);
        return services;
    }
}
