using ControleFamiliarAPI.Models;
using ControleFamiliarAPI.Models.Enums;

namespace ControleFamiliarAPI.Data
{
    /// <summary>
    /// Categorias criadas automaticamente para toda família nova.
    /// </summary>
    // São só um ponto de partida: pertencem à família como qualquer outra
    // categoria e podem ser renomeadas ou excluídas. Nada no sistema obriga a
    // usá-las — a ideia é que ninguém precise cadastrar "Água" e "Luz" na mão
    // antes de lançar a primeira despesa.
    //
    // Por que uma cópia por família, e não uma tabela global de categorias do
    // sistema: todo o modelo é isolado por família (Categoria.FamiliaId é
    // obrigatório) e as transações apontam para a categoria. Categoria global
    // exigiria FamiliaId anulável, uma regra nova em todo lugar que filtra por
    // família, e ainda travaria a edição — o usuário não poderia renomear
    // "Mercado" para "Supermercado" sem afetar todo mundo.
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
        /// Monta as categorias padrão já vinculadas à família informada.
        /// </summary>
        public static IEnumerable<Categoria> ParaFamilia(int familiaId) =>
            Itens.Select(item => new Categoria
            {
                Descricao = item.Descricao,
                Finalidade = item.Finalidade,
                FamiliaId = familiaId
            });
    }
}
