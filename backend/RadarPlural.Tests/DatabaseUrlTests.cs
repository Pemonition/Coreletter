using Npgsql;
using RadarPlural.Api;

namespace RadarPlural.Tests;

// Provedores gerenciados (Render, Neon) entregam a conexão como URL postgresql://;
// o Npgsql só aceita o formato chave=valor.
public class DatabaseUrlTests
{
    [Fact]
    public void UrlDoProvedorViraConnectionStringDoNpgsql()
    {
        var result = new NpgsqlConnectionStringBuilder(DatabaseUrl.ToNpgsql("postgresql://app_user:s3gr%40do@db.internal:6543/coreletter"));

        Assert.Equal("db.internal", result.Host);
        Assert.Equal(6543, result.Port);
        Assert.Equal("coreletter", result.Database);
        Assert.Equal("app_user", result.Username);
        Assert.Equal("s3gr@do", result.Password);
    }

    [Fact]
    public void UrlSemPortaUsaPadraoESslModeDaQueryEhRespeitado()
    {
        var result = new NpgsqlConnectionStringBuilder(DatabaseUrl.ToNpgsql("postgres://u:p@host.example/db?sslmode=require"));

        Assert.Equal(5432, result.Port);
        Assert.Equal(SslMode.Require, result.SslMode);
    }

    [Fact]
    public void ConnectionStringNoFormatoChaveValorNaoMuda()
    {
        const string local = "Host=localhost;Port=5433;Database=radarplural;Username=radarplural;Password=fixture";

        Assert.Equal(local, DatabaseUrl.ToNpgsql(local));
    }
}
