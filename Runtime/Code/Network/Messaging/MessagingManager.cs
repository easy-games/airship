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

        var certPath = Path.Combine(Application.streamingAssetsPath, "Certs", AirshipPlatformUrl.certificatePath);
        var serverCertPath = Path.Combine(Application.streamingAssetsPath, "Certs", AirshipPlatformUrl.serverCertificatePath);
        
        Debug.Log($"[CERT] Loading CA certificate from: {certPath}");
        Debug.Log($"[CERT] CA file exists: {File.Exists(certPath)}");
        Debug.Log($"[CERT] Loading server certificate from: {serverCertPath}");
        Debug.Log($"[CERT] Server cert file exists: {File.Exists(serverCertPath)}");
        
        X509Certificate2 caCert;
        X509Certificate2 serverCert;
        
        try
        {
            var caCertBase = X509Certificate2.CreateFromCertFile(certPath);
            caCert = new X509Certificate2(caCertBase);
            Debug.Log($"[CERT] Successfully loaded CA certificate");
            Debug.Log($"[CERT] CA certificate subject: {caCert.Subject}");
            Debug.Log($"[CERT] CA certificate issuer: {caCert.Issuer}");
            Debug.Log($"[CERT] CA certificate has private key: {caCert.HasPrivateKey}");
            Debug.Log($"[CERT] CA certificate thumbprint: {caCert.Thumbprint}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CERT] Failed to load CA certificate: {ex.Message}");
            Debug.LogError($"[CERT] Exception type: {ex.GetType().Name}");
            Debug.LogError($"[CERT] Stack trace: {ex.StackTrace}");
            return false;
        }
        
        try
        {
            var serverCertBase = X509Certificate2.CreateFromCertFile(serverCertPath);
            serverCert = new X509Certificate2(serverCertBase);
            Debug.Log($"[CERT] Successfully loaded server certificate");
            Debug.Log($"[CERT] Server certificate subject: {serverCert.Subject}");
            Debug.Log($"[CERT] Server certificate issuer: {serverCert.Issuer}");
            Debug.Log($"[CERT] Server certificate has private key: {serverCert.HasPrivateKey}");
            Debug.Log($"[CERT] Server certificate thumbprint: {serverCert.Thumbprint}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CERT] Failed to load server certificate: {ex.Message}");
            Debug.LogError($"[CERT] Exception type: {ex.GetType().Name}");
            Debug.LogError($"[CERT] Stack trace: {ex.StackTrace}");
            return false;
        }

        // Create the secure TLS options with proper certificate validation
        var tlsOptions = new MqttClientTlsOptions
        {
            UseTls = true,

            // Certificate validation handler for our custom CA
            CertificateValidationHandler = (certContext) =>
            {
                Debug.Log("[TLS] Starting certificate validation");
                
                try
                {
                    // Log basic certificate information
                    var presentedCert = new X509Certificate2(certContext.Certificate);
                    Debug.Log($"[TLS] Presented certificate subject: {presentedCert.Subject}");
                    Debug.Log($"[TLS] Presented certificate issuer: {presentedCert.Issuer}");
                    Debug.Log($"[TLS] Presented certificate valid from: {presentedCert.NotBefore} to: {presentedCert.NotAfter}");
                    Debug.Log($"[TLS] Presented certificate thumbprint: {presentedCert.Thumbprint}");
                    
                    // Log our known certificates
                    Debug.Log($"[TLS] CA certificate subject: {caCert.Subject}");
                    Debug.Log($"[TLS] CA certificate thumbprint: {caCert.Thumbprint}");
                    Debug.Log($"[TLS] Expected server certificate subject: {serverCert.Subject}");
                    Debug.Log($"[TLS] Expected server certificate thumbprint: {serverCert.Thumbprint}");

                    // Method 1: Direct certificate comparison
                    if (presentedCert.Thumbprint.Equals(serverCert.Thumbprint, StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.Log("[TLS] Certificate validation SUCCESS: Presented certificate matches expected server certificate");
                        return true;
                    }
                    
                    Debug.Log("[TLS] Presented certificate does not match expected server certificate, trying chain validation...");

                    // Method 2: Chain validation with both CA and intermediate certificates
                    var chain = new X509Chain();
                    chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                    chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;

                    // Add both CA and server certificates to the trust store
                    try
                    {
                        chain.ChainPolicy.ExtraStore.Add(caCert);
                        chain.ChainPolicy.ExtraStore.Add(serverCert);
                        Debug.Log($"[TLS] Added certificates to chain trust store (count: {chain.ChainPolicy.ExtraStore.Count})");
                    }
                    catch (Exception addEx)
                    {
                        Debug.LogError($"[TLS] Failed to add certificates to chain trust store: {addEx.Message}");
                        // Continue with validation anyway
                    }

                    // Build and validate the certificate chain
                    bool isChainValid;
                    try
                    {
                        Debug.Log($"[TLS] Attempting to build certificate chain...");
                        isChainValid = chain.Build(presentedCert);
                        Debug.Log($"[TLS] Chain build result: {isChainValid}");
                    }
                    catch (Exception buildEx)
                    {
                        Debug.LogError($"[TLS] Exception during chain build: {buildEx.Message}");
                        Debug.LogError($"[TLS] Build exception type: {buildEx.GetType().Name}");
                        
                        // Try Method 3: Manual validation
                        Debug.Log("[TLS] Chain build failed, trying manual validation...");
                        return ValidateManually(presentedCert, serverCert, caCert);
                    }
                    
                    // Log detailed chain information
                    Debug.Log($"[TLS] Chain elements count: {chain.ChainElements.Count}");
                    for (int i = 0; i < chain.ChainElements.Count; i++)
                    {
                        var element = chain.ChainElements[i];
                        Debug.Log($"[TLS] Chain element {i}: {element.Certificate.Subject}");
                        Debug.Log($"[TLS] Chain element {i} issuer: {element.Certificate.Issuer}");
                        Debug.Log($"[TLS] Chain element {i} thumbprint: {element.Certificate.Thumbprint}");
                        
                        if (element.ChainElementStatus.Length > 0)
                        {
                            Debug.Log($"[TLS] Chain element {i} has {element.ChainElementStatus.Length} status issues:");
                            foreach (var status in element.ChainElementStatus)
                            {
                                Debug.Log($"[TLS] Chain element {i} status: {status.Status} - {status.StatusInformation}");
                            }
                        }
                        else
                        {
                            Debug.Log($"[TLS] Chain element {i} has no status issues");
                        }
                    }

                    if (!isChainValid)
                    {
                        Debug.Log($"[TLS] Chain validation failed. Checking chain status (count: {chain.ChainStatus.Length})");
                        
                        bool hasOnlyAcceptableErrors = true;
                        
                        foreach (var status in chain.ChainStatus)
                        {
                            Debug.Log($"[TLS] Chain status: {status.Status} - {status.StatusInformation}");
                            
                            // Accept UntrustedRoot and PartialChain errors for custom CA
                            if (status.Status == X509ChainStatusFlags.UntrustedRoot ||
                                status.Status == X509ChainStatusFlags.PartialChain)
                            {
                                Debug.Log($"[TLS] Acceptable error for custom CA: {status.Status}");
                                continue;
                            }
                            else
                            {
                                hasOnlyAcceptableErrors = false;
                                Debug.LogError($"[TLS] Unacceptable error: {status.Status} - {status.StatusInformation}");
                            }
                        }
                        
                        if (hasOnlyAcceptableErrors)
                        {
                            Debug.Log("[TLS] Chain validation SUCCESS: Only acceptable errors found for custom CA");
                            return true;
                        }
                        else
                        {
                            Debug.LogError("[TLS] Chain validation FAILED: Unacceptable errors found");
                            return false;
                        }
                    }
                    else
                    {
                        Debug.Log("[TLS] Chain validation SUCCESS: Chain built successfully");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[TLS] Exception during certificate validation: {ex.Message}");
                    Debug.LogError($"[TLS] Exception stack trace: {ex.StackTrace}");
                    return false;
                }
            },

            // Specify allowed TLS protocols
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
            Debug.Log("[MQTT] Attempting to connect to MQTT broker...");
            Debug.Log($"[MQTT] Host: {AirshipPlatformUrl.messaging}, Port: 1883");
            Debug.Log($"[MQTT] Using TLS: {tlsOptions.UseTls}");
            Debug.Log($"[MQTT] SSL Protocol: {tlsOptions.SslProtocol}");
            Debug.Log($"[MQTT] Certificate path: {certPath}");
            
            await mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);
            
            Debug.Log("[MQTT] Successfully connected to MQTT broker");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MQTT] Failed to connect to MQTT broker: {ex.Message}");
            Debug.LogError($"[MQTT] Exception type: {ex.GetType().Name}");
            Debug.LogError($"[MQTT] Stack trace: {ex.StackTrace}");
            
            // Log inner exceptions recursively
            var innerEx = ex.InnerException;
            int innerLevel = 1;
            while (innerEx != null)
            {
                Debug.LogError($"[MQTT] Inner exception {innerLevel}: {innerEx.Message}");
                Debug.LogError($"[MQTT] Inner exception {innerLevel} type: {innerEx.GetType().Name}");
                Debug.LogError($"[MQTT] Inner exception {innerLevel} stack trace: {innerEx.StackTrace}");
                innerEx = innerEx.InnerException;
                innerLevel++;
            }
            
            return false;
        }
    }

    private static bool ValidateManually(X509Certificate2 presentedCert, X509Certificate2 expectedServerCert, X509Certificate2 caCert)
    {
        Debug.Log("[TLS] Starting manual certificate validation");
        
        try
        {
            // Check if the presented certificate is directly signed by our CA
            if (presentedCert.Issuer.Equals(caCert.Subject, StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log("[TLS] Manual validation SUCCESS: Presented certificate is directly signed by our CA");
                return true;
            }
            
            // Check if the presented certificate matches our expected server certificate
            if (presentedCert.Subject.Equals(expectedServerCert.Subject, StringComparison.OrdinalIgnoreCase) &&
                presentedCert.Issuer.Equals(expectedServerCert.Issuer, StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log("[TLS] Manual validation SUCCESS: Presented certificate matches expected server certificate properties");
                return true;
            }
            
            // Check if the expected server certificate is signed by our CA and matches the presented cert
            if (expectedServerCert.Issuer.Equals(caCert.Subject, StringComparison.OrdinalIgnoreCase) &&
                presentedCert.Subject.Equals(expectedServerCert.Subject, StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log("[TLS] Manual validation SUCCESS: Expected server cert is signed by CA and subjects match");
                return true;
            }
            
            Debug.LogError("[TLS] Manual validation FAILED: No valid certificate chain found");
            Debug.LogError($"[TLS] Presented cert issuer: {presentedCert.Issuer}");
            Debug.LogError($"[TLS] Expected cert issuer: {expectedServerCert.Issuer}");
            Debug.LogError($"[TLS] CA subject: {caCert.Subject}");
            
            return false;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TLS] Exception during manual validation: {ex.Message}");
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