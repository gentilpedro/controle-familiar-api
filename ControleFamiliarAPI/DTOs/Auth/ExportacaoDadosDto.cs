namespace ControleFamiliarAPI.DTOs.Auth
{
    // Formato de saída dedicado à exportação (LGPD, art. 18, V) — não
    // reaproveita os DTOs de listagem porque aqui os campos precisam ser
    // legíveis por uma pessoa (ex.: Finalidade/Tipo como texto, não como o
    // int cru que a API usa internamente para o frontend).
    public class ExportacaoDadosDto
    {
        public ExportacaoUsuarioDto Usuario { get; set; } = new();
        public ExportacaoFamiliaDto Familia { get; set; } = new();
        public List<ExportacaoPessoaDto> Pessoas { get; set; } = new();
        public List<ExportacaoCategoriaDto> Categorias { get; set; } = new();
        public List<ExportacaoTransacaoDto> Transacoes { get; set; } = new();
    }

    public class ExportacaoUsuarioDto
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool EhAdministrador { get; set; }
    }

    public class ExportacaoFamiliaDto
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string CodigoConvite { get; set; } = string.Empty;
        public DateTime CriadoEm { get; set; }
    }

    public class ExportacaoPessoaDto
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public int Idade { get; set; }
    }

    public class ExportacaoCategoriaDto
    {
        public int Id { get; set; }
        public string Descricao { get; set; } = string.Empty;
        public string Finalidade { get; set; } = string.Empty;
    }

    public class ExportacaoTransacaoDto
    {
        public int Id { get; set; }
        public string Descricao { get; set; } = string.Empty;
        public decimal Valor { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string Pessoa { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
    }
}
