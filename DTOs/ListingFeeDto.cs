namespace NAVCalculationSystem.DTOs
{
    public class ListingFeeDto
    {
        public int FundCode { get; set; }
        public string FundName { get; set; }
        public decimal PortfolioMarketValue { get; set; }
        public decimal AnnualRate { get; set; }
        public decimal DailyListingFee { get; set; }
        public int NAVDays { get; set; }
        public decimal AccrueListingFee { get; set; }
    }
}
