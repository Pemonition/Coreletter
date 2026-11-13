using RadarPlural.Application.Common;

namespace RadarPlural.Api.Http;

// Traduz o resultado do caso de uso para HTTP. Erros saem como ProblemDetails (RFC 9457) e mantêm o campo
// "error" do contrato original, então o front e o Test-Api.ps1 continuam funcionando sem mudança.
public static class ResultMapping
{
    public static IResult ToHttp<T>(this Result<T> result, Func<T, IResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value!) : result.Error!.ToProblem();

    public static IResult ToProblem(this Error error)
    {
        var status = error.Kind switch
        {
            ErrorKind.Validation => StatusCodes.Status400BadRequest,
            ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status500InternalServerError
        };
        return Results.Problem(statusCode: status, detail: error.Message);
    }
}
