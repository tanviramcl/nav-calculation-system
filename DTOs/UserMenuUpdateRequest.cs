public class UserMenuUpdateRequest
{
    public string UserId { get; set; }
    public int ProjectId { get; set; } // optional, can be used to filter menus
    public List<int> MenuIds { get; set; } = new List<int>(); // menus user should have access
}