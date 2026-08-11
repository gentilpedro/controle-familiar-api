using System.ComponentModel.DataAnnotations;
using ControleFamiliarAPI.Models.Enums;

namespace ControleFamiliarAPI.DTOs.Categoria
{
    public class CategoriaUpdateDto
    {
        // Campos opcionais de propósito, como no PessoaUpdateDto: é um PATCH
        // parcial (ver CategoriaService.Atualizar) — o cliente manda só o que
        // quer alterar, então nenhum dos dois pode ser [Required].
        [MaxLength(400)]
        public string? Descricao { get; set; }

        public FinalidadeCategoria? Finalidade { get; set; }
    }
}
