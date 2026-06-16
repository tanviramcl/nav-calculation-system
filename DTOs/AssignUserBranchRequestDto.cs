namespace NAVCalculationSystem.DTOs
{

	public class AssignUserBranchRequestDto
	{
		public int? Id { get; set; }
		public string UserId { get; set; }
		public int ProjectId { get; set; }
		public List<string> Branches { get; set; }
		public string AssignedBy { get; set; }
	}

}
