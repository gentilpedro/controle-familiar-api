using ControleFamiliarAPI.Models;
using ControleFamiliarAPI.Models.Enums;

namespace ControleFamiliarAPI.Data
{
    /// <summary>
    /// Catálogo de categorias do sistema, disponível para todas as famílias.
    /// </summary>
    // Não pertencem a ninguém: existem uma única vez no banco, com
    // Categoria.FamiliaId nulo, e aparecem na listagem de qualquer família.
    // Ninguém pode renomeá-las ou excluí-las — quem quiser um nome próprio cria
    // a categoria dele, que aí sim tem dono.
    //
    // Esta lista é a fonte da verdade do catálogo. A migration
    // SeedCategoriasDoSistema insere exatamente estes itens; mexer aqui não
    // altera o banco sozinho, é preciso uma migration nova.
    public static class CategoriasPadrao
    {
        public static readonly IReadOnlyList<(string Descricao, FinalidadeCategoria Finalidade)> Itens =
        [
            ("Salário", FinalidadeCategoria.Receita),
            ("Renda extra", FinalidadeCategoria.Receita),

            ("Moradia", FinalidadeCategoria.Despesa),
            ("Água", FinalidadeCategoria.Despesa),
            ("Luz", FinalidadeCategoria.Despesa),
            ("Gás", FinalidadeCategoria.Despesa),
            ("Internet e telefone", FinalidadeCategoria.Despesa),
            ("Mercado", FinalidadeCategoria.Despesa),
            ("Transporte", FinalidadeCategoria.Despesa),
            ("Saúde", FinalidadeCategoria.Despesa),
            ("Educação", FinalidadeCategoria.Despesa),
            ("Lazer", FinalidadeCategoria.Despesa),

            ("Outros", FinalidadeCategoria.Ambas),
        ];

        /// <summary>
        /// Monta as categorias do sistema, sem família dona.
        /// </summary>
        // Usada pelos testes de integração, onde o schema vem de EnsureCreated
        // a partir do modelo e as migrations não rodam — sem isto o catálogo
        // não existiria lá.
        public static IEnumerable<Categoria> DoSistema() =>
            Itens.Select(item => new Categoria
            {
                Descricao = item.Descricao,
                Finalidade = item.Finalidade,
                FamiliaId = null
            });
    }
}
