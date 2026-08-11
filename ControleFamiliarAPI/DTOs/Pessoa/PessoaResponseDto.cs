namespace ControleFamiliarAPI.DTOs.Pessoa
{
    public class PessoaResponseDto
    {
        public int Id { get; set; }

        public string Nome { get; set; } = string.Empty;

        public int Idade { get; set; }

        /// <summary>
        /// True quando esta pessoa representa uma conta da família. O cliente
        /// usa para marcar quem é membro e para não oferecer exclusão de quem
        /// só sai pela tela de Minha Família.
        /// </summary>
        public bool EhMembro { get; set; }
    }
}