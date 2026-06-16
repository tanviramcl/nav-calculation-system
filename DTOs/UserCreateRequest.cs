public class UserCreateRequest
{
    public string UserId { get; set; }
    public string UserName { get; set; }
    public string? UserEmail { get; set; }

    public string? PhoneNumber { get; set; }
    public string? EmpId { get; set; } 

    // Optional fields
    public string FundCode { get; set; } = "IAMCL";   // default example
    public string BranchCode { get; set; } = "AMC/01"; // default example
    public string UserStatus { get; set; } = "V";


    public IFormFile? ImageFile { get; set; }

	public string? LastUpdatedBy { get; set; }
	public DateTime? LastUpdatedDate { get; set; }
    public string? CreatedBy { get; set; }  
}
