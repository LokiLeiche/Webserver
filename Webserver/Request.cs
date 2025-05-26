namespace Webserver
{
    public class Request
    {
        public Header header;
        public string time;
        public string? body;
        public Request(string request)
        {
            Console.WriteLine("Request received!");
            time = DateTime.Now.ToString();

            string head;
            int headEndIdx = request.IndexOf("\r\n\r\n");

            if (headEndIdx > 0 && headEndIdx < request.Length - 8)
            {
                Console.WriteLine($"Head end index: {headEndIdx}");
                this.body = request.Substring(headEndIdx + 4);
                head = request.Substring(0, headEndIdx);
            }
            else
            {
                int idx = request.IndexOf("\r\n\r\n");
                if (idx > 0) head = request.Substring(0, idx);
                else head = request;
            }
            this.header = new(head);

            string[] substrings = head.Split(["\r\n"], StringSplitOptions.RemoveEmptyEntries);
            try
            {
                int idx = head.IndexOf("\r\n");
                if (idx > 0) substrings.Append(head.Substring(0, idx));
                else substrings.Append(head);
            }
            catch
            {
                Console.WriteLine($"Appending first substring failed, full request: {this.header.full}");
            }

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
                        case "POST": // todo: how do post params/data work??
                            header.method = "GET";
                            sub = substring.Substring(substring.IndexOf(" ") + 1);
                            header.path = sub.Substring(0, sub.IndexOf(" "));
                            header.protocol = sub.Substring(sub.IndexOf(" ") + 1);
                            break;
                        // todo: implements other methods, check examples
                        case "Connection:":
                            header.connection = substring.Substring(substring.IndexOf(" ") + 1);
                            Console.WriteLine($"Connection: {header.connection}");
                            break;
                        case "Cache-Control:":
                            header.cacheControl = int.Parse(substring.Substring(substring.IndexOf("max-age=") + 8));
                            break;
                            // todo: add other headers whenever I need them or they become important
                    }
                }
                catch { }
            }
            Console.WriteLine($"Requested File: {this.header.path}");
        }
    }

    public class Header(string header)
    {
        public string full = header;
        public string? host;
        public string? method;
        public string? path;
        public string? protocol;
        public string? connection;
        public int? cacheControl;
    }
}