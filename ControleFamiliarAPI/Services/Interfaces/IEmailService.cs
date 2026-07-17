namespace ControleFamiliarAPI.Services.Interfaces
{
    public interface IEmailService
    {
        Task EnviarConviteFamilia(string destinatario, string nomeFamilia, string codigoConvite, string convidadoPor);
    }
}
