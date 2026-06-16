using Microsoft.AspNetCore.Mvc;
using NAVCalculationSystem.DTOs;
using NAVCalculationSystem.Services;
using Microsoft.AspNetCore.Authorization;

namespace NAVCalculationSystem.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto model)
        {
            try
            {
                var result = await _authService.RegisterAsync(model);
                return Ok(new { message = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto model)
        {
            try
            {
                var token = await _authService.LoginAsync(model);

           
                if (token == null)
                {
                    return Unauthorized(new { error = "Invalid credentials." });
                }


                bool isPasswordUpToDate = await _authService.IsUserPassUptoDate(model.UserId);


                return Ok(new 
                { 
                    token = token, 
                    userId = model.UserId, 
                    passwordUpToDate = isPasswordUpToDate 
                });
            }
            catch (Exception ex)
            {
                return Unauthorized(new { error = ex.Message });
            }
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutDto model)
        {
            try
            {
                var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

                // Save token into blacklist
                await _authService.BlacklistTokenAsync(token);

                // Update logout time in LOGINHISTORY
                await _authService.UpdateLogoutTimeAsync(model.UserId, model.ProjectId);

                return Ok(new { message = "Logout successful" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }




    }
}
