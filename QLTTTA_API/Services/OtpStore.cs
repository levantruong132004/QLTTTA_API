using System.Collections.Concurrent;

namespace QLTTTA_API.Services
{
    public class OtpEntry
    {
        public string CorrelationId { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty; // register | forgot
        public string TargetEmail { get; set; } = string.Empty;
        public string? Username { get; set; }
        public string OtpCode { get; set; } = string.Empty;
        public DateTime ExpiresAtUtc { get; set; }
        public object? Payload { get; set; } // Store RegisterRequest or other DTO
    }

    public interface IOtpStore
    {
        OtpEntry Create(string purpose, string targetEmail, string? username, object? payload, TimeSpan ttl);
        bool TryGet(string correlationId, out OtpEntry? entry);
        bool Validate(string correlationId, string otp, out OtpEntry? entry);
        void Remove(string correlationId);
    }

    public class InMemoryOtpStore : IOtpStore
    {
        private readonly ConcurrentDictionary<string, OtpEntry> _store = new();
        private static readonly char[] _digits = "0123456789".ToCharArray();
        private readonly Random _rng = new Random();

        public OtpEntry Create(string purpose, string targetEmail, string? username, object? payload, TimeSpan ttl)
        {
            var code = GenerateCode(6);
            var id = Guid.NewGuid().ToString("N");
            var entry = new OtpEntry
            {
                CorrelationId = id,
                Purpose = purpose,
                TargetEmail = targetEmail,
                Username = username,
                OtpCode = code,
                ExpiresAtUtc = DateTime.UtcNow.Add(ttl),
                Payload = payload
            };
            _store[id] = entry;
            return entry;
        }

        public bool TryGet(string correlationId, out OtpEntry? entry)
        {
            if (_store.TryGetValue(correlationId, out var e))
            {
                if (e.ExpiresAtUtc > DateTime.UtcNow)
                {
                    entry = e;
                    return true;
                }
                _store.TryRemove(correlationId, out _);
            }
            entry = null;
            return false;
        }

        public bool Validate(string correlationId, string otp, out OtpEntry? entry)
        {
            if (TryGet(correlationId, out entry))
            {
                if (string.Equals(entry!.OtpCode, otp, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            entry = null;
            return false;
        }

        public void Remove(string correlationId)
        {
            _store.TryRemove(correlationId, out _);
        }

        private string GenerateCode(int length)
        {
            var chars = new char[length];
            for (int i = 0; i < length; i++)
            {
                chars[i] = _digits[_rng.Next(_digits.Length)];
            }
            return new string(chars);
        }
    }
}
