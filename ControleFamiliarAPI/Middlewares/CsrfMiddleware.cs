using ControleFamiliarAPI.Services;

namespace ControleFamiliarAPI.Middlewares
{
    /// <summary>
    /// Barra requisições de escrita autenticadas por cookie que não venham do
    /// próprio frontend.
    /// </summary>
    // Enquanto a sessão vivia no header Authorization, CSRF não existia: um site
    // malicioso não consegue montar aquele header numa requisição para outro
    // domínio. Cookie é diferente — o navegador o anexa sozinho, inclusive numa
    // requisição disparada por qualquer página que o usuário abra.
    //
    // A defesa aqui é a do header customizado: um formulário HTML ou uma
    // navegação disparada por outro site não consegue definir cabeçalho
    // nenhum. Para mandar um header customizado, o navegador é obrigado a fazer
    // preflight CORS — e o preflight falha, porque a política só libera as
    // origens configuradas em Cors:AllowedOrigins.
    //
    // O SameSite=Lax do cookie já é a primeira barreira; isto é a segunda, para
    // o caso de um navegador antigo ou de uma configuração que afrouxe o Lax.
    public class CsrfMiddleware
    {
        public const string NomeHeader = "X-Requisicao-FiscalHub";

        private static readonly string[] MetodosSeguros = ["GET", "HEAD", "OPTIONS", "TRACE"];

        private readonly RequestDelegate _next;

        public CsrfMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (PrecisaValidar(context) && !context.Request.Headers.ContainsKey(NomeHeader))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";

                await context.Response.WriteAsync(
                    $$"""{"success":false,"data":null,"message":"Requisição bloqueada: header {{NomeHeader}} ausente."}""");

                return;
            }

            await _next(context);
        }

        private static bool PrecisaValidar(HttpContext context)
        {
            // Métodos que não alteram estado não precisam da checagem.
            if (MetodosSeguros.Contains(context.Request.Method))
                return false;

            // Só quem se autentica POR COOKIE está sujeito a CSRF.
            //
            // Se veio Authorization, é ele que autentica e o cookie é
            // irrelevante — um site malicioso não consegue forjar esse header
            // numa requisição para outro domínio. Exigir a checagem aí
            // quebraria Scalar, curl e Postman sem ganho nenhum. A ordem
            // importa: um cliente pode ter os dois ao mesmo tempo (é o caso do
            // HttpClient dos testes, que guarda cookies sozinho).
            if (context.Request.Headers.ContainsKey("Authorization"))
                return false;

            if (!context.Request.Cookies.ContainsKey(CookieDeSessao.Nome))
                return false;

            // O webhook e os endpoints anônimos de auth não dependem do cookie
            // para autorizar; um usuário logado que caia neles não deve ser
            // bloqueado por causa de um cookie que a requisição nem usa.
            var caminho = context.Request.Path;

            return !caminho.StartsWithSegments("/api/webhooks")
                && !caminho.StartsWithSegments("/api/auth/login")
                && !caminho.StartsWithSegments("/api/auth/registrar");
        }
    }
}
