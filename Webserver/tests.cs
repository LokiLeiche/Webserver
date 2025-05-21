using System.Net;
using System.Text;
using Xunit;

// first time fucking around with tests, these probably suck and there's better ways to do it but whatever, still learning
namespace Webserver
{
    public class Tests
    {
        [Theory]
        [InlineData(400, "Bad Request")]
        [InlineData(200, "OK")]
        [InlineData(404, "Not Found")]
        [InlineData(500, "Internal Server Error")]
        public void ResponseMessage(int status, string expected)
        {
            Assert.Equal(expected, ResponseCodes.GetMessage(status));
        }

        [Fact]
        public void FailResponseMessage()
        {
            bool error = false;
            try
            {
                string msg = ResponseCodes.GetMessage(1);
            }
            catch
            {
                error = true;
            }

            Assert.True(error);
        }

        [Fact]
        public void BlockedIps()
        {
            Assert.Equal(["0.0.0.0", "0.0.0.0"], Filter.LoadBlockedIPs());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void RequestClass(int runs)
        {
            string raw = "GET /index.html HTTP/1.1\r\nHost: home.lokiscripts.com\r\nConnection: keep-alive\r\nCache-Control: max-age=1\r\n\r\n";
            string head = "GET /index.html HTTP/1.1\r\nHost: home.lokiscripts.com\r\nConnection: keep-alive\r\nCache-Control: max-age=1";
            string host = "home.lokiscripts.com";
            string path = "/index.html";
            string prot = "HTTP/1.1";
            string method = "GET";
            string connection = "keep-alive";
            int? cacheControl = 1;
            string? body = null;
            switch (runs)
            {
                case 1:
                    raw = "GET /index.php HTTP/1.0\r\nHost: api.lokiscripts.com\r\nConnection: keep-alive\r\nCache-Control: max-age=15\r\n\r\nTestBody";
                    head = "GET /index.php HTTP/1.0\r\nHost: api.lokiscripts.com\r\nConnection: keep-alive\r\nCache-Control: max-age=15";
                    prot = "HTTP/1.0";
                    host = "api.lokiscripts.com";
                    path = "/index.php";
                    cacheControl = 15;
                    body = "TestBody";
                    break;
            }

            Request request = new(raw);
            Assert.Equal(head, request.header.full);
            Assert.Equal(host, request.header.host);
            Assert.Equal(path, request.header.path);
            Assert.Equal(prot, request.header.protocol);
            Assert.Equal(method, request.header.method);
            Assert.Equal(cacheControl, request.header.cacheControl);
            Assert.Equal(connection, request.header.connection);
            Assert.Equal(body, request.body);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        [InlineData(8)]
        [InlineData(9)]
        public void RequestCheck(int runs)
        {
            string ip = "192.168.0.1";
            string rawRequest = "GET /index.php HTTP/1.1\r\nHost: api.lokiscripts.com\r\nConnection: keep-alive\r\nCache-Control: max-age=15\r\n\r\nTestBody";

            switch (runs)
            {
                case 1:
                    ip = "0.0.0.0";
                    break;
                case 2:
                    rawRequest = "GET /index.php HTTP/1.1\r\nConnection: keep-alive\r\nCache-Control: max-age=15\r\n\r\nTestBody";
                    break;
                case 3:
                    rawRequest = "Host: api.lokiscripts.com\r\nConnection: keep-alive\r\nCache-Control: max-age=15\r\n\r\nTestBody";
                    break;
                case 4:
                    rawRequest = "GET /index.php HTTP/1.0\r\nHost: api.lokiscripts.com\r\nConnection: keep-alive\r\nCache-Control: max-age=15\r\n\r\nTestBody";
                    break;
                case 5:
                    rawRequest = "GET /.env HTTP/1.1\r\nHost: api.lokiscripts.com\r\nConnection: keep-alive\r\nCache-Control: max-age=15\r\n\r\nTestBody";
                    break;
                case 6:
                    rawRequest = "GET /../something.test HTTP/1.1\r\nHost: api.lokiscripts.com\r\nConnection: keep-alive\r\nCache-Control: max-age=15\r\n\r\nTestBody";
                    break;
                case 7:
                    rawRequest = "GET /~/something.test HTTP/1.1\r\nHost: api.lokiscripts.com\r\nConnection: keep-alive\r\nCache-Control: max-age=15\r\n\r\nTestBody";
                    break;
            }

            Filter.RemoveTimeout(ip);
            IPAddress address = IPAddress.Parse(ip);
            IPEndPoint? endpoint = new(address, 5000);

            if (runs == 9) endpoint = null;

            Request request = new(rawRequest);
            byte[]? response = Filter.CheckRequest(request, endpoint);

            if (runs == 0 || runs == 8) Assert.Null(response);
            else Assert.NotNull(response);



            switch (runs)
            {
                case 1:
                    Assert.NotNull(response);
                    Assert.Equal(Webserver.BuildResponse(403, []), response);
                    break;

                case 2:
                case 3:
                    Assert.Equal(Webserver.BuildResponse(400, Encoding.UTF8.GetBytes("Your request is missing one of these header params: method, host, path, protocol!")), response);
                    break;

                case 4:
                    Assert.Equal(Webserver.BuildResponse(505, Encoding.UTF8.GetBytes("Your request failed to use protocol HTTP/1.1!")), response);
                    break;

                case 5:
                    Assert.Equal(Webserver.BuildResponse(200, Encoding.UTF8.GetBytes("You fucking idiot do you really think I let you scrape my .env file? Jokes on you, chances are I don't even have one on this server. Eat this 200 response, I hope a human checked this and just wasted their time.")), response);
                    break;

                case 6:
                case 7:
                    Console.WriteLine(response);
                    Assert.Equal(Webserver.BuildResponse(403, Encoding.UTF8.GetBytes("Suspicious path")), response);
                    break;

                case 8:
                    Assert.Null(response);
                    int trys = 0;
                    while (response == null) // spam to get timeout
                    {
                        trys++;
                        response = Filter.CheckRequest(request, endpoint);

                        if (trys > 100)
                        {
                            Assert.Fail("No timeout received");
                        }
                    }
                    response = Filter.CheckRequest(request, endpoint);
                    Assert.NotNull(response);
                    break;

                case 9:
                    Assert.Equal(Webserver.BuildResponse(400, Encoding.UTF8.GetBytes("Unable to read client IP")), response);
                    break;
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void SSLConf(int runs)
        {
            string arg = "cert_file";

            switch (runs)
            {
                case 1:
                    arg = "cert_pw";
                    break;

                case 2:
                    arg = "";
                    break;
            }

            string conf = Webserver.GetSLLConf(arg);

            switch (runs)
            {
                case 0:
                    Assert.Equal("SSL/home.lokiscripts.com.pfx", conf);
                    break;
                case 1:
                    Assert.NotEqual("", conf);
                    break;
                case 2:
                    Assert.Equal("", conf);
                    break;
            }
        }

        [Theory]
        [InlineData("index.html", "text/html")]
        [InlineData("banner.png", "image/png")]
        [InlineData("banner.jpg", "image/jpeg")]
        [InlineData("document.pdf", "application/pdf")]
        [InlineData("folder.zip", "application/zip")]
        [InlineData("file.txt", "text/plain")]
        [InlineData("favicon.ico", "image/vnd.microsoft.icon")]
        [InlineData("hello.gif", "image/gif")]
        [InlineData("style.css", "application/octet-stream")]

        public void contentType(string file, string expected)
        {
            Assert.Equal(expected, Webserver.GetContentType(file));
        }

        [Fact]
        public void WebsiteConfig()
        {
            Webserver.LoadConfig();
            Assert.NotEmpty(Webserver.websites);
        }
    }
}
