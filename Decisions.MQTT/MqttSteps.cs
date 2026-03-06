using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DecisionsFramework;
using DecisionsFramework.Design.ConfigurationStorage.Attributes;
using DecisionsFramework.ServiceLayer.Services.Projects.Settings;
using DecisionsFramework.Design.Flow;
using DecisionsFramework.Design.Flow.CoreSteps;
using DecisionsFramework.Design.Flow.Mapping;
using DecisionsFramework.Design.Properties;
using DecisionsFramework.Design.Properties.Attributes;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace Decisions.MqttMessageQueue
{
    /// <summary>
    /// Base class for all MQTT flow steps. Provides a queue selection dropdown
    /// that lists all configured MQTT queues.
    /// </summary>
    [Writable]
    public abstract class BaseMqttStep : BaseFlowAwareStep
    {
        protected static readonly Log Log = new Log("MQTT Step");

        [WritableValue]
        protected string queueName;

        [SelectStringEditor(nameof(QueueNames))]
        public string QueueName
        {
            get { return queueName; }
            set { queueName = value; }
        }

        [PropertyHidden]
        public string[] QueueNames
        {
            get
            {
                var queues = MqttUtils.GetSettings(Flow?.GetProjectId())?.Queues;
                if (queues == null || queues.Length == 0)
                    return new[] { "-- Configure queues in Jobs & Events" };
                return queues.Select(q => q.DisplayName).ToArray();
            }
            set { }
        }

        protected MqttMessageQueue GetQueue()
        {
            var queues = MqttUtils.GetSettings(Flow?.GetProjectId())?.Queues;
            if (queues == null) return null;
            var matches = queues.Where(q => q.DisplayName == queueName).ToArray();
            if (matches.Length > 1)
                Log.Warn($"[MQTT] Multiple queues found with name '{queueName}' — using the first match.");
            return matches.FirstOrDefault();
        }
    }

    /// <summary>
    /// Publishes a text message to an MQTT topic. Uses the selected queue's
    /// connection settings. The topic and QoS can be overridden per step or
    /// per invocation.
    /// </summary>
    [Writable]
    [ValidationRules]
    [AutoRegisterStep("Publish MQTT Message", "Integration/MQTT")]
    public class PublishMqttMessage : BaseMqttStep, ISyncStep, IDataConsumer, IDefaultInputMappingStep, IValidationSource
    {
        private const string INPUT_MESSAGE = "Message";
        private const string INPUT_TOPIC_OVERRIDE = "Topic Override";
        private const string INPUT_RETAIN = "Retain";

        // --- Step-level configuration ---

        [WritableValue]
        private string qosOverride = "Use Queue Default";

        [SelectStringEditor(nameof(QosOverrideOptions))]
        public string QosOverride
        {
            get { return qosOverride; }
            set { qosOverride = value; }
        }

        [PropertyHidden]
        public string[] QosOverrideOptions => new[]
        {
            "Use Queue Default",
            "0 - At Most Once",
            "1 - At Least Once",
            "2 - Exactly Once"
        };

        // --- Flow interfaces ---

        public IInputMapping[] DefaultInputs => null;

        public DataDescription[] InputData => new DataDescription[]
        {
            new DataDescription(typeof(string), INPUT_MESSAGE),
            new DataDescription(new DecisionsNativeType(typeof(string)), INPUT_TOPIC_OVERRIDE, false, true, false),
            new DataDescription(new DecisionsNativeType(typeof(bool)), INPUT_RETAIN, false, true, false)
        };

        public override OutcomeScenarioData[] OutcomeScenarios => new[]
        {
            new OutcomeScenarioData("Done")
        };

        public ValidationIssue[] GetValidationIssues()
        {
            if (string.IsNullOrEmpty(queueName) || queueName.StartsWith("--"))
                return new[] { new ValidationIssue(this, "An MQTT queue must be selected", "", BreakLevel.Fatal) };
            return new ValidationIssue[0];
        }

        public ResultData Run(StepStartData data)
        {
            MqttMessageQueue queue = GetQueue();
            if (queue == null)
                throw new Exception($"MQTT queue '{queueName}' not found.");

            string message = data[INPUT_MESSAGE] as string ?? string.Empty;

            string topicOverride = null;
            if (data.ContainsKey(INPUT_TOPIC_OVERRIDE))
                topicOverride = data[INPUT_TOPIC_OVERRIDE] as string;

            bool retain = false;
            if (data.ContainsKey(INPUT_RETAIN) && data[INPUT_RETAIN] is bool b)
                retain = b;

            string topic = !string.IsNullOrEmpty(topicOverride) ? topicOverride : queue.Topic;

            if (topic.Contains('#') || topic.Contains('+'))
                throw new InvalidOperationException(
                    $"Cannot publish to wildcard topic '{topic}'. Specify a concrete topic.");

            int qos = GetEffectiveQos(queue);

            var factory = new MqttFactory();
            using var client = factory.CreateMqttClient();
            try
            {
                string clientId = $"decisions-mqtt-pub-{Guid.NewGuid():N}";
                var options = MqttUtils.BuildClientOptions(queue, clientId, persistentSession: false);
                client.ConnectAsync(options).GetAwaiter().GetResult();

                var mqttMessage = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(Encoding.UTF8.GetBytes(message))
                    .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)qos)
                    .WithRetainFlag(retain)
                    .Build();

                client.PublishAsync(mqttMessage).GetAwaiter().GetResult();
                client.DisconnectAsync().GetAwaiter().GetResult();

                Log.Debug($"[MQTT] Published to '{topic}' (QoS {qos}, Retain={retain})");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"[MQTT] Failed to publish to topic '{topic}'");
                try { client.DisconnectAsync().GetAwaiter().GetResult(); } catch { }
                throw;
            }

            return new ResultData("Done");
        }

        private int GetEffectiveQos(MqttMessageQueue queue)
        {
            if (qosOverride == "2 - Exactly Once") return 2;
            if (qosOverride == "1 - At Least Once") return 1;
            if (qosOverride == "0 - At Most Once") return 0;
            return queue.GetQosInt();
        }
    }

    /// <summary>
    /// Enables a previously disabled MQTT queue, resuming message processing.
    /// </summary>
    [Writable]
    [ValidationRules]
    [AutoRegisterStep("Enable MQTT Queue", "Integration/MQTT")]
    public class EnableMqttQueue : BaseMqttStep, ISyncStep
    {
        public IInputMapping[] DefaultInputs => null;

        public override OutcomeScenarioData[] OutcomeScenarios => new[]
        {
            new OutcomeScenarioData("Done")
        };

        public ResultData Run(StepStartData data)
        {
            MqttMessageQueue queue = GetQueue();
            if (queue != null && queue.Disabled)
                queue.ToggleDisableQueue();
            return new ResultData("Done");
        }
    }

    /// <summary>
    /// Disables an MQTT queue, pausing message processing without removing the queue definition.
    /// </summary>
    [Writable]
    [ValidationRules]
    [AutoRegisterStep("Disable MQTT Queue", "Integration/MQTT")]
    public class DisableMqttQueue : BaseMqttStep, ISyncStep
    {
        public IInputMapping[] DefaultInputs => null;

        public override OutcomeScenarioData[] OutcomeScenarios => new[]
        {
            new OutcomeScenarioData("Done")
        };

        public ResultData Run(StepStartData data)
        {
            MqttMessageQueue queue = GetQueue();
            if (queue != null && !queue.Disabled)
                queue.ToggleDisableQueue();
            return new ResultData("Done");
        }
    }
}
