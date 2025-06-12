using System;
using System.Collections;
using System.Threading.Tasks;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using MQTTnet;
using MQTTnet.Client;
using System.Threading;
using System.Linq;
using Code.Util;
using System.Collections.Generic;
using Code.Platform.Shared;
using UnityEngine.Rendering.Universal;
using System.Collections.Concurrent;

record PubSubMessage
{
    public TopicDescription topic { get; set; }
    public string payload { get; set; }
}

public enum Scope
{
    Game = 0,
    Server = 1,
}

public record TopicDescription
{
    public Scope scope { get; set; }
    public string topicNamespace { get; set; }
    public string topicName { get; set; }
}

public record ParseTopicResponse
{
    public TopicDescription topic { get; set; }
    public bool isValid { get; set; }
    public string errorMessage { get; set; }
}

[LuauAPI(LuauContext.Protected)]
public class MessagingManager : Singleton<MessagingManager>
{
    private static ConcurrentBag<PubSubMessage> queuedOutgoingPackets = new();
    private static ConcurrentBag<TopicDescription> pendingSubscriptions = new();
    private static ConcurrentBag<TopicDescription> pendingUnsubscribes = new();
    private static MqttFactory mqttFactory = new MqttFactory();
    public event Action<TopicDescription, string> OnEvent;
    public event Action<string> OnDisconnected;
    private static bool disconnectedIntent = false;

    private static IMqttClient mqttClient;

    private static ServerBootstrap serverBootstrap;

    private void Awake()
    {
        DontDestroyOnLoad(this);
    }

    private void Start()
    {
        MessagingManager.serverBootstrap = FindFirstObjectByType<ServerBootstrap>();
    }

