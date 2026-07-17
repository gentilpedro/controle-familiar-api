using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace ControleFamiliarAPI.Models
{
    public class Usuario : IdentityUser<int>
    {
        /// <summary>
        /// Nome de exibição do usuário.
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Nome { get; set; } = string.Empty;

        /// <summary>
        /// Família à qual este usuário pertence. Toda conta pertence a
        /// exatamente uma família — o uso "individual" é simplesmente
        /// uma família com um único membro.
        /// </summary>
        [Required]
        public int FamiliaId { get; set; }

        public Familia? Familia { get; set; }

        /// <summary>
        /// Administrador pode gerenciar os membros da família: remover,
        /// promover/rebaixar outros admins e regenerar o código de convite.
        /// Quem cria a família (modo "Nova" no cadastro) já nasce admin;
        /// quem entra por código de convite entra como membro comum.
        /// </summary>
        public bool EhAdministrador { get; set; }
    }
}
