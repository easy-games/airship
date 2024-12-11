using System;
using UnityEngine.Networking;

namespace Proyecto26
{
    public class RequestException : Exception
    {
        private RequestHelper _request;
        public RequestHelper Request
        {
            get { return _request; }
            private set { _request = value; }
        }

        private bool _isHttpError;
        public bool IsHttpError
        {
            get { return _isHttpError; }
            private set { _isHttpError = value; }
        }

        private bool _isNetworkError;
        public bool IsNetworkError
        {
            get { return _isNetworkError; }
            private set { _isNetworkError = value; }
        }

        private long _statusCode;
        public long StatusCode
        {
            get { return _statusCode; }
            private set { _statusCode = value; }
        }

        private string _serverMessage;
        public string ServerMessage
        {
            get { return _serverMessage; }
            set { _serverMessage = value; }
        }

        private string _response;
        public string Response
        {
            get { return _response; }
            set { _response = value; }
        }

        private UnityWebRequest _unityWebRequest;

        public UnityWebRequest unityWebRequest
        {
            get { return _unityWebRequest; }
            set { _unityWebRequest = value; }
        }


        public RequestException() { }

        public RequestException(string message): base(message) { }

        public RequestException(RequestHelper request, string message, bool isHttpError, bool isNetworkError, long statusCode, string response, UnityWebRequest unityRequest) : base(message) {
            _request = request;
            _isHttpError = isHttpError;
            _isNetworkError = isNetworkError;
            _statusCode = statusCode;
            _response = response;
            _unityWebRequest = unityRequest;
        }
    }
}
