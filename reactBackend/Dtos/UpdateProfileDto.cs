namespace reactBackend.Dtos
{
    public class UpdateProfileDto
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
    }


    public class CheckEmailDto
    {
        public string Email { get; set; }
    }
}
