using System.Collections.Generic;

namespace Code.Http {
    [LuauAPI]
    public struct HttpResponse {
        public bool success;
// workable data
        public string data;
        public string error;
// meta data
        public int statusCode;
        public Dictionary<string, string> headers;

        public string GetHeader(string headerName) {
            return headers[headerName];
        }
    }
}