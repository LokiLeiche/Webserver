namespace Webserver.Request;

/// <summary>
/// Manages caching requested files up to configured cache size to speed up response times
/// </summary>
public static class Cache
{
    private static readonly Dictionary<string, byte[]> Files = [];
    private static readonly FileSystemWatcher watcher; // this is used to minitor the Websites directory for changes and refresh cached files

    static Cache() // constructor to initialize watcher
    {
        watcher = new("Websites")
        {
            Filter = "*.*",
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
        };
        watcher.Changed += OnFileChanged;
        watcher.Deleted += OnFileChanged;

        watcher.EnableRaisingEvents = true;
    }


    public static bool DoesFileExist(string path)
    {
        return Files.ContainsKey(path) || File.Exists(path);
    }

    /// <summary>
    /// Reads a file either from cache or if not cached from disk
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static byte[] ReadFile(string path)
    {
        if (Files.TryGetValue(path, out byte[]? value)) return value;
        Files[path] = File.ReadAllBytes(path);
        PreventCacheOverflow();
        return Files[path];
    }

    /// <summary>
    /// Goes through existing cache entries and makes sure the cache does not overflow by deleting oldest entries if needed
    /// </summary>
    public static void PreventCacheOverflow()
    {
        UInt64 currCacheSize = 0; // check the current cache size
        foreach (KeyValuePair<string, byte[]> entry in Files)
            currCacheSize += (UInt64)entry.Value.Length;


        // delete first cache entry until size is acceptable again, first because first should be the one that's loaded the longest time ago
        while (currCacheSize > Config.CacheSize)
        {
            KeyValuePair<string, byte[]> entry = Files.First();
            currCacheSize -= (UInt64)entry.Value.Length;
            Files.Remove(entry.Key);
        }
    }

    /// <summary>
    /// Keep track of file changes, triggered by watcher. If a file changes, refresh it in cache if cached
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    internal static void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        List<string> keys = [];
        foreach (string path in Files.Keys)
        {
            string fullPath = Path.GetFullPath(path);
            if (fullPath == e.FullPath)
            {
                if (e.ChangeType == WatcherChangeTypes.Deleted) Files.Remove(path);
                else Files[path] = File.ReadAllBytes(path);
                return;
            }
        }
    }
}
