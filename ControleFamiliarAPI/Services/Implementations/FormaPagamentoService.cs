using ControleFamiliarAPI.Data;
using ControleFamiliarAPI.DTOs.FormaPagamento;
using ControleFamiliarAPI.Exceptions;
using ControleFamiliarAPI.Models;
using ControleFamiliarAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ControleFamiliarAPI.Services.Implementations
{
    /// <summary>
    /// CRUD das formas de pagamento — mesmo desenho do CategoriaService:
    /// catálogo do sistema sem dono ao lado das criadas por cada família.
    /// </summary>
    public class FormaPagamentoService : IFormaPagamentoService
    {
        private readonly AppDbContext _context;
        private readonly ICurrentUserService _currentUser;

        public FormaPagamentoService(AppDbContext context, ICurrentUserService currentUser)
        {
            _context = context;
            _currentUser = currentUser;
        }

        public async Task<List<FormaPagamentoResponseDto>> Listar(CancellationToken cancellationToken = default)
        {
            // As do sistema (FamiliaId null) aparecem para todo mundo; as da
            // família, só para ela. Uma família nunca vê a de outra.
            return await _context.FormasPagamento
                .Where(f => f.FamiliaId == _currentUser.FamiliaId || f.FamiliaId == null)
                .OrderBy(f => f.FamiliaId == null ? 0 : 1)
                .ThenBy(f => f.Id)
                .Select(f => new FormaPagamentoResponseDto
                {
                    Id = f.Id,
                    Descricao = f.Descricao,
                    EhDoSistema = f.FamiliaId == null
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<FormaPagamentoResponseDto> Criar(FormaPagamentoCreateDto dto, CancellationToken cancellationToken = default)
        {
            var formaPagamento = new FormaPagamento
            {
                Descricao = dto.Descricao,
                FamiliaId = _currentUser.FamiliaId
            };

            _context.FormasPagamento.Add(formaPagamento);
            await _context.SaveChangesAsync(cancellationToken);

            return new FormaPagamentoResponseDto
            {
                Id = formaPagamento.Id,
                Descricao = formaPagamento.Descricao,
                EhDoSistema = formaPagamento.EhDoSistema
            };
        }

        public async Task<FormaPagamentoResponseDto> Atualizar(int id, FormaPagamentoUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var formaPagamento = await BuscarVisivel(id, cancellationToken);

            // Renomear uma do sistema mudaria o catálogo de todas as famílias
            // de uma vez, pelo mesmo motivo que ela não pode ser excluída.
            if (formaPagamento.EhDoSistema)
                throw new ForbiddenException("Formas de pagamento padrão do sistema não podem ser editadas.");

            if (!string.IsNullOrWhiteSpace(dto.Descricao))
                formaPagamento.Descricao = dto.Descricao;

            await _context.SaveChangesAsync(cancellationToken);

            return new FormaPagamentoResponseDto
            {
                Id = formaPagamento.Id,
                Descricao = formaPagamento.Descricao,
                EhDoSistema = formaPagamento.EhDoSistema
            };
        }

        public async Task Deletar(int id, CancellationToken cancellationToken = default)
        {
            var formaPagamento = await BuscarVisivel(id, cancellationToken);

            // Do sistema não tem dono, então não é de ninguém para excluir —
            // apagá-la sumiria com ela para todas as famílias.
            if (formaPagamento.EhDoSistema)
                throw new ForbiddenException("Formas de pagamento padrão do sistema não podem ser excluídas.");

            // A FK é Restrict: sem esta checagem o SaveChanges estouraria
            // DbUpdateException e o usuário veria um 500 em vez da explicação
            // de por que a exclusão não pode acontecer.
            var emUso = await _context.Transacoes
                .AnyAsync(t => t.FormaPagamentoId == id, cancellationToken);

            if (emUso)
                throw new BusinessRuleException("Esta forma de pagamento está em uso por transações e não pode ser excluída.");

            _context.FormasPagamento.Remove(formaPagamento);
            await _context.SaveChangesAsync(cancellationToken);
        }

        // Busca sem filtrar por família para conseguir distinguir os dois
        // casos: forma do sistema (existe, mas ninguém edita nem apaga) de
        // forma de outra família (que, para quem pergunta, não existe).
        private async Task<FormaPagamento> BuscarVisivel(int id, CancellationToken cancellationToken)
        {
            var formaPagamento = await _context.FormasPagamento
                .FirstOrDefaultAsync(
                    f => f.Id == id && (f.FamiliaId == _currentUser.FamiliaId || f.FamiliaId == null),
                    cancellationToken);

            if (formaPagamento == null)
                throw new NotFoundException("Forma de pagamento não encontrada.");

            return formaPagamento;
        }
    }
}
