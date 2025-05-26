using System.Text;
using System.Text.Json;

namespace Webserver
{
    public static class Links
    {
        private static Dictionary<string, string> links = [];
        public static void Init()
        {
            _ = RefreshLinks();
        }

        public static async Task RefreshLinks()
        {
            while (true)
            {
                Dictionary<string, string> newLinks = [];

                string jsonString = File.ReadAllText("Config/links.json");

                JsonElement root = JsonDocument.Parse(jsonString).RootElement;

                if (root.ValueKind != JsonValueKind.Object) throw new Exception("Unexpected Config/links.json Syntax!");

                foreach (JsonProperty property in root.EnumerateObject())
                {
                    newLinks[property.Name] = property.Value.ToString();
                }

                links = newLinks;
                await Task.Delay(1000 * 60 * 5); // refresh every 5 minutes
            }
        }

        public static byte[] RedirectToLink(string? path)
        {
            path = path?[1..];
            if (path == "robots.txt")
            {
                return Webserver.BuildResponse(200, Encoding.UTF8.GetBytes("User-agent: *\nDisallow: /"));
            }

            if (path != null && links.ContainsKey(path))
            {
                return Webserver.BuildResponse(302, [], $"Location: {links[path]}");
            }
            else
            {
                return Webserver.BuildResponse(404, Encoding.UTF8.GetBytes("Link not found!"));
            }
        }
    }
}