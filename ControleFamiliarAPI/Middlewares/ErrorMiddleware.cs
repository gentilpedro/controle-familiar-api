using ControleFamiliarAPI.Exceptions;
using ControleFamiliarAPI.Responses;
using System.Text.Json;

namespace ControleFamiliarAPI.Middlewares
{
    public class ErrorMiddleware
    {
        private readonly RequestDelegate _next;

        // O restante da API serializa em camelCase (padrão web do
        // ASP.NET Core via [ApiController]); sem isso, essa resposta sairia
        // em PascalCase e o front não conseguiria ler ex.Message.
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

        public ErrorMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = ex switch
                {
                    UnauthorizedException => StatusCodes.Status401Unauthorized,
                    ForbiddenException => StatusCodes.Status403Forbidden,
                    _ => StatusCodes.Status400BadRequest
                };

                var response = new ApiResponse<string>(ex.Message);

                var json = JsonSerializer.Serialize(response, SerializerOptions);

                await context.Response.WriteAsync(json);
            }
        }
    }
}