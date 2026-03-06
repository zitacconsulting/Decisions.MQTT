using System.Collections.Generic;
using System.Runtime.Serialization;
using DecisionsFramework;
using DecisionsFramework.Data.Messaging;
using DecisionsFramework.Data.ORMapper;
using DecisionsFramework.Design.ConfigurationStorage.Attributes;
using DecisionsFramework.Design.Properties;
using DecisionsFramework.Design.Properties.Attributes;
using DecisionsFramework.ServiceLayer;
using DecisionsFramework.ServiceLayer.Utilities;
using Decisions.MessageQueues;
using MQTTnet.Formatter;

namespace Decisions.MqttMessageQueue
{
    [AutoRegisterNativeType]
    [DataContract]
    public class MqttMessageQueue : BaseMqDefinition
    {
        public override string LogCategory => "MQTT";
        protected override IMessageQueue CreateNewQueueImpl() => new MqttMessageQueueImpl(this);
        protected override BaseMqClusterNotification CreateNewClusterNotification() => new MqttClusterNotification();

        // --- Definition ---

        [ReadonlyEditor]
        [PropertyClassification(1, "Topic Filter Info", new string[] { "1 Definition" })]
        public string TopicFilterNote
        {
            get => "The MQTT topic filter to subscribe to. Use exact topics (e.g. 'sensors/temp') or wildcards: '+' matches a single level (e.g. 'sensors/+/temp'), '#' matches all remaining levels and must appear last (e.g. 'factory/line1/#').";
            set { }
        }

        [ORMField]
        [WritableValue]
        private string topic;

        [DataMember]
        [PropertyClassification(2, "Topic Filter", "1 Definition")]
        public string Topic
        {
            get { return topic; }
            set { topic = value; OnPropertyChanged(); }
        }

        [ReadonlyEditor]
        [PropertyClassification(3, "Quality of Service Info", new string[] { "1 Definition" })]
        public string QosNote
        {
            get => "Quality of Service (QoS) controls how reliably messages are delivered between the broker and Decisions.\n\n" +
                   "0 – At Most Once: The message is sent once with no confirmation. Fastest and lowest overhead, but messages may be lost if the network drops or Decisions is restarting. Use for high-frequency data where occasional loss is acceptable (e.g. sensor readings).\n\n" +
                   "1 – At Least Once: The broker retries until Decisions acknowledges the message. Guarantees delivery, but the same message may arrive more than once if the connection drops mid-acknowledgement. Your flow should handle duplicates if this matters.\n\n" +
                   "2 – Exactly Once: A four-way handshake ensures each message is delivered exactly once. Safest, but slowest. Use when duplicate processing would cause real problems (e.g. financial transactions, commands that must not run twice).";
            set { }
        }

        [ORMField]
        [WritableValue]
        private string qosLevel = "1 - At Least Once";

        [DataMember]
        [PropertyClassification(4, "Quality of Service (QoS)", "1 Definition")]
        [SelectStringEditor(nameof(QosOptions))]
        public string QosLevel
        {
            get { return qosLevel; }
            set { qosLevel = value; }
        }

        [PropertyHidden]
        public string[] QosOptions => new[]
        {
            "0 - At Most Once",
            "1 - At Least Once",
            "2 - Exactly Once"
        };

        public int GetQosInt()
        {
            if (qosLevel?.StartsWith("2") == true) return 2;
            if (qosLevel?.StartsWith("0") == true) return 0;
            return 1;
        }

        // --- Connection overrides ---

        [ORMField]
        [WritableValue]
        private bool overrideSettings;

        [DataMember]
        [PropertyClassification(1, "Override Global Settings", "2 Connection")]
        public bool OverrideSettings
        {
            get { return overrideSettings; }
            set { overrideSettings = value; OnPropertyChanged(); OnPropertyChanged(nameof(EffectiveProtocolVersion)); }
        }

        [ORMField]
        [WritableValue]
        private string server;

