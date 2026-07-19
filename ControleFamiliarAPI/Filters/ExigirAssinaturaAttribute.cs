using ControleFamiliarAPI.Exceptions;
using ControleFamiliarAPI.Services.Interfaces;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace ControleFamiliarAPI.Filters
{
    // Atributos não suportam injeção de dependência no construtor (só
    // aceitam constantes em tempo de compilação), então o IAssinaturaService
    // é resolvido em tempo de execução a partir do HttpContext.RequestServices
    // dentro do próprio filtro - padrão usual do ASP.NET Core para esse caso.
    public class ExigirAssinaturaAttribute : Attribute, IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var assinaturaService = context.HttpContext.RequestServices.GetRequiredService<IAssinaturaService>();
            var status = await assinaturaService.ObterStatus(context.HttpContext.RequestAborted);

            if (!status.TemAcesso)
                throw new PagamentoRequeridoException("É necessário ter uma assinatura ativa para acessar este recurso.");

            await next();
        }
    }
}
