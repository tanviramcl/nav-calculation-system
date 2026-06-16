namespace NAVCalculationSystem.DTOs
{

    public class LoginDto
    {
        public string UserId { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        public int ProjectId { get; set; } 
    }
}
