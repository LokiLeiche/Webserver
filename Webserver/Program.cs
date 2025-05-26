using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Webserver
{

    public class Webserver
    {
        public static string GetSLLConf(string arg)
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

        private static Dictionary<string, List<string>> logs = [];
        public static readonly List<Website> websites = [];
        public static readonly int httpPort = 80;
        public static readonly int httpsPort = 443;
        private static readonly string certPath = GetSLLConf("cert_file"); // path of your SLL cert file
        private static readonly string certPw = GetSLLConf("cert_pw"); // the password you entered when creating ts
        private static Dictionary<string, byte[]> fileCache = [];
        private static Dictionary<int, Stopwatch> timers = [];

        public static void LoadConfig()
        {
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

                    string dir = doc.GetProperty("directory").GetString() ?? "";
                    string domain = doc.GetProperty("domain").GetString() ?? "";

                    websites.Add(new Website(enabled, domain, dir));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"There has been an error trying to read the configuration from {file}: {ex.ToString()}\nIt will be skipped and not loaded.");
                }
            }
        }
        
        public static async Task LogManager()
        {
            while (true)
            {
                Dictionary<string, List<string>> currLogs = logs;
                logs = [];
                logs.Add("logs/denied.txt", []);
                logs.Add("logs/requests_full.txt", []);
                logs.Add("logs/served.txt", []);

                foreach (KeyValuePair<string, List<string>> file in currLogs)
                {
                    foreach (string log in file.Value)
                    {
                        File.AppendAllText(file.Key, log);
                    }
                }
                await Task.Delay(1000 * 5);
            }
        }

        public static async Task Main(string[] args)
        {
            logs.Add("logs/denied.txt", []);
            logs.Add("logs/requests_full.txt", []);
            logs.Add("logs/served.txt", []);

            _ = Filter.ClearRequests();

            LoadConfig();

            _ = LogManager();

            _ = ListenHttp();

            Links.Init();

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
            //Console.WriteLine("Connection opened!");
            NetworkStream stream = client.GetStream();
            SslStream? sslStream = null;

            IPEndPoint? remoteEndpoint = client.Client.RemoteEndPoint as IPEndPoint;

            if (ssl)
            {
                sslStream = new(stream, false);
                try
                {
                    await sslStream.AuthenticateAsServerAsync(new X509Certificate2(certPath, certPw));
                }
                catch
                {
                    Console.WriteLine("SSL auth failed!");
                    client.Close();
                    return;
                }
            }



            using (stream)
            using (sslStream)
            using (client)
            {
                while (true)
                {
                    CancellationTokenSource cts = new(10000);
                    try
                    {
                        byte[] buffer = new byte[8192];
                        int bytesRead;
                        if (ssl && sslStream != null)
                        {
                            var readTask = sslStream.ReadAsync(buffer, 0, buffer.Length, cts.Token);
                            var timeoutTask = Task.Delay(Timeout.Infinite, cts.Token);
                            Task completedTask = await Task.WhenAny(readTask, timeoutTask);
                            if (completedTask == readTask)
                            {
                                bytesRead = await readTask;
                            }
                            else
                            {
                                Console.WriteLine("Read operation timed out!");
                                cts.Cancel();
                                break;
                            }
                        }
                        else
                        {
                            var readTask = stream.ReadAsync(buffer, 0, buffer.Length, cts.Token);
                            var timeoutTask = Task.Delay(Timeout.Infinite, cts.Token);
                            Task completedTask = await Task.WhenAny(readTask, timeoutTask);
                            if (completedTask == readTask)
                            {
                                bytesRead = await readTask;
                            }
                            else
                            {
                                Console.WriteLine("Read operation timed out!");
                                cts.Cancel();
                                break;
                            }
                        }
                        float startTime = DateTime.Now.Millisecond * 1000 + DateTime.Now.Microsecond;

                        if (bytesRead < 1) break;

                        Request request = new(Encoding.UTF8.GetString(buffer, 0, bytesRead));

                        string? blockReason = null;
                        try
                        {
                            blockReason = await HandleRequest(request, ssl, remoteEndpoint, sslStream, stream, client);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Internal server error: {ex}");
                            byte[] resp = BuildResponse(500, []);

                            if (ssl && sslStream != null) await sslStream.WriteAsync(resp);
                            else await stream.WriteAsync(resp);

                            break;
                        }

                        if (blockReason == null)
                        {
                            logs["logs/served.txt"].Add($"\n{request.time} - {remoteEndpoint?.Address.ToString()} - {request.header.host}{request.header.path}");
                        }
                        else
                        {
                            logs["logs/denied.txt"].Add($"\n{request.time} - {remoteEndpoint?.Address.ToString()} - {request.header.host}{request.header.path} - Reason: {blockReason}");
                            break;
                        }
                        logs["logs/requests_full.txt"].Add($"\n{request.time} - {remoteEndpoint?.Address.ToString()} - {request.header.host}{request.header.path} - {JsonSerializer.Serialize(request.header.full)}");
                        Console.WriteLine($"Request handled in {DateTime.Now.Millisecond * 1000 + DateTime.Now.Microsecond - startTime}hs!");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                        break;
                    }
                    finally
                    {
                        cts?.Dispose();
                    }
                }
            }
            client.Close();
            //Console.WriteLine("Connection closed!");
        }

        private static async Task<string?> HandleRequest(Request request, bool ssl, IPEndPoint? remoteEndpoint, SslStream? sslStream, NetworkStream stream, TcpClient client)
        {
            Filter.Response filterResponse = Filter.CheckRequest(request, remoteEndpoint);
            if (filterResponse.response != null)
            {
                if (ssl && sslStream != null) await sslStream.WriteAsync(filterResponse.response);
                else await stream.WriteAsync(filterResponse.response);
                //client.Close();

                return filterResponse.reason;
            }


            // temporary
            if (request.header.path == "robots.txt" || request.header.path == "/robots.txt")
            {
                byte[] resp = BuildResponse(200, File.ReadAllBytes("Websites/localhost/robots.txt"));

                if (ssl && sslStream != null) await sslStream.WriteAsync(resp);
                else await stream.WriteAsync(resp);
                //client.Close();

                return null;
            }

            // todo: support other methods, figure shit out how that works
            if (request.header.method != "GET" && request.header.method != "POST")
            {
                byte[] resp = BuildResponse(405, Encoding.UTF8.GetBytes("Only GET is supported for now!"));

                if (ssl && sslStream != null) await sslStream.WriteAsync(resp);
                else await stream.WriteAsync(resp);
                //client.Close();

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

                if (ssl && sslStream != null) await sslStream.WriteAsync(resp);
                else await stream.WriteAsync(resp);
                //client.Close();

                return $"Unknown or disabled host: {request.header.host}";
            }

            if (target.domain == "links.lokiscripts.com" || target.domain == "link.lokiscripts.com")
            {
                byte[] resp = Links.RedirectToLink(request.header.path);

                if (ssl && sslStream != null) await sslStream.WriteAsync(resp);
                else await stream.WriteAsync(resp);
                //client.Close();

                return null;
            }


            if (Path.GetExtension(request.header.path) == string.Empty)
            {
                string file_try = "index.html";
                bool breakLoop = false;
                while (!File.Exists(target.directory + request.header.path + file_try) && !breakLoop)
                {
                    // make compiler shut up because request.header.path is checked for null in Filter.cs
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    Console.WriteLine(request.header.path[^1]);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    if (request.header.path[^1] != '/')
                    {
                        request.header.path += "/";
                    }
                    switch (file_try)
                    {
                        case "index.html":
                            file_try = "index.php";
                            break;

                        // add other files maybe
                        case "index.php":
                            breakLoop = true;
                            break;
                    }
                }
                request.header.path += file_try;
            }

            if (!File.Exists(target.directory + request.header.path))
            {
                byte[] resp = BuildResponse(404, Encoding.UTF8.GetBytes("File not found!"));

                if (ssl && sslStream != null) await sslStream.WriteAsync(resp);
                else await stream.WriteAsync(resp);
                //client.Close();

                return $"Requested file not found: {request.header.host}/{request.header.path}";
            }


            // actually serve the request
            string contentType = GetContentType(target.directory + request.header.path);

            byte[] file;

            try
            {
                file = fileCache[target.domain + request.header.path];
                Console.WriteLine("Serve file from cache");
            }
            catch
            {
                file = File.ReadAllBytes(target.directory + request.header.path);
                fileCache.Add(target.domain + request.header.path, file);
                Console.WriteLine("File was not cached");
            }
            
            byte[] response = BuildResponse(200, file, $"Content-Type: {contentType}");

            if (ssl && sslStream != null) await sslStream.WriteAsync(response);
            else await stream.WriteAsync(response);
            //client.Close();

            return null;
        }


        public static byte[] BuildResponse(int status, byte[] bodyBytes, string headerAdditions = "")
        {
            string header = "HTTP/1.1 ";
            header += status.ToString() + " ";
            header += ResponseCodes.GetMessage(status) + "\r\n";
            header += "Content-Length: " + bodyBytes.Length + "\r\n";
            header += "Keep-Alive: timeout=10, max=100";

            if (!string.IsNullOrEmpty(headerAdditions)) header += headerAdditions + "\r\n";

            header += "\r\n";

            byte[] headerBytes = Encoding.UTF8.GetBytes(header);

            MemoryStream responseStream = new();
            responseStream.Write(headerBytes, 0, headerBytes.Length);
            responseStream.Write(bodyBytes, 0, bodyBytes.Length);

            return responseStream.ToArray();
        }

        public static string GetContentType(string file)
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
                ".php" => "text/plain", // todo: temporary solution, implement php runtime
                _ => "application/octet-stream",
            };
        }
    }

    public class Website(bool enabled, string domain, string dir)
    {
        public readonly bool enabled = enabled;
        public readonly string domain = domain;
        public readonly string directory = dir;
    }
}
