using ControleFamiliarAPI.Models;

namespace ControleFamiliarAPI.Data
{
    /// <summary>
    /// Catálogo de formas de pagamento do sistema, disponível para todas as
    /// famílias.
    /// </summary>
    // Mesmo desenho de CategoriasPadrao: não pertencem a ninguém (FamiliaId
    // nulo), aparecem na listagem de qualquer família e ninguém pode
    // renomeá-las ou excluí-las — quem quiser outra (cartão, boleto...) cria a
    // dela, que aí sim tem dono.
    //
    // Esta lista é a fonte da verdade do catálogo. A migration
    // AdicionaFormaPagamento insere exatamente estes itens; mexer aqui não
    // altera o banco sozinho, é preciso uma migration nova.
    public static class FormasPagamentoPadrao
    {
        public static readonly IReadOnlyList<string> Itens =
        [
            "Pix",
            "Dinheiro",
            "Saque",
        ];

        /// <summary>
        /// Monta as formas de pagamento do sistema, sem família dona.
        /// </summary>
        // Usada pelos testes de integração, onde o schema vem de EnsureCreated
        // a partir do modelo e as migrations não rodam — sem isto o catálogo
        // não existiria lá.
        public static IEnumerable<FormaPagamento> DoSistema() =>
            Itens.Select(descricao => new FormaPagamento
            {
                Descricao = descricao,
                FamiliaId = null
            });
    }
}
