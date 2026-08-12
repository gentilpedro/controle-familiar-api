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
            - Identificador único
            - Nome
            - Idade

            Essas informações são utilizadas para vincular transações financeiras
            ao responsável pela receita ou despesa.
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> Listar(CancellationToken cancellationToken)
        {
            return Ok(await _service.Listar(cancellationToken));
        }

        [HttpPost]
        [Tags("Pessoas")]
        [EndpointSummary("Cria uma nova pessoa")]
        [EndpointDescription("""
            Registra uma nova pessoa que poderá realizar transações financeiras.
            Apenas administradores da família podem cadastrar pessoas.

            Dados necessários:
            - Nome (máximo de 200 caracteres)
            - Idade

            Regras de negócio:
            - Pessoas menores de 18 anos não podem registrar receitas
            - Ao remover uma pessoa, todas as transações associadas a ela
              serão removidas automaticamente
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult> Criar(PessoaCreateDto dto, CancellationToken cancellationToken)
        {
            var pessoa = await _service.Criar(dto, cancellationToken);
            return Ok(new ApiResponse<object>(pessoa));
        }

        [HttpPatch("{id}")]
        [Tags("Pessoas")]
        [EndpointSummary("Atualiza os dados de uma pessoa")]
        [EndpointDescription("""
            Permite alterar o nome ou idade de uma pessoa já cadastrada.
            O identificador da pessoa deve ser informado na rota.
            Apenas administradores da família podem editar pessoas.
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> Atualizar(int id, PessoaUpdateDto dto, CancellationToken cancellationToken)
        {
            await _service.Atualizar(id, dto, cancellationToken);
            return Ok(new ApiResponse<string>("Pessoa atualizada com sucesso"));
        }

        [HttpDelete("{id}")]
        [Tags("Pessoas")]
        [EndpointSummary("Remove uma pessoa do sistema")]
        [EndpointDescription("""
            Remove uma pessoa cadastrada através do seu identificador.
            Apenas administradores da família podem remover pessoas.

            Importante:
            - Todas as transações associadas a essa pessoa
              serão removidas automaticamente do sistema
            - Pessoa vinculada a um membro da família não pode ser removida
              por aqui — remova o membro em Minha Família
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> Deletar(int id, CancellationToken cancellationToken)
        {
            await _service.Deletar(id, cancellationToken);
            return Ok(new ApiResponse<string>("Pessoa removida com sucesso"));
        }
    }
}
