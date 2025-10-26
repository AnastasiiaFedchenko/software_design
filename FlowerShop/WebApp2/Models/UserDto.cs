namespace WebApp2.Models
{
    public class UserDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
    }

    public class CreateUserRequest
    {
        public string Name { get; set; }
        public string Email { get; set; }
    }

    public class UpdateUserRequest
    {
        public string Name { get; set; }
        public string Email { get; set; }
    }
}