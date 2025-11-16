using System.Collections.Concurrent;
using QLTTTA_API.Models;

namespace QLTTTA_API.Services
{
    public class QrLoginChallenge
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string RequesterDevice { get; set; } = "pc"; // pc|mobile
        public string? RequesterInfo { get; set; }
        public string Status { get; set; } = "pending"; // pending|scanned|approved|denied|expired|used
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(3);

        // Approver info (when scanned/approved)
        public string? ApproverSessionId { get; set; }
        public string? ApproverDevice { get; set; }
        public int? ApproverUserId { get; set; }
        public string? ApproverUsername { get; set; }
        public string? ApproverDisplayName { get; set; }

        // Secret to allow requester to exchange for a session when approved
        public string? GrantToken { get; set; }
        public bool Consumed { get; set; } = false;
    }

    public interface IQrLoginService
    {
        QrLoginChallenge Create(string requesterDevice, string? requesterInfo = null, TimeSpan? ttl = null);
        bool TryGet(string id, out QrLoginChallenge? challenge);
        bool TryScan(string id, string approverSessionId, string approverDevice, int approverUserId, string approverUsername, string? approverDisplayName, out QrLoginChallenge? challenge);
        bool TryApprove(string id, bool approve, out QrLoginChallenge? challenge);
        void Cleanup();
    }

    public class InMemoryQrLoginService : IQrLoginService
    {
        private readonly ConcurrentDictionary<string, QrLoginChallenge> _store = new();

        public QrLoginChallenge Create(string requesterDevice, string? requesterInfo = null, TimeSpan? ttl = null)
        {
            var c = new QrLoginChallenge
            {
                RequesterDevice = Normalize(requesterDevice),
                RequesterInfo = requesterInfo,
            };
            if (ttl.HasValue) c.ExpiresAt = DateTime.UtcNow.Add(ttl.Value);
            _store[c.Id] = c;
            return c;
        }

        public bool TryGet(string id, out QrLoginChallenge? challenge)
        {
            challenge = null;
            if (_store.TryGetValue(id, out var c))
            {
                if (c.ExpiresAt <= DateTime.UtcNow && c.Status is not ("used" or "denied"))
                {
                    c.Status = "expired";
                }
                challenge = c;
                return true;
            }
            return false;
        }

        public bool TryScan(string id, string approverSessionId, string approverDevice, int approverUserId, string approverUsername, string? approverDisplayName, out QrLoginChallenge? challenge)
        {
            challenge = null;
            if (!_store.TryGetValue(id, out var c)) return false;
            if (c.ExpiresAt <= DateTime.UtcNow)
            {
                c.Status = "expired";
                challenge = c;
                return true;
            }
            if (c.Status is "approved" or "denied" or "used") { challenge = c; return true; }
            c.Status = "scanned";
            c.ApproverSessionId = approverSessionId;
            c.ApproverDevice = Normalize(approverDevice);
            c.ApproverUserId = approverUserId;
            c.ApproverUsername = approverUsername;
            c.ApproverDisplayName = approverDisplayName;
            challenge = c;
            return true;
        }

        public bool TryApprove(string id, bool approve, out QrLoginChallenge? challenge)
        {
            challenge = null;
            if (!_store.TryGetValue(id, out var c)) return false;
            if (c.ExpiresAt <= DateTime.UtcNow)
            {
                c.Status = "expired";
                challenge = c;
                return true;
            }
            if (approve)
            {
                c.Status = "approved";
                c.GrantToken = Guid.NewGuid().ToString("N");
            }
            else
            {
                c.Status = "denied";
            }
            challenge = c;
            return true;
        }

        public void Cleanup()
        {
            var now = DateTime.UtcNow;
            foreach (var kv in _store.ToArray())
            {
                if (kv.Value.ExpiresAt.AddMinutes(5) < now || kv.Value.Status is "used" or "denied")
                {
                    _store.TryRemove(kv.Key, out _);
                }
            }
        }

        private static string Normalize(string d)
        {
            d = (d ?? "pc").Trim().ToLowerInvariant();
            return (d == "mobile") ? "mobile" : "pc";
        }
    }
}
