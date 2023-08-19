namespace Code.Http {
    public struct HttpGetResponse {
        public long statusCode;
        public string data;
        public string error;
    }

    public struct HttpPostResponse {
        public long statusCode;
        public string data;
        public string error;
    }
}