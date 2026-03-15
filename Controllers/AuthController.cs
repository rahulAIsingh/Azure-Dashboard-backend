using System.Threading.Tasks;
using AzureFinOps.API.DTOs;
using AzureFinOps.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AzureFinOps.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IScopeService _scopeService;

        public AuthController(IAuthService authService, IScopeService scopeService)
        {
            _authService = authService;
            _scopeService = scopeService;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _authService.ValidateUserAsync(request.Email, request.Password);

            if (user == null)
            {
                return Unauthorized(new { message = "Invalid email or password" });
            }

            var scopes = await _scopeService.GetUserScopesAsync(user.Id);
            var token = _authService.GenerateJwtToken(user, scopes);

            var response = new LoginResponseDto
            {
                Token = token,
                User = new UserDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    Role = user.Role.Name,
                    Scopes = scopes
                }
            };

            return Ok(response);
        }
    }
}
