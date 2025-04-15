using System.Collections.Generic;

namespace Code.Http {
    public struct HttpResponse {
        public bool success;
// workable data
        public string data;
        public string error;
// meta data
        public int statusCode;
        public Dictionary<string, string> headers;
    }
}