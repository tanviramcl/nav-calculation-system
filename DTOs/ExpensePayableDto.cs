namespace NAVCalculationSystem.DTOs
{
    public class ExpensePayableDto
    {
        public DateTime NAV_DATE { get; set; }

        public int FUND_CD { get; set; }

        public int EXPENSE_TYPE_ID { get; set; }

        public decimal PORTFOLIO_MARKET_VALUE { get; set; }

        public decimal ANNUAL_RATE { get; set; }

        public decimal DAILY_FEE { get; set; }

        public int NAV_DAYS { get; set; }

        public decimal ACCRUED_FEE { get; set; }

        public string? skipReason { get; set; }
    }
}
