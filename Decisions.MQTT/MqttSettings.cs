using System.Runtime.Serialization;
using DecisionsFramework.Data.ORMapper;
using DecisionsFramework.Design.ConfigurationStorage.Attributes;
using DecisionsFramework.Design.Properties;
using DecisionsFramework.Design.Properties.Attributes;
using DecisionsFramework.ServiceLayer.Utilities;
using Decisions.MessageQueues;

namespace Decisions.MqttMessageQueue
{
    public class MqttSettings : BaseMqSettings<MqttMessageQueue>
    {
        public override string LogCategory => "MQTT Settings";
        protected override string AddQueueActionText => "Add MQTT Queue";
        protected override string QueueTypeName => "MQTT";
        public override string ModuleName => "Decisions.MQTT";

        // --- Transport ---

        [ORMField]
        [WritableValue]
        private string server;

        [DataMember]
        [PropertyClassification(1, "Broker Host", "Settings")]
        public string Server
        {
            get { return server; }
            set { server = value; OnPropertyChanged(); }
        }

        [ORMField]
        [WritableValue]
        private bool useDefaultPort = true;

        [DataMember]
        [PropertyClassification(2, "Use Default Port", "Settings")]
        public bool UseDefaultPort
        {
            get { return useDefaultPort; }
            set { useDefaultPort = value; OnPropertyChanged(); }
        }

        [ORMField]
        [WritableValue]
        private int port = 1883;

        [DataMember]
        [PropertyClassification(3, "Port", "Settings")]
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
        [PropertyClassification(4, "Use TLS/SSL", "Settings")]
        public bool UseTls
        {
            get { return useTls; }
            set { useTls = value; OnPropertyChanged(); OnPropertyChanged(nameof(EffectivePortNote)); UpdateDefaultPort(); }
        }

        [ORMField]
        [WritableValue]
        private bool allowUntrustedCertificates;

        [DataMember]
        [PropertyClassification(5, "Allow Untrusted Certificates", "Settings")]
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
        [PropertyClassification(6, "Use WebSocket Transport", "Settings")]
        public bool UseWebSocket
        {
            get { return useWebSocket; }
            set { useWebSocket = value; OnPropertyChanged(); OnPropertyChanged(nameof(EffectivePortNote)); UpdateDefaultPort(); }
        }

        [ORMField]
        [WritableValue]
        private string webSocketPath = "/mqtt";

        [DataMember]
        [PropertyClassification(7, "WebSocket Path", "Settings")]
        [BooleanPropertyHidden(nameof(UseWebSocket), false)]
        public string WebSocketPath
        {
            get { return webSocketPath; }
            set { webSocketPath = value; }
        }

        // --- Authentication ---

        [ReadonlyEditor]
        [PropertyClassification(8, "Effective Port", "Settings")]
        [BooleanPropertyHidden(nameof(UseDefaultPort), false)]
        public string EffectivePortNote
        {
            get => $"Using default port: {GetEffectivePort()}";
            set { }
        }

        [ORMField]
        [WritableValue]
        private string username;

        [DataMember]
        [PropertyClassification(9, "Username", "Settings")]
        public string Username
        {
            get { return username; }
            set { username = value; }
        }

        [ORMField(4000, typeof(FixedLengthStringFieldConverter))]
        [WritableValue]
        private string password;

        [DataMember]
        [PropertyClassification(10, "Password", "Settings")]
        [PasswordText]
        public string Password
        {
            get { return password; }
            set { password = value; }
        }

        // --- Protocol & Session ---

        [ORMField]
        [WritableValue]
        private string protocolVersion = "3.1.1";

        [ReadonlyEditor]
        [PropertyClassification(11, "Protocol Version Info", "Settings")]
        public string ProtocolVersionNote
        {
            get => "MQTT 3.1.1 is the most widely supported version and works with virtually all brokers. " +
                   "MQTT 5.0 adds advanced features such as Shared Subscriptions (for load-balanced Decisions cluster deployments) and User Properties (metadata on each message). " +
                   "Only switch to 5.0 if your broker supports it and you need these features.";
            set { }
        }

        [DataMember]
        [PropertyClassification(12, "Protocol Version", "Settings")]
        [SelectStringEditor(nameof(ProtocolVersionOptions))]
        public string ProtocolVersion
        {
            get { return protocolVersion; }
            set { protocolVersion = value; }
        }

        [PropertyHidden]
        public string[] ProtocolVersionOptions => new[] { "3.1.1", "5.0" };

        [ORMField]
        [WritableValue]
        private int keepAliveSeconds = 60;

        [DataMember]
        [PropertyClassification(13, "Keep Alive (seconds)", "Settings")]
        public int KeepAliveSeconds
        {
            get { return keepAliveSeconds; }
            set { keepAliveSeconds = value; }
        }

        [ORMField]
        [WritableValue]
        private int connectionTimeoutSeconds = 10;

        [DataMember]
        [PropertyClassification(14, "Connection Timeout (seconds)", "Settings")]
        public int ConnectionTimeoutSeconds
        {
            get { return connectionTimeoutSeconds; }
            set { connectionTimeoutSeconds = value; }
        }

        [ReadonlyEditor]
        [PropertyClassification(15, "Persistent Session Info", "Settings")]
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
        [PropertyClassification(16, "Persistent Session", "Settings")]
        public bool PersistentSession
        {
            get { return persistentSession; }
            set { persistentSession = value; }
        }

        private void UpdateDefaultPort()
        {
            port = useWebSocket ? (useTls ? 8084 : 8083) : (useTls ? 8883 : 1883);
            OnPropertyChanged(nameof(Port));
        }

        public int GetEffectivePort()
        {
            if (UseDefaultPort)
            {
                if (UseWebSocket) return UseTls ? 8084 : 8083;
                return UseTls ? 8883 : 1883;
            }
            return Port;
        }
    }
}
