using ControleFamiliarAPI.DTOs.Auth;
using ControleFamiliarAPI.Responses;
using ControleFamiliarAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;

namespace ControleFamiliarAPI.Controllers
{
    [ApiController]
    [Route("api/familia")]
    [Authorize]
    public class FamiliaController : ControllerBase
    {
        private readonly IFamiliaService _service;

        public FamiliaController(IFamiliaService service)
        {
            _service = service;
        }

        [HttpGet]
        [Tags("Família")]
        [EndpointSummary("Dados da família do usuário logado")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> Obter(CancellationToken cancellationToken)
        {
            return Ok(new ApiResponse<FamiliaDto>(await _service.Obter(cancellationToken)));
        }

        [HttpDelete("membros/{usuarioId}")]
        [Tags("Família")]
        [EndpointSummary("Remove um membro da família (admin)")]
        [EndpointDescription("""
            Remove um membro da família. O removido não perde a conta: passa
            a ter sua própria família individual, da qual é o único membro e
            administrador.

            Regras:
            - Somente administradores podem remover membros.
            - Não é possível remover a si mesmo.
            - Não é possível remover o último administrador da família.
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult> RemoverMembro(int usuarioId, CancellationToken cancellationToken)
        {
            return Ok(new ApiResponse<FamiliaDto>(await _service.RemoverMembro(usuarioId, cancellationToken)));
        }

        [HttpPost("membros/{usuarioId}/promover")]
        [Tags("Família")]
        [EndpointSummary("Promove um membro a administrador (admin)")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult> Promover(int usuarioId, CancellationToken cancellationToken)
        {
            return Ok(new ApiResponse<FamiliaDto>(await _service.PromoverAdmin(usuarioId, cancellationToken)));
        }

        [HttpPost("membros/{usuarioId}/rebaixar")]
        [Tags("Família")]
        [EndpointSummary("Remove o status de administrador de um membro (admin)")]
        [EndpointDescription("Não é possível rebaixar o último administrador da família.")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult> Rebaixar(int usuarioId, CancellationToken cancellationToken)
        {
            return Ok(new ApiResponse<FamiliaDto>(await _service.RebaixarAdmin(usuarioId, cancellationToken)));
        }

        [HttpPost("regenerar-codigo")]
        [Tags("Família")]
        [EndpointSummary("Gera um novo código de convite, invalidando o antigo (admin)")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult> RegenerarCodigo(CancellationToken cancellationToken)
        {
            return Ok(new ApiResponse<FamiliaDto>(await _service.RegenerarCodigoConvite(cancellationToken)));
        }

        [HttpPost("convidar")]
        [Tags("Família")]
        [EndpointSummary("Envia um convite por e-mail para entrar na família (admin)")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult> ConvidarPorEmail(ConvidarDto dto, CancellationToken cancellationToken)
        {
            await _service.ConvidarPorEmail(dto.Email, cancellationToken);
            return Ok(new ApiResponse<string>("Convite enviado."));
        }
    }
}
