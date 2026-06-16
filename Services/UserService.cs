using System.Data;
using Oracle.ManagedDataAccess.Client;
using Dapper;
using NAVCalculationSystem.Models;
using NAVCalculationSystem.Helpers;
using NAVCalculationSystem.DTOs;
using Microsoft.Extensions.WebEncoders.Testing;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Text;
using Microsoft.Extensions.Configuration;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
namespace NAVCalculationSystem.Services
{
    public class UserService
    {
        private readonly IConfiguration _configuration;

        public UserService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private IDbConnection CreateConnection()
        {
            return new OracleConnection(_configuration.GetConnectionString("DefaultConnection"));
        }

        // Get all users (excluding password)
        public async Task<IEnumerable<UserRegister>> GetAllUsersAsync()
        {
            using var conn = CreateConnection();

            var users = await conn.QueryAsync<UserRegister>(
                @"SELECT
                    USER_ID AS UserId,
                    NAME AS Name,
                    USER_EMAIL AS UserEmail,
                    USER_CELL_NO AS UserCellNo,
                    ACTIVE_FLAG AS ActiveFlag,
                    CREATED_DATE AS CreatedDate
                  FROM USER_REGISTER
                  ORDER BY USER_ID");

            return users;
        }


        // Get all users (excluding password)
        public async Task<IEnumerable<OpenEndFundInfoDto>> GetAllFundListAsync()
        {
            using var conn = CreateConnection();

            var users = await conn.QueryAsync<OpenEndFundInfoDto>(
                @"SELECT 
                    FUND_CD As FundCode, FUND_NM AS FundName, FUND_TYPE AS FundType 
                    FROM FUND_INFO 
                    order by FundCode ");

            return users;
        }

        // Get all users (excluding password)
        public async Task<IEnumerable<BranchDto>> GetAllBranchListAsync()
        {
            using var conn = CreateConnection();

            var branchList = await conn.QueryAsync<BranchDto>(
                @"SELECT 
                    BR_CD As BranchCode,BR_NM AS BranchName
                    FROM BRANCH_INFO 
                    order by BR_CD ");

            return branchList;
        }

        public async Task<IEnumerable<ManagementFeeDto>> GetAllManagmentFeeListAsync()
        {
            using var conn = CreateConnection();

            var sql = @"
                SELECT
                    f.F_CD AS FundCode,
                    f.F_NAME AS FundName,
                    rm.Portfolio_Market_Value AS PortfolioMarketValue,
                    rm.ANNUAL_RATE AS AnnualRate,
                    ROUND(rm.Portfolio_Market_Value * rm.ANNUAL_RATE / 365, 4) AS DailyFee,
                    1 AS NAVDays,
                    ROUND(rm.Portfolio_Market_Value * rm.ANNUAL_RATE / 365 * 1, 4) AS AccruedMfee
                FROM (
                    SELECT
                        fn.F_CD,
                        fn.Portfolio_Market_Value,
                        r.ANNUAL_RATE,
                        r.PRIORITY,
                        ROW_NUMBER() OVER (
                            PARTITION BY fn.F_CD
                            ORDER BY r.PRIORITY ASC
                        ) AS RN
                    FROM (
                        SELECT
                            pb.F_CD,
                            ROUND(SUM(pb.TOT_NOS * pb.ADC_RT), 2) AS Portfolio_Market_Value
                        FROM INVEST.PFOLIO_BK pb
                        WHERE pb.BAL_DT_CTRL = '07-Apr-2026'
                        GROUP BY pb.F_CD
                    ) fn
                    INNER JOIN NAV.MFEE_RATE_CONFIG r
                        ON r.IS_ACTIVE = 'Y'
                        AND (r.F_CD = fn.F_CD OR r.F_CD IS NULL)
                        AND fn.Portfolio_Market_Value >= r.SLAB_FROM
                        AND (r.SLAB_TO IS NULL OR fn.Portfolio_Market_Value <= r.SLAB_TO)
                ) rm
                INNER JOIN INVEST.FUND f ON rm.F_CD = f.F_CD
                WHERE rm.RN = 1
                ORDER BY f.F_CD";

            return await conn.QueryAsync<ManagementFeeDto>(sql);
        }

        public async Task<IEnumerable<FundDto>> GetUserFundListAsync(string userId)
        {
            using var conn = CreateConnection();

            var fundList = await conn.QueryAsync<FundDto>(
                @"SELECT 
            FUND_CD AS FundCode
          FROM USER_FUND
          WHERE USER_ID = :UserId
          AND VALID = 'Y'
          ORDER BY FUND_CD",
                new { UserId = userId }
            );

            return fundList;
        }


        public async Task<bool> UpdateUserPasswordAsync(string userId, string oldPassword, string newPassword)
        {
            using var conn = CreateConnection();

            // Step 1: Get the current password from DB
            var user = await conn.QueryFirstOrDefaultAsync<UserInfo>(
                @"SELECT USER_ID, USER_PASS 
                FROM USER_INFO 
                WHERE TRIM(USER_ID) = TRIM(:UserId)",
                new { UserId = userId });

            if (user == null)
                throw new Exception("User not found");

            // Step 2: Verify old password
            string encryptedOldInput = AESEncryption.Encrypt(oldPassword);
            if (encryptedOldInput != user.USER_PASS)
                throw new Exception("Old password is incorrect");

            // Step 3: Encrypt the new password
            string encryptedNewPassword = AESEncryption.Encrypt(newPassword);

            // Step 4: Update password and PASS_CHANGE_DATE
            var sql = @"
                UPDATE USER_INFO
                SET USER_PASS = :NewPass,
                    PASS_CHANGE_DATE = SYSDATE
                WHERE TRIM(USER_ID) = TRIM(:UserId)";

            int rowsAffected = await conn.ExecuteAsync(sql, new { NewPass = encryptedNewPassword, UserId = userId });

            return rowsAffected > 0;
        }



        public async Task<List<MenuDto>> GetUserMenusAsync(string userId, int projectId)
        {
            using var conn = CreateConnection();

            // Step 1: check if user has access to the project
            var projectCheck = await conn.ExecuteScalarAsync<int>(
                @"SELECT COUNT(*) 
          FROM USER_PROJECT 
          WHERE PROJECT_ID = :ProjectId
          AND USER_ID = :UserId",
                new { ProjectId = projectId, UserId = userId });

              //  Console.WriteLine($"Project access check for UserId={userId}, ProjectId={projectId}: {projectCheck} record(s) found.");

            if (projectCheck == 0)
                throw new Exception("User does not have access to this project.");

            // Step 2: Get parent menus (including name and link)
            var parentMenus = await conn.QueryAsync<ParentMenuDto>(
                @"SELECT M_ID AS ParentId, M_NAME AS MenuName, M_URL AS MenuLink
                FROM MENU
                WHERE M_ID IN (
                    SELECT DISTINCT M_PARENT_ID
                    FROM MENU 
                    WHERE M_ID IN (
                        SELECT M_ID 
                        FROM USER_MENU
                        WHERE USER_ID = :UserId
                        AND VALID IS NULL
                    )
                    AND VALID = 'Y'
                    AND M_PARENT_ID <> 0
                    AND PROJECT_ID = :ProjectId
                )
                AND VALID = 'Y'
                AND PROJECT_ID = :ProjectId
                ORDER BY M_ID",
                new { UserId = userId, ProjectId = projectId });

              //  Console.WriteLine($"Parent menus retrieved for UserId={userId}, ProjectId={projectId}: {parentMenus.Count()} record(s) found.");    

            var result = new List<MenuDto>();

            // Step 3: For each parent, load children
            foreach (var parent in parentMenus)
            {
                var children = await conn.QueryAsync<ChildMenuDto>(
                    @"SELECT 
                            M.M_ID AS MenuId,
                            M.M_NAME AS MenuName,
                            M.M_URL AS MenuLink
                        FROM MENU M
                        WHERE M.M_PARENT_ID = :ParentId
                        AND M.PROJECT_ID = :ProjectId
                        AND M.VALID = 'Y'
                        AND EXISTS (
                            SELECT 1 
                            FROM USER_MENU UM
                            WHERE UM.M_ID = M.M_ID
                                AND UM.USER_ID = :UserId
                                AND UM.VALID IS NULL
                        )
                        ORDER BY M.M_ID",
                    new { ParentId = parent.ParentId, ProjectId = projectId, UserId = userId });

                result.Add(new MenuDto
                {
                    ParentId = parent.ParentId,
                    MenuName = parent.MenuName,
                    MenuLink = parent.MenuLink,
                    Children = children.ToList()
                });
            }

            return result;
        }

        public async Task<ProjectDto> GetProjectByIdAsync(int projectId)
        {
            using var conn = CreateConnection();
            var query = @"SELECT ID, NAME, URL, VALID, PROJECT_DESC, DEVELOPED_BY, START_DT 
                    FROM PROJECTS 
                    WHERE ID = :projectId AND VALID = 'Y'";

            var project = await conn.QueryFirstOrDefaultAsync<ProjectDto>(query, new { projectId });
            return project;

        }

        public async Task<IEnumerable<BranchDto>> GetUserBranchListAsync(string userId)
        {
            using var conn = CreateConnection();

            var branchList = await conn.QueryAsync<BranchDto>(
                @"     
        SELECT 
     a.BR_CD As BranchCode,b.BR_NM AS BranchName
FROM UNIT.USER_BRANCH  a inner join BRANCH_INFO b on a.BR_CD=b.BR_CD where A.USER_ID=:UserId ",
                new { UserId = userId }
            );

            return branchList;
        }


        public async Task<IEnumerable<LoginHistoryDto>>
    GetLoginHistoryAsync(string userId, int projectId)
        {

            using var conn = CreateConnection();
            const string sql = @"
        SELECT
            ID             AS Id,
            USER_ID        AS UserId,
            LOGINTIME      AS LoginTime,
            ISONLINE       AS IsOnline,
            VALID          AS Valid,
            REMARKS        AS Remarks,
            LOGIN_BR       AS LoginBr,
            LOGIN_BK       AS LoginBk,
            LOGOUTTIME     AS LogoutTime,
            PROJECT_ID     AS ProjectId
        FROM LOGINHISTORY
        WHERE USER_ID = :UserId
          AND PROJECT_ID = :ProjectId
        ORDER BY LOGINTIME DESC";


            return await conn.QueryAsync<LoginHistoryDto>(
                sql,
                new
                {
                    UserId = userId,
                    ProjectId = projectId
                }
            );
        }



        public async Task<IEnumerable<UserGridDto>> GetUserGridAsync(
            string userId,
            string userName,
            string phoneNumber,
            string userEmail
        )
        {
            using var conn = CreateConnection();

            var sql = new StringBuilder(@"
                    SELECT 
                        U.USER_ID       AS UserId,
                        U.USER_NM       AS UserName,
                        U.USER_EMAIL    AS UserEmail,
                        U.USER_TEL      AS PhoneNumber,
                        U.USER_STATUS   AS UserStatus,
                        E.VALID         AS EmployeeStatus,
                        U.EMP_ID        AS EmpId,
                        D.NAME          AS Designation
                    FROM USER_INFO U
                    LEFT JOIN INVEST.EMP_INFO E
                        ON U.EMP_ID = E.EMP_ID
                    LEFT JOIN INVEST.EMP_DESIGNATION D
                        ON E.DESIG_ID = D.ID
                    WHERE 1= 1
                ");

            var param = new DynamicParameters();

            if (!string.IsNullOrWhiteSpace(userId))
            {
                sql.Append(" AND U.USER_ID LIKE :UserId ");
                param.Add("UserId", $"%{userId}%");
            }

            if (!string.IsNullOrWhiteSpace(userName))
            {
                sql.Append(" AND U.USER_NM LIKE :UserName ");
                param.Add("UserName", $"%{userName}%");
            }

            if (!string.IsNullOrWhiteSpace(phoneNumber))
            {
                sql.Append(" AND U.USER_TEL LIKE :PhoneNumber ");
                param.Add("PhoneNumber", $"%{phoneNumber}%");
            }

            if (!string.IsNullOrWhiteSpace(userEmail))
            {
                sql.Append(" AND U.USER_EMAIL LIKE :UserEmail ");
                param.Add("UserEmail", $"%{userEmail}%");
            }

            sql.Append(" ORDER BY U.CREATED_DATE DESC ");

            return await conn.QueryAsync<UserGridDto>(sql.ToString(), param);
        }


        public async Task<bool> CreateUserAsync(UserCreateRequest request)
        {
            using var conn = CreateConnection();

            // 1. Check duplicate USER_ID
            var existingUser = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT USER_ID FROM USER_INFO WHERE USER_ID = :UserId",
                new { UserId = request.UserId });

            if (!string.IsNullOrEmpty(existingUser))
                throw new Exception("Duplicate User ID found.");

            // 2. Default values
            string fundCd = string.IsNullOrWhiteSpace(request.FundCode) ? "IAMCL" : request.FundCode;
            string brCd = string.IsNullOrWhiteSpace(request.BranchCode) ? "AMC/01" : request.BranchCode;
            string encryptedPassword = AESEncryption.Encrypt("123456"); // Ensure AESEncryption is implemented


            // USER_STATUS is always 'V'
            string userStatus = "V";

            string entTm = DateTime.Now.ToString("HH:mm:ss.fff");


            // 3. Insert query
            var query = @"
            INSERT INTO USER_INFO
            (
                FUND_CD,
                BR_CD,
                USER_ID,
                USER_PASS,
                USER_NM,
                USER_TEL,
                USER_STATUS,
                EMP_ID,
                CREATED_DATE,
                USER_EMAIL,
                ENT_TM,
                CREATED_BY
            )
            VALUES
            (
                :FundCd,
                :BrCd,
                :UserId,
                :UserPass,
                :UserName,
                :PhoneNumber,
                :UserStatus,
                :EmpId,
                SYSDATE,
                :UserEmail,
                :EntTm,
                :CreatedBy
            )
        ";

            var parameters = new DynamicParameters();
            parameters.Add("FundCd", fundCd);
            parameters.Add("BrCd", brCd);
            parameters.Add("UserId", request.UserId);
            parameters.Add("UserPass", encryptedPassword);
            parameters.Add("UserName", request.UserName);
            parameters.Add("PhoneNumber", string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber);
            parameters.Add("UserStatus", userStatus);
            parameters.Add("EmpId", string.IsNullOrWhiteSpace(request.EmpId) ? null : request.EmpId);
            parameters.Add("UserEmail", string.IsNullOrWhiteSpace(request.UserEmail) ? null : request.UserEmail);
            parameters.Add("EntTm", entTm);
            parameters.Add("CreatedBy", string.IsNullOrWhiteSpace(request.CreatedBy) ? "SYSTEM" : request.CreatedBy);

            // 4. Execute insert
            var rows = await conn.ExecuteAsync(query, parameters);

            return rows > 0;
        }



        public async Task<bool> UpdateUserAsync(UserCreateRequest request)
        {
            using var conn = CreateConnection();

            // 1️⃣ Check if user exists
            var existingUser = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT USER_ID FROM USER_INFO WHERE USER_ID = :UserId",
                new { UserId = request.UserId });

            if (string.IsNullOrEmpty(existingUser))
                throw new Exception("User not found.");

            // 2️⃣ Update query
            var query = @"
                UPDATE USER_INFO
                SET
                    USER_NM = :UserName,
                    USER_TEL = :PhoneNumber,
                    USER_EMAIL = :UserEmail,
                    LAST_UPDATED_BY = :LastUpdatedBy,
                    LAST_UPDATED_DATE = :LastUpdatedDate
                WHERE USER_ID = :USER_ID
            ";

            var parameters = new DynamicParameters();
            parameters.Add("UserName", request.UserName);
            parameters.Add("PhoneNumber", string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber);
            parameters.Add("UserEmail", string.IsNullOrWhiteSpace(request.UserEmail) ? null : request.UserEmail);
            parameters.Add("LastUpdatedBy", string.IsNullOrWhiteSpace(request.LastUpdatedBy) ? "system" : request.LastUpdatedBy);
            parameters.Add("LastUpdatedDate", DateTime.Now);
            parameters.Add("USER_ID", request.UserId);

            var rows = await conn.ExecuteAsync(query, parameters);

            var _imageFolder = @"\\172.16.189.3\emp_images";

            // 3️⃣ Handle image update on network share
            if (request.ImageFile != null && request.ImageFile.Length > 0)
            {
                // Get credentials from configuration
                var username = _configuration["NetworkShare:Username"];
                var password = _configuration["NetworkShare:Password"];
                var domain = _configuration["NetworkShare:Domain"];
                var credentials = new System.Net.NetworkCredential(username, password, domain);

                using (new NetworkConnection(_imageFolder, credentials))
                {
                    if (!Directory.Exists(_imageFolder))
                        Directory.CreateDirectory(_imageFolder);

                    // Delete old image if exists
                    var oldImagePath = Directory.GetFiles(_imageFolder, request.UserId + ".*").FirstOrDefault();
                    if (!string.IsNullOrEmpty(oldImagePath))
                        File.Delete(oldImagePath);

                    // Save new image as real JPG
                    var newFilePath = Path.Combine(_imageFolder, request.UserId + ".jpg");

                    using var ms = new MemoryStream();
                    await request.ImageFile.CopyToAsync(ms);
                    ms.Position = 0;

                    using var img = Image.FromStream(ms);
                    img.Save(newFilePath, ImageFormat.Jpeg);
                }
            }

            return rows > 0;
        }

        public async Task<bool> ResetPasswordAsync(string userId)
        {
            using var conn = CreateConnection();

            // Check if user exists
            var existingUser = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT USER_ID FROM USER_INFO WHERE USER_ID = :UserId",
                new { UserId = userId });

            if (string.IsNullOrEmpty(existingUser))
                return false; // user not found

            // Encrypt default password
            string defaultPassword = "123456";
            string encryptedPassword = AESEncryption.Encrypt(defaultPassword);

            // Update password
            var query = @"
                UPDATE USER_INFO
                SET USER_PASS = :UserPass,
                    PASS_CHANGE_DATE = NULL
                WHERE USER_ID = :UserId
            ";

            var rows = await conn.ExecuteAsync(query, new { UserPass = encryptedPassword, UserId = userId });

            return rows > 0;
        }


        public async Task<bool> DeleteUserAsync(string userId)
        {
            using var conn = CreateConnection();

            // Check if user exists
            var existingUser = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT USER_ID FROM USER_INFO WHERE USER_ID = :UserId",
                new { UserId = userId });

            if (string.IsNullOrEmpty(existingUser))
                return false; // user not found

            // Soft delete by setting USER_STATUS = NULL
            var query = @"
                UPDATE USER_INFO
                SET USER_STATUS = NULL
                WHERE USER_ID = :UserId
            ";

            var rows = await conn.ExecuteAsync(query, new { UserId = userId });

            return rows > 0;
        }

