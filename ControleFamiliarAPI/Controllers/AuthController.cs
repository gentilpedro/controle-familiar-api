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
        public async Task<ActionResult> Registrar(RegistrarDto dto)
        {
            var resultado = await _service.Registrar(dto);
            return Ok(new ApiResponse<AuthResponseDto>(resultado));
        }

        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        [Tags("Autenticação")]
        [EndpointSummary("Autentica um usuário existente")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult> Login(LoginDto dto)
        {
            var resultado = await _service.Login(dto);
            return Ok(new ApiResponse<AuthResponseDto>(resultado));
        }

        [HttpGet("me")]
        [Authorize]
        [Tags("Autenticação")]
        [EndpointSummary("Retorna os dados do usuário autenticado e da sua família")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult> Me()
        {
            var resultado = await _service.Me();
            return Ok(new ApiResponse<MeDto>(resultado));
        }
    }
}
