using ControleFamiliarAPI.DTOs.Categoria;
using ControleFamiliarAPI.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ControleFamiliarAPI.Controllers
{

    /// <summary>
    /// Controller responsável pelo gerenciamento das categorias financeiras.
    /// </summary>
    /// <remarks>
    /// As categorias são utilizadas para classificar as transações financeiras
    /// registradas no sistema de controle de gastos familiares.
    /// 
    /// Cada categoria possui uma finalidade que define se ela pode ser usada
    /// para receitas, despesas ou ambas.
    /// </remarks>

    [ApiController]
    [Route("api/categorias")]
    public class CategoriasController : ControllerBase
    {
        private readonly ICategoriaService _service;

        /// <summary>
        /// Inicializa uma nova instância da controller de categorias.
        /// </summary>
        /// <param name="service">Serviço responsável pela lógica de negócio das categorias</param>

        public CategoriasController(ICategoriaService service)
        {
            _service = service;
        }

        /// <summary>
        /// Lista todas as categorias cadastradas.
        /// </summary>
        /// <remarks>
        /// Retorna todas as categorias disponíveis no sistema.
        /// 
        /// Cada categoria contém:
        /// 
        /// - Identificador único
        /// - Descrição da categoria
        /// - Finalidade (Receita, Despesa ou Ambas)
        /// 
        /// Essas categorias são utilizadas para classificar as transações financeiras.
        /// </remarks>
        /// <returns>
        /// Lista de categorias cadastradas.
        /// </returns>
        /// <response code="200">Lista retornada com sucesso</response>

        [HttpGet]
        public async Task<ActionResult> Listar()
        {
            return Ok(await _service.Listar());
        }

        /// <summary>
        /// Cria uma nova categoria financeira.
        /// </summary>
        /// <remarks>
        /// Registra uma nova categoria que poderá ser utilizada
        /// para classificar receitas e/ou despesas no sistema.
        /// 
        /// Dados necessários:
        /// 
        /// - Descrição da categoria (máximo de 400 caracteres)
        /// - Finalidade da categoria
        /// 
        /// Finalidades possíveis:
        /// 
        /// - 1 → Receita
        /// - 2 → Despesa
        /// - 3 → Ambas
        /// 
        /// Exemplo de categorias:
        /// 
        /// - Salário
        /// - Alimentação
        /// - Transporte
        /// - Lazer
        /// - Investimentos
        /// </remarks>
        /// <param name="dto">Objeto contendo os dados da categoria</param>
        /// <returns>
        /// Categoria criada com sucesso.
        /// </returns>
        /// <response code="200">Categoria criada com sucesso</response>
        /// <response code="400">Erro de validação</response>

        [HttpPost]
        public async Task<ActionResult> Criar(CategoriaCreateDto dto)
        {
            var categoria = await _service.Criar(dto);
            return Ok(categoria);
        }

        /// <summary>
        /// Remove uma categoria do sistema.
        /// </summary>
        /// <remarks>
        /// Remove uma categoria cadastrada através do seu identificador.
        /// 
        /// Importante:
        /// 
        /// Caso existam transações vinculadas a esta categoria,
        /// a remoção poderá ser bloqueada dependendo das regras
        /// definidas na camada de serviço.
        /// </remarks>
        /// <param name="id">Identificador da categoria</param>
        /// <returns>
        /// Confirmação da remoção da categoria.
        /// </returns>
        /// <response code="204">Categoria removida com sucesso</response>
        /// <response code="404">Categoria não encontrada</response>

        [HttpDelete("{id}")]
        public async Task<ActionResult> Deletar(int id)
        {
            await _service.Deletar(id);
            return NoContent();
        }
    }
}