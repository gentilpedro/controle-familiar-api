using ControleFamiliarAPI.Data;
using ControleFamiliarAPI.DTOs.Categoria;
using ControleFamiliarAPI.Exceptions;
using ControleFamiliarAPI.Models;
using ControleFamiliarAPI.Models.Enums;
using ControleFamiliarAPI.Services.Implementations;
using Microsoft.EntityFrameworkCore;

namespace ControleFamiliarAPI.Tests.Unit;

public class CategoriaServiceTests
{
    private static AppDbContext CriarContexto()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task Deletar_CategoriaInexistente_LancaNotFoundException()
    {
        using var context = CriarContexto();
        var service = new CategoriaService(context, new FakeCurrentUserService());

        await Assert.ThrowsAsync<NotFoundException>(() => service.Deletar(999));
    }

    [Fact]
    public async Task Atualizar_AlteraSomenteOsCamposEnviados()
    {
        using var context = CriarContexto();
        context.Categorias.Add(new Categoria
        {
            Id = 1,
            Descricao = "Mercado",
            Finalidade = FinalidadeCategoria.Despesa,
            FamiliaId = 1
        });
        await context.SaveChangesAsync();

        var service = new CategoriaService(context, new FakeCurrentUserService());

        // Só a descrição vai no DTO: a finalidade tem que sobreviver ao PATCH.
        var atualizada = await service.Atualizar(1, new CategoriaUpdateDto { Descricao = "Supermercado" });

        Assert.Equal("Supermercado", atualizada.Descricao);
        Assert.Equal(FinalidadeCategoria.Despesa, atualizada.Finalidade);
        Assert.False(atualizada.EhDoSistema);
    }

    [Fact]
    public async Task Atualizar_CategoriaDoSistema_LancaForbiddenException()
    {
        using var context = CriarContexto();
        context.Categorias.Add(new Categoria
        {
            Id = 1,
            Descricao = "Água",
            Finalidade = FinalidadeCategoria.Despesa,
            FamiliaId = null
        });
        await context.SaveChangesAsync();

        var service = new CategoriaService(context, new FakeCurrentUserService());

        // Ela aparece para todas as famílias; renomear mexeria no catálogo de todas.
        await Assert.ThrowsAsync<ForbiddenException>(
            () => service.Atualizar(1, new CategoriaUpdateDto { Descricao = "Conta de água" }));
    }

    [Fact]
    public async Task Atualizar_CategoriaDeOutraFamilia_LancaNotFoundException()
    {
        using var context = CriarContexto();
        context.Categorias.Add(new Categoria
        {
            Id = 1,
            Descricao = "Escola das crianças",
            Finalidade = FinalidadeCategoria.Despesa,
            FamiliaId = 2
        });
        await context.SaveChangesAsync();

        // FakeCurrentUserService é da família 1: categoria de outra família nem
        // existe do ponto de vista de quem pergunta, por isso 404 e não 403.
        var service = new CategoriaService(context, new FakeCurrentUserService());

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.Atualizar(1, new CategoriaUpdateDto { Descricao = "Outra coisa" }));
    }
}
