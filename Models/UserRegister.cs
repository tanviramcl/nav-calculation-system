namespace NAVCalculationSystem.Models
{
    public class UserRegister
    {
        public long UserId { get; set; }
        public string UserCellNo { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string UserPassword { get; set; } = string.Empty;
        public int? ActiveFlag { get; set; }
        public int? AgentId { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? UpdateDate { get; set; }
        public DateTime? LastLogin { get; set; }
        public string? RegistrationComplete { get; set; }
    }
}