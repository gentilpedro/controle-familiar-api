using ControleFamiliarAPI.DTOs.Categoria;
using ControleFamiliarAPI.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ControleFamiliarAPI.Controllers
{
    [ApiController]
    [Route("api/categorias")]
    public class CategoriasController : ControllerBase
    {
        private readonly ICategoriaService _service;

        public CategoriasController(ICategoriaService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult> Listar()
        {
            return Ok(await _service.Listar());
        }

        [HttpPost]
        public async Task<ActionResult> Criar(CategoriaCreateDto dto)
        {
            var categoria = await _service.Criar(dto);
            return Ok(categoria);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> Deletar(int id)
        {
            await _service.Deletar(id);
            return NoContent();
        }
    }
}