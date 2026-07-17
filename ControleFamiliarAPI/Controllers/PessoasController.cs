using ControleFamiliarAPI.DTOs.Pessoa;
using ControleFamiliarAPI.Responses;
using ControleFamiliarAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;

namespace ControleFamiliarAPI.Controllers
{
    [ApiController]
    [Route("api/pessoas")]
    [Authorize]
    public class PessoasController : ControllerBase
    {
        private readonly IPessoaService _service;

        public PessoasController(IPessoaService service)
        {
            _service = service;
        }

        [HttpGet]
        [Tags("Pessoas")]
        [EndpointSummary("Lista todas as pessoas cadastradas")]
        [EndpointDescription("""
            Retorna todas as pessoas registradas no sistema.
            
            Cada pessoa possui:
            - Identificador �nico
            - Nome
            - Idade
            
            Essas informa��es s�o utilizadas para vincular transa��es financeiras
            ao respons�vel pela receita ou despesa.
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> Listar()
        {
            return Ok(await _service.Listar());
        }

        [HttpPost]
        [Tags("Pessoas")]
        [EndpointSummary("Cria uma nova pessoa")]
        [EndpointDescription("""
            Registra uma nova pessoa que poder� realizar transa��es financeiras.
            
            Dados necess�rios:
            - Nome (m�ximo de 200 caracteres)
            - Idade
            
            Regras de neg�cio:
            - Pessoas menores de 18 anos n�o podem registrar receitas
            - Ao remover uma pessoa, todas as transa��es associadas a ela
              ser�o removidas automaticamente
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> Criar(PessoaCreateDto dto)
        {
            var pessoa = await _service.Criar(dto);
            return Ok(new ApiResponse<object>(pessoa));
        }

        [HttpPatch("{id}")]
        [Tags("Pessoas")]
        [EndpointSummary("Atualiza os dados de uma pessoa")]
        [EndpointDescription("""
            Permite alterar o nome ou idade de uma pessoa j� cadastrada.
            O identificador da pessoa deve ser informado na rota.
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> Atualizar(int id, PessoaUpdateDto dto)
        {
            await _service.Atualizar(id, dto);
            return Ok(new ApiResponse<string>("Pessoa atualizada com sucesso"));
        }

        [HttpDelete("{id}")]
        [Tags("Pessoas")]
        [EndpointSummary("Remove uma pessoa do sistema")]
        [EndpointDescription("""
            Remove uma pessoa cadastrada atrav�s do seu identificador.
            
            Importante:
            - Todas as transa��es associadas a essa pessoa
              ser�o removidas automaticamente do sistema
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> Deletar(int id)
        {
            await _service.Deletar(id);
            return Ok(new ApiResponse<string>("Pessoa removida com sucesso"));
        }
    }
}