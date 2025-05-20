using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

// TODO LIST:
// * Proper logs: Time, source ip, path, full request maybe even?

namespace Webserver
{

    public class Webserver
    {
        private static string GetSLLConf(string arg)
        {
            try
            {
                string JsonString = File.ReadAllText("SSL/ssl_config.json");
                JsonElement doc = JsonDocument.Parse(JsonString).RootElement;

                string path = doc.GetProperty(arg).GetString() ?? "";
                return path;
            }
            catch
            {
                return "";
            }
        }


        public static readonly List<Website> websites = [];
        public static readonly int httpPort = 80;
        public static readonly int httpsPort = 443;
        private static readonly string certPath = GetSLLConf("cert_file"); // path of your SLL cert file
        private static readonly string certPw = GetSLLConf("cert_pw"); // the password you entered when creating ts
        public static async Task Main(string[] args)
        {
            _ = Filter.ClearRequests();

            List<string> files = Directory.GetFiles("Config/Websites/").ToList();
            foreach (string file in files)
            {
                if (Path.GetExtension(file) != ".json") continue;
                try
                {
                    string jsonString = File.ReadAllText(file);
                    JsonElement doc = JsonDocument.Parse(jsonString).RootElement;

                    bool enabled = doc.GetProperty("enabled").GetBoolean();
                    if (!enabled) continue;

                    string? dir = doc.GetProperty("directory").GetString();
                    string? domain = doc.GetProperty("domain").GetString();

                    websites.Add(new Website(enabled, domain, dir));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"There has been an error trying to read the configuration from {file}: {ex.ToString()}\nIt will be skipped and not loaded.");
                }
            }

            _ = ListenHttp();
            //_ = ListenHttps();

            TcpListener httpsListener = new(IPAddress.Any, httpsPort);
            httpsListener.Start();
            Console.WriteLine($"Start listening on port {httpsPort}");
            while (true)
            {
                TcpClient httpsClient = await httpsListener.AcceptTcpClientAsync();
                _ = ProcessClient(httpsClient, true);
            }


        }

        private static async Task ListenHttp()
        {
            TcpListener httpListener = new(IPAddress.Any, httpPort);
            httpListener.Start();
            Console.WriteLine($"Start listening on port {httpPort}");
            while (true)
            {
                TcpClient httpClient = await httpListener.AcceptTcpClientAsync();
                _ = ProcessClient(httpClient, false);
            }
        }

        private static async Task ListenHttps()
        {
            TcpListener httpsListener = new(IPAddress.Any, httpsPort);
            httpsListener.Start();
            Console.WriteLine($"Start listening on port {httpsPort}");
            while (true)
            {
                TcpClient httpsClient = await httpsListener.AcceptTcpClientAsync();
                _ = ProcessClient(httpsClient, true);
            }
        }

        static async Task ProcessClient(TcpClient client, bool ssl)
        {
            Console.WriteLine("Request received!");
            NetworkStream stream = client.GetStream();
            SslStream? sslStream = null;

            IPEndPoint? remoteEndpoint = client.Client.RemoteEndPoint as IPEndPoint;

            if (ssl)
            {
                sslStream = new(stream, false);
            }

            try
            {
                byte[] buffer = new byte[1024];
                int bytesRead;
                if (ssl && sslStream != null)
                {
                    await sslStream.AuthenticateAsServerAsync(new X509Certificate2(certPath, certPw));
                    bytesRead = await sslStream.ReadAsync(buffer, 0, buffer.Length);
                }
                else
                {
                    bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                }

                if (bytesRead < 0)
                {
                    Console.WriteLine("Closing connection because there was no content!");

                    byte[] resp = BuildResponse(400, Encoding.UTF8.GetBytes("Your request doesn't contain any readable content!"));

                    if (ssl && sslStream != null) await sslStream.WriteAsync(resp, 0, resp.Length);
                    else await stream.WriteAsync(resp, 0, resp.Length);
                    client.Close();

                    return; // todo: when could this happen, how to handle?
                }


                Request request = new(Encoding.UTF8.GetString(buffer, 0, bytesRead));

                string? blockReason = await HandleRequest(request, ssl, remoteEndpoint, sslStream, stream, client);

                if (blockReason == null)
                {
                    File.AppendAllText("logs/served.txt", $"\n{request.time} - {remoteEndpoint?.Address.ToString()} - {request.header.host}/{request.header.path}");
                }
                else
                {
                    File.AppendAllText("logs/denied.txt", $"\n{request.time} - {remoteEndpoint?.Address.ToString()} - {request.header.host}/{request.header.path} - Reason: {blockReason}");
                }
                File.AppendAllText("logs/requests_full.txt", $"\n{request.time} - {remoteEndpoint?.Address.ToString()} - {request.header.host}/{request.header.path} - {JsonSerializer.Serialize(request.header.full)}");
                Console.WriteLine("Request served and log saved");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                client.Close();
            }
        }

