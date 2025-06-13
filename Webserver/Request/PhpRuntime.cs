using System.Diagnostics;
using System.Net;

namespace Webserver.Request;

public class PhpRequest(Request request, Config.Website target, string requestedFile, IPEndPoint localEndpoint, IPEndPoint remoteEndpoint)
{
    public string ResponseBody { get; private set; } = "";
    public string ResponseHeaders { get; private set; } = "";
    private readonly Request request = request;
    private readonly Config.Website target = target;
    private readonly string requestedFile = requestedFile;
    private readonly IPEndPoint localEndpoint = localEndpoint;
    private readonly IPEndPoint remoteEndpoint = remoteEndpoint;

    public async Task ProcessRequest()
    {
        // credits: https://blog.dragonbyte.de/2021/02/03/simple-c-web-server-with-php-support-using-cgi/
        string tempPath = Path.GetTempPath();
        Process process = new();
        process.StartInfo.FileName = "php-cgi";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardInput = true;
        process.StartInfo.CreateNoWindow = true;

        process.StartInfo.EnvironmentVariables.Clear();

        process.StartInfo.EnvironmentVariables.Add("GATEWAY_INTERFACE", "CGI/1.1");
        process.StartInfo.EnvironmentVariables.Add("SERVER_PROTOCOL", "HTTP/1.1");
        process.StartInfo.EnvironmentVariables.Add("REDIRECT_STATUS", "200");
        process.StartInfo.EnvironmentVariables.Add("DOCUMENT_ROOT", target.directory);
        process.StartInfo.EnvironmentVariables.Add("SCRIPT_NAME", Path.GetFileName(requestedFile));
        process.StartInfo.EnvironmentVariables.Add("SCRIPT_FILENAME", Path.GetFullPath(requestedFile));
        process.StartInfo.EnvironmentVariables.Add("QUERY_STRING", request.query_string);
        process.StartInfo.EnvironmentVariables.Add("CONTENT_LENGTH", request.body.Length.ToString());
        process.StartInfo.EnvironmentVariables.Add("CONTENT_TYPE", GetHeader("Content-Type") ?? "text/plain");
        process.StartInfo.EnvironmentVariables.Add("REQUEST_METHOD", request.header["method"]);
        process.StartInfo.EnvironmentVariables.Add("USER_AGENT", GetHeader("User-Agent") ?? "");
        process.StartInfo.EnvironmentVariables.Add("SERVER_ADDR", localEndpoint.Address.ToString());
        process.StartInfo.EnvironmentVariables.Add("REMOTE_ADDR", remoteEndpoint.Address.ToString());
        process.StartInfo.EnvironmentVariables.Add("REMOTE_PORT", remoteEndpoint.Port.ToString());
        process.StartInfo.EnvironmentVariables.Add("REFERER", GetHeader("Referer") ?? "");
        process.StartInfo.EnvironmentVariables.Add("REQUEST_URI", requestedFile.Substring(target.directory.Length));
        process.StartInfo.EnvironmentVariables.Add("HTTP_COOKIE", GetHeader("Cookie") ?? "");
        process.StartInfo.EnvironmentVariables.Add("HTTP_ACCEPT", GetHeader("Accept") ?? "text/html");
        process.StartInfo.EnvironmentVariables.Add("HTTP_ACCEPT_CHARSET", GetHeader("Accept-Charset") ?? "");
        process.StartInfo.EnvironmentVariables.Add("HTTP_ACCEPT_ENCODING", GetHeader("Accept-Encoding") ?? "");
        process.StartInfo.EnvironmentVariables.Add("HTTP_ACCEPT_LANGUAGE", GetHeader("Accept-Language") ?? "");
        process.StartInfo.EnvironmentVariables.Add("TMPDIR", tempPath);
        process.StartInfo.EnvironmentVariables.Add("TEMP", tempPath);

        process.Start();

        StreamWriter sw = process.StandardInput;
        await sw.BaseStream.WriteAsync(request.body.AsMemory(0, request.body.Length));
        sw.Close();


        bool headersEnd = false;
        StreamReader sr = process.StandardOutput;


        string? line;
        while (true)
        {
            line = await sr.ReadLineAsync();
            if (line == null) break;
            if (!headersEnd)
            {
                if (line == "")
                {
                    headersEnd = true;
                    continue;
                }

                this.ResponseHeaders += line + "\r\n";
            }
            else this.ResponseBody += line;
        }

        process.Kill();
        process.Dispose();
    }

    private string? GetHeader(string header)
    {
        this.request.header.TryGetValue(header, out string? value);
        return value;
    }
}
