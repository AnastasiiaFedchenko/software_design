namespace WebApp.Services
{
    public class EmailSettings
    {
        public string AdminEmail { get; set; } = "fkjgbkrjbnrjnvj@gmail.com";
        public string FromEmail { get; set; } = "noreply@example.com";
        public string SmtpHost { get; set; } = "";
        public int SmtpPort { get; set; } = 587;
        public string SmtpUser { get; set; } = "";
        public string SmtpPassword { get; set; } = "";
        public bool UseSsl { get; set; } = true;
    }
}
