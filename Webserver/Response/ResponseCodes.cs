namespace Webserver.Response;

/// <summary>
/// A list of all HTTP response codes and commonly used descriptive tests for them to be used in ResponseBuilder
/// </summary>
internal static class ResponseCodes
{
    internal static readonly Dictionary<int, string> codes = new()
    {
        // informational responses
        { 100, "Continue" },
        { 101, "Switching Protocols" },
        { 102, "Processing" },
        { 103, "Early Hints" },

        // successful responses
        { 200, "OK" },
        { 201, "Created" },
        { 202, "Accepted" },
        { 203, "Non-Authorative Information" }, // todo: research what exactly that does later
        { 204, "No Content" },
        { 205, "Reset Content" },
        { 206, "Partial Content" },
        { 207, "Multi-Status" }, // todo: research that
        { 208, "Already Reported" },
        { 226, "IM Used" },

        // redirection responses
        { 300, "Multiple Choices" },
        { 301, "Moved Permanently" },
        { 302, "Found" }, // temporarily moved
        { 303, "See Other" },
        { 304, "Not Modified" },
        // 305 Use Proxy, deprecated
        // 306 unused but reserved
        { 307, "Temporary Redirect" }, // same method must be used, 302 allows changed method
        { 308, "Permanent Redirect" },

        // client error responses
        { 400, "Bad Request" },
        { 401, "Unauthorized" }, // due to unknown identity
        { 402, "Payment Required" },
        { 403, "Forbidden" }, // due to known identity
        { 404, "Not Found" },
        { 405, "Method not Allowed" },
        { 406, "Not Acceptable" },
        { 407, "Proxy Authentication Required" },
        { 408, "Request Timeout" },
        { 409, "Conflict" },
        { 410, "Gone" },
        { 411, "Length Required" },
        { 412, "Precondition Failed" },
        { 413, "Content Too Large" },
        { 414, "URI Too Long" },
        { 415, "Unsupported Media Type" },
        { 416, "Range Not Satisfiable" },
        { 417, "Expectation Failed" },
        { 418, "I'm a teapot" },
        { 421, "Misdirected Request" },
        { 422, "Unprocessable Content" },
        { 423, "Locked" },
        { 424, "Failed Dependancy" },
        { 425, "Too Early" },
        { 426, "Upgrade Required" },
        { 428, "Precondition Required" },
        { 429, "Too Many Requests" },
        { 431, "Request Header Fields Too Large" },
        { 451, "Unavailable For Legal Reasons" },

        // server error responses
        { 500, "Internal Server Error" },
        { 501, "Not Implemented" },
        { 502, "Bad Gateway" },
        { 503, "Service Unavailable" },
        { 504, "Gateway Timeout" },
        { 505, "HTTP Version Not Supported" },
        { 506, "Variant Also Negotiates" },
        { 507, "Insufficient Storage" },
        { 508, "Loop Detected" },
        { 510, "Not Extended" },
        { 511, "Network Authentication Required" }
    };
}
