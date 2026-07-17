namespace ControleFamiliarAPI.DTOs.Auth
{
    public class UsuarioDto
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool EhAdministrador { get; set; }
    }

    public class MembroDto
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public bool EhAdministrador { get; set; }
    }

    public class FamiliaDto
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string CodigoConvite { get; set; } = string.Empty;
        public List<MembroDto> Membros { get; set; } = new();
    }

    public class AuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiraEm { get; set; }
        public UsuarioDto Usuario { get; set; } = new();
        public FamiliaDto Familia { get; set; } = new();
    }

    public class MeDto
    {
        public UsuarioDto Usuario { get; set; } = new();
        public FamiliaDto Familia { get; set; } = new();
    }
}
