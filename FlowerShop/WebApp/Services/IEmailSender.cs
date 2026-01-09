namespace WebApp.Services
{
    public interface IEmailSender
    {
        void Send(string to, string subject, string body);
    }
}
