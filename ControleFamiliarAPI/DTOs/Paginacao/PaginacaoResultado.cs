namespace ControleFamiliarAPI.DTOs.Paginacao
{
    public class PaginacaoResultado<T>
    {
        public List<T> Itens { get; set; } = new();

        public int PaginaAtual { get; set; }

        public int TamanhoPagina { get; set; }

        public int TotalItens { get; set; }

        public int TotalPaginas => TamanhoPagina == 0 ? 0 : (int)Math.Ceiling(TotalItens / (double)TamanhoPagina);
    }
}
