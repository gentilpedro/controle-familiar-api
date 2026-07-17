using ControleFamiliarAPI.Data;
using ControleFamiliarAPI.Exceptions;
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
}
