using ClosedXML.Excel;
using ControleFamiliarAPI.DTOs.Relatorios;
using ControleFamiliarAPI.Services.Interfaces;
using ControleFamiliarAPI.Data;
using ControleFamiliarAPI.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

public class RelatorioService : IRelatorioService
{
    // TTL curto: não invalidamos o cache quando uma Transacao é criada (isso
    // acoplaria TransacaoService a este cache), então a janela de dado
    // desatualizado precisa ficar pequena por si só. 30s já evita recalcular
    // do zero quando o dashboard carrega e, em seguida, o Excel é baixado.
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IMemoryCache _cache;

    public RelatorioService(AppDbContext context, ICurrentUserService currentUser, IMemoryCache cache)
    {
        _context = context;
        _currentUser = currentUser;
        _cache = cache;
    }

    public Task<ResumoPessoasDto> TotaisPorPessoa(CancellationToken cancellationToken = default)
    {
        var chave = $"relatorio:totais-pessoa:{_currentUser.FamiliaId}";

        return _cache.GetOrCreateAsync(chave, async entrada =>
        {
            entrada.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await CalcularTotaisPorPessoa(cancellationToken);
        })!;
    }

    private async Task<ResumoPessoasDto> CalcularTotaisPorPessoa(CancellationToken cancellationToken)
    {
        var pessoas = await _context.Pessoas
            .Where(p => p.FamiliaId == _currentUser.FamiliaId)
            .Select(p => new { p.Id, p.Nome })
            .ToListAsync(cancellationToken);

        // Uma única agregação (GROUP BY PessoaId) em vez de duas subqueries
        // correlacionadas por pessoa (uma pra receita, outra pra despesa) —
        // mais barato conforme o volume de Transacoes cresce. Pessoas sem
        // nenhuma transação não aparecem aqui de propósito (GroupBy só produz
        // linha pra quem tem pelo menos uma) — o merge abaixo preenche 0/0
        // pra elas, preservando o comportamento anterior.
        var totaisPorPessoa = await _context.Transacoes
            .Where(t => t.FamiliaId == _currentUser.FamiliaId)
            .GroupBy(t => t.PessoaId)
            .Select(g => new
            {
                PessoaId = g.Key,
                TotalReceitas = g.Where(t => t.Tipo == TipoTransacao.Receita).Sum(t => (decimal?)t.Valor) ?? 0,
                TotalDespesas = g.Where(t => t.Tipo == TipoTransacao.Despesa).Sum(t => (decimal?)t.Valor) ?? 0
            })
            .ToDictionaryAsync(x => x.PessoaId, cancellationToken);

        var resultado = pessoas
            .Select(p =>
            {
                totaisPorPessoa.TryGetValue(p.Id, out var totais);
                return new TotaisPessoaDto
                {
                    Pessoa = p.Nome,
                    TotalReceitas = totais?.TotalReceitas ?? 0,
                    TotalDespesas = totais?.TotalDespesas ?? 0
                };
            })
            .ToList();

        return new ResumoPessoasDto
        {
            Pessoas = resultado,
            TotalReceitas = resultado.Sum(x => x.TotalReceitas),
            TotalDespesas = resultado.Sum(x => x.TotalDespesas)
        };
    }

    public Task<List<TotaisCategoriaDto>> TotaisPorCategoria(CancellationToken cancellationToken = default)
    {
        var chave = $"relatorio:totais-categoria:{_currentUser.FamiliaId}";

        return _cache.GetOrCreateAsync(chave, async entrada =>
        {
            entrada.AbsoluteExpirationRelativeToNow = CacheTtl;

            // Sem Include: o GroupBy(t => t.Categoria!.Descricao) já resolve o
            // JOIN sozinho a partir do acesso à navegação — Include aqui seria
            // ignorado pelo EF Core.
            return await _context.Transacoes
                .Where(t => t.FamiliaId == _currentUser.FamiliaId && t.Tipo == TipoTransacao.Despesa)
                .GroupBy(t => t.Categoria!.Descricao)
                .Select(g => new TotaisCategoriaDto
                {
                    Categoria = g.Key,
                    Total = g.Sum(x => x.Valor)
                })
                .ToListAsync(cancellationToken);
        })!;
    }

    public async Task<byte[]> GerarExcelTotaisPessoa(CancellationToken cancellationToken = default)
    {
        var resumo = await TotaisPorPessoa(cancellationToken);

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Relatório");

        int linha = 1;

        ws.Cell(linha, 1).Value = "RELATÓRIO FINANCEIRO - TOTAIS POR PESSOA";
        ws.Range(linha, 1, linha, 4).Merge();
        ws.Cell(linha, 1).Style.Font.Bold = true;
        ws.Cell(linha, 1).Style.Font.FontSize = 16;

        linha += 2;

        ws.Cell(linha, 1).Value = $"Gerado em: {DateTime.Now:dd/MM/yyyy HH:mm}";
        linha += 2;

        ws.Cell(linha, 1).Value = "Pessoa";
        ws.Cell(linha, 2).Value = "Receitas";
        ws.Cell(linha, 3).Value = "Despesas";
        ws.Cell(linha, 4).Value = "Saldo";

        ws.Range(linha, 1, linha, 4).Style.Font.Bold = true;

        linha++;

        foreach (var item in resumo.Pessoas)
        {
            ws.Cell(linha, 1).Value = item.Pessoa;
            ws.Cell(linha, 2).Value = item.TotalReceitas;
            ws.Cell(linha, 3).Value = item.TotalDespesas;
            ws.Cell(linha, 4).Value = item.TotalReceitas - item.TotalDespesas;

            ws.Cell(linha, 2).Style.NumberFormat.Format = "R$ #,##0.00";
            ws.Cell(linha, 3).Style.NumberFormat.Format = "R$ #,##0.00";
            ws.Cell(linha, 4).Style.NumberFormat.Format = "R$ #,##0.00";

            linha++;
        }

        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return stream.ToArray();
    }

    public async Task<byte[]> GerarExcelTotaisCategoria(CancellationToken cancellationToken = default)
    {
        var categorias = await TotaisPorCategoria(cancellationToken);

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Relatório");

        ws.Cell(1, 1).Value = "Categoria";
        ws.Cell(1, 2).Value = "Total";

        int linha = 2;

        foreach (var item in categorias)
        {
            ws.Cell(linha, 1).Value = item.Categoria;
            ws.Cell(linha, 2).Value = item.Total;

            ws.Cell(linha, 2).Style.NumberFormat.Format = "R$ #,##0.00";

            linha++;
        }

        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return stream.ToArray();
    }
}
