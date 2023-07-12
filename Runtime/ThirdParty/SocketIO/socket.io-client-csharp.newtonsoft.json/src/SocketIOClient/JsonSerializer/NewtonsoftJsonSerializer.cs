using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace SocketIOClient.JsonSerializer
{
    public class NewtonsoftJsonSerializer : IJsonSerializer
    {
		public JsonSerializeResult Serialize(object[] data)
		{
			var converter = new ByteArrayConverter();
			var options = GetOptions();
			options.Converters.Add(converter);
			string json = JsonConvert.SerializeObject(data, options);
			return new JsonSerializeResult
			{
				Json = json,
				Bytes = converter.Bytes
			};
		}

		public T Deserialize<T>(string json)
		{
			var options = GetOptions();
			return JsonConvert.DeserializeObject<T>(json, options);
		}

		public T Deserialize<T>(string json, IList<byte[]> bytes)
		{
			var options = GetOptions();
			var converter = new ByteArrayConverter();
			options.Converters.Add(converter);
			converter.Bytes.AddRange(bytes);
			return JsonConvert.DeserializeObject<T>(json, options);
		}

		public object Deserialize(string json, Type type)
		{
			return JsonConvert.DeserializeObject(json, type);
		}

		public object Deserialize(string json, Type type, IList<byte[]> incomingBytes)
		{
			return JsonConvert.DeserializeObject(json, type);
		}

		private JsonSerializerSettings GetOptions()
		{
			JsonSerializerSettings options = null;
			if (OptionsProvider != null)
			{
				options = OptionsProvider();
			}
			if (options == null)
			{
				options = new JsonSerializerSettings();
			}
			return options;
		}

		public Func<JsonSerializerSettings> OptionsProvider { get; set; }
	}
}