using System.Text;

namespace Webserver.Response;

/// <summary>
/// Builds the actual bytes that are sent to the client via stream
/// </summary>
public static class ResponseBuilder
{
    public static byte[] BuildResponse(UInt16 status, byte[] bodyBytes, string? addiotionalHeaders = null)
    {
        string header = "";

        // response-line
        header += "HTTP/1.1 ";
        header += status + " ";
        header += ResponseCodes.codes[status] + "\r\n";

        // other headers
        header += "Keep-Alive: timeout=10, max=100\r\n";
        header += "Content-Length: " + bodyBytes.Length + "\r\n";

        if (!String.IsNullOrEmpty(addiotionalHeaders)) header += addiotionalHeaders;
        else header += "Content-Type: text/plain\r\n";


        // seperator to body
        header += "\r\n";


        // append body to head
        byte[] headerBytes = Encoding.UTF8.GetBytes(header);
        MemoryStream stream = new();
        stream.Write(headerBytes, 0, headerBytes.Length);
        stream.Write(bodyBytes, 0, bodyBytes.Length);


        // convert MemoryStream to byte array and return
        byte[] response = stream.ToArray();
        return response;
    }
}
