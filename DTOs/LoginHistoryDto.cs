namespace NAVCalculationSystem.DTOs
{

	public class LoginHistoryDto
	{
		public long Id { get; set; }
		public string UserId { get; set; }
		public DateTime? LoginTime { get; set; }
		public string IsOnline { get; set; }
		public string Valid { get; set; }
		public string Remarks { get; set; }
		public string LoginBr { get; set; }
		public string LoginBk { get; set; }
		public DateTime? LogoutTime { get; set; }
		public int? ProjectId { get; set; }
	}
}
