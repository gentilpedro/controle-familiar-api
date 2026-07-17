using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ControleFamiliarAPI.DTO.Auth;
using ControleFamiliarAPI.Exceptions;
using ControleFamiliarAPI.Services.Interfaces;
using ControleGastos.Api.Data;
using ControleGastos.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace ControleFamiliarAPI.Services.Implementations
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Usuario> _userManager;
        private readonly SignInManager<Usuario> _signInManager;
        private readonly ICurrentUserService _currentUser;
        private readonly IConfiguration _configuration;

        public AuthService(
            AppDbContext context,
            UserManager<Usuario> userManager,
            SignInManager<Usuario> signInManager,
            ICurrentUserService currentUser,
            IConfiguration configuration)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _currentUser = currentUser;
            _configuration = configuration;
        }

        public async Task<AuthResponseDto> Registrar(RegistrarDto dto)
        {
            var criandoFamiliaNova = !dto.ModoFamilia.Equals("Entrar", StringComparison.OrdinalIgnoreCase);

            // Criar a Familia (via _context) e criar o Usuario (via UserManager,
            // que usa o mesmo AppDbContext) são duas SaveChanges separadas. Sem
            // uma transação cobrindo as duas, uma falha no CreateAsync (ex.:
            // e-mail duplicado) deixaria a Familia já persistida órfã no banco.
            await using var transacao = await _context.Database.BeginTransactionAsync();

            var familia = criandoFamiliaNova
                ? await CriarFamilia(dto.NomeFamilia, dto.Nome)
                : await EntrarEmFamilia(dto.CodigoConvite);

            var usuario = new Usuario
            {
                UserName = dto.Email,
                Email = dto.Email,
                Nome = dto.Nome,
                FamiliaId = familia.Id,
                // Quem cria a família nasce admin; quem entra por código de
                // convite entra como membro comum.
                EhAdministrador = criandoFamiliaNova
            };

            var resultado = await _userManager.CreateAsync(usuario, dto.Senha);

            if (!resultado.Succeeded)
                throw new BusinessRuleException(string.Join(" ", resultado.Errors.Select(e => e.Description)));

            await transacao.CommitAsync();

            return await MontarResposta(usuario, familia);
        }

        public async Task<AuthResponseDto> Login(LoginDto dto)
        {
            var usuario = await _userManager.FindByEmailAsync(dto.Email);

            if (usuario == null)
                throw new UnauthorizedException("Email ou senha inválidos.");

            // lockoutOnFailure: true faz o Identity contar tentativas erradas e
            // bloquear a conta temporariamente após o limite configurado em
            // Program.cs (options.Lockout), sem depender de nada além do que já
            // vem pronto no UserManager/SignInManager.
            var resultado = await _signInManager.CheckPasswordSignInAsync(usuario, dto.Senha, lockoutOnFailure: true);

            if (resultado.IsLockedOut)
                throw new UnauthorizedException("Conta temporariamente bloqueada por excesso de tentativas. Tente novamente mais tarde.");

            if (!resultado.Succeeded)
                throw new UnauthorizedException("Email ou senha inválidos.");

            var familia = await _context.Familias.FindAsync(usuario.FamiliaId)
                ?? throw new Exception("Família do usuário não encontrada.");

            return await MontarResposta(usuario, familia);
        }

        public async Task<MeDto> Me()
        {
            var usuario = await _userManager.FindByIdAsync(_currentUser.UsuarioId.ToString())
                ?? throw new UnauthorizedException("Usuário não encontrado.");

            var familia = await _context.Familias.FindAsync(usuario.FamiliaId)
                ?? throw new Exception("Família do usuário não encontrada.");

            return new MeDto
            {
                Usuario = new UsuarioDto
                {
                    Id = usuario.Id,
                    Nome = usuario.Nome,
                    Email = usuario.Email!,
                    EhAdministrador = usuario.EhAdministrador
                },
                Familia = await MontarFamiliaDto(familia)
            };
        }

        private async Task<Familia> CriarFamilia(string? nomeFamilia, string nomeUsuario)
        {
            var familia = new Familia
            {
                Nome = string.IsNullOrWhiteSpace(nomeFamilia) ? $"Família de {nomeUsuario}" : nomeFamilia,
                CodigoConvite = await GerarCodigoConviteUnico()
            };

            _context.Familias.Add(familia);
            await _context.SaveChangesAsync();

            return familia;
        }

        private async Task<Familia> EntrarEmFamilia(string? codigoConvite)
        {
            if (string.IsNullOrWhiteSpace(codigoConvite))
                throw new BusinessRuleException("Informe o código de convite da família.");

            var familia = await _context.Familias
                .FirstOrDefaultAsync(f => f.CodigoConvite == codigoConvite.Trim().ToUpperInvariant());

            if (familia == null)
                throw new BusinessRuleException("Código de convite inválido.");

            return familia;
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

        private async Task<AuthResponseDto> MontarResposta(Usuario usuario, Familia familia)
        {
            var (token, expiraEm) = GerarToken(usuario);

            return new AuthResponseDto
            {
                Token = token,
                ExpiraEm = expiraEm,
                Usuario = new UsuarioDto
                {
                    Id = usuario.Id,
                    Nome = usuario.Nome,
                    Email = usuario.Email!,
                    EhAdministrador = usuario.EhAdministrador
                },
                Familia = await MontarFamiliaDto(familia)
            };
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

        private (string token, DateTime expiraEm) GerarToken(Usuario usuario)
        {
            var jwtConfig = _configuration.GetSection("Jwt");
            var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtConfig["Key"]!));
            var credenciais = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256);
            var expiraEm = DateTime.UtcNow.AddHours(double.Parse(jwtConfig["ExpiraHoras"] ?? "12"));

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new(ClaimTypes.Email, usuario.Email!),
                new("nome", usuario.Nome),
                new("familiaId", usuario.FamiliaId.ToString())
            };

            var token = new JwtSecurityToken(
                issuer: jwtConfig["Issuer"],
                audience: jwtConfig["Audience"],
                claims: claims,
                expires: expiraEm,
                signingCredentials: credenciais
            );

            return (new JwtSecurityTokenHandler().WriteToken(token), expiraEm);
        }
    }
}
