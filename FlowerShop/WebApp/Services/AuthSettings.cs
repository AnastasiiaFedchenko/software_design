namespace WebApp.Services
{
    public class AuthSettings
    {
        public int MaxFailedAttempts { get; set; } = 3;
        public int TwoFactorCodeLength { get; set; } = 6;
        public int TwoFactorCodeTtlMinutes { get; set; } = 5;
        public bool ShowTwoFactorCode { get; set; } = false;
        public bool ShowRecoveryCode { get; set; } = false;
    }
}