    public static async Task<bool> ConnectAsyncInternal()
    {
        if (RunCore.IsEditor())
        {
            return false;
        }

        if (!MessagingManager.serverBootstrap)
        {
            Debug.LogError("MessagingManager: ServerBootstrap not found in scene. Please ensure it is present.");
            return false;
        }

        var test = UnityMainThreadDispatcher.Instance; // Generate instance now so it is available later

        var mqttClient = mqttFactory.CreateMqttClient();
        MessagingManager.mqttClient = mqttClient;

        // TODO: Figure out how to properly handle certificate validation from our private CA
        var tlsOptions = new MqttClientTlsOptions()
        {
            UseTls = true,
            AllowUntrustedCertificates = true,
            IgnoreCertificateChainErrors = true,
            IgnoreCertificateRevocationErrors = true,
            CertificateValidationHandler = (x) => true,
        };

        var mqttClientOptions = new MqttClientOptionsBuilder()
            .WithTlsOptions(tlsOptions)
            .WithTcpServer(host: AirshipPlatformUrl.messaging, port: 1883)
            .WithCleanSession(true)
            .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V311)
            .WithWillQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce)
            .WithCredentials($"gameserver:{MessagingManager.serverBootstrap.gameId}:{MessagingManager.serverBootstrap.serverId}", MessagingManager.serverBootstrap.airshipJWT)
            .WithTimeout(TimeSpan.FromSeconds(10))
            .Build();

        disconnectedIntent = false;

        mqttClient.ApplicationMessageReceivedAsync += e =>
        {
            if (disconnectedIntent)
            {
                return Task.CompletedTask;
            }
            if (e.ApplicationMessage == null || string.IsNullOrEmpty(e.ApplicationMessage.Topic))
            {
                Debug.LogError("Received an empty application message or topic.");
                return Task.CompletedTask;
            }

            ParseTopicResponse parseResult = ParseMqttTopic(e.ApplicationMessage.Topic);

            if (!parseResult.isValid)
            {
                Debug.LogError($"Invalid topic, error: {parseResult.errorMessage}");
                return Task.CompletedTask;
            }

            UnityMainThreadDispatcher.Instance.Enqueue(Instance.FireOnEvent(parseResult.topic, e.ApplicationMessage.ConvertPayloadToString()));

            return Task.CompletedTask;
        };

        mqttClient.ConnectedAsync += async e =>
        {
            if (disconnectedIntent)
            {
                return;
            }

            while (pendingSubscriptions.TryTake(out var topic))
            {
                await SubscribeAsync(topic);
            }

            while (queuedOutgoingPackets.TryTake(out var msg))
            {
                await PublishAsync(msg.topic, msg.payload);
            }

            while (pendingUnsubscribes.TryTake(out var topic))
            {
                await UnsubscribeAsync(topic);
            }
        };

        mqttClient.DisconnectedAsync += async e =>
        {
            disconnectedIntent = true;
            Debug.LogWarning($"Disconnected from messaging server {e.ReasonString}");
            UnityMainThreadDispatcher.Instance.Enqueue(Instance.FireOnDisconnect(e.ReasonString));
            MessagingManager.mqttClient = null;
        };


        await mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);

        return true;
    }

    public static async Task Disconnect()
    {
        disconnectedIntent = true;
        if (MessagingManager.mqttClient != null)
        {
            await MessagingManager.mqttClient.DisconnectAsync();
        }

        pendingSubscriptions.Clear();
        pendingUnsubscribes.Clear();
        queuedOutgoingPackets.Clear();
    }

    private void OnDisable()
    {
        if (IsConnected())
        {
            // Fire and forget
            mqttClient.DisconnectAsync();
        }
    }

    private IEnumerator FireOnEvent(TopicDescription topic, string data)
    {
        Instance.OnEvent?.Invoke(topic, data);
        yield return null;
    }

    private IEnumerator FireOnDisconnect(string reason)
    {
        Instance.OnDisconnected?.Invoke(reason);
        yield return null;
    }

    private static string GetFullTopic(TopicDescription topic)
    {
        var gamePrefix = $"org/{MessagingManager.serverBootstrap.organizationId}/game/{MessagingManager.serverBootstrap.gameId}";
        if (topic.scope == Scope.Server)
        {
            return $"{gamePrefix}/server/{MessagingManager.serverBootstrap.serverId}/{topic.topicNamespace}/{topic.topicName}";
        }
        else if (topic.scope == Scope.Game)
        {
            return $"{gamePrefix}/{topic.topicNamespace}/{topic.topicName}";
        }
        else
        {
            // This should not happen, but just in case
            Debug.LogError($"Unknown topic scope: {topic.scope}");
            throw new ArgumentException($"Unknown topic scope: {topic.scope}");
        }
    }

    private static ParseTopicResponse ParseMqttTopic(string topic)
    {
        var topicParts = topic.Split('/');
        if (topicParts.Length < 6)
        {
            return new ParseTopicResponse
            {
                isValid = false,
                errorMessage = $"Invalid topic, parts count ({topicParts.Length}): {topic}"
            };
        }
        if (topicParts[0] != "org" || topicParts[1] != MessagingManager.serverBootstrap.organizationId || topicParts[2] != "game" || topicParts[3] != MessagingManager.serverBootstrap.gameId)
        {
            return new ParseTopicResponse
            {
                isValid = false,
                errorMessage = $"Invalid topic, unknown format: {topic}"
            };
        }

        if (topicParts.Length == 6)
        {
            // This is a game topic
            var topicNamespace = topicParts[4];
            var topicName = topicParts[5];

            return new ParseTopicResponse
            {
                topic = new TopicDescription
                {
                    scope = Scope.Game,
                    topicNamespace = topicNamespace,
                    topicName = topicName,
                },
                isValid = true,
                errorMessage = null
            };
        }
        else if (topicParts.Length == 8)
        {
            if (topicParts[4] != "server" || topicParts[5] != MessagingManager.serverBootstrap.serverId)
            {
                return new ParseTopicResponse
                {
                    isValid = false,
                    errorMessage = $"Invalid topic, unknown format: {topic}"
                };
            }

            // This is a server topic
            var topicNamespace = topicParts[6];
            var topicName = topicParts[7];

            return new ParseTopicResponse
            {
                isValid = true,
                topic = new TopicDescription
                {
                    scope = Scope.Server,
                    topicNamespace = topicNamespace,
                    topicName = topicName,
                },
            };
        }
        else
        {
            return new ParseTopicResponse
            {
                isValid = false,
                errorMessage = $"Invalid topic, parts count ({topicParts.Length}): {topic}"
            };
        }
    }

    public static async Task<bool> SubscribeAsync(TopicDescription topic)
    {
        if (!IsConnected())
        {
            Debug.Log($"Queueing subscribe request {topic.topicNamespace}/{topic.topicName}");
            pendingSubscriptions.Add(topic);
            return true; // Optimistically return true
        }

        var fullTopic = GetFullTopic(topic);
        var mqttSubscribeOptions = mqttFactory.CreateSubscribeOptionsBuilder().WithTopicFilter(topic: fullTopic, qualityOfServiceLevel: MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce).Build();
        var res = await MessagingManager.mqttClient.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None);
        var res0 = res.Items.ToArray()[0];
        if (res0.ResultCode != MqttClientSubscribeResultCode.GrantedQoS0)
        {
            Debug.LogError($"Failed to subscribe to {fullTopic}: {res0.ResultCode}");
            return false;
        }

        return true;
    }

    public static async Task<bool> UnsubscribeAsync(TopicDescription topic)
    {
        if (!IsConnected())
        {
            Debug.Log($"Queueing unsubscribe request {topic.topicNamespace}/{topic.topicName}");
            pendingUnsubscribes.Add(topic);
            return true; // Optimistically return true
        }

        var fullTopic = GetFullTopic(topic);
        var mqttUnsubscribeOptions = mqttFactory.CreateUnsubscribeOptionsBuilder().WithTopicFilter(topic: fullTopic).Build();
        var res = await MessagingManager.mqttClient.UnsubscribeAsync(mqttUnsubscribeOptions, CancellationToken.None);
        var res0 = res.Items.ToArray()[0];
        if (res0.ResultCode == MqttClientUnsubscribeResultCode.NoSubscriptionExisted || res0.ResultCode == MqttClientUnsubscribeResultCode.Success)
        {
            // No subscription existed or successfully unsubscribed
            return true;
        }

        Debug.LogError($"Failed to subscribe to {fullTopic}: {res0.ResultCode}");
        return false;
    }

    public static async Task<bool> PublishAsync(TopicDescription topic, string data)
    {
        if (!IsConnected())
        {
            Debug.Log($"Queueing publish request {topic.topicNamespace}/{topic.topicName}");
            MessagingManager.queuedOutgoingPackets.Add(new PubSubMessage
            {
                topic = topic,
                payload = data,
            });
            return true; // Optimistically return true
        }

        var fullTopic = GetFullTopic(topic);
        var applicationMessage = new MqttApplicationMessageBuilder()
                .WithTopic(fullTopic)
                .WithPayload(data)
                .Build();

        var res = await MessagingManager.mqttClient.PublishAsync(applicationMessage, CancellationToken.None);
        if (!res.IsSuccess)
        {
            Debug.LogError($"Failed to publish to {fullTopic}: {res.ReasonCode}");
            return false;
        }

        return true;
    }

    public static bool IsConnected()
    {
        return !disconnectedIntent && MessagingManager.mqttClient != null && MessagingManager.mqttClient.IsConnected;
    }
}