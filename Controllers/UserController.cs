using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NAVCalculationSystem.Models;
using NAVCalculationSystem.Services;
using NAVCalculationSystem.DTOs;
using Microsoft.Extensions.Configuration;

namespace NAVCalculationSystem.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UserController : ControllerBase
    {
        private readonly UserService _userService;
        private readonly IConfiguration _config;
        private readonly string _imageFolder = @"\\172.16.189.3\emp_images";

        public UserController(UserService userService, IConfiguration config)
        {
            _userService = userService;
            _config = config;
        }
        // GET: api/users
        // Protected endpoint – JWT required
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _userService.GetAllUsersAsync();
            return Ok(users);
        }

        [HttpGet("funds")]
        [Authorize]
        public async Task<IActionResult> GetFunds()
        {
            var fundLists = await _userService.GetAllFundListAsync();
            return Ok(fundLists);
        }

        [HttpGet("branchList")]
        [Authorize]
        public async Task<IActionResult> GetAllBranchListAsync()
        {
            var branchLists = await _userService.GetAllBranchListAsync();
            return Ok(branchLists);
        }

        [HttpGet("userBranchList/{userId}")]
        [Authorize]
        public async Task<IActionResult> GetUserBranchListAsync(string userId)
        {
            var branchList = await _userService.GetUserBranchListAsync(userId);
            return Ok(branchList);
        }

        [HttpGet("userFundList/{userId}")]
        [Authorize]
        public async Task<IActionResult> GetUserFundListAsync(string userId)
        {
            Console.WriteLine($"Fetching fund list for user: {userId}");
            var fundList = await _userService.GetUserFundListAsync(userId);
            return Ok(fundList);
        }



        [HttpPost("user-fund-add")]
        [Authorize]
        public async Task<IActionResult> AddUserFund([FromBody] AssignUserFundRequestDto request)
        {
            try
            {

                Console.WriteLine("--------request-----------", request);
                var result = await _userService.AssignUserFundAsync(request);

                Console.WriteLine("--------result-----------", result);

                if (result == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Failed to add Fund to user"
                    });
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("user-branch-add")]
        [Authorize]
        public async Task<IActionResult> AddUserBranch([FromBody] AssignUserBranchRequestDto request)
        {
            try
            {
                Console.WriteLine($"Request UserId: {request}");

                var result = await _userService.AssignUserBranchAsync(request);

                Console.WriteLine("--------result-----------");
                Console.WriteLine(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine("--------error-----------");
                Console.WriteLine(ex.Message);

                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }


        [HttpPost("update-password")]
        [Authorize]
        public async Task<IActionResult> UpdatePassword([FromBody] UpdatePasswordDto model)
        {
            try
            {
                bool updated = await _userService.UpdateUserPasswordAsync(model.UserId, model.OldPassword, model.NewPassword);
                if (!updated)
                    return BadRequest(new { error = "Password update failed." });

                return Ok(new { message = "Password updated successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("menus")]
        [Authorize]
        public async Task<IActionResult> GetUserMenus(string userId, int projectId)
        {
            try
            {
                var menus = await _userService.GetUserMenusAsync(userId, projectId);
                return Ok(menus);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }



        [HttpGet("project")]
        [Authorize]
        public async Task<IActionResult> GetProjectById(int projectId)
        {
            try
            {
                var project = await _userService.GetProjectByIdAsync(projectId);

                if (project == null)
                    return NotFound(new { message = "Project not found" });

                return Ok(project);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // Optional: public endpoint – no JWT needed
        [HttpGet("public")]
        [AllowAnonymous]
        public IActionResult GetPublicUsers()
        {
            return Ok("This is a public endpoint. No token required.");
        }



        [HttpGet("image/{fileName}")]
        [Authorize]
        public IActionResult GetEmployeeImage(string fileName)
        {
            var username = _config["NetworkShare:Username"];
            var password = _config["NetworkShare:Password"];
            var domain = _config["NetworkShare:Domain"];
            var credentials = new System.Net.NetworkCredential(username, password, domain);

            using (new NetworkConnection(_imageFolder, credentials))
            {
                // Look for the exact file first
                var filePath = Directory.GetFiles(_imageFolder)
                    .FirstOrDefault(f => Path.GetFileName(f).Equals(fileName, StringComparison.OrdinalIgnoreCase));

                // If file not found, use default
                if (filePath == null)
                {
                    filePath = Directory.GetFiles(_imageFolder)
                        .FirstOrDefault(f => Path.GetFileName(f).Equals("default.jpg", StringComparison.OrdinalIgnoreCase));

                    if (filePath == null)
                        return NotFound();
                }

                // Determine content type
                var ext = Path.GetExtension(filePath).ToLower();
                var contentType = ext switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    _ => "application/octet-stream"
                };

                var fileBytes = System.IO.File.ReadAllBytes(filePath);
                return File(fileBytes, contentType);
            }
        }

        [HttpGet("login-history")]
        [Authorize]
        public async Task<IActionResult> GetLoginHistory(
            [FromQuery] string userId,
            [FromQuery] int projectId)
        {
            var result = await _userService.GetLoginHistoryAsync(userId, projectId);

            // Return empty array if no data
            return Ok(result ?? Enumerable.Empty<LoginHistoryDto>());
        }

        [HttpGet("user-dashboard-stats")]

        public async Task<IActionResult> GetUserDashboardStats()
        {
            var result = await _userService.getUserDashboardStatsService();
            return Ok(result);
        }




        [HttpGet("user-grid")]
        [Authorize]
        public async Task<IActionResult> GetUserGrid(
            string userId = null,
            string userName = null,
            string phoneNumber = null,
            string userEmail = null
        )
        {
            try
            {
                var users = await _userService.GetUserGridAsync(
                    userId, userName, phoneNumber, userEmail
                );

                return Ok(users); // return empty list if none found
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }


        // [HttpPost("user-save")]
        // [Authorize]
        // public async Task<IActionResult> SaveUser([FromForm] UserCreateRequest request)
        // {
        //     try
        //     {
        //         // 1. Save user to DB
        //         var result = await _userService.CreateUserAsync(request);
        //         if (!result)
        //             return BadRequest(new { message = "Failed to create user" });

        //         // 2. Save image if provided
        //         if (request.ImageFile != null && request.ImageFile.Length > 0)
        //         {
        //             var imageFolder = @"\\172.16.189.3\emp_images";
        //             if (!Directory.Exists(imageFolder))
        //             {
        //                 Directory.CreateDirectory(imageFolder);
        //             }

        //             var fileExt = Path.GetExtension(request.ImageFile.FileName);
        //             var filePath = Path.Combine(imageFolder, request.UserId + fileExt);

        //             using var stream = new FileStream(filePath, FileMode.Create);
        //             await request.ImageFile.CopyToAsync(stream);
        //         }

        //         return Ok(new { status = "success", message = "User created successfully" });
        //     }
        //     catch (Exception ex)
        //     {
        //         return BadRequest(new { error = ex.Message });
        //     }
        // }

        [HttpPost("user-save")]
        [Authorize]
        public async Task<IActionResult> SaveUser([FromForm] UserCreateRequest request)
        {
            try
            {
                // 1️⃣ Save user to DB
                var result = await _userService.CreateUserAsync(request);
                if (!result)
                    return BadRequest(new { message = "Failed to create user" });

                // 2️⃣ Save image if provided
                if (request.ImageFile != null && request.ImageFile.Length > 0)
                {
                    var username = _config["NetworkShare:Username"];
                    var password = _config["NetworkShare:Password"];
                    var domain = _config["NetworkShare:Domain"];
                    var credentials = new System.Net.NetworkCredential(username, password, domain);

                    // Use NetworkConnection to connect to the network share
                    using (new NetworkConnection(_imageFolder, credentials))
                    {
                        if (!Directory.Exists(_imageFolder))
                            Directory.CreateDirectory(_imageFolder);

                        var fileExt = Path.GetExtension(request.ImageFile.FileName);
                        var filePath = Path.Combine(_imageFolder, request.UserId + fileExt);

                        using var stream = new FileStream(filePath, FileMode.Create);
                        await request.ImageFile.CopyToAsync(stream);
                    }
                }

                return Ok(new { status = "success", message = "User created successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }


        [HttpPut("user-update")]
        [Authorize]
        public async Task<IActionResult> UpdateUser([FromForm] UserCreateRequest request)
        {
            try
            {
                var result = await _userService.UpdateUserAsync(request);
                if (!result)
                    return NotFound(new { message = "User not found or update failed" });

                return Ok(new { status = "success", message = "User updated successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("reset-password")]
        [Authorize]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            try
            {
                var result = await _userService.ResetPasswordAsync(request.UserId);
                if (!result)
                    return NotFound(new { message = "User not found" });

                return Ok(new { status = "success", message = "Password has been reset to '123456'" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpDelete("user-delete/{userId}")]
        [Authorize]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            try
            {
                var result = await _userService.DeleteUserAsync(userId);
                if (!result)
                    return NotFound(new { message = "User not found" });

                return Ok(new { status = "success", message = "User has been deactivated" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpDelete("user-toggle/{userId}")]
        [Authorize]
        public async Task<IActionResult> ToggleUserStatus(string userId)
        {
            try
            {
                var result = await _userService.ToggleUserStatusAsync(userId);
                if (result == null)
                    return NotFound(new { message = "User not found or status could not be changed" });

                return Ok(new { status = "success", message = $"User has been {result}" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("user-details/{userId}")]
        [Authorize]
        public async Task<IActionResult> GetUserDetails(string userId)
        {
            try
            {
                var user = await _userService.GetUserDetailsAsync(userId);
                if (user == null)
                    return NotFound(new { message = "User not found" });

                return Ok(user);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("active-user-ids")]
        [Authorize]
        public async Task<IActionResult> GetActiveUserIds()
        {
            try
            {
                var userIds = await _userService.GetActiveUserIdsAsync();
                if (!userIds.Any())
                    return NotFound(new { message = "No active users found" });

                return Ok(userIds);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("valid-projects")]
        [Authorize]
        public async Task<IActionResult> GetProjects()
        {
            try
            {
                var projects = await _userService.GetValidProjectsAsync();
                if (!projects.Any())
                    return NotFound(new { message = "No projects found" });

                return Ok(projects);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("user-projects/{userId}")]
        [Authorize]
        public async Task<IActionResult> GetUserProjects(string userId)
        {
            try
            {
                var projects = await _userService.GetUserProjectsAsync(userId);

                if (!projects.Any())
                    return NotFound(new { message = "No projects found for this user" });

                return Ok(projects);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("user-project-valid/{userId}/{projectId}")]
        [Authorize]
        public async Task<IActionResult> CheckUserProjectValid(string userId, int projectId)
        {
            try
            {
                var isValid = await _userService.IsUserProjectValidAsync(userId, projectId);
                return Ok(new { userId, isValid });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }


        [HttpPost("user-project-add")]
        [Authorize]
        public async Task<IActionResult> AddUserProject([FromBody] UserProjectCreateRequest request)
        {
            try
            {
                var result = await _userService.AddUserProjectAsync(request);
                if (!result)
                    return BadRequest(new { message = "Failed to add project to user" });

                return Ok(new { status = "success", message = "Project added to user successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("user-project-revoke")]
        [Authorize]
        public async Task<IActionResult> RevokeUserProject([FromBody] UserProjectRevokeRequest request)
        {
            try
            {
                var result = await _userService.RevokeUserProjectAsync(request);
                if (!result)
                    return BadRequest(new { message = "Failed to revoke project access" });

                return Ok(new { status = "success", message = "Project access revoked successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }



        [HttpGet("user-menu/{userId}/{projectId}")]
        [Authorize]
        public async Task<IActionResult> GetUserMenu(string userId, int projectId)
        {
            try
            {
                var menus = await _userService.GetUserMenuAsync(userId, projectId);

                if (!menus.Any())
                    return NotFound(new { message = "No menus found for this user/project" });

                return Ok(menus);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }


        [HttpPost("user-menu-update")]
        [Authorize]
        public async Task<IActionResult> UpdateUserMenu([FromBody] UserMenuUpdateRequest request)
        {
            try
            {
                var result = await _userService.UpdateUserMenuAsync(request);
                if (!result)
                    return BadRequest(new { message = "Failed to update user menu permissions" });

                return Ok(new { status = "success", message = "User menu permissions updated successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("employees/active")]
        [Authorize]
        public async Task<IActionResult> GetActiveEmployees()
        {
            try
            {
                var result = await _userService.GetActiveEmployeesAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("user-exists/{userId}")]
        [Authorize]
        public async Task<IActionResult> UserExists(string userId)
        {
            try
            {
                var exists = await _userService.UserExistsAsync(userId);
                return Ok(new { userId, exists });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("project-wise-user-count")]
        public async Task<IActionResult> GetProjectWiseUserCount()
        {
            var result = await _userService.GetProjectWiseUserCountAsync();

            if (!result.Any())
                return NotFound("No project data found.");

            return Ok(result);
        }

    }
}
