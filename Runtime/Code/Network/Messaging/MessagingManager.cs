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
using Code.Platform.Shared;
using System.Security.Cryptography.X509Certificates;
using System.IO;

class PubSubMessage
{
    public TopicDescription topic { get; set; }
    public string payload { get; set; }
}

public enum Scope
{
    Game = 0,
    Server = 1,
}

public class TopicDescription
{
    public Scope scope { get; set; }
    public string topicNamespace { get; set; }
    public string topicName { get; set; }
}

public class ParseTopicResponse
{
    public TopicDescription topic { get; set; }
    public bool isValid { get; set; }
    public string errorMessage { get; set; }
}

[LuauAPI(LuauContext.Protected)]
public class MessagingManager : Singleton<MessagingManager>
{
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

        // Cert files are located in airship-player/Assets/StreamingAssets/Certs/
        var certPath = Path.Combine(Application.streamingAssetsPath, "Certs", AirshipPlatformUrl.certificatePath);
        X509Certificate2 caCert;
        
        try
        {
            var caCertBase = X509Certificate2.CreateFromCertFile(certPath);
            caCert = new X509Certificate2(caCertBase);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to load CA certificate from {certPath}: {ex.Message}");
            return false;
        }

        var tlsOptions = new MqttClientTlsOptions
        {
            UseTls = true,
            CertificateValidationHandler = (certContext) =>
            {
                try
                {
                    var presentedCert = new X509Certificate2(certContext.Certificate);

                    // Chain validation with custom CA
                    var chain = new X509Chain();
                    chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                    chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;

                    try
                    {
                        chain.ChainPolicy.ExtraStore.Add(caCert);
                    }
                    catch (Exception addEx)
                    {
                        Debug.LogError($"TLS certificate validation failed to add CA certificate to trust store: {addEx.Message}");
                    }

                    bool isChainValid;
                    try
                    {
                        isChainValid = chain.Build(presentedCert);
                    }
                    catch (Exception buildEx)
                    {
                        Debug.LogError($"TLS chain build failed: {buildEx.Message}");
                        return false;
                    }

                    if (!isChainValid)
                    {
                        bool hasOnlyAcceptableErrors = true;
                        
                        foreach (var status in chain.ChainStatus)
                        {
                            if (status.Status != X509ChainStatusFlags.UntrustedRoot &&
                                status.Status != X509ChainStatusFlags.PartialChain)
                            {
                                hasOnlyAcceptableErrors = false;
                                Debug.LogError($"TLS validation failed with unacceptable error: {status.Status} - {status.StatusInformation}");
                            }
                        }
                        
                        return hasOnlyAcceptableErrors;
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"TLS certificate validation exception: {ex.Message}");
                    return false;
                }
            },
            SslProtocol = System.Security.Authentication.SslProtocols.Tls12
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
        };

        mqttClient.DisconnectedAsync += async e =>
        {
            disconnectedIntent = true;
            UnityMainThreadDispatcher.Instance.Enqueue(Instance.FireOnDisconnect(e.ReasonString));
            MessagingManager.mqttClient = null;
        };

        try
        {
            await mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"MQTT connection failed: {ex.Message}");
            
            // Log inner exceptions for debugging
            var innerEx = ex.InnerException;
            int innerLevel = 1;
            while (innerEx != null)
            {
                Debug.LogError($"MQTT inner exception {innerLevel}: {innerEx.Message}");
                innerEx = innerEx.InnerException;
                innerLevel++;
            }
            
            return false;
        }
    }


    public static async Task Disconnect()
    {
        disconnectedIntent = true;
        if (MessagingManager.mqttClient != null)
        {
            await MessagingManager.mqttClient.DisconnectAsync();
        }
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

    public static async Task<bool> SubscribeAsync(Scope scope, string topicNamespace, string topicName)
    {
        if (!IsConnected())
        {
            return false;
        }

        var topic = new TopicDescription
        {
            scope = scope,
            topicNamespace = topicNamespace,
            topicName = topicName,
        };

        var fullTopic = GetFullTopic(topic);
        var mqttSubscribeOptions = mqttFactory.CreateSubscribeOptionsBuilder().WithTopicFilter(topic: fullTopic, qualityOfServiceLevel: MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce).Build();
        MqttClientSubscribeResult res;
        try
        {
            res = await MessagingManager.mqttClient.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception while subscribing to {fullTopic}: {ex.Message}");
            return false;
        }

        var res0 = res.Items.ToArray()[0];
        if (res0.ResultCode != MqttClientSubscribeResultCode.GrantedQoS0)
        {
            Debug.LogError($"Failed to subscribe to {fullTopic}: {res0.ResultCode}");
            return false;
        }

        return true;
    }

    public static async Task<bool> UnsubscribeAsync(Scope scope, string topicNamespace, string topicName)
    {
        if (!IsConnected())
        {
            return false;
        }

        var topic = new TopicDescription
        {
            scope = scope,
            topicNamespace = topicNamespace,
            topicName = topicName,
        };

        var fullTopic = GetFullTopic(topic);
        var mqttUnsubscribeOptions = mqttFactory.CreateUnsubscribeOptionsBuilder().WithTopicFilter(topic: fullTopic).Build();
        MqttClientUnsubscribeResult res;
        try
        {
            res = await MessagingManager.mqttClient.UnsubscribeAsync(mqttUnsubscribeOptions, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception while unsubscribing to {fullTopic}: {ex.Message}");
            return false;
        }

        var res0 = res.Items.ToArray()[0];
        if (res0.ResultCode == MqttClientUnsubscribeResultCode.NoSubscriptionExisted || res0.ResultCode == MqttClientUnsubscribeResultCode.Success)
        {
            // No subscription existed or successfully unsubscribed
            return true;
        }

        Debug.LogError($"Failed to unsubscribe to {fullTopic}: {res0.ResultCode}");
        return false;
    }

    public static async Task<bool> PublishAsync(Scope scope, string topicNamespace, string topicName, string data)
    {
        if (!IsConnected())
        {
            return false;
        }

        var topic = new TopicDescription
        {
            scope = scope,
            topicNamespace = topicNamespace,
            topicName = topicName,
        };

        var fullTopic = GetFullTopic(topic);
        var applicationMessage = new MqttApplicationMessageBuilder()
                .WithTopic(fullTopic)
                .WithPayload(data)
                .Build();

        MqttClientPublishResult res;
        try
        {
            res = await MessagingManager.mqttClient.PublishAsync(applicationMessage, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception while publishing to {fullTopic}: {ex.Message}");
            return false;
        }

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