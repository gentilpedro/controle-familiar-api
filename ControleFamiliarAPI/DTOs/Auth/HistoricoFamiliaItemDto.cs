namespace ControleFamiliarAPI.DTOs.Auth
{
    /// <summary>
    /// Um evento do histórico de membros da família — criação, entrada,
    /// remoção ou saída. Recorte curado de RegistroAuditoria: só as ações que
    /// contam a história de quem esteve na família, não a trilha completa de
    /// auditoria (que também cobre promoção/rebaixamento de admin).
    /// </summary>
    public class HistoricoFamiliaItemDto
    {
        /// <summary>
        /// "CriacaoFamilia", "EntradaFamilia", "RemocaoMembro" ou "ExclusaoConta".
        /// </summary>
        public string Acao { get; set; } = string.Empty;

        /// <summary>
        /// Nome de quem o evento é sobre, no momento em que aconteceu. Nulo só
        /// é possível em registro anterior a esta coluna existir.
        /// </summary>
        public string? NomeAlvo { get; set; }

        public DateTime CriadoEm { get; set; }
    }
}
