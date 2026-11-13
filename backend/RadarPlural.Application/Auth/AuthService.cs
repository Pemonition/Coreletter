using System.Net.Mail;
using RadarPlural.Application.Common;
using RadarPlural.Domain.Members;
using RadarPlural.Domain.Ports;

namespace RadarPlural.Application.Auth;

// Casos de uso de conta. A criação do cookie de sessão fica no adapter web: aqui só se decide QUEM é o usuário.
public sealed class AuthService(IMemberRepository members, IPasswordHasher hasher)
{
    public const string InvalidRegistration = "Informe e-mail, nome (até 80) e senha de 10 a 128 caracteres.";
    // Mesma mensagem para e-mail repetido e falha de gravação: não revela quais e-mails já têm conta.
    public const string RegistrationRefused = "Não foi possível cadastrar esse e-mail.";
    public const string InvalidCredentials = "Credenciais inválidas.";

    public async Task<Result<UserDto>> RegisterAsync(RegisterInput input, CancellationToken ct)
    {
        var email = Member.NormalizeEmail(input.Email);
        if (!IsValidEmail(email)
            || string.IsNullOrWhiteSpace(input.DisplayName) || input.DisplayName.Length > Member.MaxDisplayNameLength
            || input.Password is null || input.Password.Length is < Member.MinPasswordLength or > Member.MaxPasswordLength)
            return Error.Validation(InvalidRegistration);

        if (await members.EmailExistsAsync(email, ct)) return Error.Validation(RegistrationRefused);

        var member = new Member { Email = email, DisplayName = input.DisplayName.Trim() };
        member.PasswordHash = hasher.Hash(member, input.Password);
        if (!await members.TryAddAsync(member, ct)) return Error.Validation(RegistrationRefused);
        return UserDto.From(member);
    }

    public async Task<Result<UserDto>> LoginAsync(LoginInput input, CancellationToken ct)
    {
        var member = await members.FindByEmailAsync(Member.NormalizeEmail(input.Email), ct);
        if (member is null || input.Password is null || input.Password.Length > Member.MaxPasswordLength || !hasher.Verify(member, input.Password))
            return Error.Unauthorized(InvalidCredentials);
        return UserDto.From(member);
    }

    public async Task<Result<UserDto>> CurrentAsync(Guid memberId, CancellationToken ct)
    {
        var member = await members.FindByIdAsync(memberId, ct);
        return member is null ? Error.Unauthorized() : UserDto.From(member);
    }

    private static bool IsValidEmail(string email) =>
        email.Length <= Member.MaxEmailLength && MailAddress.TryCreate(email, out var address) && address.Address == email;
}
