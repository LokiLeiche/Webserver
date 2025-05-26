using System.Net;
using System.Text;
using System.Text.Json;

namespace Webserver
{
    public class Filter
    {
        public class Response(byte[]? resp, string? rsn)
        {
            public byte[]? response = resp;
            public string? reason = rsn;
        }
        public static List<string> LoadBlockedIPs()
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
                recentRequests = [];
                await Task.Delay(1000 * 60);
            }
        }

        public static Response CheckRequest(Request request, IPEndPoint? endpoint)
        {
            string? ip = endpoint?.Address.ToString();
            if (ip == null)
            {
                Console.WriteLine("No IP readable");
                return new(Webserver.BuildResponse(400, Encoding.UTF8.GetBytes("Unable to read client IP")), "No client IP");
            }

            if (blockedIps.Contains(ip))
            {
                Console.WriteLine("IP blocked");
                return new(Webserver.BuildResponse(403, []), "IP blocked");
            }


            if (recentRequests.ContainsKey(ip))
            {
                recentRequests[ip] += 1;
            }
            else
            {
                recentRequests[ip] = 1;
            }

            if (recentRequests[ip] > 90)
            {
                BlockIP(ip, 60 * 5);
            }

            // todo: rework this and figure out how to keep track of time // I'm now comming back to this and have no idea what the issue is, maybe I already done it lol. Still have to actually test ts tho
            int timeout = IsBlocked(ip);
            if (timeout > 0)
            {
                Console.WriteLine("timeouted");
                return new(Webserver.BuildResponse(429, Encoding.UTF8.GetBytes($"You've been blocked from this server for another {timeout} seconds. This can happen due to rate limits or suspicious requests")), $"Timeout: {timeout}");
            }

            if (request.header.method == null || request.header.host == null || request.header.path == null || request.header.protocol == null)
            {
                string missingHeader;
                if (request.header.method == null) missingHeader = "Method";
                else if (request.header.host == null) missingHeader = "Host";
                else if (request.header.path == null) missingHeader = "Path";
                else missingHeader = "Protocol";
                Console.WriteLine($"missing header: {missingHeader}");
                return new(Webserver.BuildResponse(400, Encoding.UTF8.GetBytes($"Your request is missing the header for {missingHeader}")), $"Missing essential header: {missingHeader}");
            }

            if (request.header.protocol != "HTTP/1.1")
            {
                Console.WriteLine("wrong http version");
                return new(Webserver.BuildResponse(505, Encoding.UTF8.GetBytes("Your request failed to use protocol HTTP/1.1!")), "Protocol is not HTTP/1.1");
            }

            if (request.header.path.Contains(".env"))
            {
                BlockIP(ip, 60 * 60 * 6);
                Console.WriteLine("scraping .env");
                return new(Webserver.BuildResponse(200, Encoding.UTF8.GetBytes("You fucking idiot do you really think I let you scrape my .env file? Jokes on you, chances are I don't even have one on this server. Eat this 200 response, I hope a human checked this and just wasted their time.")), "Scraping .env");
            }

            if (request.header.path.Contains("cgi-bin/luci/"))
            {
                BlockIP(ip, 60 * 60 * 24);
                Console.WriteLine("Scraping luci");
                return new(Webserver.BuildResponse(200, Encoding.UTF8.GetBytes("Stop fucking scraping ma shit")), "Scraping files for vulnerabilities");
            }

            if (request.header.path.Contains("..") || request.header.path.Contains("~/"))
            {
                BlockIP(ip, 60 * 60);
                Console.WriteLine("path contains cd commands");
                return new(Webserver.BuildResponse(403, Encoding.UTF8.GetBytes("Suspicious path")), "CD commands in path");
            }

            return new(null, null);
        }

        private static void BlockIP(string ip, int time)
        {
            if (!timeouts.ContainsKey(ip))
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

        public static int IsBlocked(string ip)
        {
            if (timeouts.ContainsKey(ip))
            {
                DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
                DateTimeOffset unixEpoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
                TimeSpan timeDifference = nowUtc - unixEpoch;
                long unixTimestampSeconds = (long)timeDifference.TotalSeconds;

                long delta = timeouts[ip] - unixTimestampSeconds;
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

        public static void RemoveTimeout(string ip)
        {
            timeouts[ip] = 0;
            recentRequests.Remove(ip);
        }
    }
}
