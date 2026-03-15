using ControleFamiliarAPI.DTO.Pessoa;
using ControleFamiliarAPI.Responses;
using ControleFamiliarAPI.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ControleFamiliarAPI.Controllers
{
    [ApiController]
    [Route("api/pessoas")]
    public class PessoasController : ControllerBase
    {
        private readonly IPessoaService _service;

        public PessoasController(IPessoaService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult> Listar()
        {
            return Ok(await _service.Listar());
        }

        [HttpPost]
        public async Task<ActionResult> Criar(PessoaCreateDto dto)
        {
            var pessoa = await _service.Criar(dto);
            return Ok(new ApiResponse<object>(pessoa));
        }

        [HttpPut("{id}")]
        public async Task<ActionResult> Atualizar(int id, PessoaUpdateDto dto)
        {
            await _service.Atualizar(id, dto);
            return Ok(new ApiResponse<string>("Pessoa não encontrada"));
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> Deletar(int id)
        {
            await _service.Deletar(id);
            return Ok(new ApiResponse<string>("Pessoa Removida com Sucesso"));
        }
    }
}