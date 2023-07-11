using Newtonsoft.Json.Linq;

namespace SocketIOClient.Messages
{
	public interface IJsonMessage : IMessage
    {
        JArray JsonElements { get; }
    }
}