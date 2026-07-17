namespace ControleFamiliarAPI.Services.Interfaces
{
    public interface IEmailService
    {
        Task EnviarConviteFamilia(string destinatario, string nomeFamilia, string codigoConvite, string convidadoPor);

        Task EnviarConfirmacaoEmail(string destinatario, string nomeUsuario, string linkConfirmacao);
    }
}
