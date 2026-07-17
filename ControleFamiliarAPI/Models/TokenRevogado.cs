namespace ControleFamiliarAPI.Models
{
    /// <summary>
    /// Registro de um JWT revogado antes do vencimento natural (logout).
    /// Verificado a cada requisição autenticada (ver Program.cs,
    /// OnTokenValidated) contra a claim "jti" do token. Linhas expiradas são
    /// removidas de forma oportunista a cada novo logout — não precisam de
    /// job de limpeza separado, o volume é baixo (só tokens revogados antes
    /// de expirar, não todo token emitido).
    /// </summary>
    public class TokenRevogado
    {
        public string Jti { get; set; } = string.Empty;

        public DateTime ExpiraEm { get; set; }
    }
}
