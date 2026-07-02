using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NAVCalculationSystem.Services;

namespace NAVCalculationSystem.Controllers
{
    [ApiController]
    [Route("api/nav")]
    [Authorize]
    public class NavController : ControllerBase
    {
        private readonly NavCalService _navCalService;

        public NavController(NavCalService navCalService)
        {
            _navCalService = navCalService;
        }

        [HttpGet("nav-Process-funds")]
        public async Task<IActionResult> GetFunds()
        {
            try
            {
                var funds = await _navCalService.GetFundsAsync();

                // Console.WriteLine("Funds retrieved successfully:");
                // foreach (var fund in funds)
                // {
                //     Console.WriteLine($"Fund Code: {fund.F_CD}, Fund Name: {fund.F_NAME}, Fund Type: {fund.F_TYPE}, Last NAV No: {fund.LAST_NAVNO}, Last NAV Date: {fund.LAST_NAVDATE}, Next NAV No: {fund.NAVNO}");
                // }

                return Ok(new
                {
                    funds
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = "An error occurred while retrieving funds.",
                    error = ex.Message
                });
            }
        }
    }
}