namespace ControleFamiliarAPI.Exceptions
{
    /// <summary>
    /// Lançada quando o usuário está autenticado mas não tem permissão para
    /// a ação (ex.: não é administrador da família). Mapeada pelo
    /// ErrorMiddleware para uma resposta HTTP 403.
    /// </summary>
    public class ForbiddenException : Exception
    {
        public ForbiddenException(string message) : base(message)
        {
        }
    }
}
