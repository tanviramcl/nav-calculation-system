using System.Data;
using Oracle.ManagedDataAccess.Client;
using Dapper;
using NAVCalculationSystem.Models;
using NAVCalculationSystem.DTOs;
using BCrypt.Net;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Net.Security;
using NAVCalculationSystem.Helpers;

namespace NAVCalculationSystem.Services
{
    public class AuthService
    {
        private readonly IConfiguration _configuration;
        public AuthService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private IDbConnection CreateConnection()
        {
            return new OracleConnection(_configuration.GetConnectionString("DefaultConnection"));
        }

        public async Task<string> RegisterAsync(RegisterDto model)
        {
            using var conn = CreateConnection();

            // 1. Check duplicate USER_ID
            var existingUser = await conn.QueryFirstOrDefaultAsync<UserInfo>(
                "SELECT USER_ID FROM USER_INFO WHERE USER_ID = :UserId",
                new { UserId = model.UserId });

            if (existingUser != null)
                throw new Exception("Duplicate User ID found.");

            // 2. Default values
            string fundCd = string.IsNullOrWhiteSpace(model.FundCd) ? "IAMCL" : model.FundCd;
            string brCd = string.IsNullOrWhiteSpace(model.BrCd) ? "AMC/01" : model.BrCd;
            string encryptedPassword = AESEncryption.Encrypt(model.Password);

            // 3. Insert user
            var sql = @"
                INSERT INTO USER_INFO
                (
                    FUND_CD,
                    BR_CD,
                    USER_ID,
                    USER_PASS,
                    USER_NM,
                    USER_ADDR,
                    USER_STATUS,
                    ENT_DT,
                    PASS_CHANGE_DATE,
                    CREATED_DATE
                )
                VALUES
                (
                    :FundCd,
                    :BrCd,
                    :UserId,
                    :Password,
                    :UserName,
                    :UserAddress,
                    'V',
                    SYSDATE,
                    ADD_MONTHS(SYSDATE, 3),
                    SYSDATE
                )";

            await conn.ExecuteAsync(sql, new
            {
                FundCd = fundCd,
                BrCd = brCd,
                UserId = model.UserId,
                Password = encryptedPassword,
                UserName = model.UserName,
                UserAddress = model.UserAddress
            });

            return "User registered successfully.";
        }


        public async Task<string> LoginAsync(LoginDto model)
        {
            using var conn = CreateConnection();

            var user = await conn.QueryFirstOrDefaultAsync<UserInfo>(
                @"SELECT 
                        USER_ID,
                        USER_PASS
                FROM USER_INFO
                WHERE TRIM(USER_ID) = TRIM(:UserId)
                    AND UPPER(USER_STATUS) = 'V'",
                new { UserId = model.UserId });

            if (user == null)
                throw new Exception("User not found");


            string encryptedInput = AESEncryption.Encrypt(model.Password);


            if (encryptedInput != user.USER_PASS)
            {
                throw new Exception("Invalid credentials");
            }

            await SaveLoginHistoryAsync(user.USER_ID, model.ProjectId);

            return GenerateJwtToken(user.USER_ID);
        }


        public async Task<int> SaveLoginHistoryAsync(string userId, int projectId)
        {
            using var conn = CreateConnection();

            // 1. Get new ID
            var newId = await conn.ExecuteScalarAsync<int>("SELECT NVL(MAX(ID), 0) + 1 FROM LOGINHISTORY");

            // 2. Close last active session for the same user
            var closeQuery = @"
        UPDATE LOGINHISTORY 
        SET ISONLINE = 0,
            LOGOUTTIME = SYSDATE
        WHERE ID IN (
            SELECT ID FROM (
                SELECT ID 
                FROM LOGINHISTORY
                WHERE USER_ID = :UserId AND ISONLINE = 1
                ORDER BY LOGINTIME DESC
            ) WHERE ROWNUM = 1
        )";

            await conn.ExecuteAsync(closeQuery, new { UserId = userId });

            // 3. Insert new login entry with projectId
            var insertQuery = @"
        INSERT INTO LOGINHISTORY 
        (ID, USER_ID, LOGINTIME, ISONLINE, LOGIN_BR, LOGIN_BK, PROJECT_ID)
        VALUES (:ID, :UserId, SYSDATE, 1, 'AMC/01', 'IAMCL', :ProjectId)";

            await conn.ExecuteAsync(insertQuery, new
            {
                ID = newId,
                UserId = userId,
                ProjectId = projectId
            });

            return newId;
        }



        public async Task<bool> IsUserPassUptoDate(string userId)
        {
            using var conn = CreateConnection();

            // If PASS_CHANGE_DATE is NULL, return false immediately
            var checkPassChangeDate = await conn.ExecuteScalarAsync<int>(@"SELECT COUNT(*) FROM USER_INFO WHERE USER_ID = :UserId AND PASS_CHANGE_DATE IS NULL", new { UserId = userId });
            
            if (checkPassChangeDate > 0) // Fixed: was checking == 0
                return false;
            
            var sql = @"
                SELECT COUNT(*) 
                FROM USER_INFO
                WHERE USER_ID = :UserId
                AND (UPPER(USER_STATUS) = 'V' 
                    OR MONTHS_BETWEEN(SYSDATE, PASS_CHANGE_DATE) < 3)";

            var result = await conn.ExecuteScalarAsync<int>(sql, new { UserId = userId });
            return result > 0;
        }


        private string GenerateJwtToken(string userId)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");

            var claims = new[]
            {
                new Claim("userId", userId),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(Convert.ToDouble(jwtSettings["ExpiryMinutes"])),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }


        public async Task BlacklistTokenAsync(string token)
        {
            TokenStore.BlacklistedTokens[token] = true; // thread-safe add
        }

        public async Task UpdateLogoutTimeAsync(string userId, int projectId)
        {
            using (var conn = CreateConnection())
            {
                string closeQuery = @"
                    UPDATE LOGINHISTORY 
                    SET LOGOUTTIME = SYSDATE,
                        ISONLINE = '0'
                    WHERE ID = (
                        SELECT ID FROM (
                            SELECT ID 
                            FROM LOGINHISTORY
                            WHERE USER_ID = :UserId
                            AND PROJECT_ID = :ProjectId
                            AND LOGOUTTIME IS NULL
                            ORDER BY LOGINTIME DESC
                        )
                        WHERE ROWNUM = 1
                    )";

                await conn.ExecuteAsync(
                    closeQuery,
                    new { UserId = userId, ProjectId = projectId }
                );
            }
        }




    }

}