        [DataMember]
        [PropertyClassification(2, "Broker Host", "2 Connection")]
        [BooleanPropertyHidden(nameof(OverrideSettings), false)]
        public string Server
        {
            get { return server; }
            set { server = value; OnPropertyChanged(); }
        }

        [ORMField]
        [WritableValue]
        private bool useDefaultPort = true;

        [DataMember]
        [PropertyClassification(3, "Use Default Port", "2 Connection")]
        [BooleanPropertyHidden(nameof(OverrideSettings), false)]
        public bool UseDefaultPort
        {
            get { return useDefaultPort; }
            set { useDefaultPort = value; OnPropertyChanged(); }
        }

        [ORMField]
        [WritableValue]
        private int port = 1883;

        [DataMember]
        [PropertyClassification(4, "Port", "2 Connection")]
        [BooleanPropertyHidden(nameof(OverrideSettings), false)]
        [BooleanPropertyHidden(nameof(UseDefaultPort), true)]
        public int Port
        {
            get { return port; }
            set { port = value; }
        }

        [ORMField]
        [WritableValue]
        private bool useTls;

        [DataMember]
        [PropertyClassification(5, "Use TLS/SSL", "2 Connection")]
        [BooleanPropertyHidden(nameof(OverrideSettings), false)]
        public bool UseTls
        {
            get { return useTls; }
            set { useTls = value; OnPropertyChanged(); OnPropertyChanged(nameof(EffectivePortNote)); UpdateDefaultPort(); }
        }

        [ORMField]
        [WritableValue]
        private bool allowUntrustedCertificates;

        [DataMember]
        [PropertyClassification(6, "Allow Untrusted Certificates", "2 Connection")]
        [BooleanPropertyHidden(nameof(OverrideSettings), false)]
        [BooleanPropertyHidden(nameof(UseTls), false)]
        public bool AllowUntrustedCertificates
        {
            get { return allowUntrustedCertificates; }
            set { allowUntrustedCertificates = value; }
        }

        [ORMField]
        [WritableValue]
        private bool useWebSocket;

        [DataMember]
        [PropertyClassification(7, "Use WebSocket Transport", "2 Connection")]
        [BooleanPropertyHidden(nameof(OverrideSettings), false)]
        public bool UseWebSocket
        {
            get { return useWebSocket; }
            set { useWebSocket = value; OnPropertyChanged(); OnPropertyChanged(nameof(EffectivePortNote)); UpdateDefaultPort(); }
        }

        [ORMField]
        [WritableValue]
        private string webSocketPath = "/mqtt";

        [DataMember]
        [PropertyClassification(8, "WebSocket Path", "2 Connection")]
        [BooleanPropertyHidden(nameof(OverrideSettings), false)]
        [BooleanPropertyHidden(nameof(UseWebSocket), false)]
        public string WebSocketPath
        {
            get { return webSocketPath; }
            set { webSocketPath = value; }
        }

        [ReadonlyEditor]
        [PropertyClassification(9, "Effective Port", ["2 Connection"])]
        [BooleanPropertyHidden(nameof(OverrideSettings), false)]
        [BooleanPropertyHidden(nameof(UseDefaultPort), false)]
        public string EffectivePortNote
        {
            get
            {
                int port = UseWebSocket ? (UseTls ? 8084 : 8083) : (UseTls ? 8883 : 1883);
                return $"Using default port: {port}";
            }
            set { }
        }

        [ORMField]
        [WritableValue]
        private string username;

        [DataMember]
        [PropertyClassification(10, "Username", "2 Connection")]
        [BooleanPropertyHidden(nameof(OverrideSettings), false)]
        public string Username
        {
            get { return username; }
            set { username = value; }
        }

        [ORMField(4000, typeof(FixedLengthStringFieldConverter))]
        [WritableValue]
        private string password;

