using Microsoft.Extensions.Options;

namespace WebApp.Services
{
    public class AuthStateService
    {
        private readonly AuthSettings _settings;
        private readonly object _sync = new();
        private readonly Dictionary<int, AuthState> _states = new();

        public AuthStateService(IOptions<AuthSettings> options)
        {
            _settings = options.Value;
        }

        public bool IsLocked(int userId)
        {
            lock (_sync)
            {
                return _states.TryGetValue(userId, out var state) && state.IsLocked;
            }
        }

        public string? GetRecoveryCode(int userId)
        {
            lock (_sync)
            {
                return _states.TryGetValue(userId, out var state) ? state.RecoveryCode : null;
            }
        }

        public int RemainingAttempts(int userId)
        {
            lock (_sync)
            {
                if (!_states.TryGetValue(userId, out var state))
                {
                    return _settings.MaxFailedAttempts;
                }
                return Math.Max(0, _settings.MaxFailedAttempts - state.FailedAttempts);
            }
        }

        public void RegisterFailedAttempt(int userId)
        {
            lock (_sync)
            {
                var state = GetOrCreate(userId);
                state.FailedAttempts += 1;
                if (state.FailedAttempts >= _settings.MaxFailedAttempts)
                {
                    state.IsLocked = true;
                    state.RecoveryCode = GenerateCode(_settings.TwoFactorCodeLength);
                }
            }
        }

        public void ResetAttempts(int userId)
        {
            lock (_sync)
            {
                var state = GetOrCreate(userId);
                state.FailedAttempts = 0;
                state.IsLocked = false;
                state.RecoveryCode = null;
            }
        }

        public string GenerateTwoFactorCode(int userId)
        {
            lock (_sync)
            {
                var state = GetOrCreate(userId);
                state.TwoFactorCode = GenerateCode(_settings.TwoFactorCodeLength);
                state.TwoFactorExpiresAt = DateTimeOffset.UtcNow.AddMinutes(_settings.TwoFactorCodeTtlMinutes);
                return state.TwoFactorCode;
            }
        }

        public bool ValidateTwoFactorCode(int userId, string code)
        {
            lock (_sync)
            {
                if (!_states.TryGetValue(userId, out var state))
                {
                    return false;
                }

                if (state.TwoFactorCode == null || state.TwoFactorExpiresAt == null)
                {
                    return false;
                }

                if (DateTimeOffset.UtcNow > state.TwoFactorExpiresAt.Value)
                {
                    state.TwoFactorCode = null;
                    state.TwoFactorExpiresAt = null;
                    return false;
                }

                if (!string.Equals(state.TwoFactorCode, code, StringComparison.Ordinal))
                {
                    return false;
                }

                state.TwoFactorCode = null;
                state.TwoFactorExpiresAt = null;
                return true;
            }
        }

        public bool TryRecover(int userId, string code)
        {
            lock (_sync)
            {
                if (!_states.TryGetValue(userId, out var state))
                {
                    return false;
                }

                if (!state.IsLocked || string.IsNullOrEmpty(state.RecoveryCode))
                {
                    return false;
                }

                if (!string.Equals(state.RecoveryCode, code, StringComparison.Ordinal))
                {
                    return false;
                }

                state.IsLocked = false;
                state.RecoveryCode = null;
                state.FailedAttempts = 0;
                return true;
            }
        }

        public string? PeekTwoFactorCode(int userId)
        {
            lock (_sync)
            {
                return _states.TryGetValue(userId, out var state) ? state.TwoFactorCode : null;
            }
        }

        private AuthState GetOrCreate(int userId)
        {
            if (!_states.TryGetValue(userId, out var state))
            {
                state = new AuthState();
                _states[userId] = state;
            }
            return state;
        }

        private static string GenerateCode(int length)
        {
            var random = new Random();
            var digits = new char[length];
            for (int i = 0; i < length; i++)
            {
                digits[i] = (char)('0' + random.Next(0, 10));
            }
            return new string(digits);
        }

        private class AuthState
        {
            public int FailedAttempts { get; set; }
            public bool IsLocked { get; set; }
            public string? RecoveryCode { get; set; }
            public string? TwoFactorCode { get; set; }
            public DateTimeOffset? TwoFactorExpiresAt { get; set; }
        }
    }
}
