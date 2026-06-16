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
                    // Console.WriteLine($"Retrieved {((IEnumerable<ManagementFeeDto>)result).Count()} management fee records.");


                    break;

                case 3:
                    result = await _payableService.GetAllCustodianFeeListAsync(
                        expenseTypeId,
                        navDate,
                        days);
                    // Console.WriteLine($"Retrieved {((IEnumerable<CustodianFeeDto>)result).Count()} custodian fee records.");
                    break;

                 case 4:
                  
                    result = await _payableService.GetAllTrusteeFeeListAsync(
                        expenseTypeId,
                        navDate,
                        days);
                 Console.WriteLine($"Retrieved {((IEnumerable<TrusteeFeeDto>)result).Count()} Trustee fee records.");
                    break;




                default:
                    return BadRequest("Invalid Expense Type");
            }

            if (result is System.Collections.ICollection collection)
            {
                Console.WriteLine($"Retrieved {collection.Count} records.");
            }


            return Ok(result);


        }
        [HttpGet("expenseTypeList")]
        [Authorize]
        public async Task<IActionResult> GetAllExpenseTypesAsync()
        {
            var result = await _payableService.GetAllExpenseTypesAsync();

            // Console.WriteLine(
            //     $"Retrieved {result.Count()} expense type records."
            // );

            return Ok(result);
        }
    }
}