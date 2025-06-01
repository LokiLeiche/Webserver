using System.Net;
using System.Net.Sockets;

namespace Webserver;

static class Programm
{
    /// <summary>
    /// Main entry point for the programm
    /// </summary>
    /// <param name="args"></param>
    static async Task Main(string[] args)
    {
        Console.WriteLine(Config.CacheSize); // temporary to initialize Config


        // initialize the TCPListeners asynchronously
        if (Config.SslEnabled) _ = ListenTCP(true);
        await ListenTCP(false); // await this even though it will never finish to keep the programm running
    }

    /// <summary>
    /// Start listening on cofnigured http or https port and hand established connections off to ConnectionHandler
    /// </summary>
    /// <param name="ssl"></param>
    /// <returns></returns>
    static async Task ListenTCP(bool ssl)
    {
        UInt16 port = ssl ? Config.HttpsPort : Config.HttpPort;
        TcpListener listener = new(IPAddress.Any, port);
        listener.Start();
        Console.WriteLine($"Start listening on port {port}");

        while (true) // keep listening for clients, pass found clients async to ConnectionHandler to immidiately listen again for new clients
        {
            TcpClient client = await listener.AcceptTcpClientAsync();
            ConnectionHandler handler = new(client, ssl);
            if (ssl) await handler.SSLAuthentication();
            _ = handler.Listen();
        }
    }
}
