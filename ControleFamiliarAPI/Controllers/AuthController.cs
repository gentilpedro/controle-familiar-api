using ControleFamiliarAPI.DTOs.Auth;
using ControleFamiliarAPI.Responses;
using ControleFamiliarAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.RateLimiting;

namespace ControleFamiliarAPI.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _service;

        public AuthController(IAuthService service)
        {
            _service = service;
        }

        [HttpPost("registrar")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        [Tags("Autenticação")]
        [EndpointSummary("Cria uma nova conta de usuário")]
        [EndpointDescription("""
            Cria uma nova conta. O usuário pode:

            - Criar uma família nova (ModoFamilia = "Nova"), sendo o único
              membro dela — equivalente ao uso individual.
            - Entrar em uma família já existente (ModoFamilia = "Entrar"),
              informando o CodigoConvite de um membro dessa família, passando
              a compartilhar Pessoas, Categorias e Transações com ela.

            Retorna o token JWT já pronto para uso.
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> Registrar(RegistrarDto dto, CancellationToken cancellationToken)
        {
            var resultado = await _service.Registrar(dto, cancellationToken);
            return Ok(new ApiResponse<AuthResponseDto>(resultado));
        }

        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        [Tags("Autenticação")]
        [EndpointSummary("Autentica um usuário existente")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult> Login(LoginDto dto, CancellationToken cancellationToken)
        {
            var resultado = await _service.Login(dto, cancellationToken);
            return Ok(new ApiResponse<AuthResponseDto>(resultado));
        }

        [HttpGet("me")]
        [Authorize]
        [Tags("Autenticação")]
        [EndpointSummary("Retorna os dados do usuário autenticado e da sua família")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult> Me(CancellationToken cancellationToken)
        {
            var resultado = await _service.Me(cancellationToken);
            return Ok(new ApiResponse<MeDto>(resultado));
        }

        [HttpPatch("me")]
        [Authorize]
        [Tags("Autenticação")]
        [EndpointSummary("Atualiza nome e/ou e-mail do usuário autenticado")]
        [EndpointDescription("""
            Atualização parcial: campos omitidos permanecem inalterados.

            Alterar o e-mail marca-o como não confirmado novamente e reenvia
            o e-mail de confirmação.
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> AtualizarPerfil(AtualizarPerfilDto dto, CancellationToken cancellationToken)
        {
            var resultado = await _service.AtualizarPerfil(dto, cancellationToken);
            return Ok(new ApiResponse<MeDto>(resultado));
        }

        [HttpDelete("me")]
        [Authorize]
        [Tags("Autenticação")]
        [EndpointSummary("Exclui a conta do usuário autenticado")]
        [EndpointDescription("""
            Exclui definitivamente a conta do usuário autenticado (LGPD, art. 18, VI).

            - Se for o único membro da família, os dados dessa família
              (pessoas, categorias, transações) também são excluídos — não
              sobra dado órfão.
            - Se a família for compartilhada, apenas a conta do usuário é
              removida; os dados continuam disponíveis para os demais membros.
            - Se for o único administrador de uma família compartilhada, é
              preciso promover outro administrador antes de excluir a conta.

            A operação é irreversível.
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> ExcluirConta(CancellationToken cancellationToken)
        {
            await _service.ExcluirConta(cancellationToken);
            return Ok(new ApiResponse<string>("Conta excluída com sucesso."));
        }

        [HttpPost("logout")]
        [Authorize]
        [Tags("Autenticação")]
        [EndpointSummary("Invalida o token usado nesta requisição")]
        [EndpointDescription("""
            Revoga o token JWT enviado nesta requisição — qualquer tentativa de
            reutilizá-lo depois disso é rejeitada com 401, mesmo que ainda não
            tenha expirado. Além de descartar o token no cliente, chame este
            endpoint para garantir que ele pare de funcionar imediatamente
            (útil se o dispositivo foi perdido, por exemplo).
            """)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult> Logout(CancellationToken cancellationToken)
        {
            await _service.Logout(cancellationToken);
            return Ok(new ApiResponse<string>("Sessão encerrada."));
        }

        [HttpGet("confirmar-email")]
        [AllowAnonymous]
        [Tags("Autenticação")]
        [EndpointSummary("Confirma o e-mail do usuário a partir do link enviado no cadastro")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> ConfirmarEmail([FromQuery] int usuarioId, [FromQuery] string token, CancellationToken cancellationToken)
        {
            await _service.ConfirmarEmail(usuarioId, token, cancellationToken);
            return Ok(new ApiResponse<string>("E-mail confirmado com sucesso."));
        }
    }
}
