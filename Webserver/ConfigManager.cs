using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Webserver;

/// <summary>
/// Class for managing everything regarding Configuration
/// </summary>
public static class Config
{
    public static readonly UInt64 CacheSize; // size of cache pool in byte, therefore UInt64 cause can't be negative but everything over ~4gb would be too big for UInt32
    public static readonly string[] BlockedIps;
    public static readonly string[] BlockedPaths;
    public static readonly bool SslEnabled;
    public static readonly string SslPath;
    public static readonly string SslPass;
    public static readonly UInt16 HttpPort;
    public static readonly UInt16 HttpsPort;
    public static readonly Website[] Websites;



    /// <summary>
    /// Reads the config.json file and stores all config values
    /// </summary>
    /// <exception cref="FileNotFoundException"></exception>
    static Config() // Constructor
    {
        //  make sure the file even exists
        if (!File.Exists("Config/config.json")) throw new FileNotFoundException("No config.json file found!");

        // read the json
        string configJson = File.ReadAllText("Config/config.json");
        // remove comments from config.json before parsing to avoid syntax errors
        string regex = @"//.*\n"; // catches everything between // as start of a comment and \n as and of line
        configJson = Regex.Replace(configJson, regex, "\n"); // replace with \n to get rid of everything except newline

        JsonDocument jsonDocument = JsonDocument.Parse(configJson);
        JsonElement root = jsonDocument.RootElement;


        // read and assign all values
        CacheSize = root.GetProperty("cacheSize").GetUInt64();

        JsonElement blockedIpsJson = root.GetProperty("blockedIps");
        BlockedIps = blockedIpsJson.EnumerateArray() // convert from json to string array
                                    .Select(element => element.GetString() ?? "")
                                    .ToArray();
        JsonElement blockedPathsJson = root.GetProperty("blockedPaths");
        BlockedPaths = blockedPathsJson.EnumerateArray()
                                        .Select(element => element.GetString() ?? "")
                                        .ToArray();

        SslEnabled = root.GetProperty("enableSsl").GetBoolean();
        SslPath = root.GetProperty("sslFile").GetString() ?? "";
        SslPass = root.GetProperty("sslPassword").GetString() ?? "";
        HttpPort = root.GetProperty("httpPort").GetUInt16();
        HttpsPort = root.GetProperty("httpsPort").GetUInt16();

        JsonElement websitesJson = root.GetProperty("websites");
        websitesJson.EnumerateObject();

        List<Website> TempWebsites = []; // temporary list to be dynamicx
        foreach (JsonProperty site in websitesJson.EnumerateObject())
        {
            try // read all properties and if that checks out and site is enabled, add it to the list
            {
                string domain = site.Name;
                JsonElement properties = site.Value;
                bool enabled = properties.GetProperty("enabled").GetBoolean();
                if (!enabled) continue; // skip disabled sites, no reason to load them
                string dir = properties.GetProperty("public_directory").GetString() ?? "";
                string logDir = properties.GetProperty("log_directory").GetString() ?? "";
                string[] aliases = properties.GetProperty("alias").EnumerateArray()
                                                                    .Select(element => element.GetString() ?? "")
                                                                    .ToArray();
                if (String.IsNullOrEmpty(dir) || String.IsNullOrEmpty(logDir))
                { // notify the user that they fucked up
                    Console.WriteLine($"Skipping configuration for {domain} due to syntax errors or incomplete information");
                    continue;
                }

                // make sure the given directories exist
                if (!Directory.Exists(dir))
                {
                    Console.WriteLine($"The configured directory {dir} for {domain} does not exist! Skipping this website");
                    continue;
                }

                if (!Directory.Exists(logDir))
                {
                    Console.WriteLine($"The configured log directory {logDir} for {domain} does not exist! Skipping this website");
                    continue;
                }

                // make sure domain isn't already configured
                foreach (Website tempSite in TempWebsites)
                {
                    bool exists = false;
                    exists = tempSite.domain == domain;

                    exists = exists || tempSite.aliases.Contains(domain);
                    exists = exists || aliases.Contains(tempSite.domain);

                    foreach (string alias in tempSite.aliases)
                    {
                        exists = exists || aliases.Contains(alias);
                    }

                    if (exists)
                    {
                        Console.WriteLine($"Domains (including aliases) can only be configured once and not override eachother. Skipping configuration for {domain}");
                        throw new Exception();
                    }
                }

                TempWebsites.Add(new(domain, dir, logDir, aliases));
            }
            catch { }
        }
        Websites = TempWebsites.ToArray(); // convert dynamic list to static array because it wont be changed anymore


        // if ssl is enabled, make sure the certificate exists
        if (SslEnabled)
            if (!File.Exists(SslPath))
                throw new FileNotFoundException($"No SSL certificate file found under path {SslPath}!");
    }
    
    /// <summary>
    /// Type for keeping track of configured Websites
    /// </summary>
    /// <param name="host"></param>
    /// <param name="publicDir"></param>
    /// <param name="logDir"></param>
    /// <param name="aliases"></param>
    public class Website(string host, string publicDir, string logDir, string[] aliases)
    {
        public readonly string directory = publicDir;
        public readonly string domain = host;
        public readonly string logDir = logDir;
        public readonly string[] aliases = aliases;
    }
}


