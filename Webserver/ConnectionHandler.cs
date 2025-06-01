using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Webserver.Response;

namespace Webserver;

/// <summary>
/// Main class for managing connections to clients
/// </summary>
/// <param name="client"></param>
/// <param name="ssl"></param>
public class ConnectionHandler(TcpClient client, bool ssl)
{
    readonly TcpClient client = client;
    readonly bool ssl = ssl;
    Stream stream = client.GetStream();

    /// <summary>
    /// Authenticate connection via ssl certificate
    /// </summary>
    /// <returns></returns>
    /// <exception cref="AuthenticationException"></exception>
    public async Task SSLAuthentication()
    {
        if (!this.ssl) return;

        SslStream sslStream = new(stream, false);
        try
        {
            await sslStream.AuthenticateAsServerAsync(new X509Certificate2(Config.SslPath, Config.SslPass));
        }
        catch
        {
            this.client.Close();
            throw new AuthenticationException("SSL Authentication failed! Make sure the SSL certificate is valid for all domains and the password is correct!");
        }
        stream = sslStream;
    }

    /// <summary>
    /// Manages the connection to ConnectionHandler.client, reads from the stream passing requests to the RequestHandler and closing the connection after a timeout
    /// </summary>
    /// <returns></returns>
    public async Task Listen()
    {
        CancellationTokenSource cts;
        while (true)
        {
            cts = new(10000);
            try
            {
                byte[] buffer = new byte[8192]; // todo: is this enough?
                int bytesRead;

                var readTask = stream.ReadAsync(buffer, 0, buffer.Length, cts.Token);
                var timeoutTask = Task.Delay(Timeout.Infinite, cts.Token);
                Task completedTask = await Task.WhenAny(readTask, timeoutTask);
                if (completedTask == readTask)
                    bytesRead = await readTask;
                else
                { // after 10 seconds with no new request from the client, the connection times out and closes
                    cts.Cancel();
                    break;
                }

                if (bytesRead < 1) break; // empty request

                // actually handle the request
                try
                {
                    Request.RequestHandler handler = new(Encoding.UTF8.GetString(buffer, 0, bytesRead), client.Client.RemoteEndPoint as IPEndPoint);
                    Request.RequestHandler.Response response = handler.HandleRequest();
                    await stream.WriteAsync(response.response);
                    string log = $"{handler.endpoint?.Address.ToString() ?? ""} - {handler.request.header["Host"]}{handler.request.header["path"]}";
                    log = response.served ? log + " - Served" : log + $" - Denied - Reason: {response.reason}";
                    LogManager.AddLog(handler.target?.logDir ?? "Websites/", log, log + handler.request.full);
                }
                catch (Exception ex)
                {
                    if (ex is ArgumentException)
                    { // if this is reached, the client probably sent bad headers, return bad request
                        byte[] response = ResponseBuilder.BuildResponse(400, []);
                        await stream.WriteAsync(response);
                    }
                    else
                    { // in this case something probably went wrong while parsing the headers, return server error
                        byte[] response = ResponseBuilder.BuildResponse(500, []);
                        await stream.WriteAsync(response);
                    }
                    break;
                }
            }
            catch
            {
                break;
            }
            finally
            {
                cts.Dispose();
            }
        }
        cts.Dispose();
        this.client.Close();
    }
}
