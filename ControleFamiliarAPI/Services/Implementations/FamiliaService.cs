using System.Data;
using ControleFamiliarAPI.DTOs.Auth;
using ControleFamiliarAPI.Exceptions;
using ControleFamiliarAPI.Services.Interfaces;
using ControleFamiliarAPI.Data;
using ControleFamiliarAPI.Models;
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
        private readonly IFamiliaDtoFactory _familiaDtoFactory;
        private readonly IAuditoriaService _auditoria;

        public FamiliaService(
            AppDbContext context,
            UserManager<Usuario> userManager,
            ICurrentUserService currentUser,
            IEmailService emailService,
            IFamiliaDtoFactory familiaDtoFactory,
            IAuditoriaService auditoria)
        {
            _context = context;
            _userManager = userManager;
            _currentUser = currentUser;
            _emailService = emailService;
            _familiaDtoFactory = familiaDtoFactory;
            _auditoria = auditoria;
        }

        public async Task<FamiliaDto> Obter(CancellationToken cancellationToken = default)
        {
            return await _familiaDtoFactory.MontarFamiliaDto(await BuscarFamiliaAtual(cancellationToken), cancellationToken);
        }

        public async Task<FamiliaDto> RemoverMembro(int usuarioId, CancellationToken cancellationToken = default)
        {
            await GarantirAdmin();

            if (usuarioId == _currentUser.UsuarioId)
                throw new BusinessRuleException("Você não pode remover a si mesmo. Peça a outro administrador.");

            // Isolamento Serializable: o "ainda sobra um admin?"
            // (GarantirQueSobraAdmin) e as escritas que de fato tiram o membro
            // da família precisam ser vistas como uma unidade atômica pelo
            // banco. Sem isso, duas remoções concorrentes de administradores
            // diferentes poderiam passar as duas pelo check antes de qualquer
            // uma escrever, deixando a família sem nenhum administrador.
            // A transação também garante que a criação da nova família
            // individual e a atualização do membro sejam tudo-ou-nada.
            await using var transacao = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            var membro = await BuscarMembro(usuarioId, cancellationToken);

            await GarantirQueSobraAdmin(usuarioId, cancellationToken);

            // O removido não fica sem família: ganha uma família nova e
            // individual, da qual é o único membro e administrador.
            var novaFamilia = new Familia
            {
                Nome = $"Família de {membro.Nome}",
                CodigoConvite = await _familiaDtoFactory.GerarCodigoConviteUnico(cancellationToken)
            };

            _context.Familias.Add(novaFamilia);
            await _context.SaveChangesAsync(cancellationToken);

            // Mesmo tratamento de quem cria a conta: a família nova nasce com as
            // categorias padrão. Sem isto, quem é removido cairia num painel
            // completamente vazio, diferente de qualquer outra família nova.
            _context.Categorias.AddRange(CategoriasPadrao.ParaFamilia(novaFamilia.Id));
            await _context.SaveChangesAsync(cancellationToken);

            membro.FamiliaId = novaFamilia.Id;
            membro.EhAdministrador = true;
            await _userManager.UpdateAsync(membro);

            await _auditoria.Registrar("RemocaoMembro", usuarioId, cancellationToken);

            await transacao.CommitAsync(cancellationToken);

            return await _familiaDtoFactory.MontarFamiliaDto(await BuscarFamiliaAtual(cancellationToken), cancellationToken);
        }

        public async Task<FamiliaDto> PromoverAdmin(int usuarioId, CancellationToken cancellationToken = default)
        {
            await GarantirAdmin();

            var membro = await BuscarMembro(usuarioId, cancellationToken);
            membro.EhAdministrador = true;
            await _userManager.UpdateAsync(membro);

            await _auditoria.Registrar("PromocaoAdmin", usuarioId, cancellationToken);

            return await _familiaDtoFactory.MontarFamiliaDto(await BuscarFamiliaAtual(cancellationToken), cancellationToken);
        }

        public async Task<FamiliaDto> RebaixarAdmin(int usuarioId, CancellationToken cancellationToken = default)
        {
            await GarantirAdmin();

            var membroExiste = await _userManager.Users
                .AnyAsync(u => u.Id == usuarioId && u.FamiliaId == _currentUser.FamiliaId, cancellationToken);

            if (!membroExiste)
                throw new NotFoundException("Membro não encontrado nesta família.");

            // Update condicional atômico: a condição "sobra outro admin?" vai
            // pro WHERE da própria escrita (uma única instrução SQL), então não
            // existe janela entre checar e escrever para uma segunda requisição
            // concorrente se intrometer — diferente de checar com
            // GarantirQueSobraAdmin e só depois chamar UpdateAsync em separado.
            var linhasAfetadas = await _context.Users
                .Where(u => u.Id == usuarioId
                    && u.FamiliaId == _currentUser.FamiliaId
                    && _context.Users.Count(outro =>
                        outro.FamiliaId == _currentUser.FamiliaId
                        && outro.EhAdministrador
                        && outro.Id != usuarioId) > 0)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.EhAdministrador, false), cancellationToken);

            if (linhasAfetadas == 0)
                throw new BusinessRuleException("A família precisa ter pelo menos um administrador.");

            await _auditoria.Registrar("RebaixamentoAdmin", usuarioId, cancellationToken);

            return await _familiaDtoFactory.MontarFamiliaDto(await BuscarFamiliaAtual(cancellationToken), cancellationToken);
        }

        public async Task<FamiliaDto> RegenerarCodigoConvite(CancellationToken cancellationToken = default)
        {
            await GarantirAdmin();

            var familia = await BuscarFamiliaAtual(cancellationToken);
            familia.CodigoConvite = await _familiaDtoFactory.GerarCodigoConviteUnico(cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return await _familiaDtoFactory.MontarFamiliaDto(familia, cancellationToken);
        }

        public async Task ConvidarPorEmail(string email, CancellationToken cancellationToken = default)
        {
            var admin = await GarantirAdmin();

            if (string.IsNullOrWhiteSpace(email))
                throw new BusinessRuleException("Informe um e-mail válido.");

            var familia = await BuscarFamiliaAtual(cancellationToken);

            await _emailService.EnviarConviteFamilia(email.Trim(), familia.Nome, familia.CodigoConvite, admin.Nome, cancellationToken);
        }

        // UserManager não expõe overloads com CancellationToken — os métodos
        // abaixo que só o usam (FindByIdAsync/UpdateAsync) não têm token pra
        // repassar.
        private async Task<Usuario> GarantirAdmin()
        {
            var usuario = await _userManager.FindByIdAsync(_currentUser.UsuarioId.ToString())
                ?? throw new UnauthorizedException("Usuário não encontrado.");

            if (!usuario.EhAdministrador)
                throw new ForbiddenException("Apenas administradores da família podem fazer isso.");

            return usuario;
        }

        private async Task<Usuario> BuscarMembro(int usuarioId, CancellationToken cancellationToken)
        {
            return await _userManager.Users
                .FirstOrDefaultAsync(u => u.Id == usuarioId && u.FamiliaId == _currentUser.FamiliaId, cancellationToken)
                ?? throw new NotFoundException("Membro não encontrado nesta família.");
        }

        private async Task<Familia> BuscarFamiliaAtual(CancellationToken cancellationToken)
        {
            return await _context.Familias.FindAsync(new object?[] { _currentUser.FamiliaId }, cancellationToken)
                ?? throw new Exception("Família não encontrada.");
        }

        /// <summary>
        /// Garante que, ao remover/rebaixar <paramref name="usuarioId"/>, a
        /// família continua com pelo menos um administrador.
        /// </summary>
        private async Task GarantirQueSobraAdmin(int usuarioId, CancellationToken cancellationToken)
        {
            var eraAdmin = await _userManager.Users
                .AnyAsync(u => u.Id == usuarioId && u.EhAdministrador, cancellationToken);

            if (!eraAdmin)
                return;

            var outrosAdmins = await _userManager.Users
                .CountAsync(u => u.FamiliaId == _currentUser.FamiliaId && u.EhAdministrador && u.Id != usuarioId, cancellationToken);

            if (outrosAdmins == 0)
                throw new BusinessRuleException("A família precisa ter pelo menos um administrador.");
        }
    }
}
