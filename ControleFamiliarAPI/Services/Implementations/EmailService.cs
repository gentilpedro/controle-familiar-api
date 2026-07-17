using System.Net;
using System.Net.Mail;
using ControleFamiliarAPI.Services.Interfaces;

namespace ControleFamiliarAPI.Services.Implementations
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Task EnviarConviteFamilia(string destinatario, string nomeFamilia, string codigoConvite, string convidadoPor, CancellationToken cancellationToken = default)
        {
            var frontendUrl = ObterFrontendUrl();
            var linkConvite = $"{frontendUrl}/registrar?codigo={codigoConvite}";

            // convidadoPor/nomeFamilia vêm de texto livre digitado pelo usuário
            // no cadastro (RegistrarDto.Nome/NomeFamilia). Como o corpo é HTML,
            // precisam ser encodados antes de entrar no template — senão viram
            // um vetor de HTML injection no e-mail do convidado.
            var convidadoPorSeguro = WebUtility.HtmlEncode(convidadoPor);
            var nomeFamiliaSeguro = WebUtility.HtmlEncode(nomeFamilia);

            var assunto = $"{convidadoPor} te convidou para a família \"{nomeFamilia}\" no Controle Financeiro";
            var corpo = $"""
                <p>Olá!</p>
                <p><strong>{convidadoPorSeguro}</strong> te convidou para entrar na família <strong>{nomeFamiliaSeguro}</strong> no Controle Financeiro, e passar a compartilhar os mesmos dados de pessoas, categorias e transações.</p>
                <p>Para entrar, cadastre-se usando o código de convite abaixo:</p>
                <p style="font-size: 20px; font-weight: bold; letter-spacing: 2px;">{codigoConvite}</p>
                <p>Ou clique direto no link: <a href="{linkConvite}">{linkConvite}</a></p>
                """;

            return EnviarAsync(destinatario, assunto, corpo, cancellationToken);
        }

        public Task EnviarConfirmacaoEmail(string destinatario, string nomeUsuario, string linkConfirmacao, CancellationToken cancellationToken = default)
        {
            var nomeSeguro = WebUtility.HtmlEncode(nomeUsuario);

            var assunto = "Confirme seu e-mail no Controle Financeiro";
            var corpo = $"""
                <p>Olá, {nomeSeguro}!</p>
                <p>Confirme seu e-mail clicando no link abaixo:</p>
                <p><a href="{linkConfirmacao}">{linkConfirmacao}</a></p>
                <p>Se você não criou uma conta no Controle Financeiro, pode ignorar este e-mail.</p>
                """;

            return EnviarAsync(destinatario, assunto, corpo, cancellationToken);
        }

        private string ObterFrontendUrl() =>
            _configuration["Frontend:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5173";

        private async Task EnviarAsync(string destinatario, string assunto, string corpoHtml, CancellationToken cancellationToken)
        {
            var smtp = _configuration.GetSection("Smtp");
            var host = smtp["Host"];

            if (string.IsNullOrWhiteSpace(host))
                throw new Exception(
                    "Envio de e-mail não configurado neste ambiente. Compartilhe a informação manualmente por enquanto.");

            var porta = int.TryParse(smtp["Port"], out var p) ? p : 587;
            var usarSsl = !bool.TryParse(smtp["EnableSsl"], out var ssl) || ssl;
            var remetente = smtp["From"];
            var nomeRemetente = smtp["FromName"] ?? "Controle Financeiro";

            var mensagem = new MailMessage
            {
                From = new MailAddress(remetente ?? smtp["Username"] ?? string.Empty, nomeRemetente),
                Subject = assunto,
                IsBodyHtml = true,
                Body = corpoHtml
            };

            mensagem.To.Add(destinatario);

            using var client = new SmtpClient(host, porta)
            {
                EnableSsl = usarSsl,
                Credentials = new NetworkCredential(smtp["Username"], smtp["Password"])
            };

            await client.SendMailAsync(mensagem, cancellationToken);
        }
    }
}
