using System.Data;
using Dapper;

namespace NAVCalculationSystem.Helpers
{
	public static class DbQuery
	{
		/// <summary>
		/// Returns MAX(columnName) from a given table.
		/// </summary>
		public static async Task<long> GetMaxIdAsync(
			IDbConnection connection,
			string tableName,
			string columnName,
			IDbTransaction? transaction = null)
		{
			if (string.IsNullOrWhiteSpace(tableName))
				throw new ArgumentException("Table name is required.", nameof(tableName));

			if (string.IsNullOrWhiteSpace(columnName))
				throw new ArgumentException("Column name is required.", nameof(columnName));


			var sql = $"SELECT NVL(MAX({columnName}), 0) FROM {tableName}";

			var result = await connection.ExecuteScalarAsync<long>(
				sql,
				transaction: transaction
			);

			return result;
		}


		public static async Task<string> GetSchemaAsync(
					IDbConnection connection,
					int fundCd,
					IDbTransaction? transaction = null)
		{
			const string sql = @"
                SELECT F_ACC_SCHEMA
                FROM INVEST.FUND_PARA
                WHERE F_CD = :FundCd
            ";

			var schema = await connection.ExecuteScalarAsync<string>(
				sql,
				new { FundCd = fundCd },
				transaction
			);

			if (string.IsNullOrWhiteSpace(schema))
				throw new Exception($"Account schema not found for FUND_CD: {fundCd}");

			return schema;
		}


		   public static async Task<string> GetNextAccountVoucherNoAsync(
            IDbConnection connection,
            string accountSchema,
            string voucherTypeNo,
            IDbTransaction? transaction = null)
        {
            if (string.IsNullOrWhiteSpace(accountSchema))
                throw new ArgumentException("Account schema is required.", nameof(accountSchema));

            if (string.IsNullOrWhiteSpace(voucherTypeNo))
                throw new ArgumentException("Voucher type is required.", nameof(voucherTypeNo));

            // 1️⃣ Get MAX CTRLNO for the given voucher type
            var sqlMaxCtrlNo = $@"
                SELECT MAX(TO_NUMBER(CTRLNO)) 
                FROM {accountSchema}.GL_TRAN
                WHERE VOUCHER_TYPE = :VoucherTypeNo
            ";

            var maxCtrlNo = await connection.ExecuteScalarAsync<long?>(
                sqlMaxCtrlNo,
                new { VoucherTypeNo = voucherTypeNo },
                transaction
            ) ?? 0;

            // 2️⃣ Get the current voucher number (VOUCHER_NO) for the MAX CTRLNO
            if (maxCtrlNo > 0)
            {
                var sqlVoucherNo = $@"
                    SELECT VOUCHER_NO 
                    FROM {accountSchema}.GL_TRAN
                    WHERE CTRLNO = :MaxCtrlNo
                ";

                var voucherNoStr = await connection.ExecuteScalarAsync<string>(
                    sqlVoucherNo,
                    new { MaxCtrlNo = maxCtrlNo },
                    transaction
                );

                if (!string.IsNullOrWhiteSpace(voucherNoStr) && int.TryParse(voucherNoStr, out int voucherNo))
                {
                    return (voucherNo + 1).ToString();
                }
            }

            // 3️⃣ If no records found, start from 1
            return "1";
        }

        
	}
}
