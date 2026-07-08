using System.Data;
using Oracle.ManagedDataAccess.Client;
using Dapper;
using NAVCalculationSystem.DTOs;
using Microsoft.Extensions.Configuration;

namespace NAVCalculationSystem.Services
{
    public class NavCalService
    {
        private readonly IConfiguration _configuration;

        public NavCalService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private IDbConnection CreateConnection()
        {
            return new OracleConnection(
                _configuration.GetConnectionString("DefaultConnection"));
        }

        public async Task<List<NavProcessDto>> GetFundsAsync()
        {
            using var conn = CreateConnection();

            const string sql = @"
        SELECT
            F.F_CD,
            F.F_NAME,
            F.F_TYPE,
            MAX(N.NAVNO) AS LAST_NAVNO,
            MAX(N.NAVDATE)
                KEEP (DENSE_RANK LAST ORDER BY N.NAVNO) AS LAST_NAVDATE,
            NVL(MAX(N.NAVNO), 0) + 1 AS NAVNO
        FROM INVEST.FUND F
        LEFT JOIN NAV.NAV_MASTER N
            ON N.NAVFUNDID = F.F_CD
        WHERE F.IS_NAV_ENABLED = 'Y'
        GROUP BY
            F.F_CD,
            F.F_NAME,
            F.F_TYPE
        ORDER BY
            F.F_CD";

            var result = await conn.QueryAsync<NavProcessDto>(sql);

            return result.ToList();
        }
        public async Task<List<PriceRefixDto>> GetLatestPriceRefixAsync()
        {
            using var conn = CreateConnection();

            const string sql = @"
                 SELECT
                    P.FUND_CD,
                    P.NAV_DATE,
                    P.REFIX_DT,
                    P.EFFECTIVE_DATE,
                    TO_CHAR(P.REFIX_DT,'DD-MON-YYYY') AS REFIX_DATE,
                    TO_CHAR(P.EFFECTIVE_DATE,'DD-MON-YYYY') AS EFFECTIVE_DT,
                    P.REFIX_SL_PR,
                    P.REFIX_REP_PR
                FROM UNIT.PRICE_REFIX P
                WHERE P.NAV_DATE = (
                    SELECT MAX(X.NAV_DATE)
                    FROM UNIT.PRICE_REFIX X
                    WHERE X.FUND_CD = P.FUND_CD
                )
                ORDER BY P.FUND_CD";

            var result = await conn.QueryAsync<PriceRefixDto>(sql);

            return result.ToList();
        }
    }

    


}