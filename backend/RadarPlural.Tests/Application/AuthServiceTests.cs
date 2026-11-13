using RadarPlural.Application.Auth;
using RadarPlural.Application.Common;

namespace RadarPlural.Tests.Application;

public class AuthServiceTests
{
    private readonly InMemoryMembers members = new();
    private AuthService Service() => new(members, new FakeHasher());

    [Fact]
    public async Task RegisterNormalizesEmailAndNeverStoresPlainPassword()
    {
        var result = await Service().RegisterAsync(new("  Ana@Example.ORG ", "senha-forte-1", " Ana "), default);

        Assert.True(result.IsSuccess);
        var stored = Assert.Single(members.Items);
        Assert.Equal("ana@example.org", stored.Email);
        Assert.Equal("Ana", stored.DisplayName);
        Assert.NotEqual("senha-forte-1", stored.PasswordHash);
        Assert.False(stored.IsAdmin); // ninguém vira admin pelo cadastro
    }

    [Theory]
    [InlineData("sem-arroba", "senha-forte-1", "Ana")]
    [InlineData("ana@example.org", "curta", "Ana")]
    [InlineData("ana@example.org", "senha-forte-1", "   ")]
    [InlineData("Ana <ana@example.org>", "senha-forte-1", "Ana")] // formato "nome <email>" não é aceito
    public async Task InvalidRegistrationIsValidationErrorAndSavesNothing(string email, string password, string name)
    {
        var result = await Service().RegisterAsync(new(email, password, name), default);

        Assert.Equal(ErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(members.Items);
    }

    [Fact]
    public async Task PasswordLimitsAreInclusive()
    {
        Assert.True((await Service().RegisterAsync(new("min@example.org", new string('a', 10), "Min"), default)).IsSuccess);
        Assert.True((await Service().RegisterAsync(new("max@example.org", new string('a', 128), "Max"), default)).IsSuccess);
        Assert.False((await Service().RegisterAsync(new("over@example.org", new string('a', 129), "Over"), default)).IsSuccess);
    }

    [Fact]
    public async Task DuplicateEmailGetsGenericMessage()
    {
        await Service().RegisterAsync(new("ana@example.org", "senha-forte-1", "Ana"), default);
        var again = await Service().RegisterAsync(new("ANA@example.org", "senha-forte-2", "Outra"), default);

        Assert.Equal(AuthService.RegistrationRefused, again.Error?.Message);
        Assert.Single(members.Items);
    }

    [Fact]
    public async Task LoginFailureDoesNotRevealWhetherEmailExists()
    {
        await Service().RegisterAsync(new("ana@example.org", "senha-forte-1", "Ana"), default);

        var wrongPassword = await Service().LoginAsync(new("ana@example.org", "senha-errada-1"), default);
        var unknownEmail = await Service().LoginAsync(new("ninguem@example.org", "senha-forte-1"), default);

        Assert.Equal(wrongPassword.Error, unknownEmail.Error);
        Assert.Equal(ErrorKind.Unauthorized, wrongPassword.Error?.Kind);
    }

    [Fact]
    public async Task CurrentUserOfDeletedAccountIsUnauthorized()
    {
        var result = await Service().CurrentAsync(Guid.NewGuid(), default);
        Assert.Equal(ErrorKind.Unauthorized, result.Error?.Kind);
    }
}
