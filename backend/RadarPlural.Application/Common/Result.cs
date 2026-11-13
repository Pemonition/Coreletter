namespace RadarPlural.Application.Common;

// Falhas esperadas (validação, permissão, não encontrado) voltam como valor, não como exceção.
// Exceção fica para o inesperado (banco fora do ar, bug), tratado pelo handler global da API.
public enum ErrorKind { Validation, Unauthorized, Forbidden, NotFound }

public sealed record Error(ErrorKind Kind, string Message)
{
    public static Error Validation(string message) => new(ErrorKind.Validation, message);
    public static Error Unauthorized(string message = "Entre na sua conta para continuar.") => new(ErrorKind.Unauthorized, message);
    public static Error Forbidden(string message = "Você não tem permissão para esta ação.") => new(ErrorKind.Forbidden, message);
    public static Error NotFound(string message = "Item não encontrado.") => new(ErrorKind.NotFound, message);
}

public readonly struct Result<T>
{
    private Result(T? value, Error? error) { Value = value; Error = error; }

    public T? Value { get; }
    public Error? Error { get; }
    public bool IsSuccess => Error is null;

    public static Result<T> Ok(T value) => new(value, null);
    public static implicit operator Result<T>(T value) => Ok(value);
    public static implicit operator Result<T>(Error error) => new(default, error);
}

// Para casos de uso sem valor de retorno (ex.: excluir).
public readonly record struct Unit
{
    public static readonly Unit Value = new();
}
