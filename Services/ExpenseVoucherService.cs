using System.Data;
using Oracle.ManagedDataAccess.Client;
using Dapper;
using NAVCalculationSystem.Models;
using NAVCalculationSystem.Helpers;
using NAVCalculationSystem.DTOs;


public class ExpenseVoucherService 
{
     private readonly IConfiguration _configuration;

    public ExpenseVoucherService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

	private IDbConnection CreateConnection()
	{
		return new OracleConnection(_configuration.GetConnectionString("InvestConnection"));
	}

		private async Task<(long TranId, long CtrlNo)> InsertExpenseGlTranAsync(
		IDbConnection connection,
		IDbTransaction transaction,
		string accountSchema,
		long tranNumber,
		long ctrlNumber,
		string accCode,
		string tranType,
		decimal amount,
		DateTime navDate,
		string expenseTypeName,
		string voucherNo,
		string voucherEntryBy,
		int noOfDays)
	{
		string bankAccNo = tranType == "D" ? accCode : " ";
		string bankAccContra = tranType == "C" ? accCode : " ";

		string sql = $@"
			INSERT INTO {accountSchema}.GL_TRAN
			(
				TRAN_ID,
				ACCCODE,
				BANKACNO,
				BANKACNO_CONTRA,
				TRAN_TIME,
				TRAN_DATE,
				REMARKS,
				TRAN_TYPE,
				VOUCHER_NO,
				TOTAL_AMNT,
				TERMINAL_NO,
				CTRLNO,
				OP_ID,
				VOUCHER_TYPE,
				RECENT,
				LATESTDEL,
				ISOUT,
				ISREV,
				OLDDATA
			)
			VALUES
			(
				:TranId,
				:AccCode,
				:BankAccNo,
				:BankAccContra,
				:TranTime,
				:TranDate,
				:Remarks,
				:TranType,
				:VoucherNo,
				:TotalAmnt,
				:TerminalNo,
				:CtrlNo,
				:OpId,
				:VoucherType,
				:Recent,
				:LatestDel,
				:IsOut,
				:IsRev,
				:OldData
			)";

		await connection.ExecuteAsync(sql, new
		{
			TranId = tranNumber,
			AccCode = accCode,
			BankAccNo = bankAccNo,
			BankAccContra = bankAccContra,
			TranTime = DateTime.Now.ToString("HH:mm:ss"),
			TranDate = navDate.ToString("dd-MMM-yyyy"),
			Remarks = $"{expenseTypeName} Expense Accrued For {noOfDays} Days",
			TranType = tranType,
			VoucherNo = voucherNo,
			TotalAmnt = amount,
			TerminalNo = 10,
			CtrlNo = ctrlNumber,
			OpId = voucherEntryBy,
			VoucherType = "14",
			Recent = "Y",
			LatestDel = "M",
			IsOut = "N",
			IsRev = "N",
			OldData = "N"
		}, transaction);

		return (tranNumber, ctrlNumber);
	}

	private (string DebitCode, string CreditCode) GetExpenseAccountCodes(int expenseTypeId)
	{
		return expenseTypeId switch
		{
			1 => ("402090000", "103020000"), // Management Fee
			2 => ("402110000", "103040000"), // Custodian Fee
			3 => ("402100000", "103030000"), // Trustee Fee

			_ => throw new Exception($"Account code not configured for ExpenseTypeId = {expenseTypeId}")
		};
	}

	public async Task<string> SaveExpensePayableVoucherAsync(
    IDbConnection connection,
    IDbTransaction transaction,
    int fundCd,
    int expenseTypeId,
    string expenseTypeName,
    decimal amount,
    DateTime navDate,
    int noOfDays,
    string voucherEntryBy)
	{
		string accountSchema =
			await DbQuery.GetSchemaAsync(connection, fundCd, transaction);

		long tranNumber =
			await DbQuery.GetMaxIdAsync(
				connection,
				$"{accountSchema}.GL_BASICINFO",
				"TRAN_ID",
				transaction) + 1;

		long ctrlNumber =
			await DbQuery.GetMaxIdAsync(
				connection,
				$"{accountSchema}.GL_BASICINFO",
				"TO_NUMBER(CTRLNO)",
				transaction) - 1;

		string voucherNo =
			await DbQuery.GetNextAccountVoucherNoAsync(
				connection,
				accountSchema,
				"14",
				transaction);

		var (debitCode, creditCode) =
			GetExpenseAccountCodes(expenseTypeId);

		// Debit Expense Head
		ctrlNumber++;

		(tranNumber, ctrlNumber) =
			await InsertExpenseGlTranAsync(
				connection,
				transaction,
				accountSchema,
				tranNumber,
				ctrlNumber,
				debitCode,
				"D",
				amount,
				navDate,
				expenseTypeName,
				voucherNo,
				voucherEntryBy,
				noOfDays);

		// Credit Payable Head
		ctrlNumber++;

		(tranNumber, ctrlNumber) =
			await InsertExpenseGlTranAsync(
				connection,
				transaction,
				accountSchema,
				tranNumber,
				ctrlNumber,
				creditCode,
				"C",
				amount,
				navDate,
				expenseTypeName,
				voucherNo,
				voucherEntryBy,
				noOfDays);

		ctrlNumber++;

		await connection.ExecuteAsync(
			$@"UPDATE {accountSchema}.GL_BASICINFO
			SET TRAN_ID = :TranId,
				CTRLNO  = :CtrlNo",
			new
			{
				TranId = tranNumber,
				CtrlNo = ctrlNumber
			},
			transaction);

		return voucherNo;
	}


   
}