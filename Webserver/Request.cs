namespace Webserver
{
    public class Request
    {
        public Header header;
        public string time;
        public string? body;
        public Request(string request)
        {
            time = DateTime.Now.ToString();

            string head;
            int headEndIdx = request.IndexOf("\r\n\r\n");

            if (headEndIdx > 0 && headEndIdx < request.Length - 8)
            {
                Console.WriteLine($"Head end index: {headEndIdx}");
                this.body = request.Substring(headEndIdx + 8);
                head = request.Substring(0, headEndIdx);
            }
            else
            {
                head = request;//.Substring(request.IndexOf("\r\n")); // why did I do this, does it serve any purpose?
            }
            this.header = new(head);

            //Console.WriteLine($"Full header: {JsonSerializer.Serialize(head)}");

            string[] substrings = head.Split(["\r\n"], StringSplitOptions.RemoveEmptyEntries);
            substrings.Append(head.Substring(0, head.IndexOf("\r\n")));
            //Console.WriteLine($"First argument: {head.Substring(0, head.IndexOf("\r\n"))} - {head.IndexOf("\r\n")}");

            //Console.WriteLine("Substrings:");
            foreach (string substring in substrings)
            {
                try
                {
                    switch (substring.Substring(0, substring.IndexOf(" ")))
                    {
                        case "Host:":
                            header.host = substring.Substring(substring.IndexOf(" ") + 1);
                            break;
                        case "GET":
                            header.method = "GET";
                            string sub = substring.Substring(substring.IndexOf(" ") + 1);
                            header.path = sub.Substring(0, sub.IndexOf(" "));
                            header.protocol = sub.Substring(sub.IndexOf(" ") + 1);
                            break;
                        // todo: implements other methods, check examples
                        case "Connection:":
                            header.connection = substring.Substring(substring.IndexOf(" ") + 1);
                            break;
                        case "Cache-Control":
                            header.cacheControl = int.Parse(substring.Substring(substring.IndexOf("max-age=") + 8));
                            break;
                        // todo: add other headers whenever I need them or they become important
                    }
                }
                catch {}
            }
            Console.WriteLine($"Requested File: {this.header.path}");
        }
    }

    public class Header(string header)
    {
        public string full = header;
        public string? host;
        public string? method;
        public string? path { get; set; }
        public string? protocol;
        public string? connection;
        public int? cacheControl;
    }
}