        public async Task<string> ToggleUserStatusAsync(string userId)
        {
            using var conn = CreateConnection();

            // Get current status
            var currentStatus = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT USER_STATUS FROM USER_INFO WHERE USER_ID = :UserId",
                new { UserId = userId });

            if (currentStatus == null)
                currentStatus = ""; // treat null as inactive

            if (string.IsNullOrEmpty(currentStatus))
            {
                // Currently inactive → activate
                var rows = await conn.ExecuteAsync(
                    "UPDATE USER_INFO SET USER_STATUS = 'V' WHERE USER_ID = :UserId",
                    new { UserId = userId });

                if (rows > 0) return "activated";
                return null;
            }
            else if (currentStatus == "V")
            {
                // Currently active → deactivate
                var rows = await conn.ExecuteAsync(
                    "UPDATE USER_INFO SET USER_STATUS = NULL WHERE USER_ID = :UserId",
                    new { UserId = userId });

                if (rows > 0) return "deactivated";
                return null;
            }

            return null;
        }


        public async Task<UserGridDto?> GetUserDetailsAsync(string userId)
        {
            using var conn = CreateConnection();

            var sql = @"
                SELECT 
                    U.USER_ID       AS UserId,
                    U.USER_NM       AS UserName,
                    U.USER_EMAIL    AS UserEmail,
                    U.USER_TEL      AS PhoneNumber,
                    U.USER_STATUS   AS UserStatus,
                    E.VALID         AS EmployeeStatus,
                    U.EMP_ID        AS EmpId,
                    D.NAME          AS Designation,
                    U.FUND_CD       AS FundCode,
                    U.BR_CD         AS BranchCode,
                    U.LAST_UPDATED_BY   AS LastUpdatedBy,
                    U.LAST_UPDATED_DATE AS LastUpdatedDate
                FROM USER_INFO U
                LEFT JOIN INVEST.EMP_INFO E
                    ON U.EMP_ID = E.EMP_ID
                LEFT JOIN INVEST.EMP_DESIGNATION D
                    ON E.DESIG_ID = D.ID
                WHERE U.USER_ID = :UserId
                AND U.USER_STATUS = 'V'
            ";

            var param = new DynamicParameters();
            param.Add("UserId", userId);

            return await conn.QueryFirstOrDefaultAsync<UserGridDto>(sql, param);
        }


