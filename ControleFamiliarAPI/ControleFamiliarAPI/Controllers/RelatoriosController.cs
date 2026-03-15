using ControleFamiliarAPI.DTOs.Relatorios;
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
        /// Consulta totais por pessoa
        /// </summary>
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
    }
}