        [DataMember]
        [PropertyClassification(11, "Password", "2 Connection")]
        [PasswordText]
        [BooleanPropertyHidden(nameof(OverrideSettings), false)]
        public string Password
        {
            get { return password; }
            set { password = value; }
        }

        [ORMField]
        [WritableValue]
        private string protocolVersion = "3.1.1";

        [ReadonlyEditor]
        [PropertyClassification(12, "Protocol Version Info", new string[] { "2 Connection" })]
        [BooleanPropertyHidden(nameof(OverrideSettings), false)]
        public string ProtocolVersionNote
        {
            get => "MQTT 3.1.1 is the most widely supported version and works with virtually all brokers. " +
                   "MQTT 5.0 adds advanced features such as Shared Subscriptions (for load-balanced Decisions cluster deployments) and User Properties (metadata on each message). " +
                   "Only switch to 5.0 if your broker supports it and you need these features.";
            set { }
        }

        [DataMember]
        [PropertyClassification(13, "Protocol Version", "2 Connection")]
        [SelectStringEditor(nameof(ProtocolVersionOptions))]
        [BooleanPropertyHidden(nameof(OverrideSettings), false)]
        public string ProtocolVersion
        {
            get { return protocolVersion; }
            set
            {
                protocolVersion = value;
                if (value != "5.0")
                    sharedSubscriptionGroup = null;
                OnPropertyChanged(nameof(EffectiveProtocolVersion));
            }
        }

        [PropertyHidden]
        public string[] ProtocolVersionOptions => new[] { "3.1.1", "5.0" };

        [PropertyHidden]
        public string EffectiveProtocolVersion =>
            MqttUtils.GetProtocolVersion(this) == MqttProtocolVersion.V500 ? "5.0" : "3.1.1";

        [ORMField]
        [WritableValue]
        private int keepAliveSeconds = 60;

        [DataMember]
        [PropertyClassification(14, "Keep Alive (seconds)", "2 Connection")]
        [BooleanPropertyHidden(nameof(OverrideSettings), false)]
        public int KeepAliveSeconds
        {
            get { return keepAliveSeconds; }
            set { keepAliveSeconds = value; }
        }

        [ORMField]
        [WritableValue]
        private int connectionTimeoutSeconds = 10;

        [DataMember]
        [PropertyClassification(15, "Connection Timeout (seconds)", "2 Connection")]
        [BooleanPropertyHidden(nameof(OverrideSettings), false)]
        public int ConnectionTimeoutSeconds
        {
            get { return connectionTimeoutSeconds; }
            set { connectionTimeoutSeconds = value; }
        }

        [ReadonlyEditor]
        [PropertyClassification(16, "Persistent Session Info", new string[] { "2 Connection" })]
        [BooleanPropertyHidden(nameof(OverrideSettings), false)]
        public string PersistentSessionNote
        {
            get => "When enabled, the broker remembers this Decisions server between disconnections and queues up any messages that arrive while offline — delivering them when the connection is restored. " +
                   "This requires the broker to store state per client, identified by the Client ID. " +
                   "If the queue is recreated with a new auto-generated Client ID, the old stored messages will never be delivered and accumulate on the broker. " +
                   "Use a fixed Client ID Override if you enable this.\n\n" +
                   "When disabled, each reconnection starts fresh — no messages are saved during downtime. " +
                   "This is simpler and more predictable for most use cases.";
            set { }
        }

        [ORMField]
        [WritableValue]
        private bool persistentSession = false;

        [DataMember]
        [PropertyClassification(17, "Persistent Session", "2 Connection")]
        [BooleanPropertyHidden(nameof(OverrideSettings), false)]
        public bool PersistentSession
        {
            get { return persistentSession; }
            set { persistentSession = value; }
        }



        // --- Advanced ---

