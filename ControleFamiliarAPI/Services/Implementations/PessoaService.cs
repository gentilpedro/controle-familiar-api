using ControleFamiliarAPI.DTOs.Pessoa;
using ControleFamiliarAPI.Exceptions;
using ControleFamiliarAPI.Services.Interfaces;
using ControleFamiliarAPI.Data;
using ControleFamiliarAPI.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ControleFamiliarAPI.Services.Implementations
{
    public class PessoaService : IPessoaService
    {
        private readonly AppDbContext _context;
        private readonly ICurrentUserService _currentUser;
        private readonly UserManager<Usuario> _userManager;

        public PessoaService(AppDbContext context, ICurrentUserService currentUser, UserManager<Usuario> userManager)
        {
            _context = context;
            _currentUser = currentUser;
            _userManager = userManager;
        }

        // Mesmo padrão de FamiliaService.GarantirAdmin — não existe um lugar
        // compartilhado pra essa checagem ainda, e duplicar um método de 6
        // linhas é mais simples que introduzir uma abstração nova pra isso.
        private async Task GarantirAdmin()
        {
            var usuario = await _userManager.FindByIdAsync(_currentUser.UsuarioId.ToString())
                ?? throw new UnauthorizedException("Usuário não encontrado.");

            if (!usuario.EhAdministrador)
                throw new ForbiddenException("Apenas administradores da família podem gerenciar pessoas.");
        }

        public async Task<List<PessoaResponseDto>> Listar(CancellationToken cancellationToken = default)
        {
            return await _context.Pessoas
                .Where(p => p.FamiliaId == _currentUser.FamiliaId)
                .Select(p => new PessoaResponseDto
                {
                    Id = p.Id,
                    Nome = p.Nome,
                    Idade = p.Idade,
                    EhMembro = p.UsuarioId != null
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<PessoaResponseDto> Criar(PessoaCreateDto dto, CancellationToken cancellationToken = default)
        {
            // Toda Pessoa criada por aqui é cadastro manual (dependente sem
            // login) — a de um membro nasce no Registrar. É o administrador
            // quem passou a gerenciar o cadastro manual da família.
            await GarantirAdmin();

            var pessoa = new Pessoa
            {
                Nome = dto.Nome,
                Idade = dto.Idade,
                FamiliaId = _currentUser.FamiliaId
            };

            _context.Pessoas.Add(pessoa);

            await _context.SaveChangesAsync(cancellationToken);

            // Pessoa criada aqui é sempre cadastro à mão — dependente sem
            // login. A de um membro nasce no Registrar, junto com a conta.
            return new PessoaResponseDto
            {
                Id = pessoa.Id,
                Nome = pessoa.Nome,
                Idade = pessoa.Idade,
                EhMembro = false
            };
        }

        public async Task Atualizar(int id, PessoaUpdateDto dto, CancellationToken cancellationToken = default)
        {
            await GarantirAdmin();

            var pessoa = await _context.Pessoas
                .FirstOrDefaultAsync(p => p.Id == id && p.FamiliaId == _currentUser.FamiliaId, cancellationToken);

            if (pessoa == null)
                throw new NotFoundException("Pessoa não encontrada.");

            if (!string.IsNullOrEmpty(dto.Nome))
                pessoa.Nome = dto.Nome;

            if (dto.Idade.HasValue)
                pessoa.Idade = dto.Idade.Value;

            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task Deletar(int id, CancellationToken cancellationToken = default)
        {
            await GarantirAdmin();

            var pessoa = await _context.Pessoas
                .FirstOrDefaultAsync(p => p.Id == id && p.FamiliaId == _currentUser.FamiliaId, cancellationToken);

            if (pessoa == null)
                throw new NotFoundException("Pessoa não encontrada.");

            // Excluir por aqui deixaria um membro ativo da família sem pessoa
            // nenhuma para lançar despesa. Quem representa uma conta só sai
            // junto com ela — removendo o membro ou excluindo a própria conta.
            if (pessoa.UsuarioId != null)
                throw new BusinessRuleException(
                    "Esta pessoa representa um membro da família. Remova o membro em Minha Família.");

            _context.Pessoas.Remove(pessoa);

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
