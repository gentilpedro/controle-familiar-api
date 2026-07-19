namespace ControleFamiliarAPI.Exceptions
{
    /// <summary>
    /// Lançada quando o usuário está autenticado mas não tem uma assinatura
    /// ativa (nem em período de teste) para acessar um recurso pago.
    /// Mapeada pelo ErrorMiddleware para uma resposta HTTP 402.
    /// </summary>
    public class PagamentoRequeridoException : Exception
    {
        public PagamentoRequeridoException(string message) : base(message)
        {
        }
    }
}