        private static async Task<string?> HandleRequest(Request request, bool ssl, IPEndPoint? remoteEndpoint, SslStream? sslStream, NetworkStream stream, TcpClient client)
        {
            byte[]? filterResponse = Filter.CheckRequest(request, remoteEndpoint);
            if (filterResponse != null)
            {
                if (ssl && sslStream != null) await sslStream.WriteAsync(filterResponse, 0, filterResponse.Length);
                else await stream.WriteAsync(filterResponse, 0, filterResponse.Length);
                client.Close();

                string reason;
                if (remoteEndpoint != null)
                {
                    int timeout = Filter.IsBlocked(remoteEndpoint.Address.ToString());
                    if (timeout > 0) reason = $"Active timeout: {timeout}s";
                    else reason = "Blocked by Filter.CheckRequest()";
                } else reason = "Blocked by Filter.CheckRequest()";

                return reason;
            }


            // todo: support other methods, figure shit out how that works
            if (request.header.method != "GET")
            {
                byte[] resp = BuildResponse(405, Encoding.UTF8.GetBytes("Only GET is supported for now!"));

                if (ssl && sslStream != null) await sslStream.WriteAsync(resp, 0, resp.Length);
                else await stream.WriteAsync(resp, 0, resp.Length);
                client.Close();

                return $"Wrong method: {request.header.method}";
            }

            Website? target = null;
            foreach (Website site in websites)
            {
                if (site.domain == request.header.host && site.enabled)
                {
                    target = site;
                    break;
                }
            }

            if (target == null)
            {
                byte[] resp = BuildResponse(404, Encoding.UTF8.GetBytes("Host not found!"));

                if (ssl && sslStream != null) await sslStream.WriteAsync(resp, 0, resp.Length);
                else await stream.WriteAsync(resp, 0, resp.Length);
                client.Close();

                return $"Unknown or disabled host: {request.header.host}";
            }


            if (Path.GetExtension(request.header.path) == string.Empty)
            {
                string file_try = "index.html";
                while (!File.Exists(target.directory + request.header.path + file_try))
                {
                    // make compiler shut up because request.header.path is checked for null in Filter.cs
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    Console.WriteLine(request.header.path[request.header.path.Length - 1]);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    if (request.header.path[request.header.path.Length - 1] != '/')
                    {
                        request.header.path += "/";
                    }
                    switch (file_try)
                    {
                        case "index.html":
                            file_try = "index.php";
                            break;

                            // add other files maybe
                    }
                }
                request.header.path += file_try;
            }

            if (!File.Exists(target.directory + request.header.path))
            {
                byte[] resp = BuildResponse(404, Encoding.UTF8.GetBytes("File not found!"));

                if (ssl && sslStream != null) await sslStream.WriteAsync(resp, 0, resp.Length);
                else await stream.WriteAsync(resp, 0, resp.Length);
                client.Close();

                return $"Requested file not found: {request.header.host}/{request.header.path}";
            }


            // actually serve the request
            string contentType = GetContentType(target.directory + request.header.path);

            byte[] file = File.ReadAllBytes(target.directory + request.header.path);
            byte[] response = BuildResponse(200, file, $"Content-Type: {contentType}");

            if (ssl && sslStream != null) await sslStream.WriteAsync(response, 0, response.Length);
            else await stream.WriteAsync(response, 0, response.Length);
            client.Close();

            return null;
        }


        public static byte[] BuildResponse(int status, byte[] bodyBytes, string headerAdditions = "")
        {
            string header = "HTTP/1.1 ";
            header += status.ToString() + " ";
            header += ResponseCodes.GetMessage(status) + "\r\n";
            header += "Content-Length: " + bodyBytes.Length + "\r\n";

            if (!String.IsNullOrEmpty(headerAdditions)) header += headerAdditions + "\r\n";

            header += "\r\n";

            byte[] headerBytes = Encoding.UTF8.GetBytes(header);

            MemoryStream responseStream = new();
            responseStream.Write(headerBytes, 0, headerBytes.Length);
            responseStream.Write(bodyBytes, 0, bodyBytes.Length);

            return responseStream.ToArray();
        }

        private static string GetContentType(string file)
        {
            string extension = Path.GetExtension(file);
            return extension switch
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
        }
    }

    public class Website
    {
        public readonly bool enabled;
        public readonly string domain;
        public readonly string directory;

        public Website(bool enabled, string? domain, string? dir)
        {
            if (String.IsNullOrEmpty(domain) || String.IsNullOrEmpty(dir))
            {
                throw new Exception("Unable to read all expected arguments from config file");
            }

            this.enabled = enabled;
            this.domain = domain;
            this.directory = dir;
        }
    }
}
