using System.ComponentModel.DataAnnotations;

namespace ControleFamiliarAPI.DTOs.Auth
{
    public class AtualizarPerfilDto
    {
        // Campos opcionais de propósito: é um PATCH parcial (ver
        // AuthService.AtualizarPerfil) — o cliente só envia o que quer
        // alterar.
        [MaxLength(200)]
        public string? Nome { get; set; }

        [EmailAddress]
        [MaxLength(256)]
        public string? Email { get; set; }
    }
}
