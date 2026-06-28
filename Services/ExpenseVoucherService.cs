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
	string voucherTypeCode,
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
			VoucherType = voucherTypeCode,
			Recent = "Y",
			LatestDel = "M",
			IsOut = "N",
			IsRev = "N",
			OldData = "N"
		}, transaction);

		return (tranNumber, ctrlNumber);
	}

	private async Task<(string DebitCode, string CreditCode)> GetExpenseAccountCodesAsync(
	IDbConnection connection,
	IDbTransaction transaction,
	int fundCd,
	int expenseTypeId)
	{
		const string sql = @"
        SELECT
            MANAGEMENT_FEE_DEBIT_CODE,
            MANAGEMENT_FEE_CREDIT_CODE,
            CUSTODIAN_FEE_DEBIT_CODE,
            CUSTODIAN_FEE_CREDIT_CODE,
            TRUSTEE_FEE_DEBIT_CODE,
            TRUSTEE_FEE_CREDIT_CODE,
            ANNUAL_FEE_DEBIT_CODE,
            ANNUAL_FEE_CREDIT_CODE,
            LISTING_FEE_DEBIT_CODE,
            LISTING_FEE_CREDIT_CODE
        FROM INVEST.FUND_PARA
        WHERE F_CD = :FundCd";

		var para = await connection.QueryFirstOrDefaultAsync(sql,
			new { FundCd = fundCd }, transaction);

		if (para == null)
			throw new Exception($"Fund parameter not found for Fund Code = {fundCd}.");

		string debitCode = expenseTypeId switch
		{
			2 => para.MANAGEMENT_FEE_DEBIT_CODE,
			3 => para.CUSTODIAN_FEE_DEBIT_CODE,
			4 => para.TRUSTEE_FEE_DEBIT_CODE,
			6 => para.ANNUAL_FEE_DEBIT_CODE,
			7 => para.LISTING_FEE_DEBIT_CODE,
			_ => throw new Exception($"ExpenseTypeId {expenseTypeId} is not configured.")
		};

		string creditCode = expenseTypeId switch
		{
			2 => para.MANAGEMENT_FEE_CREDIT_CODE,
			3 => para.CUSTODIAN_FEE_CREDIT_CODE,
			4 => para.TRUSTEE_FEE_CREDIT_CODE,
			6 => para.ANNUAL_FEE_CREDIT_CODE,
			7 => para.LISTING_FEE_CREDIT_CODE,
			_ => throw new Exception($"ExpenseTypeId {expenseTypeId} is not configured.")
		};

		if (string.IsNullOrWhiteSpace(debitCode))
			throw new Exception($"Debit account code is not configured for ExpenseTypeId = {expenseTypeId}.");

		if (string.IsNullOrWhiteSpace(creditCode))
			throw new Exception($"Credit account code is not configured for ExpenseTypeId = {expenseTypeId}.");

		return (debitCode, creditCode);
	}

	private string GetExpenseVoucherTypeCode(int expenseTypeId)
	{
		return expenseTypeId switch
		{
			2 => "23", // Management Fee Payable Voucher
			3 => "24", // Custodian Fee Payable Voucher
			4 => "25", // Trustee Fee Payable Voucher
			6 => "26", // Annual Fee Payable Voucher
			7 => "27", // Listing Fee Payable Voucher

			_ => throw new Exception(
				$"Voucher type not configured for ExpenseTypeId = {expenseTypeId}"
			)
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

		//Console.WriteLine($"Generating voucher for Fund: {fundCd}, Expense Type: {expenseTypeId}, NAV Date: {navDate:yyyy-MM-dd}, Amount: {amount}, No of Days: {noOfDays}");
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

		string voucherTypeCode = GetExpenseVoucherTypeCode(expenseTypeId);

		string voucherNo =
			await DbQuery.GetNextAccountVoucherNoAsync(
				connection,
				accountSchema,
				voucherTypeCode,
				transaction);

		var (debitCode, creditCode) =
	await GetExpenseAccountCodesAsync(
		connection,
		transaction,
		fundCd,
		expenseTypeId);


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
				voucherTypeCode,
				voucherEntryBy,
				noOfDays);

		Console.WriteLine($"Inserted Debit GL_TRAN with TranId: {tranNumber}, CtrlNo: {ctrlNumber}");

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
				voucherTypeCode,
				voucherEntryBy,
				noOfDays);

		ctrlNumber++;

		Console.WriteLine($"Inserted Debit GL_TRAN with TranId: {tranNumber}, CtrlNo: {ctrlNumber}");

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
		// return $"VCHR-{fundCd}-{expenseTypeId}-{navDate:yyyyMMdd}";
	}



}