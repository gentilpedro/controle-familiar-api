using ControleFamiliarAPI.DTOs.Relatorios;
using ControleFamiliarAPI.Responses;
using ControleGastos.Api.Data;
using ControleGastos.Api.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControleFamiliarAPI.Controllers
{
    [ApiController]
    [Route("api/relatorios")]
    public class RelatoriosController : ControllerBase
    {
        private readonly AppDbContext _context;

        public RelatoriosController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Retorna o total de receitas, despesas e saldo por pessoa.
        /// </summary>
        /// <remarks>
        /// Este endpoint calcula os totais financeiros agrupados por pessoa.
        /// Também retorna os totais gerais do sistema.
        /// </remarks>
        /// <returns>Resumo financeiro por pessoa</returns>
        [HttpGet("totais-por-pessoa")]
        public async Task<ActionResult<ResumoPessoasDto>> TotaisPorPessoa()
        {
            var pessoas = await _context.Pessoas
                .Select(p => new TotaisPessoaDto
                {
                    Pessoa = p.Nome,

                    TotalReceitas = p.Transacoes
                        .Where(t => t.Tipo == TipoTransacao.Receita)
                        .Sum(t => (decimal?)t.Valor) ?? 0,

                    TotalDespesas = p.Transacoes
                        .Where(t => t.Tipo == TipoTransacao.Despesa)
                        .Sum(t => (decimal?)t.Valor) ?? 0
                })
                .ToListAsync();

            var resumo = new ResumoPessoasDto
            {
                Pessoas = pessoas,
                TotalReceitas = pessoas.Sum(p => p.TotalReceitas),
                TotalDespesas = pessoas.Sum(p => p.TotalDespesas)
            };

            return Ok(resumo);
        }

        /// <summary>
        /// Retorna o total de despesas agrupadas por categoria.
        /// </summary>
        /// <remarks>
        /// Este endpoint é utilizado para geração de relatórios e gráficos,
        /// permitindo visualizar quanto foi gasto em cada categoria.
        /// </remarks>
        /// <returns>Lista contendo categoria e total de despesas</returns>

        [HttpGet("totais-por-categoria")]
        public async Task<ActionResult> TotaisPorCategoria()
        {
            var resultado = await _context.Transacoes
                .Include(t => t.Categoria)
                .Where(t => t.Tipo == TipoTransacao.Despesa)
                .GroupBy(t => t.Categoria!.Descricao)
                .Select(g => new
                {
                    Categoria = g.Key,
                    Total = g.Sum(t => t.Valor)
                })
                .ToListAsync();

            return Ok(resultado);
        }
    }
}