using Npgsql;
using Testcontainers.PostgreSql;

namespace RadarPlural.IntegrationTests;

// PostgreSQL real e descartável para os testes de integração.
// Padrão: sobe um container com Testcontainers (precisa de Docker em execução).
// Alternativa sem Docker: defina INTEGRATION_DATABASE_URL com um servidor PostgreSQL de teste
// (usuário com permissão CREATEDB); cada execução cria e apaga um banco com nome único.
public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? container;
    private string? adminConnection;
    private string? databaseName;

    public string ConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        var external = Environment.GetEnvironmentVariable("INTEGRATION_DATABASE_URL");
        if (string.IsNullOrWhiteSpace(external))
        {
            container = new PostgreSqlBuilder("postgres:17-alpine").Build();
            await container.StartAsync();
            ConnectionString = container.GetConnectionString();
            return;
        }

        adminConnection = external;
        databaseName = $"coreletter_it_{Guid.NewGuid():N}";
        await using (var conn = new NpgsqlConnection(adminConnection))
        {
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", conn);
            await cmd.ExecuteNonQueryAsync();
        }
        ConnectionString = new NpgsqlConnectionStringBuilder(adminConnection) { Database = databaseName }.ConnectionString;
    }

    public async Task DisposeAsync()
    {
        if (container is not null)
        {
            await container.DisposeAsync();
            return;
        }
        if (adminConnection is null) return;
        NpgsqlConnection.ClearAllPools();
        await using var conn = new NpgsqlConnection(adminConnection);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", conn);
        await cmd.ExecuteNonQueryAsync();
    }
}

// Um único banco para a coleção inteira: a API aplica as migrações uma vez e os testes usam contas com e-mail único,
// então não dependem de ordem nem limpam dados uns dos outros.
public sealed class ApiFixture : IAsyncLifetime
{
    public PostgresFixture Db { get; } = new();
    public ApiFactory Api { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Db.InitializeAsync();
        Api = new ApiFactory(Db);
        _ = Api.Server; // força a inicialização (migrações) antes do primeiro teste
    }

    public async Task DisposeAsync()
    {
        await Api.DisposeAsync();
        await Db.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "api";
}
