namespace NAVCalculationSystem.DTOs
{
    public class PriceRefixDto
    {
        public string FUND_CD { get; set; }
        public DateTime? NAV_DATE { get; set; }
        public DateTime? REFIX_DT { get; set; }
        public DateTime? EFFECTIVE_DATE { get; set; }

        public string? REFIX_DATE { get; set; }
        public string? EFFECTIVE_DT { get; set; }

        public decimal? REFIX_SL_PR { get; set; }
        public decimal? REFIX_REP_PR { get; set; }
        public decimal? NAV_MP { get; set; }
        public decimal? NAV_CP { get; set; }

    }
}
