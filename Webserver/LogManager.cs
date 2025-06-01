namespace Webserver;

/// <summary>
/// Class handling all logs
/// </summary>
public static class LogManager
{
    // keep track of all logs, instead of writing them to the files immediately, put them in a list as pipeline
    // to avoid Files being accessed and written to at the same time, causing access errors
    private static Dictionary<string, List<string>> logs = [];
    private static Dictionary<string, List<string>> fullLogs = [];
    private static Dictionary<string, List<string>> errLogs = [];
    static LogManager()
    {
        _ = WriteLogs(); // start the async loop
    }

    /// <summary>
    /// Adds a log to the pipeline, will be written to the actual file after a delay
    /// </summary>
    /// <param name="target"></param>
    /// <param name="log"></param>
    /// <param name="fullLog"></param>
    public static void AddLog(string target, string log, string fullLog)
    {
        if (logs.TryGetValue(target, out List<string>? logVal)) logVal.Add(log);
        else logs[target] = [log];

        if (fullLogs.TryGetValue(target, out List<string>? fullVal)) fullVal.Add(fullLog);
        else fullLogs[target] = [fullLog];
    }

    /// <summary>
    /// Adds an error log to the pipeline, will be written to the actual file after a delay
    /// </summary>
    /// <param name="target"></param>
    /// <param name="log"></param>
    public static void AddErrorLog(string target, string log)
    {
        if (errLogs.TryGetValue(target, out List<string>? logVal)) logVal.Add(log);
        else errLogs[target] = [log];
    }

    /// <summary>
    /// Async task that will run in a loop forever, writing each log from the pipeline to the actual files every 60 seconds
    /// </summary>
    /// <returns></returns>
    public static async Task WriteLogs()
    {
        while (true)
        {
            await Task.Delay(1000 * 60); // 60 seconds means when the program is stopped or crashed or something, last up to 60 seconds will not be written to log files
            Dictionary<string, List<string>> currLogs = logs;
            logs = [];
            Dictionary<string, List<string>> currFullLogs = fullLogs;
            fullLogs = [];
            Dictionary<string, List<string>> currErrLogs = errLogs;
            errLogs = [];


            foreach (KeyValuePair<string, List<string>> logList in currLogs)
            {
                string logText = "";
                foreach (string log in logList.Value)
                {
                    logText += log + "\n";
                }

                Console.WriteLine($"writing to log file: {logList.Key}/request.log");
                File.AppendAllText(logList.Key + "/requests.log", logText);
            }

            foreach (KeyValuePair<string, List<string>> logList in currFullLogs)
            {
                string logText = "";
                foreach (string log in logList.Value)
                {
                    logText += log + "\n";
                }
                File.AppendAllText(logList.Key + "/full.log", logText);
            }

            foreach (KeyValuePair<string, List<string>> logList in currErrLogs)
            {
                string logText = "";
                foreach (string log in logList.Value)
                {
                    logText += log + "\n";
                }
                File.AppendAllText(logList.Key + "/err.log", logText);
            }
        }
    }
}
