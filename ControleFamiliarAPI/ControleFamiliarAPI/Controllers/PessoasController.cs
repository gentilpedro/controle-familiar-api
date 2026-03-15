using ControleFamiliarAPI.DTO.Pessoa;
using ControleFamiliarAPI.Responses;
using ControleFamiliarAPI.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ControleFamiliarAPI.Controllers
{

    /// <summary>
    /// Controller responsável pelo gerenciamento das pessoas cadastradas no sistema.
    /// </summary>
    /// <remarks>
    /// Permite criar, listar, atualizar e remover pessoas.
    /// 
    /// As pessoas cadastradas são utilizadas para registrar transações financeiras
    /// (receitas e despesas) no sistema de controle de gastos familiares.
    /// </remarks>
    
    [ApiController]
    [Route("api/pessoas")]
    public class PessoasController : ControllerBase
    {
        private readonly IPessoaService _service;

        /// <summary>
        /// Inicializa uma nova instância da controller de pessoas.
        /// </summary>
        /// <param name="service">Serviço responsável pela lógica de negócio das pessoas</param>

        public PessoasController(IPessoaService service)
        {
            _service = service;
        }

        /// <summary>
        /// Lista todas as pessoas cadastradas.
        /// </summary>
        /// <remarks>
        /// Retorna todas as pessoas registradas no sistema.
        /// 
        /// Cada pessoa possui:
        /// 
        /// - Identificador único
        /// - Nome
        /// - Idade
        /// 
        /// Essas informações são utilizadas para vincular transações financeiras
        /// ao responsável pela receita ou despesa.
        /// </remarks>
        /// <returns>
        /// Lista contendo todas as pessoas cadastradas.
        /// </returns>
        /// <response code="200">Lista retornada com sucesso</response>

        [HttpGet]
        public async Task<ActionResult> Listar()
        {
            return Ok(await _service.Listar());
        }

        /// <summary>
        /// Cria uma nova pessoa no sistema.
        /// </summary>
        /// <remarks>
        /// Registra uma nova pessoa que poderá realizar transações financeiras.
        /// 
        /// Dados necessários:
        /// 
        /// - Nome (máximo de 200 caracteres)
        /// - Idade
        /// 
        /// Regras de negócio:
        /// 
        /// - Pessoas menores de 18 anos não podem registrar receitas
        /// - Ao remover uma pessoa, todas as transações associadas a ela
        ///   serão removidas automaticamente.
        /// </remarks>
        /// <param name="dto">Objeto contendo os dados da pessoa</param>
        /// <returns>
        /// Pessoa criada com sucesso.
        /// </returns>
        /// <response code="200">Pessoa criada com sucesso</response>
        /// <response code="400">Erro de validação</response>

        [HttpPost]
        public async Task<ActionResult> Criar(PessoaCreateDto dto)
        {
            var pessoa = await _service.Criar(dto);
            return Ok(new ApiResponse<object>(pessoa));
        }

        /// <summary>
        /// Atualiza os dados de uma pessoa existente.
        /// </summary>
        /// <remarks>
        /// Permite alterar o nome ou idade de uma pessoa já cadastrada.
        /// 
        /// O identificador da pessoa deve ser informado na rota.
        /// </remarks>
        /// <param name="id">Identificador da pessoa</param>
        /// <param name="dto">Dados atualizados da pessoa</param>
        /// <returns>
        /// Mensagem informando o resultado da operação.
        /// </returns>
        /// <response code="200">Pessoa atualizada com sucesso</response>
        /// <response code="404">Pessoa não encontrada</response>

        [HttpPut("{id}")]
        public async Task<ActionResult> Atualizar(int id, PessoaUpdateDto dto)
        {
            await _service.Atualizar(id, dto);
            return Ok(new ApiResponse<string>("Pessoa não encontrada"));
        }

        /// <summary>
        /// Remove uma pessoa do sistema.
        /// </summary>
        /// <remarks>
        /// Remove uma pessoa cadastrada através do seu identificador.
        /// 
        /// Importante:
        /// 
        /// - Todas as transações associadas a essa pessoa
        ///   serão removidas automaticamente do sistema.
        /// </remarks>
        /// <param name="id">Identificador da pessoa</param>
        /// <returns>
        /// Mensagem confirmando a remoção da pessoa.
        /// </returns>
        /// <response code="200">Pessoa removida com sucesso</response>
        /// <response code="404">Pessoa não encontrada</response>

        [HttpDelete("{id}")]
        public async Task<ActionResult> Deletar(int id)
        {
            await _service.Deletar(id);
            return Ok(new ApiResponse<string>("Pessoa Removida com Sucesso"));
        }
    }
}