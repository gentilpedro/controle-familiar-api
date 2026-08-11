using ControleFamiliarAPI.DTOs.Categoria;
using ControleFamiliarAPI.Exceptions;
using ControleFamiliarAPI.Services.Interfaces;
using ControleFamiliarAPI.Data;
using ControleFamiliarAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace ControleFamiliarAPI.Services.Implementations
{
    public class CategoriaService : ICategoriaService
    {
        private readonly AppDbContext _context;
        private readonly ICurrentUserService _currentUser;

        public CategoriaService(AppDbContext context, ICurrentUserService currentUser)
        {
            _context = context;
            _currentUser = currentUser;
        }

        public async Task<List<CategoriaResponseDto>> Listar(CancellationToken cancellationToken = default)
        {
            // As do sistema (FamiliaId null) aparecem para todo mundo; as da
            // família, só para ela. Uma família nunca vê categoria de outra.
            return await _context.Categorias
                .Where(c => c.FamiliaId == _currentUser.FamiliaId || c.FamiliaId == null)
                .OrderBy(c => c.FamiliaId == null ? 0 : 1)
                .ThenBy(c => c.Id)
                .Select(c => new CategoriaResponseDto
                {
                    Id = c.Id,
                    Descricao = c.Descricao,
                    Finalidade = c.Finalidade
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<CategoriaResponseDto> Criar(CategoriaCreateDto dto, CancellationToken cancellationToken = default)
        {
            var categoria = new Categoria
            {
                Descricao = dto.Descricao,
                Finalidade = dto.Finalidade,
                FamiliaId = _currentUser.FamiliaId
            };

            _context.Categorias.Add(categoria);
            await _context.SaveChangesAsync(cancellationToken);

            return new CategoriaResponseDto
            {
                Id = categoria.Id,
                Descricao = categoria.Descricao,
                Finalidade = categoria.Finalidade
            };
        }

        public async Task Deletar(int id, CancellationToken cancellationToken = default)
        {
            // Busca sem filtrar por família para conseguir distinguir os dois
            // casos: categoria do sistema (existe, mas ninguém pode apagar) de
            // categoria de outra família (que, para quem pergunta, não existe).
            var categoria = await _context.Categorias
                .FirstOrDefaultAsync(
                    c => c.Id == id && (c.FamiliaId == _currentUser.FamiliaId || c.FamiliaId == null),
                    cancellationToken);

            if (categoria == null)
                throw new NotFoundException("Categoria não encontrada.");

            // Categoria do sistema não tem dono, então não é de ninguém para
            // excluir — apagá-la sumiria com ela para todas as famílias.
            if (categoria.EhDoSistema)
                throw new ForbiddenException("Categorias padrão do sistema não podem ser excluídas.");

            _context.Categorias.Remove(categoria);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
