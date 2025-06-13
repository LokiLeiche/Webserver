using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Webserver.Response;

namespace Webserver.Request;

/// <summary>
/// Primary Class for handling received requests
/// </summary>
/// <param name="rawRequest"></param>
/// <param name="endPoint"></param>
public class RequestHandler(string rawRequest, IPEndPoint? endPoint, IPEndPoint? localEndPoint)
{
    public readonly Request request = new(rawRequest);
    public readonly IPEndPoint? endpoint = endPoint;
    public readonly IPEndPoint? localEndpoint = localEndPoint;
    public Config.Website? target = null;

    /// <summary>
    /// Processes a requests and returns a response
    /// </summary>
    /// <returns></returns>
    public async Task<Response> HandleRequest()
    {
        if (endpoint == null) return new(false, ResponseBuilder.BuildResponse(500, Encoding.UTF8.GetBytes("Unable to read IP")), "Unable to read IP"); // should not happen I think
        Filter.Response? filterResponse = Filter.CheckRequest(request, endpoint);

        if (filterResponse != null) return new(false, filterResponse.response, filterResponse.reason);

        if (!request.header.ContainsKey("Host")) return new(false, ResponseBuilder.BuildResponse(400, Encoding.UTF8.GetBytes("Missing header: Host")), "Missing header: Host");

        if (!(request.header["protocol"] == "HTTP/1.1")) return new(false, ResponseBuilder.BuildResponse(505, Encoding.UTF8.GetBytes("Failed to use HTTP/1.1")), "wrong protocol"); // todo: does HTTP/1.0 work? where is it still used?

        if (!(request.header["method"] == "GET" || request.header["method"] == "POST"))
            return new(false, ResponseBuilder.BuildResponse(405, Encoding.UTF8.GetBytes("Use GET or POST")), $"Invalid method: {request.header["method"]}"); // todo: support other methods

        foreach (Config.Website site in Config.Websites)
        {
            if (site.domain == request.header["Host"] || site.aliases.Contains(request.header["Host"]))
            {
                target = site;
                break;
            }
        }

        // todo: alternative behaviour like link redirects
        if (target == null) return new(false, ResponseBuilder.BuildResponse(404, []), $"No target found for host {request.header["Host"]}");

        try
        {
            string requestedFile = target.directory + request.header["path"];

            if (String.IsNullOrEmpty(Path.GetExtension(requestedFile)))
            {
                if (requestedFile[^1] != '/') requestedFile += '/';
                if (Cache.DoesFileExist(requestedFile + "index.html"))
                {
                    requestedFile += "index.html";
                }
                else
                {
                    requestedFile += "index.php";
                }
            }

            if (!Cache.DoesFileExist(requestedFile)) return new(false, ResponseBuilder.BuildResponse(404, []), "File does not exist");

            if (Path.GetExtension(requestedFile) == ".php") // use php-cgi to run PHP scripts, not performant for larger applications with lots of traffic, maybe implement fastCGI at some point
            {
                if (localEndpoint == null) return new(false, ResponseBuilder.BuildResponse(500, Encoding.UTF8.GetBytes("Unable to read local endpoint IP")), "Unable to read local endpoint IP"); // should not happen I think

                PhpRequest phpRequest = new(request, target, requestedFile, localEndpoint, endpoint);
                await phpRequest.ProcessRequest();

                return new(true, ResponseBuilder.BuildResponse(200, Encoding.UTF8.GetBytes(phpRequest.ResponseBody), phpRequest.ResponseHeaders));
            }

            return new(true, ResponseBuilder.BuildResponse(200, Cache.ReadFile(requestedFile), GetContentType(requestedFile)));
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);

            LogManager.AddErrorLog(target.directory, $"Error trying to request {target.domain}{request.header["path"]}: {ex}");
            Console.WriteLine($"Error trying to serve request to {target.domain} - see {target.directory}/err.log for details");
            return new(false, ResponseBuilder.BuildResponse(500, []), "Internal server error, see err.log");
        }
    }

    /// <summary>
    /// Get the Content-Type HTTP header based on file extension
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    public static string GetContentType(string file)
    {
        string extension = Path.GetExtension(file);
        string contentType = extension switch
        {
            ".html" or ".htm" => "text/html",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".pdf" => "application/pdf",
            ".zip" => "application/zip",
            ".txt" => "text/plain",
            ".ico" => "image/vnd.microsoft.icon",
            ".gif" => "image/gif",
            _ => "application/octet-stream",
        };

        return $"Content-Type: {contentType}\r\n";
    }

    /// <summary>
    /// Returntype for RequestHandler.HandleRequest()
    /// </summary>
    /// <param name="served"></param>
    /// <param name="response"></param>
    /// <param name="reason"></param>
    public class Response(bool served, byte[] response, string reason = "")
    {
        public readonly bool served = served;
        public readonly string reason = reason;
        public readonly byte[] response = response;
    }
}
