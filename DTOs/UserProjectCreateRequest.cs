public class UserProjectCreateRequest
{
    public int ProjectId { get; set; }
    public string UserId { get; set; }
    public string? Remarks { get; set; } // optional
}