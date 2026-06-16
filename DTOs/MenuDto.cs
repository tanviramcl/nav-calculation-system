namespace NAVCalculationSystem.DTOs
{

	public class MenuDto
	{
		public int ParentId { get; set; }
		public string MenuName { get; set; }
    	public string MenuLink { get; set; }
		public List<ChildMenuDto> Children { get; set; } = new List<ChildMenuDto>();
	}
}
