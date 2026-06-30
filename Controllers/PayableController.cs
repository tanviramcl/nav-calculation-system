using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NAVCalculationSystem.Services;
using NAVCalculationSystem.DTOs;

namespace NAVCalculationSystem.Controllers
{
    [ApiController]
    [Route("api/payable")]
    public class PayableController : ControllerBase
    {
        private readonly PayableService _payableService;

        public PayableController(PayableService payableService)
        {
            _payableService = payableService;
        }

        [HttpGet("managmentFeeList")]
        [Authorize]
        public async Task<IActionResult> GetAllManagmentFeeListAsync(
            int expenseTypeId,
            DateTime navDate,
            int days)
        {
            object result;

            switch (expenseTypeId)
            {
                case 2:
                    result = await _payableService.GetAllManagmentFeeListAsync(
                        expenseTypeId,
                        navDate,
                        days);
                    break;

                case 3:
                    result = await _payableService.GetAllCustodianFeeListAsync(
                        expenseTypeId,
                        navDate,
                        days);
                    break;

                case 4:
                    result = await _payableService.GetAllTrusteeFeeListAsync(
                        expenseTypeId,
                        navDate,
                        days);
                    break;

                case 6:
                    result = await _payableService.GetAllAnnualFeeListAsync(
                        expenseTypeId,
                        navDate,
                        days);
                    break;
                case 7:
                    result = await _payableService.GetAllListingFeeListAsync(
                        expenseTypeId,
                        navDate,
                        days);
                      //  Console.WriteLine("Listing Fee List Retrieved");
                    break;

                default:
                    return BadRequest("Invalid Expense Type");
            }

            return Ok(result);
        }

        [HttpGet("expenseTypeList")]
        [Authorize]
        public async Task<IActionResult> GetAllExpenseTypesAsync()
        {
            var result = await _payableService.GetAllExpenseTypesAsync();

            return Ok(result);
        }

        [HttpPost("save-payable")]
        [Authorize]
        public async Task<IActionResult> SaveExpensePayable(
            [FromBody] List<ExpensePayableDto> payableList)
        {
            if (payableList == null || !payableList.Any())
            {
                return BadRequest(new
                {
                    message = "Please select at least one record."
                });
            }

            var entryBy = User.FindFirst("userId")?.Value;

            if (string.IsNullOrWhiteSpace(entryBy))
            {
                return Unauthorized(new
                {
                    message = "Invalid user."
                });
            }

            // foreach (var item in payableList)
            // {
            //     Console.WriteLine("======================================");
            //     Console.WriteLine($"NAV_DATE               : {item.NAV_DATE:yyyy-MM-dd}");
            //     Console.WriteLine($"FUND_CD                : {item.FUND_CD}");
            //     Console.WriteLine($"FUND_NAME              : {item.FUND_NAME}");
            //     Console.WriteLine($"EXPENSE_TYPE_ID        : {item.EXPENSE_TYPE_ID}");
            //     Console.WriteLine($"EXPENSE_TYPE_NAME      : {item.EXPENSE_TYPE_NAME}");
            //     Console.WriteLine($"PORTFOLIO_MARKET_VALUE : {item.PORTFOLIO_MARKET_VALUE}");
            //     Console.WriteLine($"ANNUAL_RATE            : {item.ANNUAL_RATE}");
            //     Console.WriteLine($"DAILY_FEE              : {item.DAILY_FEE}");
            //     Console.WriteLine($"NAV_DAYS               : {item.NAV_DAYS}");
            //     Console.WriteLine($"ACCRUED_FEE            : {item.ACCRUED_FEE}");
            //     Console.WriteLine($"SKIP_REASON            : {item.skipReason}");
            // }


           var result = await _payableService
               .SaveExpensePayableAsync(payableList, entryBy);

           return Ok(result);
           
        }
    }
}