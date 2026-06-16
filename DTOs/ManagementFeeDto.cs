namespace NAVCalculationSystem.DTOs
{
    public class ManagementFeeDto
    {
        public string FundCode { get; set; } = string.Empty;
        public string FundName { get; set; } = string.Empty;
        public decimal PortfolioMarketValue { get; set; }
        public decimal AnnualRate { get; set; }
        public decimal DailyFee { get; set; }
        public int NAVDays { get; set; }
        public decimal AccruedMfee { get; set; }
    }
}