        public async Task<IEnumerable<string>> GetActiveUserIdsAsync()
        {
            using var conn = CreateConnection();

            var sql = @"
                SELECT USER_ID
                FROM USER_INFO
                WHERE USER_STATUS = 'V'
                ORDER BY USER_ID
            ";

            return await conn.QueryAsync<string>(sql);
        }

        public async Task<IEnumerable<ProjectDto>> GetValidProjectsAsync()
        {
            using var conn = CreateConnection(); // your Oracle connection

            var sql = @"
                SELECT ID, NAME
                FROM PROJECTS
                WHERE VALID IS NOT NULL
                ORDER BY ID
            ";

            return await conn.QueryAsync<ProjectDto>(sql);
        }


        public async Task<IEnumerable<ProjectDto>> GetUserProjectsAsync(string userId)
        {
            using var conn = CreateConnection(); // Oracle connection

            var sql = @"
                SELECT 
                    P.ID,
                    P.NAME,
                    P.URL,
                    P.VALID,
                    P.PROJECT_DESC AS Project_Desc,
                    P.DEVELOPED_BY,
                    P.START_DT
                FROM PROJECTS P
                INNER JOIN USER_PROJECT UP
                    ON P.ID = UP.PROJECT_ID
                WHERE UP.USER_ID = :UserId
                AND UP.VALID IS NULL
                ORDER BY P.ID
            ";

            var param = new DynamicParameters();
            param.Add("UserId", userId);


            // a sql query to update USER_PROJECT 
            // var sql = @"
            //     SELECT 
            //         P.ID,
            //         P.NAME,
            //         P.URL,
            //         P.VALID,
            //         P.PROJECT_DESC AS Project_Desc,
            //         P.DEVELOPED_BY,
            //         P.START_DT
            //     FROM PROJECTS P
            //     INNER JOIN USER_PROJECT UP
            //         ON P.ID = UP.PROJECT_ID
            //     WHERE UP.USER_ID = :UserId
            //     AND UP.VALID IS NULL
            //     ORDER BY P.ID
            // ";

            return await conn.QueryAsync<ProjectDto>(sql, param);
        }

