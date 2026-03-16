using ControleFamiliarAPI.DTO.Auth;
using ControleFamiliarAPI.Models;
using ControleGastos.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _config;    

    public AuthService(AppDbContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
    }

    public async Task<AuthResponseDto> Register(RegisterDto dto)
    {
        var existe = await _context.Usuarios
            .AnyAsync(x => x.Email == dto.Email);

        if (existe)
            throw new Exception("Email já cadastrado");

        var usuario = new Usuario
        {
            Nome = dto.Nome,
            Email = dto.Email,
            SenhaHash = BCrypt.Net.BCrypt.HashPassword(dto.Senha)
        };

        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();

        var token = GerarToken(usuario);

        return new AuthResponseDto
        {
            Nome = usuario.Nome,
            Email = usuario.Email,
            Token = token
        };
    }

    public async Task<AuthResponseDto> Login(LoginDto dto)
    {
        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(x => x.Email == dto.Email);

        if (usuario == null)
            throw new UnauthorizedAccessException("Email ou senha inválidos"); 

        var senhaValida = BCrypt.Net.BCrypt.Verify(dto.Senha, usuario.SenhaHash);

        if (!senhaValida)
            throw new Exception("Senha inválida");

        var token = GerarToken(usuario);

        return new AuthResponseDto
        {
            Nome = usuario.Nome,
            Email = usuario.Email,
            Token = token
        };
    }

    private string GerarToken(Usuario usuario)
    {
        var key = Encoding.UTF8.GetBytes(_config["Jwt:Key"]);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim(ClaimTypes.Email, usuario.Email),
            new Claim(ClaimTypes.Name, usuario.Nome)
        };

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(key),
            SecurityAlgorithms.HmacSha256
        );

        var token = new JwtSecurityToken(
            issuer: "ControleFamiliarAPI",
            audience: "ControleFamiliarAPI",
            claims: claims,
            expires: DateTime.Now.AddHours(6),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}