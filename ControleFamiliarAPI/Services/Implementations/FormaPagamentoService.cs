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
    /// Uma forma da família pode ainda ser configurada como cartão de crédito
    /// (dia de fechamento + dia de vencimento), o que liga o ciclo de fatura.
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
                    EhDoSistema = f.FamiliaId == null,
                    EhCartaoCredito = f.DiaFechamento != null && f.DiaVencimento != null,
                    DiaFechamento = f.DiaFechamento,
                    DiaVencimento = f.DiaVencimento,
                    CategoriaFaturaId = f.CategoriaFaturaId,
                    // Navegação anulável: LEFT JOIN, devolve null quando não
                    // há categoria vinculada.
                    CategoriaFatura = f.CategoriaFatura!.Descricao
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<FormaPagamentoResponseDto> Criar(FormaPagamentoCreateDto dto, CancellationToken cancellationToken = default)
        {
            ValidarCiclo(dto.DiaFechamento, dto.DiaVencimento, dto.CategoriaFaturaId);
            await GarantirCategoriaFaturaValida(dto.CategoriaFaturaId, cancellationToken);

            var formaPagamento = new FormaPagamento
            {
                Descricao = dto.Descricao,
                DiaFechamento = dto.DiaFechamento,
                DiaVencimento = dto.DiaVencimento,
                CategoriaFaturaId = dto.CategoriaFaturaId,
                FamiliaId = _currentUser.FamiliaId
            };

            _context.FormasPagamento.Add(formaPagamento);
            await _context.SaveChangesAsync(cancellationToken);

            return await Projetar(formaPagamento.Id, cancellationToken);
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

            if (dto.RemoverCartao)
            {
                // Deixa de ser cartão: some do ciclo de faturas. Os
                // lançamentos que já usaram esta forma continuam intactos —
                // só param de ser agrupados em fatura.
                formaPagamento.DiaFechamento = null;
                formaPagamento.DiaVencimento = null;
                formaPagamento.CategoriaFaturaId = null;
            }
            else
            {
                var diaFechamento = dto.DiaFechamento ?? formaPagamento.DiaFechamento;
                var diaVencimento = dto.DiaVencimento ?? formaPagamento.DiaVencimento;
                var categoriaFaturaId = dto.CategoriaFaturaId ?? formaPagamento.CategoriaFaturaId;

                // Valida o resultado final da mesclagem, não só o campo
                // enviado — mesmo raciocínio do TransacaoService.Atualizar:
                // mandar só DiaVencimento num cartão que ainda não tem
                // fechamento tem que dar 400, não gravar meio ciclo.
                ValidarCiclo(diaFechamento, diaVencimento, categoriaFaturaId);
                await GarantirCategoriaFaturaValida(categoriaFaturaId, cancellationToken);

                formaPagamento.DiaFechamento = diaFechamento;
                formaPagamento.DiaVencimento = diaVencimento;
                formaPagamento.CategoriaFaturaId = categoriaFaturaId;
            }

            await _context.SaveChangesAsync(cancellationToken);

            return await Projetar(formaPagamento.Id, cancellationToken);
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

        // Os dois dias andam juntos: um só não descreve ciclo nenhum, e
        // gravar meio ciclo deixaria o cartão num estado que a tela de
        // faturas não sabe interpretar.
        private static void ValidarCiclo(int? diaFechamento, int? diaVencimento, int? categoriaFaturaId)
        {
            if (diaFechamento.HasValue != diaVencimento.HasValue)
                throw new BusinessRuleException("Informe o dia de fechamento e o de vencimento juntos.");

            if (categoriaFaturaId.HasValue && !diaFechamento.HasValue)
                throw new BusinessRuleException("A categoria da fatura só se aplica a cartão de crédito — informe o ciclo da fatura.");
        }

        // Aceita também as do sistema (FamiliaId null): "Outros" serve tão
        // bem quanto uma categoria própria pra receber o pagamento da fatura.
        private async Task GarantirCategoriaFaturaValida(int? categoriaFaturaId, CancellationToken cancellationToken)
        {
            if (categoriaFaturaId == null)
                return;

            var existe = await _context.Categorias
                .AnyAsync(
                    c => c.Id == categoriaFaturaId
                        && (c.FamiliaId == _currentUser.FamiliaId || c.FamiliaId == null),
                    cancellationToken);

            if (!existe)
                throw new NotFoundException("Categoria não encontrada.");
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

        // Relê pela mesma projeção do Listar em vez de montar o DTO na mão:
        // é o que resolve o nome da categoria da fatura sem um Include extra,
        // e mantém uma única definição do formato de resposta.
        private async Task<FormaPagamentoResponseDto> Projetar(int id, CancellationToken cancellationToken)
        {
            var formas = await Listar(cancellationToken);
            return formas.Single(f => f.Id == id);
        }
    }
}
