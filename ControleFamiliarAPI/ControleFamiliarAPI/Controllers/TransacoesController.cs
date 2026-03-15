using ControleFamiliarAPI.DTOs.Transacao;
using ControleFamiliarAPI.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ControleFamiliarAPI.Controllers
{
    /// <summary>
    /// Controller responsável pelo gerenciamento das transações financeiras.
    /// </summary>
    /// <remarks>
    /// Permite registrar e consultar receitas e despesas vinculadas
    /// às pessoas e categorias cadastradas no sistema.
    /// </remarks>
    [ApiController]
    [Route("api/transacoes")]
    public class TransacoesController : ControllerBase
    {
        /// <summary>
        /// Inicializa uma nova instância da controller de transações.
        /// </summary>
        /// <param name="service">Serviço responsável pela lógica de negócio das transações</param>
        private readonly ITransacaoService _service;

        public TransacoesController(ITransacaoService service)
        {
            _service = service;
        }


        /// <summary>
        /// Lista todas as transações cadastradas.
        /// </summary>
        /// <remarks>
        /// Retorna todas as transações registradas no sistema contendo:
        /// 
        /// - Identificador da transação
        /// - Descrição
        /// - Valor
        /// - Tipo da transação (Receita ou Despesa)
        /// - Pessoa associada
        /// - Categoria associada
        /// </remarks>
        /// <returns>
        /// Lista de transações cadastradas.
        /// </returns>
        /// <response code="200">Lista de transações retornada com sucesso</response>
        [HttpGet]
        public async Task<ActionResult> Listar()
        {
            return Ok(await _service.Listar());
        }


        /// <summary>
        /// Cria uma nova transação financeira.
        /// </summary>
        /// <remarks>
        /// Registra uma nova transação de receita ou despesa vinculada
        /// a uma pessoa e a uma categoria existente.
        /// 
        /// Dados necessários:
        /// 
        /// - Descrição da transação
        /// - Valor (deve ser positivo)
        /// - Tipo da transação (Receita ou Despesa)
        /// - Identificador da pessoa
        /// - Identificador da categoria
        /// 
        /// Regras de negócio:
        /// 
        /// - O valor deve ser maior que zero
        /// - Pessoas menores de 18 anos podem registrar apenas despesas
        /// - A categoria deve ser compatível com o tipo da transação
        /// </remarks>
        /// <param name="dto">Objeto contendo os dados da transação</param>
        /// <returns>
        /// Confirmação da criação da transação.
        /// </returns>
        /// <response code="200">Transação criada com sucesso</response>
        /// <response code="400">Erro de validação ou regra de negócio</response>
        [HttpPost]
        public async Task<ActionResult> Criar(TransacaoCreateDto dto)
        {
            await _service.Criar(dto);

            return Ok();
        }
    }
}