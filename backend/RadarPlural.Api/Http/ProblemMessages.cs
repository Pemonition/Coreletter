namespace RadarPlural.Api.Http;

// Mensagem em português para erros gerados pelo próprio ASP.NET (sem detalhe do caso de uso).
// O front exibe o campo "error"; sem isso apareceria "Unauthorized" em inglês.
public static class ProblemMessages
{
    public static string For(int? status) => status switch
    {
        StatusCodes.Status400BadRequest => "Requisição inválida.",
        StatusCodes.Status401Unauthorized => "Entre na sua conta para continuar.",
        StatusCodes.Status403Forbidden => "Você não tem permissão para esta ação.",
        StatusCodes.Status404NotFound => "Item não encontrado.",
        StatusCodes.Status413PayloadTooLarge => "Conteúdo grande demais.",
        StatusCodes.Status429TooManyRequests => "Muitas requisições. Aguarde um minuto e tente de novo.",
        _ => "Não foi possível concluir a solicitação."
    };
}
