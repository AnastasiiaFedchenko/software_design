using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace E2E.Tests
{
    public class TestWebAppFactory : WebApplicationFactory<Program>
    {
        private readonly string _connectionString;
        private readonly IConfiguration _config;

        public TestWebAppFactory(string connectionString, IConfiguration config)
        {
            _connectionString = connectionString;
            _config = config;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = _connectionString,
                    ["TEST_CONNECTION_STRING"] = _connectionString,
                    ["AuthSettings:ShowTwoFactorCode"] = ResolveShowCode("AuthSettings:ShowTwoFactorCode", defaultWhenMissing: true),
                    ["AuthSettings:ShowRecoveryCode"] = ResolveShowCode("AuthSettings:ShowRecoveryCode", defaultWhenMissing: true),
                    ["EmailSettings:SmtpHost"] = _config["SMTP_HOST"] ?? _config["EmailSettings:SmtpHost"],
                    ["EmailSettings:SmtpPort"] = _config["SMTP_PORT"] ?? _config["EmailSettings:SmtpPort"],
                    ["EmailSettings:SmtpUser"] = _config["SMTP_USER"] ?? _config["EmailSettings:SmtpUser"],
                    ["EmailSettings:SmtpPassword"] = _config["SMTP_PASSWORD"] ?? _config["EmailSettings:SmtpPassword"],
                    ["EmailSettings:FromEmail"] = _config["SMTP_FROM"] ?? _config["EmailSettings:FromEmail"],
                    ["EmailSettings:AdminEmail"] = _config["E2E_EMAIL_USER"] ?? _config["EmailSettings:AdminEmail"],
                    ["EmailSettings:UseSsl"] = _config["SMTP_SSL"] ?? _config["EmailSettings:UseSsl"]
                };
                cfg.AddInMemoryCollection(settings!);
            });
        }

        private string ResolveShowCode(string key, bool defaultWhenMissing)
        {
            var envKey = key.Replace(":", "__");
            var envValue = Environment.GetEnvironmentVariable(envKey);
            if (!string.IsNullOrWhiteSpace(envValue))
            {
                return envValue;
            }

            var value = _config[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            if (!string.IsNullOrWhiteSpace(_config["E2E_EMAIL_USER"]) && !string.IsNullOrWhiteSpace(_config["E2E_EMAIL_PASSWORD"]))
            {
                return "false";
            }

            return defaultWhenMissing ? "true" : "false";
        }
    }
}
