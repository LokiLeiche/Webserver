namespace Webserver.Request;

/// <summary>
/// Reads a request and transforms the given data to a usable format
/// </summary>
public class Request
{
    public readonly Dictionary<string, string> header = [];
    public readonly string? body;
    public readonly DateTime time = DateTime.Now;
    public readonly string full;
    public readonly Dictionary<string, string> query = [];

    public Request(string request)
    {
        full = request;
        Console.WriteLine("Received request");
        int headEndIdx = request.IndexOf("\r\n\r\n");
        string head;
        if (headEndIdx > 0)
        {
            head = request.Substring(0, headEndIdx);
            if (headEndIdx < request.Length - 3) // todo: does it have to be 4 or 3 because length? test this
                this.body = request.Substring(headEndIdx + 4);
        }
        else head = request;

        string[] substrings = head.Split(["\r\n"], StringSplitOptions.None);
        bool first = true;
        foreach (string sub in substrings)
        {
            // request the first line since it's the Request-Line and doesn't follow Key: Value syntax
            if (first)
            {
                first = false;
                continue;
            }

            int idx = sub.IndexOf(": "); // seperator between key and value
            int offset = 2;

            if (idx == -1)
            {
                idx = sub.IndexOf(":");
                if (idx == -1) throw new Exception("Unable to parse header");
                offset = 1;
            }

            string key = sub.Substring(0, idx);
            string value = sub.Substring(idx + offset);

            header[key] = value;
        }

        // parse the Request-Line
        try
        {
            string requestLine = head.Substring(0, head.IndexOf("\r\n"));
            string[] requestParams = requestLine.Split(" ", StringSplitOptions.None);
            header["method"] = requestParams[0];

            int queryIdx = requestParams[1].IndexOf('?');
            if (queryIdx > 0)
            {
                header["path"] = requestParams[1].Substring(0, requestParams[1].IndexOf('?'));
                string query = requestParams[1].Substring(queryIdx + 1);
                string[] arguments = query.Split(['&'], StringSplitOptions.None);
                foreach (string arg in arguments)
                {
                    int seperatorIdx = arg.IndexOf('=');
                    if (seperatorIdx < 1) throw new Exception();
                    string key = arg.Substring(0, seperatorIdx);
                    string value = arg.Substring(seperatorIdx + 1);
                    this.query.Add(key, value);
                }
            }
            else header["path"] = requestParams[1];

            header["path"] = requestParams[1].Substring(0, requestParams[1].IndexOf("?"));
            header["protocol"] = requestParams[2];
        }
        catch
        {
            throw new ArgumentException("Invalid Request-Line");
        }
    }
}
