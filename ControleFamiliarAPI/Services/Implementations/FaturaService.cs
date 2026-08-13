using ControleFamiliarAPI.Data;
using ControleFamiliarAPI.DTOs.Fatura;
using ControleFamiliarAPI.DTOs.Transacao;
using ControleFamiliarAPI.Exceptions;
using ControleFamiliarAPI.Models;
using ControleFamiliarAPI.Models.Enums;
using Microsoft.EntityFrameworkCore;
using ControleFamiliarAPI.Services.Interfaces;

namespace ControleFamiliarAPI.Services.Implementations
{
    /// <summary>
    /// Faturas de cartão de crédito. Tudo aqui é <b>calculado na hora</b> a
    /// partir das transações — não existe entidade Fatura, nada é gravado e
    /// nenhuma transação é gerada.
    /// </summary>
    // Só entra na fatura o que foi lançado com a forma de pagamento daquele
    // cartão. Pix, débito e dinheiro são outras formas de pagamento, então
    // ficam de fora mesmo sendo do mesmo banco — é a forma que decide, não o
    // banco (que o app nem conhece).
    public class FaturaService : IFaturaService
    {
        private readonly AppDbContext _context;
        private readonly ICurrentUserService _currentUser;

        public FaturaService(AppDbContext context, ICurrentUserService currentUser)
        {
            _context = context;
            _currentUser = currentUser;
        }

        public async Task<List<FaturaDto>> ListarPorVencimento(int ano, int mes, CancellationToken cancellationToken = default)
        {
            if (mes < 1 || mes > 12)
                throw new BusinessRuleException("Mês deve estar entre 1 e 12.");

            if (ano < 2000 || ano > 2100)
                throw new BusinessRuleException("Ano inválido.");

            var cartoes = await BuscarCartoes(cancellationToken);
            var faturas = new List<FaturaDto>();

            foreach (var cartao in cartoes)
            {
                // Dois candidatos: o fechamento deste mês e o do mês anterior.
                // Conforme a configuração do cartão, o vencimento cai no mesmo
                // mês do fechamento (fecha dia 1, vence dia 10) ou no seguinte
                // (o caso comum: fecha dia 28, vence dia 10). Testar os dois
                // cobre as duas configurações sem um "se" sobre qual é qual.
                var esteMes = DiaDoMes(ano, mes, cartao.DiaFechamento!.Value);
                var mesAnterior = new DateOnly(ano, mes, 1).AddMonths(-1);
                var anterior = DiaDoMes(mesAnterior.Year, mesAnterior.Month, cartao.DiaFechamento.Value);

                foreach (var fechamento in new[] { anterior, esteMes })
                {
                    var vencimento = VencimentoDe(fechamento, cartao.DiaVencimento!.Value);

                    if (vencimento.Year == ano && vencimento.Month == mes)
                        faturas.Add(await MontarFatura(cartao, fechamento, cancellationToken));
                }
            }

            return faturas.OrderBy(f => f.DataVencimento).ThenBy(f => f.FormaPagamento).ToList();
        }

        public async Task<List<FaturaDto>> ListarAbertas(CancellationToken cancellationToken = default)
        {
            var hoje = HojeUtc();
            var cartoes = await BuscarCartoes(cancellationToken);
            var faturas = new List<FaturaDto>();

            foreach (var cartao in cartoes)
            {
                var fechamento = FechamentoQueContem(hoje, cartao.DiaFechamento!.Value);
                faturas.Add(await MontarFatura(cartao, fechamento, cancellationToken));
            }

            return faturas.OrderBy(f => f.DataVencimento).ThenBy(f => f.FormaPagamento).ToList();
        }

        private async Task<List<FormaPagamento>> BuscarCartoes(CancellationToken cancellationToken) =>
            // Só as da própria família: as do sistema (Pix, Dinheiro, Saque)
            // não têm ciclo e são imutáveis, então nunca são cartão.
            await _context.FormasPagamento
                .Where(f => f.FamiliaId == _currentUser.FamiliaId
                    && f.DiaFechamento != null
                    && f.DiaVencimento != null)
                .OrderBy(f => f.Descricao)
                .ToListAsync(cancellationToken);

