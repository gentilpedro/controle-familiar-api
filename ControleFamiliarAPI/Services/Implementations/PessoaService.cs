using ControleFamiliarAPI.DTOs.Pessoa;
using ControleFamiliarAPI.Exceptions;
using ControleFamiliarAPI.Services.Interfaces;
using ControleFamiliarAPI.Data;
using ControleFamiliarAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace ControleFamiliarAPI.Services.Implementations
{
    public class PessoaService : IPessoaService
    {
        private readonly AppDbContext _context;
        private readonly ICurrentUserService _currentUser;

        public PessoaService(AppDbContext context, ICurrentUserService currentUser)
        {
            _context = context;
            _currentUser = currentUser;
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
