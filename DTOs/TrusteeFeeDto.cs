namespace NAVCalculationSystem.DTOs
{
    public class TrusteeFeeDto
    {
        public string FundCode { get; set; } = string.Empty;
        public string FundName { get; set; } = string.Empty;
        public decimal PortfolioMarketValue { get; set; }
        public decimal AnnualRate { get; set; }
        public decimal DailyTrusteeFee { get; set; }
        public int NAVDays { get; set; }
        public decimal AccrueTrusteeFee { get; set; }
    }
}