        [ReadonlyEditor]
        [PropertyClassification(1, "Advanced Settings Info", new string[] { "3 Advanced" })]
        public string AdvancedNote
        {
            get => "Message Buffer Size controls the maximum number of received MQTT messages held in memory while waiting to be processed by the Decisions flow. Increase this for high-throughput topics to avoid dropping messages under load.";
            set { }
        }

        [ORMField]
        [WritableValue]
        private int messageBufferSize = 1000;

        [DataMember]
        [PropertyClassification(2, "Message Buffer Size", "3 Advanced")]
        public int MessageBufferSize
        {
            get { return messageBufferSize; }
            set { messageBufferSize = value; }
        }

        [ReadonlyEditor]
        [PropertyClassification(3, "Client ID Info", new string[] { "3 Advanced" })]
        public string ClientIdNote
        {
            get => "By default, each queue uses an auto-generated client ID ('decisions-mqtt-{queueId}'). Override this if your broker enforces specific client ID naming conventions.";
            set { }
        }

        [ORMField]
        [WritableValue]
        private string? clientIdOverride;

        [DataMember]
        [PropertyClassification(4, "Client ID Override", "3 Advanced")]
        public string? ClientIdOverride
        {
            get { return clientIdOverride; }
            set { clientIdOverride = value; }
        }

        [ReadonlyEditor]
        [PropertyClassification(5, "Shared Subscription Info", new string[] { "3 Advanced" })]
        [PropertyHiddenByValue(nameof(EffectiveProtocolVersion), "5.0", false)]
        public string SharedSubscriptionNote
        {
            get => "Shared subscriptions (MQTT 5.0) let the broker distribute messages across multiple consumers instead of delivering a copy to each one. Enter a group name (e.g. 'decisions') to subscribe as '$share/{group}/{topic}'. Each active worker thread connects independently and the broker load-balances messages between them — useful both for scaling across multiple Decisions servers and for parallel processing with multiple threads on a single server (controlled by Active Flow Count on the message handler). The single-connection lease mechanism is automatically bypassed when a group name is set. Leave empty to use the default mode where only one connection is active at a time.";
            set { }
        }

        [ORMField]
        [WritableValue]
        private string? sharedSubscriptionGroup;

        [DataMember]
        [PropertyClassification(6, "Shared Subscription Group", "3 Advanced")]
        [PropertyHiddenByValue(nameof(EffectiveProtocolVersion), "5.0", false)]
        public string? SharedSubscriptionGroup
        {
            get { return sharedSubscriptionGroup; }
            set { sharedSubscriptionGroup = value; }
        }

        // --- Last Will and Testament ---

        [ReadonlyEditor]
        [PropertyClassification(1, "Last Will Info", new string[] { "4 Last Will" })]
        public string LwtNote
        {
            get => "Last Will and Testament (LWT): a message the broker automatically publishes if this client disconnects unexpectedly. Useful for monitoring — e.g. publish '{\"status\":\"offline\"}' to a status topic when Decisions loses the connection.";
            set { }
        }

        [ORMField]
        [WritableValue]
        private bool enableLwt;

        [DataMember]
        [PropertyClassification(2, "Enable Last Will", "4 Last Will")]
        public bool EnableLwt
        {
            get { return enableLwt; }
            set { enableLwt = value; OnPropertyChanged(); }
        }

        [ORMField]
        [WritableValue]
        private string lwtTopic;

        [DataMember]
        [PropertyClassification(3, "Last Will Topic", "4 Last Will")]
        [BooleanPropertyHidden(nameof(EnableLwt), false)]
        public string LwtTopic
        {
            get { return lwtTopic; }
            set { lwtTopic = value; }
        }

        [ORMField(4000, typeof(FixedLengthStringFieldConverter))]
        [WritableValue]
        private string lwtPayload;

        [DataMember]
        [PropertyClassification(4, "Last Will Payload", "4 Last Will")]
        [BooleanPropertyHidden(nameof(EnableLwt), false)]
        public string LwtPayload
        {
            get { return lwtPayload; }
            set { lwtPayload = value; }
        }

