public class UserMenuDto
{
    public int MenuId { get; set; }
    public string MenuName { get; set; }
    public string MenuCaption { get; set; }
    public int ParentId { get; set; }
    public string MenuUrl { get; set; }
    public bool HasPermission { get; set; } // true if user has access
}