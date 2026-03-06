using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DecisionsFramework;
using DecisionsFramework.Design.Flow.Mapping;
using DecisionsFramework.ServiceLayer.Services.ContextData;
using Decisions.MessageQueues;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Formatter;
using MQTTnet.Protocol;

namespace Decisions.MqttMessageQueue
{
    public class MqttThreadJob : BaseMqThreadJob<MqttMessageQueue>
    {
        public override string LogCategory => "MQTT Flow Worker";

        private static readonly TimeSpan LeaseRenewalInterval = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan StandbyPollInterval = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan MessageReceiveTimeout = TimeSpan.FromSeconds(1);

        // Stable unique suffix for this process instance — ensures each Decisions process
        // gets a distinct client ID in shared subscription mode, even on the same machine.
        private static readonly string ProcessSuffix = $"{Environment.MachineName}-{Guid.NewGuid():N}";

        private string threadId;
        private bool isLeaseHolder;
        private IMqttClient mqttClient;
        private BlockingCollection<MqttApplicationMessage> incomingMessages;
        private DateTime lastLeaseRenewal = DateTime.MinValue;
        private volatile bool connectionLost;

        // When a Shared Subscription Group is configured all cluster nodes connect simultaneously
        // and the broker distributes messages between them. The lease mechanism is not used in
        // this mode since there is no risk of duplicate processing.
        private bool IsSharedSubscriptionMode =>
            !string.IsNullOrEmpty(queueDefinition.SharedSubscriptionGroup);

        protected override void SetUp()
        {
            threadId = $"{Environment.MachineName}-{Guid.NewGuid()}";
            incomingMessages = new BlockingCollection<MqttApplicationMessage>(queueDefinition.MessageBufferSize);

            if (IsSharedSubscriptionMode)
            {
                // All nodes participate — connect directly without lease
                isLeaseHolder = true;
                ConnectAndSubscribe();
                log.Info($"[MQTT] Thread {threadId} connected in shared subscription mode for queue '{queueDefinition.DisplayName}'");
            }
            else
            {
                isLeaseHolder = MqttLeaseManager.AcquireLease(queueDefinition.Id, threadId);

                if (isLeaseHolder)
                {
                    ConnectAndSubscribe();
                    lastLeaseRenewal = DateTime.UtcNow;
                    log.Info($"[MQTT] Thread {threadId} is active for queue '{queueDefinition.DisplayName}'");
                }
                else
                {
                    log.Info($"[MQTT] Thread {threadId} is in standby for queue '{queueDefinition.DisplayName}'");
                }
            }
        }

        protected override void ReceiveMessages()
        {
            if (isLeaseHolder && connectionLost)
                throw new InvalidOperationException(
                    $"[MQTT] Lost connection to broker for queue '{queueDefinition.DisplayName}'. Decisions will restart this thread.");

            if (isLeaseHolder)
            {
                // In lease mode: renew the lease periodically
                if (!IsSharedSubscriptionMode && DateTime.UtcNow - lastLeaseRenewal >= LeaseRenewalInterval)
                {
                    if (!MqttLeaseManager.AcquireLease(queueDefinition.Id, threadId))
                    {
                        log.Warn($"[MQTT] Thread {threadId} lost lease for queue '{queueDefinition.DisplayName}', going to standby");
                        Disconnect();
                        isLeaseHolder = false;
                        return;
                    }
                    lastLeaseRenewal = DateTime.UtcNow;
                }

                // Process one incoming message (blocks up to MessageReceiveTimeout)
                if (incomingMessages.TryTake(out var message, MessageReceiveTimeout))
                {
                    IsActive = true;

                    byte[] payload = message.PayloadSegment.ToArray();
                    string payloadText = Encoding.UTF8.GetString(payload);
                    string messageId = Guid.NewGuid().ToString();

                    var headers = new List<DataPair>
                    {
                        new DataPair("Topic", message.Topic),
                        new DataPair("QoS", ((int)message.QualityOfServiceLevel).ToString()),
                        new DataPair("Retain", message.Retain.ToString())
                    };

                    // MQTT 5: forward User Properties as headers (prefix "UserProp.")
                    if (MqttUtils.GetProtocolVersion(queueDefinition) == MqttProtocolVersion.V500
                        && message.UserProperties != null)
                    {
                        foreach (var prop in message.UserProperties)
                            headers.Add(new DataPair($"UserProp.{prop.Name}", prop.Value));
                    }

                    ProcessMessage(messageId, payload, headers, null, null, payloadText);
                }
                else
                {
                    IsActive = false;
                }
            }
            else
            {
                // Standby (lease mode only): wait, then attempt to acquire the lease
                Thread.Sleep(StandbyPollInterval);

                isLeaseHolder = MqttLeaseManager.AcquireLease(queueDefinition.Id, threadId);
                if (isLeaseHolder)
                {
                    // Drain any stale items before connecting
                    while (incomingMessages.TryTake(out _)) { }

                    ConnectAndSubscribe();
                    lastLeaseRenewal = DateTime.UtcNow;
                    log.Info($"[MQTT] Thread {threadId} acquired lease for queue '{queueDefinition.DisplayName}'");
                }
            }
        }

