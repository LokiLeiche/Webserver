namespace Webserver.Tests;

using Xunit;

public class Config
{
    [Fact]
    public void TestConfig() // the actual values as read from Webserver.Config are compared to the default config ones
    {
        UInt64 cacheSize = 1073741824;
        Assert.Equal(cacheSize, Webserver.Config.CacheSize);

        string[] blockedIps = ["0.0.0.0", "1.1.1.1"];
        Assert.Equal(blockedIps, Webserver.Config.BlockedIps);

        Assert.False(Webserver.Config.SslEnabled);
        Assert.Equal("Config/lokiscripts.com.pfx", Webserver.Config.SslPath);
        Assert.Equal("password", Webserver.Config.SslPass);
        Assert.Equal(80, Webserver.Config.HttpPort);
        Assert.Equal(443, Webserver.Config.HttpsPort);
        Assert.Equal([".env", "..", "~/", "cgi-bin/luci/"], Webserver.Config.BlockedPaths);
    }

    [Fact]
    public void TestWebsiteLoading()
    { // have to compare each attribute individually cause array of a custom class

        Webserver.Config.Website[] expected = [new("example.com", "Websites/example.com/public", "Websites/example.com/logs", ["www.example.com", "test.example.com"])];
        Webserver.Config.Website[] actual = Webserver.Config.Websites;
        
        Assert.Equal(expected.Length, actual.Length);

        for (int i = 0; i > expected.Length; i++)
        {
            Assert.Equal(expected[i].directory, actual[i].directory);
            Assert.Equal(expected[i].domain, actual[i].domain);
            Assert.Equal(expected[i].logDir, actual[i].logDir);

            Assert.Equal(expected[i].aliases, actual[i].aliases); // can handle string array comparrissons itself
        }
    }
}
