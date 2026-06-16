namespace NAVCalculationSystem.Models
{
    public class UserInfo
    {
        public string FUND_CD { get; set; }
        public string BR_CD { get; set; }
        public string USER_ID { get; set; }
        public string USER_PASS { get; set; }
        public string USER_NM { get; set; }
        public string USER_ADDR { get; set; }
        public string USER_TEL { get; set; }
        public string USER_LEVEL { get; set; }
        public string USER_STATUS { get; set; }
        public DateTime? ENT_DT { get; set; }
        public string REMARKS { get; set; }
        public string ENT_TM { get; set; }
        public string USER_MODE { get; set; }
        public string EMP_ID { get; set; }
        public DateTime? PASS_CHANGE_DATE { get; set; }
        public DateTime? CREATED_DATE { get; set; }
    }
}
