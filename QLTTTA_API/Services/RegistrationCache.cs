using System.Collections.Concurrent;
using QLTTTA_API.Models;

namespace QLTTTA_API.Services
{
    public interface IRegistrationCache
    {
        void Save(string username, RegisterRequest request, string otp, DateTime expiresAtUtc);
        (RegisterRequest? Request, string? Otp, DateTime? ExpiresAtUtc) Get(string username);
        void UpdateOtp(string username, string otp, DateTime expiresAtUtc);
        void Remove(string username);
    }

    public class InMemoryRegistrationCache : IRegistrationCache
    {
        private class Entry
        {
            public RegisterRequest Request { get; set; } = default!;
            public string Otp { get; set; } = string.Empty;
            public DateTime ExpiresAtUtc { get; set; }
        }

        private readonly ConcurrentDictionary<string, Entry> _store = new(StringComparer.OrdinalIgnoreCase);

        public void Save(string username, RegisterRequest request, string otp, DateTime expiresAtUtc)
        {
            _store[username] = new Entry { Request = request, Otp = otp, ExpiresAtUtc = expiresAtUtc };
        }

        public (RegisterRequest? Request, string? Otp, DateTime? ExpiresAtUtc) Get(string username)
        {
            if (_store.TryGetValue(username, out var e))
            {
                if (DateTime.UtcNow <= e.ExpiresAtUtc)
                    return (e.Request, e.Otp, e.ExpiresAtUtc);
                _store.TryRemove(username, out _);
            }
            return (null, null, null);
        }

        public void UpdateOtp(string username, string otp, DateTime expiresAtUtc)
        {
            _store.AddOrUpdate(username,
                _ => new Entry { Request = new RegisterRequest { Username = username }, Otp = otp, ExpiresAtUtc = expiresAtUtc },
                (_, e) => { e.Otp = otp; e.ExpiresAtUtc = expiresAtUtc; return e; });
        }

        public void Remove(string username)
        {
            _store.TryRemove(username, out _);
        }
    }
}
