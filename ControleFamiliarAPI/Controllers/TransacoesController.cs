using ControleFamiliarAPI.DTOs.Transacao;
using ControleFamiliarAPI.Responses;
using ControleFamiliarAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;

namespace ControleFamiliarAPI.Controllers
{
    [ApiController]
    [Route("api/transacoes")]
    [Authorize]
    public class TransacoesController : ControllerBase
    {
        private readonly ITransacaoService _service;

        public TransacoesController(ITransacaoService service)
        {
            _service = service;
        }

        [HttpGet]
        [Tags("Transações")]
        [EndpointSummary("Lista as transações financeiras da família, paginado")]
        [EndpointDescription("""
            Retorna as transações da família do usuário autenticado, da mais
            recente para a mais antiga, paginadas.

            Cada transação contém:
            - Identificador da transação
            - Descrição
            - Valor
            - Tipo (Receita ou Despesa)
            - Pessoa associada
            - Categoria associada

            Parâmetros de paginação (query string):
            - pagina (padrão 1)
            - tamanhoPagina (padrão 50, máximo 200)
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> Listar([FromQuery] int pagina = 1, [FromQuery] int tamanhoPagina = 50, CancellationToken cancellationToken = default)
        {
            return Ok(await _service.Listar(pagina, tamanhoPagina, cancellationToken));
        }

        [HttpPost]
        [Tags("Transações")]
        [EndpointSummary("Cria uma nova transação financeira")]
        [EndpointDescription("""
            Registra uma nova transação de receita ou despesa vinculada
            a uma pessoa e a uma categoria existente.

            Dados necessários:
            - Descrição da transação
            - Valor (deve ser positivo)
            - Tipo da transação (Receita ou Despesa)
            - Identificador da pessoa
            - Identificador da categoria

            Regras de negócio:
            - O valor deve ser maior que zero
            - Pessoas menores de 18 anos podem registrar apenas despesas
            - A categoria deve ser compatível com o tipo da transação
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> Criar(TransacaoCreateDto dto, CancellationToken cancellationToken)
        {
            await _service.Criar(dto, cancellationToken);

            return Ok();
        }

        [HttpPost("parceladas")]
        [Tags("Transações")]
        [EndpointSummary("Cria uma compra parcelada em N meses")]
        [EndpointDescription("""
            Divide o valor total em parcelas iguais (a última absorve o
            resíduo do arredondamento, a soma sempre bate com o total) e cria
            uma transação por parcela, uma por mês a partir da data
            informada. As parcelas nascem ligadas por uma série — editar ou
            excluir uma delas oferece a opção de aplicar às seguintes.

            Dados necessários:
            - Descrição, Valor total, Número de parcelas (2 a 60)
            - Tipo, Pessoa, Categoria — mesmas regras de negócio da criação
              avulsa, validadas uma vez pra toda a série
            - Data da primeira parcela — as demais caem no mesmo dia dos
              meses seguintes (se o dia não existir em algum mês, cai no
              último dia daquele mês)
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> CriarParcelada(TransacaoParceladaCreateDto dto, CancellationToken cancellationToken)
        {
            await _service.CriarParcelada(dto, cancellationToken);

            return Ok();
        }

        [HttpPost("recorrencia-percentual")]
        [Tags("Transações")]
        [EndpointSummary("Divide um valor total em ocorrências percentuais dentro de um mês")]
        [EndpointDescription("""
            Ex.: salário dividido em quinzenas — 35% no dia 15, 75% no fim
            do mês. As ocorrências nascem ligadas por uma série, mesmo
            mecanismo do parcelamento (editar/excluir uma delas oferece a
            opção de aplicar às seguintes).

            Só funciona com uma categoria que tenha AceitaDivisaoPercentual,
            hoje só a categoria de sistema "Salário" — Tipo é sempre Receita.

            Dados necessários:
            - Descrição, Valor total
            - MesReferencia (ano/mês; o dia é ignorado)
            - Ocorrencias: lista de {Dia, Percentual}, mínimo 2 — não
              precisam somar 100 entre si
            - Pessoa, Categoria
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> CriarRecorrenciaPercentual(TransacaoRecorrenciaPercentualCreateDto dto, CancellationToken cancellationToken)
        {
            await _service.CriarRecorrenciaPercentual(dto, cancellationToken);

            return Ok();
        }

        [HttpPatch("{id}")]
        [Tags("Transações")]
        [EndpointSummary("Atualiza uma transação financeira")]
        [EndpointDescription("""
            Permite alterar qualquer campo de uma transação já cadastrada.
            O identificador vai na rota; o corpo só precisa trazer os campos
            que mudam.

            As mesmas regras de negócio da criação valem aqui, aplicadas ao
            resultado final — ex.: se só o Tipo mudar, a compatibilidade com
            a Pessoa e a Categoria que a transação já tinha é checada de
            novo.

            Quando a transação pertence a uma série (parcelamento ou divisão
            percentual), AplicarAFuturas=true propaga a mudança — exceto
            Data — pra todas as ocorrências seguintes da mesma série. Sem
            efeito numa transação avulsa.
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> Atualizar(int id, TransacaoUpdateDto dto, CancellationToken cancellationToken)
        {
            await _service.Atualizar(id, dto, cancellationToken);
            return Ok(new ApiResponse<string>("Transação atualizada com sucesso"));
        }

        [HttpPatch("{id}/pago")]
        [Tags("Transações")]
        [EndpointSummary("Marca uma transação como paga/recebida ou pendente")]
        [EndpointDescription("""
            Endpoint dedicado pro clique direto na tabela — não passa pelas
            regras de negócio de Atualizar (Pessoa/Categoria/Tipo/Valor não
            mudam, só o status). "Pago" pra Despesa, "Recebido" pra Receita:
            mesmo campo, rótulo contextual conforme o Tipo da transação.
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> MarcarPago(int id, TransacaoPagoUpdateDto dto, CancellationToken cancellationToken)
        {
            await _service.MarcarPago(id, dto.Pago!.Value, cancellationToken);
            return Ok(new ApiResponse<string>("Status atualizado com sucesso"));
        }

        [HttpDelete("{id}")]
        [Tags("Transações")]
        [EndpointSummary("Remove uma transação financeira")]
        [EndpointDescription("""
            Remove a transação identificada na rota.

            Quando ela pertence a uma série, excluirFuturas=true (query
            string) remove também todas as ocorrências seguintes da mesma
            série. Sem efeito numa transação avulsa.
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> Deletar(int id, [FromQuery] bool excluirFuturas, CancellationToken cancellationToken)
        {
            await _service.Deletar(id, excluirFuturas, cancellationToken);
            return Ok(new ApiResponse<string>("Transação removida com sucesso"));
        }
    }
}
