namespace ControleFamiliarAPI.Exceptions
{
    /// <summary>
    /// Lançada quando um recurso solicitado não existe (ou não pertence à
    /// família do usuário atual). Mapeada pelo ErrorMiddleware para HTTP 404.
    /// </summary>
    public class NotFoundException : Exception
    {
        public NotFoundException(string message) : base(message)
        {
        }
    }
}
