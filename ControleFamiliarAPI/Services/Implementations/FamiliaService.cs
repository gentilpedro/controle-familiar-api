using ControleFamiliarAPI.DTO.Auth;
using ControleFamiliarAPI.Exceptions;
using ControleFamiliarAPI.Services.Interfaces;
using ControleGastos.Api.Data;
using ControleGastos.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ControleFamiliarAPI.Services.Implementations
{
    public class FamiliaService : IFamiliaService
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Usuario> _userManager;
        private readonly ICurrentUserService _currentUser;
        private readonly IEmailService _emailService;

        public FamiliaService(
            AppDbContext context,
            UserManager<Usuario> userManager,
            ICurrentUserService currentUser,
            IEmailService emailService)
        {
            _context = context;
            _userManager = userManager;
            _currentUser = currentUser;
            _emailService = emailService;
        }

        public async Task<FamiliaDto> Obter()
        {
            return await MontarFamiliaDto(await BuscarFamiliaAtual());
        }

        public async Task<FamiliaDto> RemoverMembro(int usuarioId)
        {
            await GarantirAdmin();

            if (usuarioId == _currentUser.UsuarioId)
                throw new BusinessRuleException("Você não pode remover a si mesmo. Peça a outro administrador.");

            var membro = await BuscarMembro(usuarioId);

            await GarantirQueSobraAdmin(usuarioId);

            // O removido não fica sem família: ganha uma família nova e
            // individual, da qual é o único membro e administrador.
            var novaFamilia = new Familia
            {
                Nome = $"Família de {membro.Nome}",
                CodigoConvite = await GerarCodigoConviteUnico()
            };

            _context.Familias.Add(novaFamilia);
            await _context.SaveChangesAsync();

            membro.FamiliaId = novaFamilia.Id;
            membro.EhAdministrador = true;
            await _userManager.UpdateAsync(membro);

            return await MontarFamiliaDto(await BuscarFamiliaAtual());
        }

        public async Task<FamiliaDto> PromoverAdmin(int usuarioId)
        {
            await GarantirAdmin();

            var membro = await BuscarMembro(usuarioId);
            membro.EhAdministrador = true;
            await _userManager.UpdateAsync(membro);

            return await MontarFamiliaDto(await BuscarFamiliaAtual());
        }

        public async Task<FamiliaDto> RebaixarAdmin(int usuarioId)
        {
            await GarantirAdmin();

            var membro = await BuscarMembro(usuarioId);

            await GarantirQueSobraAdmin(usuarioId);

            membro.EhAdministrador = false;
            await _userManager.UpdateAsync(membro);

            return await MontarFamiliaDto(await BuscarFamiliaAtual());
        }

        public async Task<FamiliaDto> RegenerarCodigoConvite()
        {
            await GarantirAdmin();

            var familia = await BuscarFamiliaAtual();
            familia.CodigoConvite = await GerarCodigoConviteUnico();
            await _context.SaveChangesAsync();

            return await MontarFamiliaDto(familia);
        }

        public async Task ConvidarPorEmail(string email)
        {
            var admin = await GarantirAdmin();

            if (string.IsNullOrWhiteSpace(email))
                throw new BusinessRuleException("Informe um e-mail válido.");

            var familia = await BuscarFamiliaAtual();

            await _emailService.EnviarConviteFamilia(email.Trim(), familia.Nome, familia.CodigoConvite, admin.Nome);
        }

        private async Task<Usuario> GarantirAdmin()
        {
            var usuario = await _userManager.FindByIdAsync(_currentUser.UsuarioId.ToString())
                ?? throw new UnauthorizedException("Usuário não encontrado.");

            if (!usuario.EhAdministrador)
                throw new ForbiddenException("Apenas administradores da família podem fazer isso.");

            return usuario;
        }

        private async Task<Usuario> BuscarMembro(int usuarioId)
        {
            return await _userManager.Users
                .FirstOrDefaultAsync(u => u.Id == usuarioId && u.FamiliaId == _currentUser.FamiliaId)
                ?? throw new NotFoundException("Membro não encontrado nesta família.");
        }

        private async Task<Familia> BuscarFamiliaAtual()
        {
            return await _context.Familias.FindAsync(_currentUser.FamiliaId)
                ?? throw new Exception("Família não encontrada.");
        }

        /// <summary>
        /// Garante que, ao remover/rebaixar <paramref name="usuarioId"/>, a
        /// família continua com pelo menos um administrador.
        /// </summary>
        private async Task GarantirQueSobraAdmin(int usuarioId)
        {
            var eraAdmin = await _userManager.Users
                .AnyAsync(u => u.Id == usuarioId && u.EhAdministrador);

            if (!eraAdmin)
                return;

            var outrosAdmins = await _userManager.Users
                .CountAsync(u => u.FamiliaId == _currentUser.FamiliaId && u.EhAdministrador && u.Id != usuarioId);

            if (outrosAdmins == 0)
                throw new BusinessRuleException("A família precisa ter pelo menos um administrador.");
        }

        private async Task<string> GerarCodigoConviteUnico()
        {
            string codigo;

            do
            {
                codigo = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            }
            while (await _context.Familias.AnyAsync(f => f.CodigoConvite == codigo));

            return codigo;
        }

        private async Task<FamiliaDto> MontarFamiliaDto(Familia familia)
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
