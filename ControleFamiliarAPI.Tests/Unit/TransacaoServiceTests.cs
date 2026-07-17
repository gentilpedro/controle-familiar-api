using ControleFamiliarAPI.Data;
using ControleFamiliarAPI.DTOs.Transacao;
using ControleFamiliarAPI.Exceptions;
using ControleFamiliarAPI.Models;
using ControleFamiliarAPI.Models.Enums;
using ControleFamiliarAPI.Services.Implementations;
using Microsoft.EntityFrameworkCore;

namespace ControleFamiliarAPI.Tests.Unit;

public class TransacaoServiceTests
{
    private static AppDbContext CriarContexto()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task Criar_ComValorNaoPositivo_LancaBusinessRuleException()
    {
        using var context = CriarContexto();
        var service = new TransacaoService(context, new FakeCurrentUserService());

        var dto = new TransacaoCreateDto { Descricao = "Teste", Valor = 0, Tipo = TipoTransacao.Despesa, PessoaId = 1, CategoriaId = 1 };

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.Criar(dto));
    }

    [Fact]
    public async Task Criar_ComPessoaInexistenteNaFamilia_LancaNotFoundException()
    {
        using var context = CriarContexto();
        var currentUser = new FakeCurrentUserService();

        context.Categorias.Add(new Categoria { Id = 1, Descricao = "Salário", Finalidade = FinalidadeCategoria.Ambas, FamiliaId = currentUser.FamiliaId });
        await context.SaveChangesAsync();

        var service = new TransacaoService(context, currentUser);
        var dto = new TransacaoCreateDto { Descricao = "Teste", Valor = 100, Tipo = TipoTransacao.Receita, PessoaId = 999, CategoriaId = 1 };

        await Assert.ThrowsAsync<NotFoundException>(() => service.Criar(dto));
    }

    [Fact]
    public async Task Criar_ComCategoriaInexistenteNaFamilia_LancaNotFoundException()
    {
        using var context = CriarContexto();
        var currentUser = new FakeCurrentUserService();

        context.Pessoas.Add(new Pessoa { Id = 1, Nome = "Ana", Idade = 30, FamiliaId = currentUser.FamiliaId });
        await context.SaveChangesAsync();

        var service = new TransacaoService(context, currentUser);
        var dto = new TransacaoCreateDto { Descricao = "Teste", Valor = 100, Tipo = TipoTransacao.Receita, PessoaId = 1, CategoriaId = 999 };

        await Assert.ThrowsAsync<NotFoundException>(() => service.Criar(dto));
    }

    [Fact]
    public async Task Criar_ComPessoaDeOutraFamilia_LancaNotFoundException()
    {
        using var context = CriarContexto();
        var currentUser = new FakeCurrentUserService { FamiliaId = 1 };

        // Pessoa e categoria existem, mas pertencem a outra família — o
        // isolamento por família precisa se comportar como "não existe" e
        // não vazar a linha de outra família em nenhuma mensagem.
        context.Pessoas.Add(new Pessoa { Id = 1, Nome = "Ana", Idade = 30, FamiliaId = 2 });
        context.Categorias.Add(new Categoria { Id = 1, Descricao = "Salário", Finalidade = FinalidadeCategoria.Ambas, FamiliaId = 1 });
        await context.SaveChangesAsync();

        var service = new TransacaoService(context, currentUser);
        var dto = new TransacaoCreateDto { Descricao = "Teste", Valor = 100, Tipo = TipoTransacao.Receita, PessoaId = 1, CategoriaId = 1 };

        await Assert.ThrowsAsync<NotFoundException>(() => service.Criar(dto));
    }
}
