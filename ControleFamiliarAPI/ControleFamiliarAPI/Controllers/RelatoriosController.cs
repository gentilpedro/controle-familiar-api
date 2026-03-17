using ControleFamiliarAPI.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ControleFamiliarAPI.Controllers
{
    /// <summary>
    /// Controller responsável pelos relatórios financeiros do sistema.
    /// </summary>
    /// <remarks>
    /// Esta controller fornece endpoints para geração de relatórios e exportação de dados.
    /// Os dados podem ser consumidos pelo frontend para gráficos ou exportados em formato Excel.
    /// </remarks>
    [ApiController]
    [Route("api/relatorios")]
    public class RelatoriosController : ControllerBase
    {
        private readonly IRelatorioService _relatorioService;

        public RelatoriosController(IRelatorioService relatorioService)
        {
            _relatorioService = relatorioService;
        }

        /// <summary>
        /// Retorna o total de receitas, despesas e saldo agrupado por pessoa.
        /// </summary>
        /// <remarks>
        /// Este endpoint é utilizado para geração de relatórios e gráficos no dashboard.
        /// 
        /// Exemplo de resposta:
        /// 
        /// {
        ///   "pessoas": [
        ///     {
        ///       "pessoa": "Pedro",
        ///       "totalReceitas": 5000,
        ///       "totalDespesas": 300
        ///     },
        ///     {
        ///       "pessoa": "Ana",
        ///       "totalReceitas": 1500,
        ///       "totalDespesas": 80
        ///     }
        ///   ],
        ///   "totalReceitas": 6500,
        ///   "totalDespesas": 380
        /// }
        /// </remarks>
        /// <returns>Resumo financeiro por pessoa</returns>
        /// <response code="200">Retorna o resumo financeiro</response>
        [HttpGet("totais-por-pessoa")]
        public async Task<ActionResult> TotaisPorPessoa()
        {
            var result = await _relatorioService.TotaisPorPessoa();

            return Ok(result);
        }

        /// <summary>
        /// Retorna o total de despesas agrupadas por categoria.
        /// </summary>
        /// <remarks>
        /// Este endpoint é utilizado para gerar gráficos de distribuição de despesas.
        /// 
        /// Exemplo de resposta:
        /// 
        /// [
        ///   {
        ///     "categoria": "Alimentação",
        ///     "total": 300
        ///   },
        ///   {
        ///     "categoria": "Lazer",
        ///     "total": 60
        ///   },
        ///   {
        ///     "categoria": "Transporte",
        ///     "total": 80
        ///   }
        /// ]
        /// </remarks>
        /// <returns>Lista contendo categoria e total de despesas</returns>
        /// <response code="200">Retorna as categorias com seus totais</response>
        [HttpGet("totais-por-categoria")]
        public async Task<ActionResult> TotaisPorCategoria()
        {
            var result = await _relatorioService.TotaisPorCategoria();

            return Ok(result);
        }

        /// <summary>
        /// Gera um relatório em Excel contendo o total de receitas, despesas e saldo por pessoa.
        /// </summary>
        /// <remarks>
        /// O arquivo gerado contém:
        /// 
        /// - Pessoa
        /// - Total de Receitas
        /// - Total de Despesas
        /// - Saldo
        /// 
        /// O relatório é retornado como download no formato Excel (.xlsx).
        /// </remarks>
        /// <returns>Arquivo Excel contendo o relatório financeiro</returns>
        /// <response code="200">Arquivo Excel gerado com sucesso</response>
        [HttpGet("excel-pessoa")]
        public async Task<IActionResult> ExcelPessoa()
        {
            var file = await _relatorioService.GerarExcelTotaisPessoa();

            return File(
                file,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "relatorio-financeiro-pessoas.xlsx"
            );
        }

        /// <summary>
        /// Gera um relatório em Excel contendo o total de despesas por categoria.
        /// </summary>
        /// <remarks>
        /// O relatório contém:
        /// 
        /// - Categoria
        /// - Total gasto
        /// 
        /// O arquivo é retornado no formato Excel (.xlsx).
        /// </remarks>
        /// <returns>Arquivo Excel contendo o relatório de categorias</returns>
        /// <response code="200">Arquivo Excel gerado com sucesso</response>
        [HttpGet("excel-categoria")]
        public async Task<IActionResult> ExcelCategoria()
        {
            var file = await _relatorioService.GerarExcelTotaisCategoria();

            return File(
                file,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "relatorio-financeiro-categorias.xlsx"
            );
        }
    }
}