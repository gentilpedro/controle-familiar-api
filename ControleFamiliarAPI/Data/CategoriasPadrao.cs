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
        // AceitaDivisaoPercentual só é true pra "Salário" — é o único lugar
        // do código que nomeia essa categoria por string, porque um seed
        // precisa declarar seus dados de algum jeito. A regra de negócio em
        // si (TransacaoService.CriarRecorrenciaPercentual) nunca compara
        // nome de categoria, só lê esta flag.
        public static readonly IReadOnlyList<(string Descricao, FinalidadeCategoria Finalidade, bool AceitaDivisaoPercentual)> Itens =
        [
            ("Salário", FinalidadeCategoria.Receita, true),
            ("Renda extra", FinalidadeCategoria.Receita, false),

            ("Moradia", FinalidadeCategoria.Despesa, false),
            ("Água", FinalidadeCategoria.Despesa, false),
            ("Luz", FinalidadeCategoria.Despesa, false),
            ("Gás", FinalidadeCategoria.Despesa, false),
            ("Internet e telefone", FinalidadeCategoria.Despesa, false),
            ("Mercado", FinalidadeCategoria.Despesa, false),
            ("Transporte", FinalidadeCategoria.Despesa, false),
            ("Saúde", FinalidadeCategoria.Despesa, false),
            ("Educação", FinalidadeCategoria.Despesa, false),
            ("Lazer", FinalidadeCategoria.Despesa, false),

            ("Outros", FinalidadeCategoria.Ambas, false),
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
                AceitaDivisaoPercentual = item.AceitaDivisaoPercentual,
                FamiliaId = null
            });
    }
}
