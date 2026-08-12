using ControleFamiliarAPI.Data;
using ControleFamiliarAPI.DTOs.PainelMensal;
using ControleFamiliarAPI.Exceptions;
using ControleFamiliarAPI.Models;
using ControleFamiliarAPI.Models.Enums;
using ControleFamiliarAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ControleFamiliarAPI.Services.Implementations
{
    /// <summary>
    /// Resumo financeiro do mês e o fechamento que transporta o saldo pro
    /// mês seguinte como uma transação normal.
    /// </summary>
    public class PainelMensalService : IPainelMensalService
    {
        private readonly AppDbContext _context;
        private readonly ICurrentUserService _currentUser;

        public PainelMensalService(AppDbContext context, ICurrentUserService currentUser)
        {
            _context = context;
            _currentUser = currentUser;
        }

        public async Task<ResumoMensalDto> ObterResumo(int ano, int mes, CancellationToken cancellationToken = default)
        {
            ValidarAnoMes(ano, mes);

            var transacoesDoMes = await _context.Transacoes
                .Where(t => t.FamiliaId == _currentUser.FamiliaId && t.Data.Year == ano && t.Data.Month == mes)
                .Select(t => new { t.Tipo, t.Pago, t.Valor })
                .ToListAsync(cancellationToken);

            var fechamento = await _context.FechamentosMensais
                .FirstOrDefaultAsync(
                    f => f.FamiliaId == _currentUser.FamiliaId && f.Mes == new DateOnly(ano, mes, 1),
                    cancellationToken);

            return new ResumoMensalDto
            {
                TotalReceitasConfirmadas = transacoesDoMes.Where(t => t.Tipo == TipoTransacao.Receita && t.Pago).Sum(t => t.Valor),
                TotalReceitasPendentes = transacoesDoMes.Where(t => t.Tipo == TipoTransacao.Receita && !t.Pago).Sum(t => t.Valor),
                TotalDespesasConfirmadas = transacoesDoMes.Where(t => t.Tipo == TipoTransacao.Despesa && t.Pago).Sum(t => t.Valor),
                TotalDespesasPendentes = transacoesDoMes.Where(t => t.Tipo == TipoTransacao.Despesa && !t.Pago).Sum(t => t.Valor),
                MesFechado = fechamento != null,
                FechadoEm = fechamento?.FechadoEm
            };
        }

        public async Task<ResumoMensalDto> FecharMes(int ano, int mes, CancellationToken cancellationToken = default)
        {
            ValidarAnoMes(ano, mes);

            var mesData = new DateOnly(ano, mes, 1);

            var jaFechado = await _context.FechamentosMensais
                .AnyAsync(f => f.FamiliaId == _currentUser.FamiliaId && f.Mes == mesData, cancellationToken);

            if (jaFechado)
                throw new BusinessRuleException("Este mês já foi fechado.");

            // Reaproveita o cálculo — o saldo do fechamento é sempre "o
            // saldo confirmado que o resumo já mostra", nunca outra conta.
            var resumo = await ObterResumo(ano, mes, cancellationToken);

            await using var transacaoDb = await _context.Database.BeginTransactionAsync(cancellationToken);

            int? transacaoGeradaId = null;

            if (resumo.Saldo != 0)
            {
                // Categoria de sistema, nome fixo e imutável (ninguém edita
                // categoria do sistema) — comparar por nome é seguro aqui,
                // diferente de AceitaDivisaoPercentual (que protege uma
                // escolha exposta ao usuário): isto é infraestrutura interna
                // do próprio seed, e FamiliaId nulo já isola do catálogo de
                // qualquer família.
                var categoriaSaldoAnterior = await _context.Categorias
                    .FirstAsync(c => c.Descricao == "Saldo Anterior" && c.FamiliaId == null, cancellationToken);

                // A transação de saldo não é "de" uma pessoa específica —
                // atribuída à pessoa do usuário que fechou o mês (o vínculo
                // Pessoa.UsuarioId já existe desde o cadastro). Se por
                // algum motivo não houver (conta antiga sem esse vínculo
                // ainda), cai pra qualquer pessoa da família — sempre existe
                // ao menos uma, o titular.
                var pessoa = await _context.Pessoas
                    .FirstOrDefaultAsync(
                        p => p.UsuarioId == _currentUser.UsuarioId && p.FamiliaId == _currentUser.FamiliaId,
                        cancellationToken)
                    ?? await _context.Pessoas
                        .FirstOrDefaultAsync(p => p.FamiliaId == _currentUser.FamiliaId, cancellationToken)
                    ?? throw new BusinessRuleException("Nenhuma pessoa cadastrada nesta família para atribuir o saldo transportado.");

                // AddMonths resolve a virada de ano sozinho (dezembro vira
                // janeiro do ano seguinte), sem cálculo manual.
                var transacaoGerada = new Transacao
                {
                    Descricao = $"Saldo de {mesData:MM/yyyy}",
                    Valor = Math.Abs(resumo.Saldo),
                    Tipo = resumo.Saldo >= 0 ? TipoTransacao.Receita : TipoTransacao.Despesa,
                    Data = mesData.AddMonths(1),
                    // Saldo já é líquido, não uma obrigação pendente.
                    Pago = true,
                    PessoaId = pessoa.Id,
                    CategoriaId = categoriaSaldoAnterior.Id,
                    FamiliaId = _currentUser.FamiliaId
                };

                // Não passa por ValidarRegrasDeNegocio (REGRA 1 do
                // TransacaoService): saldo transportado é contabilidade do
                // sistema, não "receita" no sentido de renda de um menor —
                // bloquear o fechamento porque o titular é menor de idade
                // seria um bug de UX, não uma proteção que faz sentido aqui.
                _context.Transacoes.Add(transacaoGerada);
                await _context.SaveChangesAsync(cancellationToken);
                transacaoGeradaId = transacaoGerada.Id;
            }

            _context.FechamentosMensais.Add(new FechamentoMensal
            {
                FamiliaId = _currentUser.FamiliaId,
                Mes = mesData,
                SaldoTransportado = resumo.Saldo,
                TransacaoGeradaId = transacaoGeradaId
            });

            await _context.SaveChangesAsync(cancellationToken);
            await transacaoDb.CommitAsync(cancellationToken);

            return await ObterResumo(ano, mes, cancellationToken);
        }

        private static void ValidarAnoMes(int ano, int mes)
        {
            if (mes < 1 || mes > 12)
                throw new BusinessRuleException("Mês deve estar entre 1 e 12.");

            if (ano < 2000 || ano > 2100)
                throw new BusinessRuleException("Ano inválido.");
        }
    }
}
