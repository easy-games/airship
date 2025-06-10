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

record PubSubMessage {
    public string topicNamespace { get; set; }
    public string topicName { get; set; }
    public string payload { get; set; }
}

[LuauAPI(LuauContext.Protected)]
public class MessagingManager : Singleton<MessagingManager>
{
    private static List<PubSubMessage> queuedOutgoingPackets = new();
    private static List<(string topicNamespace, string topicName)> pendingSubscriptions = new();
    public event Action<string, string, string> OnEvent;
    public event Action<string> OnDisconnected;

    private static IMqttClient mqttClient;

    private static ServerBootstrap serverBootstrap;

    private void Awake()
    {
        DontDestroyOnLoad(this);
    }
    
    private void Start() {
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

        var mqttFactory = new MqttFactory();
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

        var disconnected = false;

        mqttClient.ApplicationMessageReceivedAsync += e =>
        {
            if (disconnected)
            {
                return Task.CompletedTask;
            }
            var topicParts = e.ApplicationMessage.Topic.Split('/');
            if (topicParts.Length != 6)
            {
                Debug.LogError($"Invalid topic, parts count ({topicParts.Length}): {e.ApplicationMessage.Topic}");
                return Task.CompletedTask;
            }
            if (topicParts[0] != "org" || topicParts[1] != MessagingManager.serverBootstrap.organizationId || topicParts[2] != "game" || topicParts[3] != MessagingManager.serverBootstrap.gameId)
            {
                Debug.LogError($"Invalid topic, unknown format: {e.ApplicationMessage.Topic}");
                return Task.CompletedTask;
            }
            var topicNamespace = topicParts[4];
            var topicName = topicParts[5];

            UnityMainThreadDispatcher.Instance.Enqueue(Instance.FireOnEvent(topicNamespace, topicName, e.ApplicationMessage.ConvertPayloadToString()));

            return Task.CompletedTask;
        };

        mqttClient.ConnectedAsync += async e => {
            if (disconnected)
            {
                return;
            }
            var toSubscribe = pendingSubscriptions;
            pendingSubscriptions = new List<(string topicNamespace, string topicName)>();
            foreach (var (topicNamespace, topicName) in toSubscribe)
            {
                await SubscribeAsync(topicNamespace, topicName);
            }

            var toSend = queuedOutgoingPackets;
            queuedOutgoingPackets = new List<PubSubMessage>();
            foreach (var msg in toSend)
            {
                await PublishAsync(msg.topicNamespace, msg.topicName, msg.payload);
            }
        };

        mqttClient.DisconnectedAsync += async e =>
        {
            if (disconnected)
            {
                return;
            }
            Debug.LogWarning($"Disconnected from messaging server {e.ReasonString}");
            UnityMainThreadDispatcher.Instance.Enqueue(Instance.FireOnDisconnect(e.ReasonString));
            MessagingManager.mqttClient = null;
            disconnected = true;
        };


        await mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);

        return true;
    }

    public static async Task Disconnect()
    {
        if (MessagingManager.mqttClient != null)
        {
            MessagingManager.mqttClient = null;
            await MessagingManager.mqttClient.DisconnectAsync();
        }
        pendingSubscriptions.Clear();
    }

    private async void OnDisable()
    {
        await mqttClient.DisconnectAsync();
    }

    private IEnumerator FireOnEvent(string topicNamespace, string topicName, string data)
    {
        Instance.OnEvent?.Invoke(topicNamespace, topicName, data);
        yield return null;
    }

    private IEnumerator FireOnDisconnect(string reason)
    {
        Instance.OnDisconnected?.Invoke(reason);
        yield return null;
    }

    public static async Task<bool> SubscribeAsync(string topicNamespace, string topicName)
    {
        if (!IsConnected())
        {
            Debug.Log($"Queueing subscribe request {topicNamespace}/{topicName}");
            pendingSubscriptions.Add((topicNamespace, topicName));
            return true; // Optimistically return true
        }

        var fullTopic = $"org/{MessagingManager.serverBootstrap.organizationId}/game/{MessagingManager.serverBootstrap.gameId}/{topicNamespace}/{topicName}";
        var mqttFactory = new MqttFactory();
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

    public static async Task<bool> PublishAsync(string topicNamespace, string topicName, string data)
    {
        if (!IsConnected())
        {
            Debug.Log($"Queueing publish request {topicNamespace}/{topicName}");
            MessagingManager.queuedOutgoingPackets.Add(new PubSubMessage
            {
                topicNamespace = topicNamespace,
                topicName = topicName,
                payload = data,
            });
            return true; // Optimistically return true
        }

        var fullTopic = $"org/{MessagingManager.serverBootstrap.organizationId}/game/{MessagingManager.serverBootstrap.gameId}/{topicNamespace}/{topicName}";
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
        return MessagingManager.mqttClient != null && MessagingManager.mqttClient.IsConnected;
    }
}