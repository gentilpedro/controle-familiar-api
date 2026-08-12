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
                    SerieId = t.SerieId,
                    NumeroParcela = t.NumeroParcela,
                    TotalParcelas = t.TotalParcelas,
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

        public async Task CriarParcelada(TransacaoParceladaCreateDto dto, CancellationToken cancellationToken = default)
        {
            if (dto.ValorTotal <= 0)
                throw new BusinessRuleException("O valor total deve ser positivo.");

            var pessoa = await _context.Pessoas
                .FirstOrDefaultAsync(p => p.Id == dto.PessoaId && p.FamiliaId == _currentUser.FamiliaId, cancellationToken);

            if (pessoa == null)
                throw new NotFoundException("Pessoa não encontrada.");

            var categoria = await _context.Categorias
                .FirstOrDefaultAsync(
                    c => c.Id == dto.CategoriaId
                        && (c.FamiliaId == _currentUser.FamiliaId || c.FamiliaId == null),
                    cancellationToken);

            if (categoria == null)
                throw new NotFoundException("Categoria não encontrada.");

            ValidarRegrasDeNegocio(pessoa, categoria, dto.Tipo);

            // Sem isto, um valor total baixo dividido em muitas parcelas gera
            // parcela de R$0,00 (ex.: R$0,05 em 10x — Round(0.005,2) = 0.00),
            // e a "última parcela" viraria o total inteiro sozinha, com N-1
            // parcelas de R$0,00 no meio. Rejeita antes de gerar nada.
            var valorParcela = Math.Round(dto.ValorTotal / dto.NumeroParcelas, 2);
            if (valorParcela <= 0)
                throw new BusinessRuleException("Valor total muito baixo para dividir em tantas parcelas.");

            // A última parcela absorve o resíduo do arredondamento: a soma
            // das parcelas bate exatamente com ValorTotal, sempre, não
            // importa quantos centavos sobrarem na divisão.
            var valorUltimaParcela = dto.ValorTotal - valorParcela * (dto.NumeroParcelas - 1);

            var serieId = Guid.NewGuid();

            await using var transacaoDb = await _context.Database.BeginTransactionAsync(cancellationToken);

            for (var i = 0; i < dto.NumeroParcelas; i++)
            {
                _context.Transacoes.Add(new Transacao
                {
                    Descricao = dto.Descricao,
                    // Sempre a partir da data original, nunca encadeado
                    // (Data.AddMonths(1).AddMonths(1)...) — encadear
                    // acumularia o efeito de clamping do AddMonths em dia
                    // 29/30/31 (parcela cairia sempre mais cedo no mês a
                    // cada mês curto no meio da série).
                    Data = dto.DataPrimeiraParcela!.Value.AddMonths(i),
                    Valor = i == dto.NumeroParcelas - 1 ? valorUltimaParcela : valorParcela,
                    Tipo = dto.Tipo,
                    PessoaId = dto.PessoaId,
                    CategoriaId = dto.CategoriaId,
                    FamiliaId = _currentUser.FamiliaId,
                    SerieId = serieId,
                    NumeroParcela = i + 1,
                    TotalParcelas = dto.NumeroParcelas
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transacaoDb.CommitAsync(cancellationToken);
        }

        public async Task CriarRecorrenciaPercentual(TransacaoRecorrenciaPercentualCreateDto dto, CancellationToken cancellationToken = default)
        {
            if (dto.ValorTotal <= 0)
                throw new BusinessRuleException("O valor total deve ser positivo.");

            var pessoa = await _context.Pessoas
                .FirstOrDefaultAsync(p => p.Id == dto.PessoaId && p.FamiliaId == _currentUser.FamiliaId, cancellationToken);

            if (pessoa == null)
                throw new NotFoundException("Pessoa não encontrada.");

            var categoria = await _context.Categorias
                .FirstOrDefaultAsync(
                    c => c.Id == dto.CategoriaId
                        && (c.FamiliaId == _currentUser.FamiliaId || c.FamiliaId == null),
                    cancellationToken);

            if (categoria == null)
                throw new NotFoundException("Categoria não encontrada.");

            if (!categoria.AceitaDivisaoPercentual)
                throw new BusinessRuleException("Esta categoria não aceita divisão percentual.");

            // Tipo é sempre Receita: só uma categoria com
            // AceitaDivisaoPercentual libera este fluxo, e ela é de Receita
            // (hoje, só "Salário").
            ValidarRegrasDeNegocio(pessoa, categoria, TipoTransacao.Receita);

            var serieId = Guid.NewGuid();
            var totalOcorrencias = dto.Ocorrencias.Count;
            var mes = dto.MesReferencia!.Value;
            var ultimoDiaDoMes = DateTime.DaysInMonth(mes.Year, mes.Month);

            await using var transacaoDb = await _context.Database.BeginTransactionAsync(cancellationToken);

            for (var i = 0; i < totalOcorrencias; i++)
            {
                var ocorrencia = dto.Ocorrencias[i];

                // Dia aceita 1..31 no DTO, mas nem todo mês tem 31 dias — o
                // construtor de DateOnly lançaria exceção em vez de clampar
                // (diferente de AddMonths). Mesma filosofia do parcelamento:
                // dia que não existe no mês cai no último dia dele.
                var diaEfetivo = Math.Min(ocorrencia.Dia!.Value, ultimoDiaDoMes);

                _context.Transacoes.Add(new Transacao
                {
                    Descricao = dto.Descricao,
                    Valor = Math.Round(dto.ValorTotal * ocorrencia.Percentual!.Value / 100, 2),
                    Tipo = TipoTransacao.Receita,
                    Data = new DateOnly(mes.Year, mes.Month, diaEfetivo),
                    PessoaId = dto.PessoaId,
                    CategoriaId = dto.CategoriaId,
                    FamiliaId = _currentUser.FamiliaId,
                    SerieId = serieId,
                    NumeroParcela = i + 1,
                    TotalParcelas = totalOcorrencias
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transacaoDb.CommitAsync(cancellationToken);
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

            await using var transacaoDb = await _context.Database.BeginTransactionAsync(cancellationToken);

            // Data só se aplica na transação editada diretamente — nunca
            // propaga (ver AplicarCampos).
            if (dto.Data.HasValue)
                transacao.Data = dto.Data.Value;

            AplicarCampos(transacao, dto, pessoaId, categoriaId, tipo);

            // Duas edições quase simultâneas na mesma série (mais provável
            // via duplo-clique/duas abas do que dois usuários concorrentes)
            // podem se sobrepor sem erro, resultado dependendo só da ordem
            // de chegada. Diferente do invariante protegido em
            // FamiliaService.RemoverMembro (nunca ficar sem admin), aqui não
            // há invariante de sistema em jogo — aceito por ora, mesmo
            // padrão "assume uso de família pequena" já usado no projeto.
            if (dto.AplicarAFuturas && transacao.SerieId != null)
            {
                var futuras = await _context.Transacoes
                    .Where(t => t.SerieId == transacao.SerieId
                        && t.NumeroParcela >= transacao.NumeroParcela
                        && t.Id != transacao.Id)
                    .ToListAsync(cancellationToken);

                foreach (var futura in futuras)
                    AplicarCampos(futura, dto, pessoaId, categoriaId, tipo);
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transacaoDb.CommitAsync(cancellationToken);
        }

        public async Task Deletar(int id, bool excluirFuturas = false, CancellationToken cancellationToken = default)
        {
            var transacao = await _context.Transacoes
                .FirstOrDefaultAsync(t => t.Id == id && t.FamiliaId == _currentUser.FamiliaId, cancellationToken);

            if (transacao == null)
                throw new NotFoundException("Transação não encontrada.");

            await using var transacaoDb = await _context.Database.BeginTransactionAsync(cancellationToken);

            if (excluirFuturas && transacao.SerieId != null)
            {
                var futuras = await _context.Transacoes
                    .Where(t => t.SerieId == transacao.SerieId
                        && t.NumeroParcela >= transacao.NumeroParcela
                        && t.Id != transacao.Id)
                    .ToListAsync(cancellationToken);

                _context.Transacoes.RemoveRange(futuras);
            }

            _context.Transacoes.Remove(transacao);

            await _context.SaveChangesAsync(cancellationToken);
            await transacaoDb.CommitAsync(cancellationToken);
        }

        // Descrição/Valor/Pessoa/Categoria/Tipo — os campos que fazem
        // sentido propagar pra ocorrências futuras da mesma série. Data fica
        // de fora de propósito: propagar mudaria o espaçamento da série.
        private static void AplicarCampos(Transacao transacao, TransacaoUpdateDto dto, int pessoaId, int categoriaId, TipoTransacao tipo)
        {
            if (!string.IsNullOrEmpty(dto.Descricao))
                transacao.Descricao = dto.Descricao;

            if (dto.Valor.HasValue)
                transacao.Valor = dto.Valor.Value;

            transacao.PessoaId = pessoaId;
            transacao.CategoriaId = categoriaId;
            transacao.Tipo = tipo;
        }

        // Compartilhada por Criar, CriarParcelada e Atualizar: todas
        // precisam validar a mesma combinação final de Pessoa/Categoria/Tipo,
        // só a origem dos valores (DTO direto vs. mesclado com o que já
        // existia) muda.
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
