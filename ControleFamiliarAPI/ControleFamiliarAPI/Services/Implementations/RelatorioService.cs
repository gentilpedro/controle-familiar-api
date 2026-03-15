using ControleFamiliarAPI.DTOs.Relatorios;
using ControleFamiliarAPI.Services.Interfaces;
using ControleGastos.Api.Data;
using ControleGastos.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace ControleFamiliarAPI.Services.Implementations
{
    public class RelatorioService : IRelatorioService
    {
        private readonly AppDbContext _context;

        public RelatorioService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ResumoPessoasDto> TotaisPorPessoa()
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

            return new ResumoPessoasDto
            {
                Pessoas = pessoas,
                TotalReceitas = pessoas.Sum(p => p.TotalReceitas),
                TotalDespesas = pessoas.Sum(p => p.TotalDespesas)
            };
        }
    }
}