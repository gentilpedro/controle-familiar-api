namespace ControleFamiliarAPI.Services.Interfaces
{
    public interface IEmailService
    {
        Task EnviarConviteFamilia(string destinatario, string nomeFamilia, string codigoConvite, string convidadoPor, CancellationToken cancellationToken = default);

        Task EnviarConfirmacaoEmail(string destinatario, string nomeUsuario, string linkConfirmacao, CancellationToken cancellationToken = default);
    }
}
