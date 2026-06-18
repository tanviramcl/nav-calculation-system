using System.Data;
using Oracle.ManagedDataAccess.Client;
using Dapper;
using NAVCalculationSystem.DTOs;
using Microsoft.Extensions.Configuration;

namespace NAVCalculationSystem.Services
{
    public class PayableService
    {
        private readonly IConfiguration _configuration;
        

        public PayableService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private IDbConnection CreateConnection()
        {
            return new OracleConnection(
                _configuration.GetConnectionString("DefaultConnection"));
        }

        public async Task<IEnumerable<ManagementFeeDto>> GetAllManagmentFeeListAsync(
            int expenseTypeId,
            DateTime navDate,
            int days)
        {
            using var conn = CreateConnection();

            var sql = @"
                SELECT
                    f.F_CD AS FundCode,
                    f.F_NAME AS FundName,
                    rm.Portfolio_Market_Value AS PortfolioMarketValue,
                    rm.ANNUAL_RATE AS AnnualRate,
                    ROUND(
                        rm.Portfolio_Market_Value * rm.ANNUAL_RATE / 365,
                        4
                    ) AS DailyFee,
                    :Days AS NAVDays,
                    ROUND(
                        rm.Portfolio_Market_Value * rm.ANNUAL_RATE / 365 * :Days,
                        4
                    ) AS AccruedMfee
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
                            NAVFUNDID AS F_CD,
                            NAVTOTALMARKETPRICE AS Portfolio_Market_Value
                        FROM NAV.NAV_MASTER
                        WHERE NAVDATE = :NavDate
                    ) fn
                    INNER JOIN NAV.MFEE_RATE_CONFIG r
                        ON r.IS_ACTIVE = 'Y'
                        AND (r.F_CD = fn.F_CD OR r.F_CD IS NULL)
                        AND fn.Portfolio_Market_Value >= r.SLAB_FROM
                        AND (
                            r.SLAB_TO IS NULL
                            OR fn.Portfolio_Market_Value <= r.SLAB_TO
                        )
                ) rm
                INNER JOIN INVEST.FUND f
                    ON rm.F_CD = f.F_CD
                WHERE rm.RN = 1
                ORDER BY f.F_CD";

            // Console.WriteLine(
            //     $"Executing SQL for GetAllManagmentFeeListAsync with ExpenseTypeId={expenseTypeId}, NavDate={navDate:yyyy-MM-dd}, Days={days},sql={sql}"
            // );



            var result = await conn.QueryAsync<ManagementFeeDto>(
                sql,
                new
                {
                    ExpenseTypeId = expenseTypeId,
                    NavDate = navDate,
                    Days = days
                });

