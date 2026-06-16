using System.Collections.Concurrent;

namespace NAVCalculationSystem.Helpers
{
    public static class TokenStore
    {
        // Thread-safe dictionary for storing blacklisted tokens
        public static ConcurrentDictionary<string, bool> BlacklistedTokens 
            = new ConcurrentDictionary<string, bool>();
    }
}