        // IsUserProjectValidAsync
        public async Task<bool> IsUserProjectValidAsync(string userId, int projectId)
        {
            using var conn = CreateConnection();

            var sql = @"
                SELECT COUNT(*)
                FROM USER_PROJECT
                WHERE USER_ID = :UserId
                AND PROJECT_ID = :ProjectId
                AND VALID IS NULL
            ";

            var count = await conn.ExecuteScalarAsync<int>(sql, new { UserId = userId, ProjectId = projectId });
            return count > 0;

        }



        public async Task<bool> AddUserProjectAsync(UserProjectCreateRequest request)
        {
            using var conn = CreateConnection();


            //check if project exists
            var projectExists = await conn.QueryFirstOrDefaultAsync<int>(
                "SELECT COUNT(*) FROM PROJECTS WHERE ID = :ProjectId AND VALID IS NOT NULL",
                new { request.ProjectId });

            if (projectExists == 0)
                throw new Exception("Project does not exist.");

            //check if user exists
            var userExists = await conn.QueryFirstOrDefaultAsync<int>(
                "SELECT COUNT(*) FROM USER_INFO WHERE USER_ID = :UserId AND USER_STATUS = 'V'",
                new { request.UserId });
            if (userExists == 0)
            {
                throw new Exception("User does not exist.");
            }


            // Optional: check if record already exists
            var existing = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT USER_ID FROM USER_PROJECT WHERE USER_ID = :UserId AND PROJECT_ID = :ProjectId",
                new { request.UserId, request.ProjectId });

