namespace NAVCalculationSystem.DTOs
{
    public class CustodianFeeDto
    {
        public string FundCode { get; set; }
        public string FundName { get; set; }

        public decimal PortfolioListedMarketValue { get; set; }
        public decimal PfolioNlistedMarketValue { get; set; }
        public decimal TotalFdrAmount { get; set; }

        public decimal TotalSellBuyAmountCharge { get; set; }

        public decimal PortfolioMarketValue { get; set; }
        public decimal AnnualRate { get; set; }

        public decimal DailyCustodianFee { get; set; }

        public string NAVDays { get; set; }

        public decimal AccrueCustodianFe { get; set; }
    }
}
