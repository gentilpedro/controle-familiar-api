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

        public async Task EnviarConviteFamilia(string destinatario, string nomeFamilia, string codigoConvite, string convidadoPor)
        {
            var smtp = _configuration.GetSection("Smtp");
            var host = smtp["Host"];

            if (string.IsNullOrWhiteSpace(host))
                throw new Exception(
                    "Envio de e-mail não configurado neste ambiente. Compartilhe o código de convite manualmente por enquanto.");

            var porta = int.TryParse(smtp["Port"], out var p) ? p : 587;
            var usarSsl = !bool.TryParse(smtp["EnableSsl"], out var ssl) || ssl;
            var remetente = smtp["From"];
            var nomeRemetente = smtp["FromName"] ?? "Controle Financeiro";
            var frontendUrl = _configuration["Frontend:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5173";

            var linkConvite = $"{frontendUrl}/registrar?codigo={codigoConvite}";

            // convidadoPor/nomeFamilia vêm de texto livre digitado pelo usuário
            // no cadastro (RegistrarDto.Nome/NomeFamilia). Como o corpo é HTML
            // (IsBodyHtml = true), precisam ser encodados antes de entrar no
            // template — senão viram um vetor de HTML injection no e-mail do
            // convidado.
            var convidadoPorSeguro = WebUtility.HtmlEncode(convidadoPor);
            var nomeFamiliaSeguro = WebUtility.HtmlEncode(nomeFamilia);

            var mensagem = new MailMessage
            {
                From = new MailAddress(remetente ?? smtp["Username"] ?? string.Empty, nomeRemetente),
                Subject = $"{convidadoPor} te convidou para a família \"{nomeFamilia}\" no Controle Financeiro",
                IsBodyHtml = true,
                Body = $"""
                    <p>Olá!</p>
                    <p><strong>{convidadoPorSeguro}</strong> te convidou para entrar na família <strong>{nomeFamiliaSeguro}</strong> no Controle Financeiro, e passar a compartilhar os mesmos dados de pessoas, categorias e transações.</p>
                    <p>Para entrar, cadastre-se usando o código de convite abaixo:</p>
                    <p style="font-size: 20px; font-weight: bold; letter-spacing: 2px;">{codigoConvite}</p>
                    <p>Ou clique direto no link: <a href="{linkConvite}">{linkConvite}</a></p>
                    """
            };

            mensagem.To.Add(destinatario);

            using var client = new SmtpClient(host, porta)
            {
                EnableSsl = usarSsl,
                Credentials = new NetworkCredential(smtp["Username"], smtp["Password"])
            };

            await client.SendMailAsync(mensagem);
        }
    }
}
