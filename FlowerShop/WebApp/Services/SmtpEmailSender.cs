using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace WebApp.Services
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly EmailSettings _settings;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(IOptions<EmailSettings> options, ILogger<SmtpEmailSender> logger)
        {
            _settings = options.Value;
            _logger = logger;
        }

        public void Send(string to, string subject, string body)
        {
            if (string.IsNullOrWhiteSpace(_settings.SmtpHost))
            {
                _logger.LogWarning("SMTP host is not configured. Email was not sent.");
                Console.Error.WriteLine("SMTP host is not configured. Email was not sent.");
                return;
            }

            try
            {
                _logger.LogInformation("Sending email: from {From} to {To} via {Host}:{Port}", _settings.FromEmail, to, _settings.SmtpHost, _settings.SmtpPort);
                Console.WriteLine($"Sending email: from {_settings.FromEmail} to {to} via {_settings.SmtpHost}:{_settings.SmtpPort}");

                using var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
                {
                    EnableSsl = _settings.UseSsl,
                    Credentials = new NetworkCredential(_settings.SmtpUser, _settings.SmtpPassword)
                };

                using var message = new MailMessage(_settings.FromEmail, to, subject, body);
                client.Send(message);
                _logger.LogInformation("Email sent to {Email} via {Host}:{Port}", to, _settings.SmtpHost, _settings.SmtpPort);
                Console.WriteLine($"Email sent to {to}");
            }
            catch (SmtpException ex)
            {
                _logger.LogError(ex, "SMTP error while sending email to {Email}", to);
                Console.Error.WriteLine("SMTP error while sending email:");
                Console.Error.WriteLine(ex.ToString());
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while sending email to {Email}", to);
                Console.Error.WriteLine("Unexpected error while sending email:");
                Console.Error.WriteLine(ex.ToString());
                throw;
            }
        }
    }
}
