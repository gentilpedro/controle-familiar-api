namespace ControleFamiliarAPI.DTOs.Assinatura
{
    public class AssinaturaStatusDto
    {
        public bool TemAcesso { get; set; }
        public string StatusIndividual { get; set; } = string.Empty;
        public string StatusFamilia { get; set; } = string.Empty;
        public bool TrialIndividualUsado { get; set; }
        public DateTime? AssinaturaIndividualValidaAte { get; set; }
        public DateTime? AssinaturaFamiliaValidaAte { get; set; }
    }
}