            return result;
        }

        public async Task<IEnumerable<CustodianFeeDto>> GetAllCustodianFeeListAsync(
    int expenseTypeId,
    DateTime navDate,
    int days)
        {
            using var conn = CreateConnection();

            var sql = @"
            SELECT
                F.F_CD AS FundCode,
                F.F_NAME AS FundName,

                NVL(LV.PORTFOLIO_LISTED_MARKET_VALUE,0)
                    AS PortfolioListedMarketValue,

                NVL(NL.TOTAL_NONLISTED_MARKET_VALUE,0)
                    AS PfolioNlistedMarketValue,

                NVL(FD.TOTAL_FDR_AMOUNT,0)
                    AS TotalFdrAmount,

                NVL(LV.PORTFOLIO_LISTED_MARKET_VALUE,0)
                    + NVL(NL.TOTAL_NONLISTED_MARKET_VALUE,0)
                    + NVL(FD.TOTAL_FDR_AMOUNT,0)
                    AS PortfolioMarketValue,

                CASE F.F_CD
                    WHEN 15 THEN 0.075
                    WHEN 16 THEN 0.075
                    WHEN 32 THEN 0.070
                    ELSE 0.100
                END AS AnnualRate,

                ROUND(
                    (
                        (
                            NVL(LV.PORTFOLIO_LISTED_MARKET_VALUE,0)
                            + NVL(NL.TOTAL_NONLISTED_MARKET_VALUE,0)
                            + NVL(FD.TOTAL_FDR_AMOUNT,0)
                        )
                        *
                        CASE F.F_CD
                            WHEN 15 THEN 0.075
                            WHEN 16 THEN 0.075
                            WHEN 32 THEN 0.070
                            ELSE 0.100
                        END
                        / 100
                    )
                    * :Days / 365,
                8) AS DailyCustodianFee,

                :Days AS NAVDays,

                ROUND(
                    (
                        (
                            NVL(LV.PORTFOLIO_LISTED_MARKET_VALUE,0)
                            + NVL(NL.TOTAL_NONLISTED_MARKET_VALUE,0)
                            + NVL(FD.TOTAL_FDR_AMOUNT,0)
                        )
                        *
                        CASE F.F_CD
                            WHEN 15 THEN 0.075
                            WHEN 16 THEN 0.075
                            WHEN 32 THEN 0.070
                            ELSE 0.100
                        END
                        / 100
                    )
                    * :Days / 365,
                8) * :Days AS AccrueCustodianFe

            FROM NAV.FUND F

            LEFT JOIN
            (
                SELECT
                    NAVFUNDID AS F_CD,
                    SUM(NAVTOTALMARKETPRICE) AS PORTFOLIO_LISTED_MARKET_VALUE
                FROM NAV.NAV_MASTER
                WHERE NAVDATE = :NavDate
                GROUP BY NAVFUNDID
            ) LV
                ON F.F_CD = LV.F_CD

            LEFT JOIN
            (
                SELECT
                    Q.F_CD,
                    SUM(Q.TOT_MARKET_PRICE) AS TOTAL_NONLISTED_MARKET_VALUE
                FROM
                (
                    SELECT
                        D.F_CD,
                        ROUND(D.NO_SHARES * MP.MARKET_RATE,8) AS TOT_MARKET_PRICE
                    FROM
                    (
                        SELECT
                            A.F_CD,
                            A.COMP_CD,
                            A.NO_SHARES,
                            B.TRAN_DATE
                        FROM
                        (
                            SELECT
                                A.F_CD,
                                A.COMP_CD,
                                SUM(DECODE(TRAN_TP,'B',NO_SHARES,'S',-NO_SHARES)) NO_SHARES
                            FROM INVEST.NON_LISTED_SECURITIES_DETAILS A
                            WHERE A.INV_DATE <= :NavDate
                            GROUP BY A.F_CD,A.COMP_CD
                        ) A
                        INNER JOIN
                        (
                            SELECT
                                COMP_CD,
                                MAX(TRAN_DATE) TRAN_DATE
                            FROM INVEST.NONLISTED_MARKET_PRICE
                            WHERE TRAN_DATE <= :NavDate
                            GROUP BY COMP_CD
                        ) B
                            ON A.COMP_CD = B.COMP_CD
                    ) D
                    INNER JOIN INVEST.NONLISTED_MARKET_PRICE MP
                        ON D.COMP_CD = MP.COMP_CD
                        AND D.TRAN_DATE = MP.TRAN_DATE
                ) Q
                GROUP BY Q.F_CD
            ) NL
                ON F.F_CD = NL.F_CD

            LEFT JOIN
            (
                SELECT
                    A.FUND_CD AS F_CD,
                    SUM(A.FDR_AMOUNT) AS TOTAL_FDR_AMOUNT
                FROM INVEST.FDR_INFO A
                WHERE A.VALID IS NULL
                AND A.FDR_OPEN_DT <= :NavDate
                AND A.FDR_MATUR_DT > :NavDate
                GROUP BY A.FUND_CD
            ) FD
                ON F.F_CD = FD.F_CD

            WHERE F.VALID = 'Y'
            AND NVL(LV.PORTFOLIO_LISTED_MARKET_VALUE,0) > 0
            ORDER BY F.F_CD";

            Console.WriteLine(
                $"Executing SQL for GetAllCustodianFeeListAsync " + sql +
                $"ExpenseTypeId={expenseTypeId}, " +
                $"NavDate={navDate:yyyy-MM-dd}, Days={days}"
            );

            return await conn.QueryAsync<CustodianFeeDto>(
                sql,
                new
                {
                    ExpenseTypeId = expenseTypeId,
                    NavDate = navDate,
                    Days = days
                });
        }

        public async Task<IEnumerable<TrusteeFeeDto>> GetAllTrusteeFeeListAsync(
            int expenseTypeId,
            DateTime navDate,
            int days)
        {
            using var conn = CreateConnection();

            var sql = @"SELECT
                F.F_CD AS FundCode,
                F.F_NAME AS FundName,

                NVL(LV.PORTFOLIO_LISTED_MARKET_VALUE, 0) AS PortfolioMarketValue,
                0.10 AS AnnualRate,

                ROUND(
                    (
                        NVL(LV.PORTFOLIO_LISTED_MARKET_VALUE,0)
                        * 0.10 / 100
                    )
                    * :Days / 365,
                8) AS DailyTrusteeFee,

                :Days AS NAVDays,

                ROUND(
                    (
                        NVL(LV.PORTFOLIO_LISTED_MARKET_VALUE,0)
                        * 0.10 / 100
                        * :Days / 365
                    ),
                8) * :Days AS AccrueTrusteeFee


               

            FROM NAV.FUND F

            LEFT JOIN
            (
                SELECT
                    NAVFUNDID AS F_CD,
                    SUM(NAVTOTALMARKETPRICE) AS PORTFOLIO_LISTED_MARKET_VALUE
                FROM NAV.NAV_MASTER
                WHERE NAVDATE = :NavDate
                GROUP BY NAVFUNDID
            ) LV
                ON F.F_CD = LV.F_CD

            WHERE F.VALID = 'Y'
            AND NVL(LV.PORTFOLIO_LISTED_MARKET_VALUE,0) > 0
            ORDER BY F.F_CD";

            Console.WriteLine(sql);

            return await conn.QueryAsync<TrusteeFeeDto>(
                sql,
                new
                {
                    ExpenseTypeId = expenseTypeId,
                    NavDate = navDate,
                    Days = days
                });
        }

        public async Task<IEnumerable<ExpenseTypeDto>> GetAllExpenseTypesAsync()
        {
            using var conn = CreateConnection();

            var sql = @"
                SELECT
                    EXPENSE_TYPE_ID   AS ExpenseTypeId,
                    EXPENSE_TYPE_NAME AS ExpenseTypeName,
                    IS_ACTIVE         AS IsActive,
                    CREATED_DATE      AS CreatedDate
                FROM NAV.EXPENSE_TYPE
                ORDER BY EXPENSE_TYPE_ID";

            return await conn.QueryAsync<ExpenseTypeDto>(sql);
        }

        public async Task<SaveExpensePayableResult> SaveExpensePayableAsync(
            IEnumerable<ExpensePayableDto> payableList,
            string entryBy)
        {
            using var connection = CreateConnection();

            connection.Open();

            using var transaction = connection.BeginTransaction();

            var skippedRecords = new List<ExpensePayableDto>();

            int insertedCount = 0;

            try
            {
                var maxId = await connection.ExecuteScalarAsync<long>(
                    @"SELECT NVL(MAX(ID),0)
                    FROM EXPENSE_ACCRUAL_DETAILS",
                    transaction: transaction);

                long currentId = maxId;

                var insertSql = @"
                        INSERT INTO EXPENSE_ACCRUAL_DETAILS
                        (
                            ID,
                            NAV_DATE,
                            FUND_CD,
                            EXPENSE_TYPE_ID,
                            PORTFOLIO_MARKET_VALUE,
                            ANNUAL_RATE,
                            DAILY_FEE,
                            NAV_DAYS,
                            ACCRUED_FEE,
                            PAY_CREATED_BY,
                            PAY_CREATED_DATE
                        )
                        VALUES
                        (
                            :ID,
                            :NAV_DATE,
                            :FUND_CD,
                            :EXPENSE_TYPE_ID,
                            :PORTFOLIO_MARKET_VALUE,
                            :ANNUAL_RATE,
                            :DAILY_FEE,
                            :NAV_DAYS,
                            :ACCRUED_FEE,
                            :PAY_CREATED_BY,
                            SYSDATE
                        )";

                foreach (var r in payableList)
                {
                    var savePoint =
                        $"SP_PAY_{r.FUND_CD}_{r.EXPENSE_TYPE_ID}_{r.NAV_DATE:yyyyMMdd}";

                    await connection.ExecuteAsync(
                        $"SAVEPOINT {savePoint}",
                        transaction: transaction);

                    try
                    {
                        // Duplicate Check
                        var exists = await connection.ExecuteScalarAsync<int>(
                            @"
                                SELECT COUNT(1)
                                FROM EXPENSE_ACCRUAL_DETAILS
                                WHERE FUND_CD = :FUND_CD
                                AND EXPENSE_TYPE_ID = :EXPENSE_TYPE_ID
                                AND TRUNC(NAV_DATE) = TRUNC(:NAV_DATE)",
                            new
                            {
                                FUND_CD = r.FUND_CD,
                                EXPENSE_TYPE_ID = r.EXPENSE_TYPE_ID,
                                NAV_DATE = r.NAV_DATE
                            },
                            transaction);

                        if (exists > 0)
                        {
                            r.skipReason =
                                "Expense payable already exists.";

                            skippedRecords.Add(r);

                            continue;
                        }

                        // Validation
                        if (r.ACCRUED_FEE <= 0)
                        {
                            r.skipReason =
                                "Accrued fee must be greater than zero.";

                            skippedRecords.Add(r);

                            continue;
                        }

                        currentId++;

                        await connection.ExecuteAsync(
                            insertSql,
                            new
                            {
                                ID = currentId,
                                NAV_DATE = r.NAV_DATE,
                                FUND_CD = r.FUND_CD,
                                EXPENSE_TYPE_ID = r.EXPENSE_TYPE_ID,
                                PORTFOLIO_MARKET_VALUE = r.PORTFOLIO_MARKET_VALUE,
                                ANNUAL_RATE = r.ANNUAL_RATE,
                                DAILY_FEE = r.DAILY_FEE,
                                NAV_DAYS = r.NAV_DAYS,
                                ACCRUED_FEE = r.ACCRUED_FEE,
                                PAY_CREATED_BY = entryBy
                            },
                            transaction);

                        insertedCount++;
                    }
                    catch (Exception ex)
                    {
                        await connection.ExecuteAsync(
                            $"ROLLBACK TO SAVEPOINT {savePoint}",
                            transaction: transaction);

                        r.skipReason = $"Error: {ex.Message}";

                        skippedRecords.Add(r);
                    }
                }

                transaction.Commit();

                return new SaveExpensePayableResult
                {
                    InsertedCount = insertedCount,
                    SkippedRecords = skippedRecords
                };
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }
    public class SaveExpensePayableResult
    {
        public int InsertedCount { get; set; }

        public List<ExpensePayableDto> SkippedRecords { get; set; }
            = new();
    }
}