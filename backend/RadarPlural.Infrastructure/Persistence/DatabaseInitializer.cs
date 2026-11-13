using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RadarPlural.Domain.Members;

namespace RadarPlural.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    // Aplica as migrações versionadas e promove o administrador de demonstração, se a conta já existir.
    // Chamado na inicialização da API (padrão, Render) ou isolado com "--migrate" (Job/initContainer no Kubernetes).
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));

        await db.Database.MigrateAsync(ct); // o próprio EF registra no log cada migração aplicada

        var adminEmail = Member.NormalizeEmail(configuration["BootstrapAdminEmail"]);
        if (adminEmail.Length == 0) return;
        var admin = await db.Members.SingleOrDefaultAsync(x => x.Email == adminEmail, ct);
        if (admin is null || admin.IsAdmin) return;
        admin.IsAdmin = true;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Conta {MemberId} promovida a administradora pelo BootstrapAdminEmail", admin.Id);
    }
}
