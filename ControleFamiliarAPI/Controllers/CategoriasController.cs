using ControleFamiliarAPI.DTOs.Categoria;
using ControleFamiliarAPI.Filters;
using ControleFamiliarAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;

namespace ControleFamiliarAPI.Controllers
{

    [ApiController]
    [Route("api/categorias")]
    [Authorize]
    [ExigirAssinatura]
    public class CategoriasController : ControllerBase
    {
        private readonly ICategoriaService _service;

        public CategoriasController(ICategoriaService service)
        {
            _service = service;
        }

        [HttpGet]
        [EndpointSummary("Lista todas as categorias cadastradas")]
        [EndpointDescription("""
            Retorna todas as categorias disponíveis no sistema.

            Cada categoria contém:
            - Identificador único
            - Descrição da categoria
            - Finalidade (Receita, Despesa ou Ambas)

            Essas categorias são utilizadas para classificar as transações financeiras.
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> Listar(CancellationToken cancellationToken)
        {
            return Ok(await _service.Listar(cancellationToken));
        }

        [HttpPost]
        [EndpointSummary("Cria uma nova categoria financeira")]
        [EndpointDescription("""
            Registra uma nova categoria para classificação de receitas e despesas.

            Dados necessários:
            - Descrição da categoria (máximo de 400 caracteres)
            - Finalidade da categoria

            Finalidades possíveis:
            1 → Receita
            2 → Despesa
            3 → Ambas

            Exemplos:
            - Salário
            - Alimentação
            - Transporte
            - Lazer
            - Investimentos
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> Criar(CategoriaCreateDto dto, CancellationToken cancellationToken)
        {
            var categoria = await _service.Criar(dto, cancellationToken);
            return Ok(categoria);
        }

        [HttpDelete("{id}")]
        [EndpointSummary("Remove uma categoria do sistema")]
        [EndpointDescription("""
            Remove uma categoria através do seu identificador.

            Importante:
            Caso existam transações vinculadas, a remoção pode ser bloqueada
            de acordo com as regras de negócio.
            """)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> Deletar(int id, CancellationToken cancellationToken)
        {
            await _service.Deletar(id, cancellationToken);
            return NoContent();
        }
    }
}
