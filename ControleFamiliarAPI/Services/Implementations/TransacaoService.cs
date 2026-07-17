using ControleFamiliarAPI.DTOs.Transacao;
using ControleFamiliarAPI.Exceptions;
using ControleFamiliarAPI.Services.Interfaces;
using ControleGastos.Api.Data;
using ControleGastos.Api.Models;
using ControleGastos.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace ControleFamiliarAPI.Services.Implementations
{
    /// <summary>
    /// Serviço responsável pela lógica de negócio das transações financeiras.
    /// </summary>
    public class TransacaoService : ITransacaoService
    {
        private readonly AppDbContext _context;
        private readonly ICurrentUserService _currentUser;

        public TransacaoService(AppDbContext context, ICurrentUserService currentUser)
        {
            _context = context;
            _currentUser = currentUser;
        }

        public async Task<List<TransacaoResponseDto>> Listar()
        {
            return await _context.Transacoes
                .Where(t => t.FamiliaId == _currentUser.FamiliaId)
                .Include(t => t.Pessoa)
                .Include(t => t.Categoria)
                .Select(t => new TransacaoResponseDto
                {
                    Id = t.Id,
                    Descricao = t.Descricao,
                    Valor = t.Valor,
                    Tipo = t.Tipo,
                    Pessoa = t.Pessoa!.Nome,
                    Categoria = t.Categoria!.Descricao
                })
                .ToListAsync();
        }

        public async Task Criar(TransacaoCreateDto dto)
        {
            if (dto.Valor <= 0)
                throw new BusinessRuleException("O valor deve ser positivo.");

            var pessoa = await _context.Pessoas
                .FirstOrDefaultAsync(p => p.Id == dto.PessoaId && p.FamiliaId == _currentUser.FamiliaId);

            if (pessoa == null)
                throw new NotFoundException("Pessoa não encontrada.");

            var categoria = await _context.Categorias
                .FirstOrDefaultAsync(c => c.Id == dto.CategoriaId && c.FamiliaId == _currentUser.FamiliaId);

            if (categoria == null)
                throw new NotFoundException("Categoria não encontrada.");

            // REGRA 1
            if (pessoa.Idade < 18 && dto.Tipo == TipoTransacao.Receita)
                throw new BusinessRuleException("Menores de idade só podem registrar despesas.");

            // REGRA 2
            if (dto.Tipo == TipoTransacao.Receita &&
                categoria.Finalidade == FinalidadeCategoria.Despesa)
                throw new BusinessRuleException("Categoria incompatível.");

            if (dto.Tipo == TipoTransacao.Despesa &&
                categoria.Finalidade == FinalidadeCategoria.Receita)
                throw new BusinessRuleException("Categoria incompatível.");

            var transacao = new Transacao
            {
                Descricao = dto.Descricao,
                Valor = dto.Valor,
                Tipo = dto.Tipo,
                PessoaId = dto.PessoaId,
                CategoriaId = dto.CategoriaId,
                FamiliaId = _currentUser.FamiliaId
            };

            _context.Transacoes.Add(transacao);

            await _context.SaveChangesAsync();
        }
    }
}