        [ORMField]
        [WritableValue]
        private string lwtQosLevel = "0 - At Most Once";

        [DataMember]
        [PropertyClassification(5, "Last Will Quality of Service (QoS)", "4 Last Will")]
        [SelectStringEditor(nameof(QosOptions))]
        [BooleanPropertyHidden(nameof(EnableLwt), false)]
        public string LwtQosLevel
        {
            get { return lwtQosLevel; }
            set { lwtQosLevel = value; }
        }

        [ORMField]
        [WritableValue]
        private bool lwtRetain;

        [DataMember]
        [PropertyClassification(6, "Last Will Retain", "4 Last Will")]
        [BooleanPropertyHidden(nameof(EnableLwt), false)]
        public bool LwtRetain
        {
            get { return lwtRetain; }
            set { lwtRetain = value; }
        }

        private void UpdateDefaultPort()
        {
            port = useWebSocket ? (useTls ? 8084 : 8083) : (useTls ? 8883 : 1883);
            OnPropertyChanged(nameof(Port));
        }

        public int GetLwtQosInt()
        {
            if (lwtQosLevel?.StartsWith("2") == true) return 2;
            if (lwtQosLevel?.StartsWith("1") == true) return 1;
            return 0;
        }

        public override ValidationIssue[] GetAdditionalValidationIssues()
        {
            var issues = new List<ValidationIssue>();

            if (string.IsNullOrEmpty(Topic))
                issues.Add(new ValidationIssue(this, "Topic filter must be supplied", "", BreakLevel.Fatal));
            else
                ValidateTopicFilter(Topic, issues);

            if (OverrideSettings && string.IsNullOrEmpty(Server))
                issues.Add(new ValidationIssue(this, "Broker host must be supplied when overriding settings", "", BreakLevel.Fatal));

            if (EnableLwt && string.IsNullOrEmpty(LwtTopic))
                issues.Add(new ValidationIssue(this, "Last Will Topic must be supplied when Last Will is enabled", "", BreakLevel.Fatal));

            if (MessageBufferSize < 1)
                issues.Add(new ValidationIssue(this, "Message Buffer Size must be at least 1", "", BreakLevel.Fatal));

            if (!string.IsNullOrEmpty(SharedSubscriptionGroup) && MqttUtils.GetProtocolVersion(this) != MqttProtocolVersion.V500)
                issues.Add(new ValidationIssue(this, "Shared Subscription Group requires Protocol Version 5.0", "", BreakLevel.Fatal));

            if (!string.IsNullOrEmpty(SharedSubscriptionGroup) &&
                (SharedSubscriptionGroup.Contains('/') || SharedSubscriptionGroup.Contains('+') || SharedSubscriptionGroup.Contains('#')))
                issues.Add(new ValidationIssue(this, "Shared Subscription Group must not contain '/', '+' or '#'", "", BreakLevel.Fatal));

            return issues.ToArray();
        }

        private void ValidateTopicFilter(string filter, List<ValidationIssue> issues)
        {
            string[] segments = filter.Split('/');
            for (int i = 0; i < segments.Length; i++)
            {
                string seg = segments[i];
                if (seg.Contains('#'))
                {
                    if (seg != "#")
                        issues.Add(new ValidationIssue(this, $"Invalid topic filter: '#' must occupy an entire level, not combined with other characters (segment '{seg}')", "", BreakLevel.Fatal));
                    else if (i != segments.Length - 1)
                        issues.Add(new ValidationIssue(this, "Invalid topic filter: '#' must only appear at the last level (e.g. 'sensors/#')", "", BreakLevel.Fatal));
                }
                if (seg.Contains('+') && seg != "+")
                    issues.Add(new ValidationIssue(this, $"Invalid topic filter: '+' must occupy an entire level, not combined with other characters (segment '{seg}')", "", BreakLevel.Fatal));
            }
        }
    }
}
