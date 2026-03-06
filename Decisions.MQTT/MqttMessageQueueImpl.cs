using System;
using System.Text;
using DecisionsFramework;
using DecisionsFramework.Data.Messaging;
using DecisionsFramework.Data.Messaging.Implementations;
using Decisions.MessageQueues;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace Decisions.MqttMessageQueue
{
    public class MqttMessageQueueImpl : SimpleMessageQueueImpl<MqttMessageQueue>
    {
        private static readonly Log Log = new Log("MQTT");

        protected override IMqThreadManager CreateThreadManager()
            => new BaseMqThreadManager<MqttThreadJob, MqttMessageQueue>("MQTT", QueueDefinition);

        protected override string QueueTypeName => "MQTT";

        public MqttMessageQueueImpl(MqttMessageQueue queueDef) : base(queueDef) { }

        public override MessageQueueMessage GetMessage(string handlerId, MessageFetchType messageFetchType, bool blockForMessage)
        {
            Log.Warn("GetMessage is not supported for MQTT — MQTT is push-based");
            return null;
        }

        public override long? GetMessageCount()
        {
            Log.Warn("GetMessageCount is not supported for MQTT");
            return null;
        }

        public override long MessageCount
            => throw new NotSupportedException("Message count is not available for MQTT");

        public override string GetAlternativeTestQueueResult()
        {
            var factory = new MqttFactory();
            using var client = factory.CreateMqttClient();
            try
            {
                string testClientId = $"decisions-mqtt-test-{Guid.NewGuid():N}";
                var options = MqttUtils.BuildClientOptions(QueueDefinition, testClientId, persistentSession: false);
                client.ConnectAsync(options).GetAwaiter().GetResult();
                client.DisconnectAsync().GetAwaiter().GetResult();
                return $"Successfully connected to MQTT broker for '{QueueDefinition.DisplayName}'";
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to connect to MQTT broker for '{QueueDefinition.DisplayName}'");
                return $"Could not connect to MQTT broker: {ex.Message}";
            }
        }

        public override void PushMessage(string id, byte[] message)
        {
            string topic = QueueDefinition.Topic;
            if (topic.Contains('#') || topic.Contains('+'))
                throw new InvalidOperationException(
                    $"Cannot publish to wildcard topic '{topic}'. Use a specific topic for publishing.");

            var factory = new MqttFactory();
            using var client = factory.CreateMqttClient();
            try
            {
                string publishClientId = $"decisions-mqtt-pub-{Guid.NewGuid():N}";
                var options = MqttUtils.BuildClientOptions(QueueDefinition, publishClientId, persistentSession: false);
                client.ConnectAsync(options).GetAwaiter().GetResult();

                var mqttMessage = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(message)
                    .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)QueueDefinition.GetQosInt())
                    .Build();

                client.PublishAsync(mqttMessage).GetAwaiter().GetResult();
                client.DisconnectAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to publish MQTT message to topic '{topic}'");
                try { client.DisconnectAsync().GetAwaiter().GetResult(); } catch { }
                throw;
            }
        }
    }
}