        private async Task<FaturaDto> MontarFatura(FormaPagamento cartao, DateOnly fechamento, CancellationToken cancellationToken)
        {
            var fechamentoAnterior = FechamentoAnterior(fechamento, cartao.DiaFechamento!.Value);
            var vencimento = VencimentoDe(fechamento, cartao.DiaVencimento!.Value);

            // Intervalo aberto no início e fechado no fim: compra feita NO dia
            // do fechamento ainda entra nesta fatura, e a do dia seguinte já é
            // da próxima. É a convenção dos cartões daqui.
            var lancamentos = await _context.Transacoes
                .Where(t => t.FamiliaId == _currentUser.FamiliaId
                    && t.FormaPagamentoId == cartao.Id
                    && t.Data > fechamentoAnterior
                    && t.Data <= fechamento)
                .OrderByDescending(t => t.Data)
                .ThenByDescending(t => t.Id)
                .Select(t => new TransacaoResponseDto
                {
                    Id = t.Id,
                    Descricao = t.Descricao,
                    Valor = t.Valor,
                    Tipo = t.Tipo,
                    Data = t.Data,
                    Pago = t.Pago,
                    SerieId = t.SerieId,
                    NumeroParcela = t.NumeroParcela,
                    TotalParcelas = t.TotalParcelas,
                    Pessoa = t.Pessoa!.Nome,
                    Categoria = t.Categoria!.Descricao,
                    FormaPagamento = t.FormaPagamento!.Descricao,
                    PessoaId = t.PessoaId,
                    CategoriaId = t.CategoriaId,
                    FormaPagamentoId = t.FormaPagamentoId
                })
                .ToListAsync(cancellationToken);

            return new FaturaDto
            {
                FormaPagamentoId = cartao.Id,
                FormaPagamento = cartao.Descricao,
                DataInicio = fechamentoAnterior.AddDays(1),
                DataFechamento = fechamento,
                DataVencimento = vencimento,
                Fechada = HojeUtc() > fechamento,
                // Estorno (lançado como Receita no cartão) abate a fatura,
                // como no extrato de verdade.
                Total = lancamentos.Where(l => l.Tipo == TipoTransacao.Despesa).Sum(l => l.Valor)
                      - lancamentos.Where(l => l.Tipo == TipoTransacao.Receita).Sum(l => l.Valor),
                QuantidadeLancamentos = lancamentos.Count,
                CategoriaFaturaId = cartao.CategoriaFaturaId,
                CategoriaFatura = cartao.CategoriaFaturaId == null
                    ? null
                    : await _context.Categorias
                        .Where(c => c.Id == cartao.CategoriaFaturaId)
                        .Select(c => c.Descricao)
                        .FirstOrDefaultAsync(cancellationToken),
                TotalPagamentosLancados = await SomarPagamentosLancados(cartao, vencimento, cancellationToken),
                Lancamentos = lancamentos
            };
        }

        /// <summary>
        /// Despesas lançadas na categoria da fatura dentro do mês do
        /// vencimento — é assim que a tela sabe se o pagamento já foi feito.
        /// </summary>
        // Regra deliberadamente simples e explicável: "o que está na
        // categoria X, no mês em que esta fatura vence". Pagamento lançado em
        // outro mês não é reconhecido — preferi isso a uma janela elástica em
        // torno do vencimento, que acertaria mais casos e erraria de um jeito
        // impossível de explicar pro usuário.
        private async Task<decimal?> SomarPagamentosLancados(FormaPagamento cartao, DateOnly vencimento, CancellationToken cancellationToken)
        {
            if (cartao.CategoriaFaturaId == null)
                return null;

            var inicio = new DateOnly(vencimento.Year, vencimento.Month, 1);
            var fim = inicio.AddMonths(1);

            return await _context.Transacoes
                .Where(t => t.FamiliaId == _currentUser.FamiliaId
                    && t.CategoriaId == cartao.CategoriaFaturaId
                    && t.Tipo == TipoTransacao.Despesa
                    && t.Data >= inicio
                    && t.Data < fim)
                .SumAsync(t => t.Valor, cancellationToken);
        }

        // O "hoje" do servidor. UtcNow como no resto do projeto: num fuso
        // atrás de UTC isso pode adiantar a virada do dia em algumas horas,
        // o que aqui só afeta o rótulo Aberta/Fechada na data exata do
        // fechamento — não muda soma nenhuma.
        private static DateOnly HojeUtc() => DateOnly.FromDateTime(DateTime.UtcNow);

        // Dia que não existe no mês (29/30/31) cai no último dia dele — mesma
        // filosofia de clamping do parcelamento e da recorrência percentual.
        private static DateOnly DiaDoMes(int ano, int mes, int dia) =>
            new(ano, mes, Math.Min(dia, DateTime.DaysInMonth(ano, mes)));

        // Fechamento do ciclo que contém a data: o primeiro fechamento >= ela.
        private static DateOnly FechamentoQueContem(DateOnly data, int diaFechamento)
        {
            var noMes = DiaDoMes(data.Year, data.Month, diaFechamento);

            if (data <= noMes)
                return noMes;

            var proximo = new DateOnly(data.Year, data.Month, 1).AddMonths(1);
            return DiaDoMes(proximo.Year, proximo.Month, diaFechamento);
        }

        // Vence no mesmo mês do fechamento quando o dia do vencimento vem
        // depois dele; senão, no mês seguinte (o caso comum: fecha 28, vence
        // 10). AddMonths resolve a virada de ano sozinho.
        private static DateOnly VencimentoDe(DateOnly fechamento, int diaVencimento)
        {
            var noMes = DiaDoMes(fechamento.Year, fechamento.Month, diaVencimento);

            if (noMes > fechamento)
                return noMes;

            var proximo = new DateOnly(fechamento.Year, fechamento.Month, 1).AddMonths(1);
            return DiaDoMes(proximo.Year, proximo.Month, diaVencimento);
        }

        // Derivado do trio (ano, mês, dia) do mês anterior, nunca de
        // fechamento.AddMonths(-1): num cartão que fecha dia 31, o fechamento
        // de fevereiro é 28, e AddMonths(-1) devolveria 28/01 — os dias 29 a
        // 31 de janeiro sumiriam de todas as faturas.
        private static DateOnly FechamentoAnterior(DateOnly fechamento, int diaFechamento)
        {
            var mesAnterior = new DateOnly(fechamento.Year, fechamento.Month, 1).AddMonths(-1);
            return DiaDoMes(mesAnterior.Year, mesAnterior.Month, diaFechamento);
        }
    }
}
