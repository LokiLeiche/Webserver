using System.Net;
using System.Text;
using System.Text.Json;

namespace Webserver
{
    public class Filter
    {
        private static List<string> LoadBlockedIPs()
        {
            string JsonString = File.ReadAllText("Config/blocked_ips.json");
            JsonElement doc = JsonDocument.Parse(JsonString).RootElement;

            List<string>? ips = JsonSerializer.Deserialize<List<string>>(JsonString);

            ips ??= [];

            return ips;
        }

        private static readonly List<string> blockedIps = LoadBlockedIPs();
        private static readonly Dictionary<string, long> timeouts = [];
        private static Dictionary<string, int> recentRequests = [];
        public static async Task ClearRequests()
        {
            while (true)
            {
                await Task.Delay(1000 * 60);
                recentRequests = [];
            }
        }

        public static byte[]? CheckRequest(Request request, IPEndPoint? endpoint)
        {
            string? ip = endpoint?.Address.ToString();
            if (ip == null)
            {
                return Webserver.BuildResponse(400, Encoding.UTF8.GetBytes("Unable to read client IP"));
            }

            if (blockedIps.Contains(ip))
            {
                return Webserver.BuildResponse(403, []);
            }


            if (recentRequests.ContainsKey(ip))
            {
                recentRequests[ip] += 1;
            }
            else
            {
                recentRequests[ip] = 1;
            }

            if (recentRequests[ip] > 30)
            {
                BlockIP(ip, 60 * 5);
            }

            // todo: rework this and figure out how to keep track of time
            int blocked = IsBlocked(ip);
            if (blocked > 0)
            {
                return Webserver.BuildResponse(429, Encoding.UTF8.GetBytes($"You've been blocked from this server for another {blocked} seconds. This can happen due to rate limits or suspicious requests"));
            }

            if (request.header.method == null || request.header.host == null || request.header.path == null || request.header.protocol == null)
            {
                return Webserver.BuildResponse(400, Encoding.UTF8.GetBytes("Your request is missing one of these header params: method, host, path, protocol!"));
            }

            if (request.header.protocol != "HTTP/1.1")
            {
                return Webserver.BuildResponse(505, Encoding.UTF8.GetBytes("Your request failed to use protocol HTTP/1.1!"));
            }

            if (request.header.path.Contains(".env"))
            {
                BlockIP(ip, 60 * 60 * 6);
                return Webserver.BuildResponse(200, Encoding.UTF8.GetBytes("You fucking idiot do you really think I let you scrape my .env file? Jokes on you, chances are I don't even have one on this server. Eat this 200 response, I hope a human checked this and just wasted their time."));
            }

            if (request.header.path.Contains("..") || request.header.path.Contains("~/"))
            {
                BlockIP(ip, 60 * 60);
                return Webserver.BuildResponse(403, Encoding.UTF8.GetBytes("Suspicious path"));
            }

            return null;
        }

        private static void BlockIP(string ip, int time)
        {
            if (timeouts[ip] < 1)
            {
                DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
                DateTimeOffset unixEpoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
                TimeSpan timeDifference = nowUtc - unixEpoch;
                long unixTimestampSeconds = (long)timeDifference.TotalSeconds;
                timeouts[ip] = unixTimestampSeconds + time;
            }
            else
            {
                timeouts[ip] += time;
            }
        }

        private static int IsBlocked(string ip)
        {
            if (timeouts.ContainsKey(ip))
            {
                DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
                DateTimeOffset unixEpoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
                TimeSpan timeDifference = nowUtc - unixEpoch;
                long unixTimestampSeconds = (long)timeDifference.TotalSeconds;

                long delta = unixTimestampSeconds - timeouts[ip];
                if (delta > 0)
                {
                    return (int)delta;
                }
                else
                {
                    timeouts.Remove(ip);
                    return 0;
                }
            }
            else
            {
                return 0;
            }
        }
    }
}
