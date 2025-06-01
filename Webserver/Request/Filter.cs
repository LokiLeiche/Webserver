using System.Net;
using System.Text;
using Webserver.Response;

namespace Webserver.Request;

/// <summary>
/// Class for filtering requests, managing timeouts/rate limits and IP blocks, etc.
/// </summary>
public static class Filter
{
    private static Dictionary<string, long> timeouts = [];
    private static Dictionary<string, int> recentRequests = [];

    static Filter()
    {
        _ = ClearRequests();
    }
    /// <summary>
    /// async loop for sweeping recent requests every minute to keep track of rate limits
    /// </summary>
    /// <returns></returns>
    internal static async Task ClearRequests()
    {
        while (true)
        {
            recentRequests = [];
            await Task.Delay(1000 * 60); // todo: one minute is probably not optimal? maybe put it in the config
        }
    }

    /// <summary>
    /// Checks a request for anything suspicious like scraping files, exceding rate limits etc. and blocks it if suspicious
    /// </summary>
    /// <param name="request"></param>
    /// <param name="endpoint"></param>
    /// <returns></returns>
    public static Response? CheckRequest(Request request, IPEndPoint endpoint)
    {
        string ip = endpoint.Address.ToString();
        if (Config.BlockedIps.Contains(ip))
            return new(ResponseBuilder.BuildResponse(403, Encoding.UTF8.GetBytes("You've been blocked from accessing this server.")), "IP blocked");


        // protect blocked paths like .env file
        foreach (string path in Config.BlockedPaths)
        {
            if (request.header["path"].Contains(path))
            {
                BlockIP(ip, 60 * 60 * 6); // block for 6 hours for scraping
                return new(ResponseBuilder.BuildResponse(403, []), $"requesting blocked path {path}");
            }
        }


        // rate limiting at 30 requests per minute
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

        int timeout = IsBlocked(ip);
        if (timeout > 0)
        {
            return new(ResponseBuilder.BuildResponse(429, Encoding.UTF8.GetBytes($"Time out for {timeout} seconds. Try again after 15 minutes")), $"Timeouted for {timeout}s");
        }

        return null;
    }

    /// <summary>
    /// Times a given IP out for given time in seconds
    /// </summary>
    /// <param name="ip"></param>
    /// <param name="time"></param>
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

    /// <summary>
    /// Checks is an IP has an ongoing timeout and if yes, returns the remaining time
    /// </summary>
    /// <param name="ip"></param>
    /// <returns></returns>
    private static int IsBlocked(string ip)
    {
        if (timeouts.TryGetValue(ip, out long value))
        {
            DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
            DateTimeOffset unixEpoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
            TimeSpan timeDifference = nowUtc - unixEpoch;
            long unixTimestampSeconds = (long)timeDifference.TotalSeconds;

            long delta = value - unixTimestampSeconds;
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

    /// <summary>
    /// Return-Type for Filter.CheckResponse()
    /// </summary>
    /// <param name="response"></param>
    /// <param name="reason"></param>
    public class Response(byte[] response, string reason)
    {
        public byte[] response = response;
        public string reason = reason;
    }
}
