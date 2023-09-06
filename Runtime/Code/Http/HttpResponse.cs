namespace Code.Http {
    public struct HttpGetResponse {
        public bool success;
        public int statusCode;
        public string data;
        public string error;
    }

    public struct HttpPostResponse {
        public bool success;
        public int statusCode;
        public string data;
        public string error;
    }
}