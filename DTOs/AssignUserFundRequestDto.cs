namespace NAVCalculationSystem.DTOs
{

	public class AssignUserFundRequestDto
	{
		public int? Id { get; set; }
		public string UserId { get; set; }
		public int ProjectId { get; set; }
		public List<string> Funds { get; set; }
		public string AssignedBy { get; set; }
	}

}
