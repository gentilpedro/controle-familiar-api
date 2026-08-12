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
            // Data primeiro, Id como desempate: duas transações lançadas no
            // mesmo dia mantêm a ordem de criação entre si, e uma parcela
            // futura (Data adiante, criada hoje) aparece na posição
            // correspondente à data dela, não no topo por ter Id maior.
            var query = _context.Transacoes
                .Where(t => t.FamiliaId == _currentUser.FamiliaId)
                .OrderByDescending(t => t.Data)
                .ThenByDescending(t => t.Id);

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
                    Data = t.Data,
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

            ValidarRegrasDeNegocio(pessoa, categoria, dto.Tipo);

            var transacao = new Transacao
            {
                Descricao = dto.Descricao,
                Valor = dto.Valor,
                Tipo = dto.Tipo,
                Data = dto.Data!.Value,
                PessoaId = dto.PessoaId,
                CategoriaId = dto.CategoriaId,
                FamiliaId = _currentUser.FamiliaId
            };

            _context.Transacoes.Add(transacao);

            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task Atualizar(int id, TransacaoUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var transacao = await _context.Transacoes
                .FirstOrDefaultAsync(t => t.Id == id && t.FamiliaId == _currentUser.FamiliaId, cancellationToken);

            if (transacao == null)
                throw new NotFoundException("Transação não encontrada.");

            if (dto.Valor.HasValue && dto.Valor.Value <= 0)
                throw new BusinessRuleException("O valor deve ser positivo.");

            // As regras (REGRA 1/REGRA 2) valem sobre o resultado final, não
            // só sobre o campo que mudou — editar só a Descrição não deveria
            // reavaliar nada, mas editar só o Tipo precisa checar contra a
            // Pessoa e a Categoria que a transação já tinha.
            var pessoaId = dto.PessoaId ?? transacao.PessoaId;
            var categoriaId = dto.CategoriaId ?? transacao.CategoriaId;
            var tipo = dto.Tipo ?? transacao.Tipo;

            var pessoa = await _context.Pessoas
                .FirstOrDefaultAsync(p => p.Id == pessoaId && p.FamiliaId == _currentUser.FamiliaId, cancellationToken);

            if (pessoa == null)
                throw new NotFoundException("Pessoa não encontrada.");

            var categoria = await _context.Categorias
                .FirstOrDefaultAsync(
                    c => c.Id == categoriaId
                        && (c.FamiliaId == _currentUser.FamiliaId || c.FamiliaId == null),
                    cancellationToken);

            if (categoria == null)
                throw new NotFoundException("Categoria não encontrada.");

            ValidarRegrasDeNegocio(pessoa, categoria, tipo);

            if (!string.IsNullOrEmpty(dto.Descricao))
                transacao.Descricao = dto.Descricao;

            if (dto.Valor.HasValue)
                transacao.Valor = dto.Valor.Value;

            if (dto.Data.HasValue)
                transacao.Data = dto.Data.Value;

            transacao.PessoaId = pessoaId;
            transacao.CategoriaId = categoriaId;
            transacao.Tipo = tipo;

            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task Deletar(int id, CancellationToken cancellationToken = default)
        {
            var transacao = await _context.Transacoes
                .FirstOrDefaultAsync(t => t.Id == id && t.FamiliaId == _currentUser.FamiliaId, cancellationToken);

            if (transacao == null)
                throw new NotFoundException("Transação não encontrada.");

            _context.Transacoes.Remove(transacao);

            await _context.SaveChangesAsync(cancellationToken);
        }

        // Compartilhada por Criar e Atualizar: as duas precisam validar a
        // mesma combinação final de Pessoa/Categoria/Tipo, só a origem dos
        // valores (DTO direto vs. mesclado com o que já existia) muda.
        private static void ValidarRegrasDeNegocio(Pessoa pessoa, Categoria categoria, TipoTransacao tipo)
        {
            // REGRA 1
            if (pessoa.Idade < 18 && tipo == TipoTransacao.Receita)
                throw new BusinessRuleException("Menores de idade só podem registrar despesas.");

            // REGRA 2
            if (tipo == TipoTransacao.Receita && categoria.Finalidade == FinalidadeCategoria.Despesa)
                throw new BusinessRuleException("Categoria incompatível.");

            if (tipo == TipoTransacao.Despesa && categoria.Finalidade == FinalidadeCategoria.Receita)
                throw new BusinessRuleException("Categoria incompatível.");
        }
    }
}
