using ControleFamiliarAPI.DTOs.Categoria;
using ControleFamiliarAPI.Exceptions;
using ControleFamiliarAPI.Services.Interfaces;
using ControleGastos.Api.Data;
using ControleGastos.Api.Models;
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

        public async Task<List<CategoriaResponseDto>> Listar()
        {
            return await _context.Categorias
                .Where(c => c.FamiliaId == _currentUser.FamiliaId)
                .Select(c => new CategoriaResponseDto
                {
                    Id = c.Id,
                    Descricao = c.Descricao,
                    Finalidade = c.Finalidade
                })
                .ToListAsync();
        }

        public async Task<CategoriaResponseDto> Criar(CategoriaCreateDto dto)
        {
            var categoria = new Categoria
            {
                Descricao = dto.Descricao,
                Finalidade = dto.Finalidade,
                FamiliaId = _currentUser.FamiliaId
            };

            _context.Categorias.Add(categoria);
            await _context.SaveChangesAsync();

            return new CategoriaResponseDto
            {
                Id = categoria.Id,
                Descricao = categoria.Descricao,
                Finalidade = categoria.Finalidade
            };
        }

        public async Task Deletar(int id)
        {
            var categoria = await _context.Categorias
                .FirstOrDefaultAsync(c => c.Id == id && c.FamiliaId == _currentUser.FamiliaId);

            if (categoria == null)
                throw new NotFoundException("Categoria não encontrada.");

            _context.Categorias.Remove(categoria);
            await _context.SaveChangesAsync();
        }
    }
}