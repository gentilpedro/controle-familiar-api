using ControleFamiliarAPI.Data;
using ControleFamiliarAPI.DTOs.Auth;
using ControleFamiliarAPI.Models;
using ControleFamiliarAPI.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ControleFamiliarAPI.Services.Implementations
{
    public class FamiliaDtoFactory : IFamiliaDtoFactory
    {
        private const int TamanhoCodigoConvite = 8;

        private readonly AppDbContext _context;
        private readonly UserManager<Usuario> _userManager;

        public FamiliaDtoFactory(AppDbContext context, UserManager<Usuario> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<string> GerarCodigoConviteUnico()
        {
            string codigo;

            do
            {
                codigo = Guid.NewGuid().ToString("N")[..TamanhoCodigoConvite].ToUpperInvariant();
            }
            while (await _context.Familias.AnyAsync(f => f.CodigoConvite == codigo));

            return codigo;
        }

        public async Task<FamiliaDto> MontarFamiliaDto(Familia familia)
        {
            var membros = await _userManager.Users
                .Where(u => u.FamiliaId == familia.Id)
                .OrderBy(u => u.Id)
                .Select(u => new MembroDto { Id = u.Id, Nome = u.Nome, EhAdministrador = u.EhAdministrador })
                .ToListAsync();

            return new FamiliaDto
            {
                Id = familia.Id,
                Nome = familia.Nome,
                CodigoConvite = familia.CodigoConvite,
                Membros = membros
            };
        }
    }
}
