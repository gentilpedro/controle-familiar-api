using ControleFamiliarAPI.DTOs.Paginacao;
using ControleFamiliarAPI.DTOs.Transacao;
using ControleFamiliarAPI.Exceptions;
using ControleFamiliarAPI.Services.Interfaces;
using ControleFamiliarAPI.Data;
using ControleFamiliarAPI.Models;
using ControleFamiliarAPI.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace ControleFamiliarAPI.Services.Implementations
{
    /// <summary>
    /// Serviço responsável pela lógica de negócio das transações financeiras.
    /// </summary>
    public class TransacaoService : ITransacaoService
    {
        private const int TamanhoPaginaMaximo = 200;

        private readonly AppDbContext _context;
        private readonly ICurrentUserService _currentUser;

        public TransacaoService(AppDbContext context, ICurrentUserService currentUser)
        {
            _context = context;
            _currentUser = currentUser;
        }

        public async Task<PaginacaoResultado<TransacaoResponseDto>> Listar(int pagina, int tamanhoPagina, CancellationToken cancellationToken = default)
        {
            pagina = Math.Max(pagina, 1);
            tamanhoPagina = Math.Clamp(tamanhoPagina, 1, TamanhoPaginaMaximo);

            // Sem Include: o Select abaixo já projeta só os campos escalares de
            // Pessoa/Categoria, então o EF Core gera o JOIN sozinho a partir da
            // navegação — Include aqui seria ignorado e só confundiria a leitura.
            var query = _context.Transacoes
                .Where(t => t.FamiliaId == _currentUser.FamiliaId)
                .OrderByDescending(t => t.Id);

            var totalItens = await query.CountAsync(cancellationToken);

            var itens = await query
                .Skip((pagina - 1) * tamanhoPagina)
                .Take(tamanhoPagina)
                .Select(t => new TransacaoResponseDto
                {
                    Id = t.Id,
                    Descricao = t.Descricao,
                    Valor = t.Valor,
                    Tipo = t.Tipo,
                    Pessoa = t.Pessoa!.Nome,
                    Categoria = t.Categoria!.Descricao
                })
                .ToListAsync(cancellationToken);

            return new PaginacaoResultado<TransacaoResponseDto>
            {
                Itens = itens,
                PaginaAtual = pagina,
                TamanhoPagina = tamanhoPagina,
                TotalItens = totalItens
            };
        }

        public async Task Criar(TransacaoCreateDto dto, CancellationToken cancellationToken = default)
        {
            if (dto.Valor <= 0)
                throw new BusinessRuleException("O valor deve ser positivo.");

            var pessoa = await _context.Pessoas
                .FirstOrDefaultAsync(p => p.Id == dto.PessoaId && p.FamiliaId == _currentUser.FamiliaId, cancellationToken);

            if (pessoa == null)
                throw new NotFoundException("Pessoa não encontrada.");

            // Aceita também as do sistema (FamiliaId null), que são justamente
            // as que qualquer família pode usar num lançamento.
            var categoria = await _context.Categorias
                .FirstOrDefaultAsync(
                    c => c.Id == dto.CategoriaId
                        && (c.FamiliaId == _currentUser.FamiliaId || c.FamiliaId == null),
                    cancellationToken);

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

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
