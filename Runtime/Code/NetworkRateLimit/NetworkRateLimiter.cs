using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Code.NetworkRateLimit {
    public class NetworkRateLimiter {
        private static Dictionary<(int, string), Queue<float>> rateLimit = new ();
        
        public static bool CheckRateLimit(int connectionId, MethodBase method, int maxCallsPerInterval, float intervalSeconds) {
            var key = (connectionId, method.Name);
            if (!rateLimit.TryGetValue(key, out var recentCalls)) {
                recentCalls = new Queue<float>(maxCallsPerInterval);
                rateLimit[key] = recentCalls;
            }

            var now = Time.unscaledTime;
            // Clear all old entries
            while (recentCalls.TryPeek(out float frontTimestamp)) {
                // If front timestamp occured more recently than intervalSeconds ago then stop dequeueing
                if (frontTimestamp > now - intervalSeconds) {
                    break;
                }
                
                recentCalls.Dequeue();
            }
            
            if (recentCalls.Count >= maxCallsPerInterval) {
                return false;
            }
            
            recentCalls.Enqueue(now);
            return true;
        }
    }
}