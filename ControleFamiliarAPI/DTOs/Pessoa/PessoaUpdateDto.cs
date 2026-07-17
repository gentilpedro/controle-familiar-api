using System.ComponentModel.DataAnnotations;

namespace ControleFamiliarAPI.DTOs.Pessoa
{
    public class PessoaUpdateDto
    {
        // Campos opcionais de propósito: é um PATCH parcial (ver
        // PessoaService.Atualizar) — o cliente só envia o que quer alterar,
        // por isso nenhum dos dois pode ser [Required].
        [MaxLength(200)]
        public string? Nome { get; set; }

        public int? Idade { get; set; }
    }
}