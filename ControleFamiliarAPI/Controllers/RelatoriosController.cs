using ControleFamiliarAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;

namespace ControleFamiliarAPI.Controllers
{
    [ApiController]
    [Route("api/relatorios")]
    [Authorize]
    public class RelatoriosController : ControllerBase
    {
        private readonly IRelatorioService _relatorioService;

        public RelatoriosController(IRelatorioService relatorioService)
        {
            _relatorioService = relatorioService;
        }

        [HttpGet("totais-por-pessoa")]
        [Tags("Relatórios")]
        [EndpointSummary("Resumo financeiro por pessoa")]
        [EndpointDescription("""
            Retorna o total de receitas, despesas e saldo agrupado por pessoa.

            Este endpoint é utilizado para geração de gráficos e dashboards.

            Exemplo de resposta:

            {
              "pessoas": [
                {
                  "pessoa": "Pedro",
                  "totalReceitas": 5000,
                  "totalDespesas": 300
                },
                {
                  "pessoa": "Ana",
                  "totalReceitas": 1500,
                  "totalDespesas": 80
                }
              ],
              "totalReceitas": 6500,
              "totalDespesas": 380
            }
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> TotaisPorPessoa(CancellationToken cancellationToken)
        {
            var result = await _relatorioService.TotaisPorPessoa(cancellationToken);
            return Ok(result);
        }

        [HttpGet("totais-por-categoria")]
        [Tags("Relatórios")]
        [EndpointSummary("Resumo de despesas por categoria")]
        [EndpointDescription("""
            Retorna o total de despesas agrupadas por categoria.

            Utilizado para grficos de distribuio de gastos.

            Exemplo de resposta:

            [
              {
                "categoria": "Alimentação",
                "total": 300
              },
              {
                "categoria": "Lazer",
                "total": 60
              },
              {
                "categoria": "Transporte",
                "total": 80
              }
            ]
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> TotaisPorCategoria(CancellationToken cancellationToken)
        {
            var result = await _relatorioService.TotaisPorCategoria(cancellationToken);
            return Ok(result);
        }

        [HttpGet("excel-pessoa")]
        [Tags("Relatórios")]
        [EndpointSummary("Exporta relatório financeiro por pessoa (Excel)")]
        [EndpointDescription("""
            Gera um arquivo Excel contendo:

            - Pessoa
            - Total de Receitas
            - Total de Despesas
            - Saldo

            O arquivo é retornado como download no formato .xlsx.
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> ExcelPessoa(CancellationToken cancellationToken)
        {
            var file = await _relatorioService.GerarExcelTotaisPessoa(cancellationToken);

            return File(
                file,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "relatorio-financeiro-pessoas.xlsx"
            );
        }

        [HttpGet("excel-categoria")]
        [Tags("Relatórios")]
        [EndpointSummary("Exporta relatório de despesas por categoria (Excel)")]
        [EndpointDescription("""
            Gera um arquivo Excel contendo:

            - Categoria
            - Total gasto

            O arquivo é retornado como download no formato .xlsx.
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> ExcelCategoria(CancellationToken cancellationToken)
        {
            var file = await _relatorioService.GerarExcelTotaisCategoria(cancellationToken);

            return File(
                file,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "relatorio-financeiro-categorias.xlsx"
            );
        }
    }
}
