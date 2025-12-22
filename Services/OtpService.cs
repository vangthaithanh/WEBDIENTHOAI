using System;
using System.Collections.Concurrent;

namespace WebDienThoai
{
    // Simple in-memory OTP storage with expiry per username.
    // For production, replace with DB or distributed cache.
    public static class OtpService
    {
        private class OtpEntry
        {
            public string Code { get; set; }
            public DateTime ExpireAt { get; set; }
        }

        private static readonly ConcurrentDictionary<string, OtpEntry> Store = new ConcurrentDictionary<string, OtpEntry>(StringComparer.OrdinalIgnoreCase);

        public static void Save(string userName, string otp, TimeSpan ttl)
        {
            Store[userName] = new OtpEntry { Code = otp, ExpireAt = DateTime.UtcNow.Add(ttl) };
        }

        public static bool Validate(string userName, string otp)
        {
            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(otp)) return false;
            if (!Store.TryGetValue(userName, out var entry)) return false;
            if (entry.ExpireAt < DateTime.UtcNow) { Store.TryRemove(userName, out _); return false; }
            return string.Equals(entry.Code, otp, StringComparison.Ordinal);
        }

        public static void Clear(string userName)
        {
            Store.TryRemove(userName, out _);
        }
    }
}
