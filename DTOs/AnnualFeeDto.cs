namespace NAVCalculationSystem.DTOs
{
    public class AnnualFeeDto
    {
        public int FundCode { get; set; }

        public string FundName { get; set; } = string.Empty;

        public decimal PortfolioMarketValue { get; set; }

        public decimal AnnualRate { get; set; }

        public decimal DailyAnnualFee { get; set; }

        public int NAVDays { get; set; }

        public decimal AccrueAnnualFee { get; set; }
    }
}
