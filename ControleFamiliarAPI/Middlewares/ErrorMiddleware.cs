using ControleFamiliarAPI.Exceptions;
using ControleFamiliarAPI.Responses;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ControleFamiliarAPI.Middlewares
{
    public class ErrorMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ErrorMiddleware> _logger;

        // O restante da API serializa em camelCase (padrão web do
        // ASP.NET Core via [ApiController]); sem isso, essa resposta sairia
        // em PascalCase e o front não conseguiria ler ex.Message.
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

        public ErrorMiddleware(RequestDelegate next, ILogger<ErrorMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                var (statusCode, mensagem) = MapearResposta(ex);

                if (statusCode == StatusCodes.Status500InternalServerError)
                    _logger.LogError(ex, "Erro não tratado em {Method} {Path}", context.Request.Method, context.Request.Path);
                else
                    _logger.LogWarning(ex, "Requisição rejeitada ({StatusCode}) em {Method} {Path}", statusCode, context.Request.Method, context.Request.Path);

                context.Response.ContentType = "application/json";
                context.Response.StatusCode = statusCode;

                var response = new ApiResponse<string>(mensagem);

                var json = JsonSerializer.Serialize(response, SerializerOptions);

                await context.Response.WriteAsync(json);
            }
        }

        // Exceções de domínio conhecidas (ForbiddenException, NotFoundException,
        // etc.) sempre trazem uma mensagem já pensada para o cliente final, então
        // é seguro devolvê-la. Qualquer exceção não mapeada aqui é tratada como
        // falha interna: o cliente recebe uma mensagem genérica (sem detalhes de
        // stack trace/SQL) e o detalhe completo vai só para o log.
        private static (int StatusCode, string Mensagem) MapearResposta(Exception ex) => ex switch
        {
            UnauthorizedException => (StatusCodes.Status401Unauthorized, ex.Message),
            ForbiddenException => (StatusCodes.Status403Forbidden, ex.Message),
            NotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            PagamentoRequeridoException => (StatusCodes.Status402PaymentRequired, ex.Message),
            BusinessRuleException => (StatusCodes.Status400BadRequest, ex.Message),
            DbUpdateException => (StatusCodes.Status409Conflict, "Não foi possível concluir a operação: o registro está em uso ou conflita com outro dado existente."),
            _ => (StatusCodes.Status500InternalServerError, "Ocorreu um erro interno. Tente novamente mais tarde.")
        };
    }
}