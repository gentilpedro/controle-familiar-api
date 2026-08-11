namespace ControleFamiliarAPI.Services
{
    /// <summary>
    /// Grava e limpa o cookie HttpOnly que carrega o token JWT da sessão.
    /// </summary>
    // O token saiu do localStorage para cá: em localStorage qualquer script na
    // página consegue lê-lo, e um XSS levaria a sessão inteira. HttpOnly é
    // invisível para JavaScript — o navegador anexa o cookie sozinho e o script
    // nunca vê o valor.
    public static class CookieDeSessao
    {
        public const string Nome = "fiscalhub_sessao";

        public static void Gravar(HttpResponse response, string token, DateTime expiraEm)
        {
            response.Cookies.Append(Nome, token, MontarOpcoes(response, expiraEm));
        }

        public static void Limpar(HttpResponse response)
        {
            // Delete precisa das MESMAS opções usadas na gravação (Path, Secure,
            // SameSite), senão o navegador trata como outro cookie e o original
            // continua no lugar.
            response.Cookies.Delete(Nome, MontarOpcoes(response, DateTime.UtcNow.AddDays(-1)));
        }

        private static CookieOptions MontarOpcoes(HttpResponse response, DateTime expiraEm)
        {
            var ehHttps = response.HttpContext.Request.IsHttps;

            return new CookieOptions
            {
                HttpOnly = true,

                // Em produção o navegador só fala com o domínio da Vercel, que
                // reescreve /api/* para cá — então o cookie é first-party e
                // SameSite=Lax basta. Lax também é o que barra o cookie de ser
                // enviado numa requisição disparada por outro site, que é a
                // primeira linha de defesa contra CSRF.
                SameSite = SameSiteMode.Lax,

                // Em http://localhost (dev sem certificado) um cookie Secure
                // seria descartado pelo navegador, e o login local pararia.
                Secure = ehHttps,

                Expires = expiraEm,

                // Sem Domain explícito: o cookie fica preso ao host que
                // respondeu. Com o proxy da Vercel esse host é o do próprio
                // site, que é exatamente o que o torna first-party.
                Path = "/"
            };
        }
    }
}
