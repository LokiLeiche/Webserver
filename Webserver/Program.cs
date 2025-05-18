using System;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Webserver;
using System.Text.Json;
using System.IO.Pipes;
using System.Reflection.Metadata.Ecma335;

namespace Webserver
{
    public class Webserver
    {
        public static readonly List<Website> websites = [];
        public static readonly int httpPort = 80;
        public static readonly int httpsPort = 443;
        private static readonly string certPath = "SSL/home.lokiscripts.com.pfx"; // path of your SLL cert file
        private static readonly string certPw = "notgivingyoumypw"; // the password you entered when creating ts
        public static async Task Main(string[] args)
        {

            List<string> files = Directory.GetFiles("Config/").ToList();
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

            TcpListener httpListener = new(IPAddress.Any, httpPort);
            TcpListener httpsListener = new(IPAddress.Any, httpsPort);
            httpListener.Start();
            Console.WriteLine($"Start listening on port {httpPort}");
            httpsListener.Start();
            Console.WriteLine($"Start listening on port {httpsPort}");

            while (true)
            {
                // todo: jeweils in eigenen thread schieben

                TcpClient httpsClient = await httpsListener.AcceptTcpClientAsync();
                _ = ProcessClient(httpsClient, true);
                // TcpClient httpClient = await httpListener.AcceptTcpClientAsync();
                // _ = ProcessClient(httpClient, false);
            }
        }

        static async Task ProcessClient(TcpClient client, bool ssl)
        {
            Console.WriteLine("Request received!");
            NetworkStream stream = client.GetStream();
            SslStream? sslStream = null;

            IPEndPoint? remoteEndpoint = client.Client.RemoteEndPoint as IPEndPoint;

            if (remoteEndpoint != null)
            {
                Console.WriteLine($"{remoteEndpoint.Address.ToString()}:{remoteEndpoint.Port}");
            }
            
            if (ssl)
            {
                sslStream = new(stream, false);
            }
            
            try
            {
                byte[] buffer = new byte[1024];
                int bytesRead;
                if (ssl && sslStream != null) {
                    await sslStream.AuthenticateAsServerAsync(new X509Certificate2(certPath, certPw));
                    bytesRead = await sslStream.ReadAsync(buffer, 0, buffer.Length);
                } else {
                    bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                }

                if (bytesRead < 0) {
                    Console.WriteLine("Closing connection because there was no content!");

                    byte[] resp = BuildResponse(400, Encoding.UTF8.GetBytes("Your request doesn't contain any readable content!"));

                    if (ssl && sslStream != null) await sslStream.WriteAsync(resp, 0, resp.Length);
                    else await stream.WriteAsync(resp, 0, resp.Length);
                    client.Close();

                    return; // todo: when could this happen, how to handle?
                }
                

                Request request = new(Encoding.UTF8.GetString(buffer, 0, bytesRead));

                if (request.header.method == null || request.header.host == null || request.header.path == null || request.header.protocol == null) {
                    byte[] resp = BuildResponse(400, Encoding.UTF8.GetBytes("Your request is missing one of these header params: method, host, path, protocol!"));

                    if (ssl && sslStream != null) await sslStream.WriteAsync(resp, 0, resp.Length);
                    else await stream.WriteAsync(resp, 0, resp.Length);
                    client.Close();

                    return;
                }

                if (request.header.protocol != "HTTP/1.1") {
                    byte[] resp = BuildResponse(502, Encoding.UTF8.GetBytes("Your request failed to use protocol HTTP/1.1!"));

                    if (ssl && sslStream != null) await sslStream.WriteAsync(resp, 0, resp.Length);
                    else await stream.WriteAsync(resp, 0, resp.Length);
                    client.Close();

                    return;
                }

                if (request.header.method != "GET") {
                    byte[] resp = BuildResponse(405, Encoding.UTF8.GetBytes("Only GET is supported for now!"));

                    if (ssl && sslStream != null) await sslStream.WriteAsync(resp, 0, resp.Length);
                    else await stream.WriteAsync(resp, 0, resp.Length);
                    client.Close();

                    return;
                }

                if (request.header.path.Contains("..")) {
                    byte[] resp = BuildResponse(403, Encoding.UTF8.GetBytes("No path with .. is allowed!"));

                    if (ssl && sslStream != null) await sslStream.WriteAsync(resp, 0, resp.Length);
                    else await stream.WriteAsync(resp, 0, resp.Length);
                    client.Close();

                    return;
                }

                Website? target = null;
                foreach (Website site in websites) {
                    if (site.domain == request.header.host && site.enabled) {
                        target = site;
                        break;
                    }
                }

                if (target == null) {
                    byte[] resp = BuildResponse(404, Encoding.UTF8.GetBytes("Host not found!"));

                    if (ssl && sslStream != null) await sslStream.WriteAsync(resp, 0, resp.Length);
                    else await stream.WriteAsync(resp, 0, resp.Length);
                    client.Close();

                    return;
                }

                if (!File.Exists(target.directory + request.header.path)) {
                    byte[] resp = BuildResponse(404, Encoding.UTF8.GetBytes("File not found!"));

                    if (ssl && sslStream != null) await sslStream.WriteAsync(resp, 0, resp.Length);
                    else await stream.WriteAsync(resp, 0, resp.Length);
                    client.Close();

                    return;
                } else {
                    // todo: testen ob das geht mit datei, config prüfen!
                    string contentType = GetContentType(target.directory + request.header.path);

                    byte[] file = File.ReadAllBytes(target.directory + request.header.path);
                    byte[] resp = BuildResponse(200, file, $"Content-Type: {contentType}");

                    if (ssl && sslStream != null) await sslStream.WriteAsync(resp, 0, resp.Length);
                    else await stream.WriteAsync(resp, 0, resp.Length);
                    client.Close();

                    return;
                }
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
