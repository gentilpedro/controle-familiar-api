using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ControleFamiliarAPI.DTOs.Auth;
using ControleFamiliarAPI.Exceptions;
using ControleFamiliarAPI.Services;
using ControleFamiliarAPI.Services.Interfaces;
using ControleFamiliarAPI.Data;
using ControleFamiliarAPI.Models;
using Microsoft.AspNetCore.Http;
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
        private readonly IFamiliaDtoFactory _familiaDtoFactory;
        private readonly IEmailService _emailService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthService> _logger;
        private readonly IAuditoriaService _auditoria;

        public AuthService(
            AppDbContext context,
            UserManager<Usuario> userManager,
            SignInManager<Usuario> signInManager,
            ICurrentUserService currentUser,
            IFamiliaDtoFactory familiaDtoFactory,
            IEmailService emailService,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration,
            ILogger<AuthService> logger,
            IAuditoriaService auditoria)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _currentUser = currentUser;
            _familiaDtoFactory = familiaDtoFactory;
            _emailService = emailService;
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
            _logger = logger;
            _auditoria = auditoria;
        }

        public async Task<AuthResponseDto> Registrar(RegistrarDto dto, CancellationToken cancellationToken = default)
        {
            // ModoFamilia só aceita exatamente "Nova"/"Entrar" (case-insensitive);
            // qualquer outro valor (typo, string vazia) é rejeitado explicitamente
            // em vez de cair silenciosamente no branch "Nova".
            bool criandoFamiliaNova;
            if (string.Equals(dto.ModoFamilia, "Nova", StringComparison.OrdinalIgnoreCase))
                criandoFamiliaNova = true;
            else if (string.Equals(dto.ModoFamilia, "Entrar", StringComparison.OrdinalIgnoreCase))
                criandoFamiliaNova = false;
            else
                throw new BusinessRuleException("ModoFamilia deve ser \"Nova\" ou \"Entrar\".");

            // Criar a Familia (via _context) e criar o Usuario (via UserManager,
            // que usa o mesmo AppDbContext) são duas SaveChanges separadas. Sem
            // uma transação cobrindo as duas, uma falha no CreateAsync (ex.:
            // e-mail duplicado) deixaria a Familia já persistida órfã no banco.
            await using var transacao = await _context.Database.BeginTransactionAsync(cancellationToken);

            var familia = criandoFamiliaNova
                ? await CriarFamilia(dto.NomeFamilia, dto.Nome, cancellationToken)
                : await EntrarEmFamilia(dto.CodigoConvite, cancellationToken);

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

            await transacao.CommitAsync(cancellationToken);

            // Best-effort: e-mail de confirmação não é obrigatório pro cadastro
            // funcionar (SMTP pode não estar configurado neste ambiente — mesma
            // filosofia do convite de família). Uma falha aqui não deve derrubar
            // um cadastro que já foi persistido com sucesso.
            await EnviarEmailConfirmacaoAsync(usuario, cancellationToken);

            return await MontarResposta(usuario, familia, cancellationToken);
        }

        public async Task<AuthResponseDto> Login(LoginDto dto, CancellationToken cancellationToken = default)
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

            var familia = await _context.Familias.FindAsync(new object?[] { usuario.FamiliaId }, cancellationToken)
                ?? throw new Exception("Família do usuário não encontrada.");

            return await MontarResposta(usuario, familia, cancellationToken);
        }

        public async Task<MeDto> Me(CancellationToken cancellationToken = default)
        {
            var usuario = await _userManager.FindByIdAsync(_currentUser.UsuarioId.ToString())
                ?? throw new UnauthorizedException("Usuário não encontrado.");

            var familia = await _context.Familias.FindAsync(new object?[] { usuario.FamiliaId }, cancellationToken)
                ?? throw new Exception("Família do usuário não encontrada.");

            return new MeDto
            {
                Usuario = MontarUsuarioDto(usuario),
                Familia = await _familiaDtoFactory.MontarFamiliaDto(familia, cancellationToken)
            };
        }

        public async Task Logout(CancellationToken cancellationToken = default)
        {
            await RevogarTokenAtual(cancellationToken);
        }

        public async Task<MeDto> AtualizarPerfil(AtualizarPerfilDto dto, CancellationToken cancellationToken = default)
        {
            var usuario = await _userManager.FindByIdAsync(_currentUser.UsuarioId.ToString())
                ?? throw new UnauthorizedException("Usuário não encontrado.");

            if (!string.IsNullOrWhiteSpace(dto.Nome))
                usuario.Nome = dto.Nome;

            var emailAlterado = !string.IsNullOrWhiteSpace(dto.Email)
                && !string.Equals(dto.Email, usuario.Email, StringComparison.OrdinalIgnoreCase);

            if (emailAlterado)
            {
                // SetEmailAsync já persiste o usuário inteiro (inclusive o
                // Nome alterado acima, mesmo AppDbContext) e zera
                // EmailConfirmed — por isso reenviamos a confirmação abaixo.
                var resultadoEmail = await _userManager.SetEmailAsync(usuario, dto.Email!);
                if (!resultadoEmail.Succeeded)
                    throw new BusinessRuleException(string.Join(" ", resultadoEmail.Errors.Select(e => e.Description)));

                await _userManager.SetUserNameAsync(usuario, dto.Email!);
                await EnviarEmailConfirmacaoAsync(usuario, cancellationToken);
            }
            else
            {
                var resultado = await _userManager.UpdateAsync(usuario);
                if (!resultado.Succeeded)
                    throw new BusinessRuleException(string.Join(" ", resultado.Errors.Select(e => e.Description)));
            }

            var familia = await _context.Familias.FindAsync(new object?[] { usuario.FamiliaId }, cancellationToken)
                ?? throw new Exception("Família do usuário não encontrada.");

            return new MeDto
            {
                Usuario = MontarUsuarioDto(usuario),
                Familia = await _familiaDtoFactory.MontarFamiliaDto(familia, cancellationToken)
            };
        }

        public async Task ExcluirConta(CancellationToken cancellationToken = default)
        {
            var usuario = await _userManager.FindByIdAsync(_currentUser.UsuarioId.ToString())
                ?? throw new UnauthorizedException("Usuário não encontrado.");

            var familiaId = usuario.FamiliaId;

            var outrosMembros = await _userManager.Users
                .Where(u => u.FamiliaId == familiaId && u.Id != usuario.Id)
                .ToListAsync(cancellationToken);

            if (outrosMembros.Count == 0)
            {
                // Único membro da família: ninguém mais usa esses dados, então
                // excluir a conta (LGPD, art. 18, VI) leva junto pessoas,
                // categorias e transações — sem isso sobraria dado órfão sem
                // titular. Ordem respeita as FKs Restrict (Transacao antes de
                // Pessoa/Categoria, Usuario antes de Familia).
                await using var transacaoDb = await _context.Database.BeginTransactionAsync(cancellationToken);

                await _context.Transacoes.Where(t => t.FamiliaId == familiaId).ExecuteDeleteAsync(cancellationToken);
                await _context.Pessoas.Where(p => p.FamiliaId == familiaId).ExecuteDeleteAsync(cancellationToken);
                await _context.Categorias.Where(c => c.FamiliaId == familiaId).ExecuteDeleteAsync(cancellationToken);

                await _auditoria.Registrar("ExclusaoConta", cancellationToken: cancellationToken);
                await RevogarTokenAtual(cancellationToken);

                var resultadoExclusao = await _userManager.DeleteAsync(usuario);
                if (!resultadoExclusao.Succeeded)
                    throw new BusinessRuleException(string.Join(" ", resultadoExclusao.Errors.Select(e => e.Description)));

                await _context.Familias.Where(f => f.Id == familiaId).ExecuteDeleteAsync(cancellationToken);

                await transacaoDb.CommitAsync(cancellationToken);
                return;
            }

            // Família compartilhada: pessoas/categorias/transações continuam
            // existindo para os outros membros — só a conta de quem está se
            // excluindo é removida. Se for o único admin, precisa promover
            // outro antes, senão a família fica sem administrador (mesma
            // invariante de FamiliaService.GarantirQueSobraAdmin).
            if (usuario.EhAdministrador && !outrosMembros.Any(m => m.EhAdministrador))
                throw new BusinessRuleException(
                    "Você é o único administrador desta família. Promova outro membro a administrador antes de excluir sua conta.");

            await _auditoria.Registrar("ExclusaoConta", cancellationToken: cancellationToken);
            await RevogarTokenAtual(cancellationToken);

            var resultado = await _userManager.DeleteAsync(usuario);
            if (!resultado.Succeeded)
                throw new BusinessRuleException(string.Join(" ", resultado.Errors.Select(e => e.Description)));
        }

        public async Task<ExportacaoDadosDto> ExportarDados(CancellationToken cancellationToken = default)
        {
            var usuario = await _userManager.FindByIdAsync(_currentUser.UsuarioId.ToString())
                ?? throw new UnauthorizedException("Usuário não encontrado.");

            var familia = await _context.Familias.FindAsync(new object?[] { usuario.FamiliaId }, cancellationToken)
                ?? throw new Exception("Família do usuário não encontrada.");

            var pessoas = await _context.Pessoas
                .Where(p => p.FamiliaId == usuario.FamiliaId)
                .Select(p => new ExportacaoPessoaDto { Id = p.Id, Nome = p.Nome, Idade = p.Idade })
                .ToListAsync(cancellationToken);

            // Finalidade/Tipo são materializados como enum primeiro e só
            // convertidos pra texto depois de trazer os dados (ToString() em
            // enum dentro de um Select não tem tradução garantida pra SQL em
            // todo provider — mais seguro resolver em memória).
            var categoriasBrutas = await _context.Categorias
                .Where(c => c.FamiliaId == usuario.FamiliaId)
                .Select(c => new { c.Id, c.Descricao, c.Finalidade })
                .ToListAsync(cancellationToken);

            var categorias = categoriasBrutas
                .Select(c => new ExportacaoCategoriaDto { Id = c.Id, Descricao = c.Descricao, Finalidade = c.Finalidade.ToString() })
                .ToList();

            var transacoesBrutas = await _context.Transacoes
                .Where(t => t.FamiliaId == usuario.FamiliaId)
                .Select(t => new { t.Id, t.Descricao, t.Valor, t.Tipo, Pessoa = t.Pessoa!.Nome, Categoria = t.Categoria!.Descricao })
                .ToListAsync(cancellationToken);

            var transacoes = transacoesBrutas
                .Select(t => new ExportacaoTransacaoDto
                {
                    Id = t.Id,
                    Descricao = t.Descricao,
                    Valor = t.Valor,
                    Tipo = t.Tipo.ToString(),
                    Pessoa = t.Pessoa,
                    Categoria = t.Categoria
                })
                .ToList();

            return new ExportacaoDadosDto
            {
                Usuario = new ExportacaoUsuarioDto
                {
                    Id = usuario.Id,
                    Nome = usuario.Nome,
                    Email = usuario.Email!,
                    EhAdministrador = usuario.EhAdministrador
                },
                Familia = new ExportacaoFamiliaDto
                {
                    Id = familia.Id,
                    Nome = familia.Nome,
                    CodigoConvite = familia.CodigoConvite,
                    CriadoEm = familia.CriadoEm
                },
                Pessoas = pessoas,
                Categorias = categorias,
                Transacoes = transacoes
            };
        }

        private async Task RevogarTokenAtual(CancellationToken cancellationToken)
        {
            var httpContext = _httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException("Nenhum contexto HTTP disponível.");

            var jti = httpContext.User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            var expClaim = httpContext.User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;

            if (string.IsNullOrEmpty(jti) || !long.TryParse(expClaim, out var expUnix))
                return;

            // Limpeza oportunista dos já expirados: evita precisar de um job
            // separado só pra isso — o volume aqui é só de tokens revogados
            // antes de vencer, não de todo token emitido.
            await _context.TokensRevogados
                .Where(t => t.ExpiraEm < DateTime.UtcNow)
                .ExecuteDeleteAsync(cancellationToken);

            _context.TokensRevogados.Add(new TokenRevogado
            {
                Jti = jti,
                ExpiraEm = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime
            });

            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task ConfirmarEmail(int usuarioId, string token, CancellationToken cancellationToken = default)
        {
            var usuario = await _userManager.FindByIdAsync(usuarioId.ToString())
                ?? throw new NotFoundException("Usuário não encontrado.");

            var resultado = await _userManager.ConfirmEmailAsync(usuario, token);

            if (!resultado.Succeeded)
                throw new BusinessRuleException("Link de confirmação inválido ou expirado.");
        }

        private async Task<Familia> CriarFamilia(string? nomeFamilia, string nomeUsuario, CancellationToken cancellationToken)
        {
            var familia = new Familia
            {
                Nome = string.IsNullOrWhiteSpace(nomeFamilia) ? $"Família de {nomeUsuario}" : nomeFamilia,
                CodigoConvite = await _familiaDtoFactory.GerarCodigoConviteUnico(cancellationToken)
            };

            _context.Familias.Add(familia);
            await _context.SaveChangesAsync(cancellationToken);

            return familia;
        }

        private async Task<Familia> EntrarEmFamilia(string? codigoConvite, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(codigoConvite))
                throw new BusinessRuleException("Informe o código de convite da família.");

            var familia = await _context.Familias
                .FirstOrDefaultAsync(f => f.CodigoConvite == codigoConvite.Trim().ToUpperInvariant(), cancellationToken);

            if (familia == null)
                throw new BusinessRuleException("Código de convite inválido.");

            // Teto do plano Família: acima de 5 membros ninguém mais entra
            // por convite, mesmo que a assinatura esteja ativa.
            var totalMembros = await _userManager.Users.CountAsync(u => u.FamiliaId == familia.Id, cancellationToken);
            if (totalMembros >= 5)
                throw new BusinessRuleException("Esta família já atingiu o limite máximo de 5 membros.");

            return familia;
        }

        private async Task<AuthResponseDto> MontarResposta(Usuario usuario, Familia familia, CancellationToken cancellationToken)
        {
            var (token, expiraEm) = GerarToken(usuario);

            return new AuthResponseDto
            {
                Token = token,
                ExpiraEm = expiraEm,
                Usuario = MontarUsuarioDto(usuario),
                Familia = await _familiaDtoFactory.MontarFamiliaDto(familia, cancellationToken)
            };
        }

        private static UsuarioDto MontarUsuarioDto(Usuario usuario) => new()
        {
            Id = usuario.Id,
            Nome = usuario.Nome,
            Email = usuario.Email!,
            EhAdministrador = usuario.EhAdministrador,
            EmailConfirmado = usuario.EmailConfirmed
        };

        private async Task EnviarEmailConfirmacaoAsync(Usuario usuario, CancellationToken cancellationToken)
        {
            try
            {
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(usuario);
                var frontendUrl = _configuration["Frontend:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5173";
                var link = $"{frontendUrl}/confirmar-email?usuarioId={usuario.Id}&token={Uri.EscapeDataString(token)}";

                await _emailService.EnviarConfirmacaoEmail(usuario.Email!, usuario.Nome, link, cancellationToken);
            }
            catch (Exception ex)
            {
                // Não loga o objeto de exceção inteiro de propósito: uma
                // SmtpException, por exemplo, pode ecoar o e-mail do
                // destinatário na própria mensagem — o tipo da exceção já
                // basta pra triagem, sem arriscar vazar dado pessoal no log.
                _logger.LogWarning(
                    "Falha ao enviar e-mail de confirmação para o usuário {UsuarioId} ({TipoExcecao}).",
                    usuario.Id,
                    ex.GetType().Name);
            }
        }

        private (string token, DateTime expiraEm) GerarToken(Usuario usuario)
        {
            var jwtConfig = _configuration.GetSection("Jwt");
            var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtConfig["Key"]!));
            var credenciais = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256);
            var expiraEm = DateTime.UtcNow.AddHours(double.Parse(jwtConfig["ExpiraHoras"] ?? "6"));

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new(ClaimTypes.Email, usuario.Email!),
                new(ClaimTypesPersonalizados.Nome, usuario.Nome),
                new(ClaimTypesPersonalizados.FamiliaId, usuario.FamiliaId.ToString()),
                // Identificador único do token, verificado a cada requisição
                // (Program.cs, OnTokenValidated) contra TokensRevogados —
                // permite logout de verdade em vez de esperar o token vencer.
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
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
