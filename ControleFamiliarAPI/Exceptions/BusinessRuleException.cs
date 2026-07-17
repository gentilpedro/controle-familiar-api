namespace ControleFamiliarAPI.Exceptions
{
    /// <summary>
    /// Lançada quando a requisição viola uma regra de negócio (dado
    /// inválido, estado inconsistente). Mapeada pelo ErrorMiddleware para
    /// HTTP 400.
    /// </summary>
    public class BusinessRuleException : Exception
    {
        public BusinessRuleException(string message) : base(message)
        {
        }
    }
}