        protected override void CleanUp()
        {
            if (isLeaseHolder)
            {
                Disconnect();
                if (!IsSharedSubscriptionMode)
                    MqttLeaseManager.ReleaseLease(queueDefinition.Id, threadId);
            }
            incomingMessages?.Dispose();
            incomingMessages = null;
        }

        private void ConnectAndSubscribe()
        {
            var factory = new MqttFactory();
            mqttClient = factory.CreateMqttClient();

            mqttClient.ApplicationMessageReceivedAsync += e =>
            {
                if (incomingMessages != null && !incomingMessages.TryAdd(e.ApplicationMessage))
                    log.Warn($"[MQTT] Message buffer full for queue '{queueDefinition.DisplayName}' — incoming message dropped. Consider increasing Message Buffer Size.");
                return Task.CompletedTask;
            };

            mqttClient.DisconnectedAsync += e =>
            {
                if (e.ClientWasConnected)
                {
                    log.Warn($"[MQTT] Connection lost for queue '{queueDefinition.DisplayName}': {e.Exception?.Message ?? "unknown reason"}");
                    connectionLost = true;
                }
                return Task.CompletedTask;
            };

            // In shared subscription mode each node must have a unique client ID.
            // In lease mode only one node connects so the shared queue client ID is fine.
            string clientId = IsSharedSubscriptionMode
                ? $"{MqttUtils.GetClientId(queueDefinition)}-{ProcessSuffix}"
                : MqttUtils.GetClientId(queueDefinition);

            // Persistent sessions make less sense in shared subscription mode: if a node
            // reconnects the broker would re-deliver its unacked messages to that specific node
            // rather than redistributing them. Use clean sessions for shared subscriptions.
            bool persistentSession = IsSharedSubscriptionMode
                ? false
                : MqttUtils.GetPersistentSession(queueDefinition);
            var options = MqttUtils.BuildClientOptions(queueDefinition, clientId, persistentSession);

            mqttClient.ConnectAsync(options).GetAwaiter().GetResult();

            string topicToSubscribe = !string.IsNullOrEmpty(queueDefinition.SharedSubscriptionGroup)
                ? $"$share/{queueDefinition.SharedSubscriptionGroup}/{queueDefinition.Topic}"
                : queueDefinition.Topic;

            var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(f => f
                    .WithTopic(topicToSubscribe)
                    .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)queueDefinition.GetQosInt()))
                .Build();

            try
            {
                mqttClient.SubscribeAsync(subscribeOptions).GetAwaiter().GetResult();
            }
            catch
            {
                Disconnect();
                throw;
            }

            log.Info($"[MQTT] Connected and subscribed to '{topicToSubscribe}' (QoS {queueDefinition.GetQosInt()})");
        }

        private void Disconnect()
        {
            var client = mqttClient;
            mqttClient = null;
            try
            {
                if (client?.IsConnected == true)
                    client.DisconnectAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                log.Warn($"[MQTT] Error disconnecting: {ex.Message}");
            }
            finally
            {
                client?.Dispose();
            }
        }
    }
}
