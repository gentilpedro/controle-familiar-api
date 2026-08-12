using System.ComponentModel.DataAnnotations;

namespace ControleFamiliarAPI.Models
{
    /// <summary>
    /// Trilha de operações sensíveis sobre dados pessoais (LGPD, art. 37).
    /// Propositalmente sem FK para Usuario/Familia: é um log de auditoria
    /// somente-inserção que precisa sobreviver à exclusão dessas entidades
    /// (ex.: ExclusaoConta apaga o Usuario e a Familia na mesma transação
    /// em que o registro é gravado) — os IDs abaixo são um retrato de quem
    /// fez o quê no momento da ação, não uma referência viva.
    /// </summary>
    public class RegistroAuditoria
    {
        public int Id { get; set; }

        [Required]
        public int UsuarioId { get; set; }

        [Required]
        public int FamiliaId { get; set; }

        /// <summary>
        /// Usuário afetado pela ação, quando a ação é sobre outra pessoa
        /// (ex.: RemocaoMembro). Nulo para ações sobre o próprio usuário
        /// (ex.: ExclusaoConta).
        /// </summary>
        public int? UsuarioAlvoId { get; set; }

        /// <summary>
        /// Nome do usuário afetado no momento da ação, denormalizado de
        /// propósito. UsuarioAlvoId (e mesmo UsuarioId) pode apontar pra uma
        /// linha que não existe mais — pelo mesmo motivo desta entidade não
        /// ter FK — então um histórico legível pra humano (ex.: "quem entrou
        /// e saiu da família") não pode depender de JOIN pra mostrar um nome.
        /// Nulo nas ações registradas antes desta coluna existir.
        /// </summary>
        [MaxLength(200)]
        public string? NomeAlvo { get; set; }

        [Required]
        [MaxLength(50)]
        public string Acao { get; set; } = string.Empty;

        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    }
}
