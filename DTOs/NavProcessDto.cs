namespace NAVCalculationSystem.DTOs
{
    public class NavProcessDto
    {
        public int F_CD { get; set; }
        public string F_NAME { get; set; } = string.Empty;
        public string F_TYPE { get; set; } = string.Empty;
        public int? LAST_NAVNO { get; set; }
        public DateTime? LAST_NAVDATE { get; set; }
        public int NAVNO { get; set; }
    }
}