            if (!string.IsNullOrEmpty(existing))
            {

                //change to reactivate if previously revoked
                var updated = await conn.ExecuteAsync(
                    "UPDATE USER_PROJECT SET VALID = NULL, REMARKS = :Remarks WHERE USER_ID = :UserId AND PROJECT_ID = :ProjectId",
                    new { Remarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks, request.UserId, request.ProjectId });
                return updated > 0;
            }




            var maxId = await DbQuery.GetMaxIdAsync(conn, "USER_PROJECT", "ID");


            // Insert query
            var sql = @"
                INSERT INTO USER_PROJECT
                (
                    ID,
                    PROJECT_ID,
                    USER_ID,
                    VALID,
                    REMARKS
                )
                VALUES
                (
                    :Id,
                    :ProjectId,
                    :UserId,
                    NULL,
                    :Remarks
                )
            ";

            var parameters = new DynamicParameters();
            parameters.Add("Id", maxId + 1);
            parameters.Add("ProjectId", request.ProjectId);
            parameters.Add("UserId", request.UserId);
            parameters.Add("Remarks", string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks);

            var rows = await conn.ExecuteAsync(sql, parameters);

            return rows > 0;
        }


        /* public async Task<object> AssignUserFundAsync(AssignUserFundRequestDto request)
         {
             using var conn = CreateConnection();

             if (request.Funds == null || request.Funds.Count == 0)
                 throw new Exception("No fund selected.");

             var insertedFunds = new List<string>();
             var updatedFunds = new List<string>();

             foreach (var fund in request.Funds)
             {
                 // ✅ USER EXISTS CHECK
                 var userExists = await conn.QueryFirstOrDefaultAsync<int>(
                     @"SELECT COUNT(*)
               FROM USER_INFO
               WHERE USER_ID = :UserId
               AND USER_STATUS = 'V'",
                     new { UserId = request.UserId }
                 );

                 if (userExists == 0)
                     throw new Exception("User does not exist.");

                 // ✅ FUND EXISTS CHECK
                 var fundExists = await conn.QueryFirstOrDefaultAsync<int>(
                     @"SELECT COUNT(*)
               FROM FUND_INFO
               WHERE FUND_CD = :FundCode",
                     new { FundCode = fund }
                 );

                 if (fundExists == 0)
                     throw new Exception($"Fund does not exist: {fund}");

                 // ✅ CHECK EXISTING USER_FUND
                 var existing = await conn.QueryFirstOrDefaultAsync<int>(
                     @"SELECT COUNT(*)
               FROM USER_FUND
               WHERE USER_ID = :UserId
               AND FUND_CD = :FundCode",
                     new
                     {
                         UserId = request.UserId,
                         FundCode = fund
                     }
                 );

                 // =========================
                 // ✅ UPDATE IF EXISTS
                 // =========================
                 if (existing > 0)
                 {
                     var updateSql = @"
                 UPDATE USER_FUND
                 SET VALID = 'Y'
                 WHERE USER_ID = :UserId
                 AND FUND_CD = :FundCode
             ";

                     await conn.ExecuteAsync(updateSql, new
                     {
                         UserId = request.UserId,
                         FundCode = fund
                     });

                     updatedFunds.Add(fund);

                     Console.WriteLine($"Updated Fund: {fund}");
                 }
                 else
                 {
                     // =========================
                     // ✅ INSERT IF NOT EXISTS
                     // =========================
                     var maxId = await DbQuery.GetMaxIdAsync(conn, "USER_FUND", "ID");

                     var insertSql = @"
                 INSERT INTO USER_FUND
                 (
                     ID,
                     USER_ID,
                     FUND_CD,
                     VALID,
                     REMARKS
                 )
                 VALUES
                 (
                     :Id,
                     :UserId,
                     :FundCode,
                     'Y',
                     NULL
                 )
             ";

                     var parameters = new DynamicParameters();
                     parameters.Add("Id", maxId + 1);
                     parameters.Add("UserId", request.UserId);
                     parameters.Add("FundCode", fund);

                     await conn.ExecuteAsync(insertSql, parameters);

                     insertedFunds.Add(fund);

                     Console.WriteLine($"Inserted Fund: {fund}");
                 }
             }

             return new
             {
                 success = true,
                 insertedCount = insertedFunds.Count,
                 updatedCount = updatedFunds.Count,
                 insertedFunds,
                 updatedFunds,
                 message = "Fund assignment completed successfully"
             };
         }*/

        public async Task<object> AssignUserFundAsync(AssignUserFundRequestDto request)
        {
            using var conn = CreateConnection();

            if (request.Funds == null || request.Funds.Count == 0)
                throw new Exception("No fund selected.");

            // ✅ USER CHECK
            var userExists = await conn.QueryFirstOrDefaultAsync<int>(
                @"SELECT COUNT(*)
          FROM USER_INFO
          WHERE USER_ID = :UserId
          AND USER_STATUS = 'V'",
                new { UserId = request.UserId }
            );

            if (userExists == 0)
                throw new Exception("User does not exist.");

            // ======================================
            // ✅ FIRST DELETE OLD FUNDS
            // ======================================
            var deleteSql = @"
        DELETE FROM USER_FUND
        WHERE USER_ID = :UserId
    ";

            await conn.ExecuteAsync(deleteSql, new
            {
                UserId = request.UserId
            });

            // ======================================
            // ✅ INSERT NEW SELECTED FUNDS
            // ======================================
            var insertedFunds = new List<string>();

            foreach (var fund in request.Funds)
            {
                // ✅ FUND CHECK
                var fundExists = await conn.QueryFirstOrDefaultAsync<int>(
                    @"SELECT COUNT(*)
              FROM FUND_INFO
              WHERE FUND_CD = :FundCode",
                    new { FundCode = fund }
                );

                if (fundExists == 0)
                    throw new Exception($"Fund does not exist: {fund}");

                var maxId = await DbQuery.GetMaxIdAsync(conn, "USER_FUND", "ID");

                var insertSql = @"
                    INSERT INTO USER_FUND
                    (
                        ID,
                        USER_ID,
                        FUND_CD,
                        VALID,
                        REMARKS
                    )
                    VALUES
                    (
                        :Id,
                        :UserId,
                        :FundCode,
                        'Y',
                        NULL
                    )
                ";

                var parameters = new DynamicParameters();

                parameters.Add("Id", maxId + 1);
                parameters.Add("UserId", request.UserId);
                parameters.Add("FundCode", fund);

                await conn.ExecuteAsync(insertSql, parameters);

                insertedFunds.Add(fund);

                Console.WriteLine($"Inserted Fund: {fund}");
            }

            return new
            {
                success = true,
                insertedCount = insertedFunds.Count,
                insertedFunds,
                message = "Fund assignment updated successfully"
            };
        }

        /*public async Task<object> AssignUserBranchAsync(AssignUserBranchRequestDto request)
        {
            using var conn = CreateConnection();

            if (request.Branches == null || request.Branches.Count == 0)
                throw new Exception("No branch selected.");

            var insertedBranches = new List<string>();
            var updatedBranches = new List<string>();

            foreach (var branch in request.Branches)
            {
                // ✅ USER CHECK
                var userExists = await conn.QueryFirstOrDefaultAsync<int>(
                    @"SELECT COUNT(*) 
              FROM USER_INFO 
              WHERE USER_ID = :UserId 
              AND USER_STATUS = 'V'",
                    new { UserId = request.UserId }
                );

                if (userExists == 0)
                    throw new Exception("User does not exist.");

                // ✅ BRANCH CHECK
                var branchExists = await conn.QueryFirstOrDefaultAsync<int>(
                    @"SELECT COUNT(*) 
              FROM BRANCH_INFO 
              WHERE BR_CD = :BranchCode",
                    new { BranchCode = branch }
                );

                if (branchExists == 0)
                    throw new Exception($"Branch does not exist: {branch}");

                // ✅ CHECK EXISTING USER_BRANCH
                var existing = await conn.QueryFirstOrDefaultAsync<int>(
                    @"SELECT COUNT(*) 
              FROM USER_BRANCH 
              WHERE USER_ID = :UserId 
              AND BR_CD = :BranchCode",
                    new
                    {
                        UserId = request.UserId,
                        BranchCode = branch
                    }
                );

                // =========================
                // ✅ UPDATE IF EXISTS
                // =========================
                if (existing > 0)
                {
                    var updateSql = @"
                UPDATE USER_BRANCH
                SET VALID = 'Y'
                WHERE USER_ID = :UserId
                AND BR_CD = :BranchCode
            ";

                    await conn.ExecuteAsync(updateSql, new
                    {
                        UserId = request.UserId,
                        BranchCode = branch
                    });

                    updatedBranches.Add(branch);

                    Console.WriteLine($"Updated Branch: {branch}");
                }
                else
                {
                    // =========================
                    // ✅ INSERT IF NOT EXISTS
                    // =========================
                    var maxId = await DbQuery.GetMaxIdAsync(conn, "USER_BRANCH", "ID");

                    var insertSql = @"
                INSERT INTO USER_BRANCH
                (
                    ID,
                    USER_ID,
                    BR_CD,
                    VALID,
                    REMARKS
                )
                VALUES
                (
                    :Id,
                    :UserId,
                    :BranchCode,
                    'Y',
                    NULL
                )
            ";

                    var parameters = new DynamicParameters();
                    parameters.Add("Id", maxId + 1);
                    parameters.Add("UserId", request.UserId);
                    parameters.Add("BranchCode", branch);

                    await conn.ExecuteAsync(insertSql, parameters);

                    insertedBranches.Add(branch);

                    Console.WriteLine($"Inserted Branch: {branch}");
                }
            }

            return new
            {
                success = true,
                insertedCount = insertedBranches.Count,
                updatedCount = updatedBranches.Count,
                insertedBranches,
                updatedBranches,
                message = "Branch assignment completed successfully"
            };
        }*/

        public async Task<object> AssignUserBranchAsync(AssignUserBranchRequestDto request)
        {
            using var conn = CreateConnection();

            if (request.Branches == null || request.Branches.Count == 0)
                throw new Exception("No branch selected.");

            // ✅ USER CHECK
            var userExists = await conn.QueryFirstOrDefaultAsync<int>(
                @"SELECT COUNT(*)
          FROM USER_INFO
          WHERE USER_ID = :UserId
          AND USER_STATUS = 'V'",
                new { UserId = request.UserId }
            );

            if (userExists == 0)
                throw new Exception("User does not exist.");

            // ======================================
            // ✅ DELETE OLD BRANCHES
            // ======================================
            var deleteSql = @"
        DELETE FROM USER_BRANCH
        WHERE USER_ID = :UserId
    ";

            await conn.ExecuteAsync(deleteSql, new
            {
                UserId = request.UserId
            });

            // ======================================
            // ✅ INSERT NEW SELECTED BRANCHES
            // ======================================
            var insertedBranches = new List<string>();

            foreach (var branch in request.Branches)
            {
                // ✅ BRANCH CHECK
                var branchExists = await conn.QueryFirstOrDefaultAsync<int>(
                    @"SELECT COUNT(*)
              FROM BRANCH_INFO
              WHERE BR_CD = :BranchCode",
                    new { BranchCode = branch }
                );

                if (branchExists == 0)
                    throw new Exception($"Branch does not exist: {branch}");

                // ✅ GET MAX ID
                var maxId = await DbQuery.GetMaxIdAsync(conn, "USER_BRANCH", "ID");

                // ✅ INSERT
                var insertSql = @"
            INSERT INTO USER_BRANCH
            (
                ID,
                USER_ID,
                BR_CD,
                VALID,
                REMARKS
            )
            VALUES
            (
                :Id,
                :UserId,
                :BranchCode,
                'Y',
                NULL
            )
        ";

                var parameters = new DynamicParameters();

                parameters.Add("Id", maxId + 1);
                parameters.Add("UserId", request.UserId);
                parameters.Add("BranchCode", branch);

                await conn.ExecuteAsync(insertSql, parameters);

                insertedBranches.Add(branch);

                Console.WriteLine($"Inserted Branch: {branch}");
            }

            return new
            {
                success = true,
                insertedCount = insertedBranches.Count,
                insertedBranches,
                message = "Branch assignment updated successfully"
            };
        }
        public async Task<IEnumerable<UserMenuDto>> GetUserMenuAsync(string userId, int projectId)
        {
            using var conn = CreateConnection();

            var sql = @"
                SELECT
                    M.M_ID           AS MenuId,
                    M.M_NAME         AS MenuName,
                    M.M_CAPTION      AS MenuCaption,
                    M.M_PARENT_ID    AS ParentId,
                    M.M_URL          AS MenuUrl,
                    CASE 
                        WHEN UM.M_ID IS NOT NULL THEN 1
                        ELSE 0
                    END AS HasPermission
                FROM MENU M
                LEFT JOIN USER_MENU UM
                    ON M.M_ID = UM.M_ID
                    AND UM.USER_ID = :UserId
                    AND UM.VALID IS NULL
                WHERE M.PROJECT_ID = :ProjectId
                AND M.VALID = 'Y'
                ORDER BY M.M_ID
            ";

            var param = new DynamicParameters();
            param.Add("UserId", userId);
            param.Add("ProjectId", projectId);

            return await conn.QueryAsync<UserMenuDto>(sql, param);
        }

        public async Task<bool> UpdateUserMenuAsync(UserMenuUpdateRequest request)
        {
            using var conn = CreateConnection();
            conn.Open();
            using var tran = conn.BeginTransaction();

            try
            {
                // 1️⃣ Validate user
                var userExists = await conn.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM USER_INFO WHERE USER_ID = :UserId AND USER_STATUS = 'V'",
                    new { request.UserId }, tran);
                if (userExists == 0)
                    throw new Exception("User does not exist.");

                // 2️⃣ Validate project
                var projectExists = await conn.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM PROJECTS WHERE ID = :ProjectId AND VALID IS NOT NULL",
                    new { request.ProjectId }, tran);
                if (projectExists == 0)
                    throw new Exception("Project does not exist.");

                // 3️⃣ Validate user-project access
                var userProjectExists = await conn.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM USER_PROJECT WHERE USER_ID = :UserId AND PROJECT_ID = :ProjectId AND VALID IS NULL",
                    new { request.UserId, request.ProjectId }, tran);
                if (userProjectExists == 0)
                    throw new Exception("User does not have access to this project.");

                // 4️⃣ If no menuIds, deactivate all menus in the project
                if (request.MenuIds == null || !request.MenuIds.Any())
                {
                    await conn.ExecuteAsync(@"
                    UPDATE USER_MENU
                    SET VALID = 'N'
                    WHERE USER_ID = :UserId
                    AND M_ID IN (SELECT M_ID FROM MENU WHERE PROJECT_ID = :ProjectId)
                    AND VALID IS NULL
                ", new { request.UserId, request.ProjectId }, tran);

                    tran.Commit();
                    return true;
                }

                // 5️⃣ Get valid menus for the project
                var validMenuIds = (await conn.QueryAsync<int>(@"
                SELECT M_ID
                FROM MENU
                WHERE PROJECT_ID = :ProjectId
                AND VALID = 'Y'
            ", new { request.ProjectId }, tran)).ToHashSet();

                // 6️⃣ Validate request menus
                foreach (var menuId in request.MenuIds)
                {
                    if (!validMenuIds.Contains(menuId))
                        throw new Exception($"Menu ID {menuId} is not valid for this project.");
                }

                // 7️⃣ Ensure parent menus are active before activating children
                var parentCheck = await conn.QueryAsync<(int ParentId, int ChildId)>(@"
                SELECT DISTINCT P.M_ID AS ParentId, C.M_ID AS ChildId
                FROM MENU C
                JOIN MENU P ON C.M_PARENT_ID = P.M_ID
                LEFT JOIN USER_MENU UM ON UM.USER_ID = :UserId AND UM.M_ID = P.M_ID
                WHERE C.M_ID IN :MenuIds
                AND C.M_PARENT_ID != 0
                AND NVL(UM.VALID, 'A') = 'N'  -- parent inactive in DB
            ", new { request.UserId, request.MenuIds }, tran);

                var invalidParents = parentCheck
                    .Where(x => !request.MenuIds.Contains(x.ParentId)) // parent not being activated
                    .ToList();

                if (invalidParents.Any())
                {
                    throw new Exception(
                        $"Cannot activate child menu(s) [{string.Join(", ", invalidParents.Select(x => x.ChildId))}] " +
                        $"because their parent menu(s) [{string.Join(", ", invalidParents.Select(x => x.ParentId).Distinct())}] are inactive."
                    );
                }



                // 9️⃣ Deactivate removed menus
                await conn.ExecuteAsync(@"
                UPDATE USER_MENU
                SET VALID = 'N'
                WHERE USER_ID = :UserId
                AND M_ID IN (SELECT M_ID FROM UNIT.MENU WHERE PROJECT_ID = :ProjectId)
                AND M_ID NOT IN :MenuIds
                AND VALID IS NULL
            ", new { request.UserId, request.ProjectId, request.MenuIds }, tran);

                // 🔟 Reactivate existing menus
                await conn.ExecuteAsync(@"
                UPDATE USER_MENU
                SET VALID = NULL
                WHERE USER_ID = :UserId
                AND M_ID IN :MenuIds
                AND VALID = 'N'
            ", new { request.UserId, request.MenuIds }, tran);

                // 1️⃣1️⃣ Insert missing menus
                foreach (var menuId in request.MenuIds)
                {
                    var newId = await conn.ExecuteScalarAsync<int>(@"
                    SELECT NVL(MAX(ID),0) + 1 FROM USER_MENU
                ", null, tran);

                    await conn.ExecuteAsync(@"
                    INSERT INTO USER_MENU (ID, USER_ID, M_ID, VALID)
                    SELECT :Id, :UserId, :MenuId, NULL
                    FROM DUAL
                    WHERE NOT EXISTS (
                        SELECT 1 FROM USER_MENU WHERE USER_ID = :UserId AND M_ID = :MenuId
                    )
                ", new { Id = newId, UserId = request.UserId, MenuId = menuId }, tran);
                }

                tran.Commit();
                return true;
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }


        public async Task<IEnumerable<EmployeeDto>> GetActiveEmployeesAsync()
        {
            using var conn = CreateConnection();
            conn.Open();

            var sql = @"
            SELECT
                E.EMP_ID   AS EmpId,
                E.NAME     AS Name,
                D.NAME     AS Designation
            FROM INVEST.EMP_INFO E
            LEFT JOIN INVEST.EMP_DESIGNATION D
                ON E.DESIG_ID = D.ID
            WHERE E.VALID = 'Y'
            ORDER BY E.RANK ASC, E.SENIORITY ASC
        ";

            return await conn.QueryAsync<EmployeeDto>(sql);
        }



        public async Task<bool> UserExistsAsync(string userId)
        {
            using var conn = CreateConnection();
            conn.Open();

            var sql = @"
            SELECT COUNT(*)
            FROM USER_INFO
            WHERE USER_ID = :UserId
        ";

            var count = await conn.ExecuteScalarAsync<int>(sql, new { UserId = userId });

            return count > 0;
        }


        public async Task<bool> RevokeUserProjectAsync(UserProjectRevokeRequest request)
        {
            using var conn = CreateConnection();
            conn.Open();


            // 1️⃣ Check if the user-project record exists and is active
            var exists = await conn.QueryFirstOrDefaultAsync<int>(@"
        SELECT COUNT(*)
        FROM USER_PROJECT
        WHERE USER_ID = :UserId
          AND PROJECT_ID = :ProjectId
          AND VALID IS NULL
    ", new { request.UserId, request.ProjectId });

            if (exists == 0)
                throw new Exception("Active user-project record not found.");

            // 2️⃣ Update VALID to 'N'
            var rows = await conn.ExecuteAsync(@"
        UPDATE USER_PROJECT
        SET VALID = 'N'
        WHERE USER_ID = :UserId
          AND PROJECT_ID = :ProjectId
          AND VALID IS NULL
    ", new { request.UserId, request.ProjectId });

            return rows > 0;
        }


        public async Task<UserDashboardStatsDto> getUserDashboardStatsService()
        {
            using var conn = CreateConnection();

            var sql = @"
        SELECT
            COUNT(*) AS TotalUsers,
            SUM(CASE WHEN USER_STATUS = 'V' THEN 1 ELSE 0 END) AS ActiveUsers,
            SUM(CASE WHEN USER_STATUS IS NULL THEN 1 ELSE 0 END) AS InactiveUsers
        FROM UNIT.USER_INFO
    ";

            return await conn.QuerySingleAsync<UserDashboardStatsDto>(sql);
        }


        public async Task<IEnumerable<ProjectUserCountDto>> GetProjectWiseUserCountAsync()
        {
            using var conn = CreateConnection();

            var sql = @"
        SELECT
            P.ID            AS ProjectId,
            P.NAME          AS ProjectName,
            COUNT(DISTINCT UP.USER_ID) AS UserCount
        FROM UNIT.PROJECTS P
        LEFT JOIN UNIT.USER_PROJECT UP
            ON UP.PROJECT_ID = P.ID
           AND UP.VALID IS NULL
        WHERE P.VALID = 'Y'
        GROUP BY P.ID, P.NAME
        ORDER BY P.NAME
    ";

            return await conn.QueryAsync<ProjectUserCountDto>(sql);
        }

    }
}
