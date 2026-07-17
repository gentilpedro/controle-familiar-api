namespace ControleFamiliarAPI.Exceptions
{
    /// <summary>
    /// Lançada quando credenciais de login são inválidas. Mapeada pelo
    /// ErrorMiddleware para uma resposta HTTP 401.
    /// </summary>
    public class UnauthorizedException : Exception
    {
        public UnauthorizedException(string message) : base(message)
        {
        }
    }
}
