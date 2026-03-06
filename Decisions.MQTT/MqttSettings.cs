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
        private bool useWebSocket;

        [DataMember]
        [PropertyClassification(5, "Use WebSocket Transport", "Settings")]
        public bool UseWebSocket
        {
            get { return useWebSocket; }
            set { useWebSocket = value; OnPropertyChanged(); OnPropertyChanged(nameof(EffectivePortNote)); UpdateDefaultPort(); }
        }

        [ORMField]
        [WritableValue]
        private string webSocketPath = "/mqtt";

        [DataMember]
        [PropertyClassification(6, "WebSocket Path", "Settings")]
        [BooleanPropertyHidden(nameof(UseWebSocket), false)]
        public string WebSocketPath
        {
            get { return webSocketPath; }
            set { webSocketPath = value; }
        }

        // --- Authentication ---

        [ReadonlyEditor]
        [PropertyClassification(7, "Effective Port", "Settings")]
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
        [PropertyClassification(8, "Username", "Settings")]
        public string Username
        {
            get { return username; }
            set { username = value; }
        }

        [ORMField(4000, typeof(FixedLengthStringFieldConverter))]
        [WritableValue]
        private string password;

        [DataMember]
        [PropertyClassification(9, "Password", "Settings")]
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

        [DataMember]
        [PropertyClassification(10, "Protocol Version", "Settings")]
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
        [PropertyClassification(11, "Keep Alive (seconds)", "Settings")]
        public int KeepAliveSeconds
        {
            get { return keepAliveSeconds; }
            set { keepAliveSeconds = value; }
        }

        [ORMField]
        [WritableValue]
        private int connectionTimeoutSeconds = 10;

        [DataMember]
        [PropertyClassification(12, "Connection Timeout (seconds)", "Settings")]
        public int ConnectionTimeoutSeconds
        {
            get { return connectionTimeoutSeconds; }
            set { connectionTimeoutSeconds = value; }
        }

        [ORMField]
        [WritableValue]
        private bool persistentSession = true;

        [DataMember]
        [PropertyClassification(13, "Persistent Session", "Settings")]
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
