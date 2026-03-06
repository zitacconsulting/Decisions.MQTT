using System;
using DecisionsFramework;
using DecisionsFramework.ServiceLayer;
using DecisionsFramework.ServiceLayer.Services.Projects.Settings;
using MQTTnet.Client;
using MQTTnet.Formatter;
using MQTTnet.Protocol;

namespace Decisions.MqttMessageQueue
{
    public static class MqttUtils
    {
        private static readonly Log Log = new Log("MQTT");

        public static MqttClientOptions BuildClientOptions(MqttMessageQueue queueDef, string clientId, bool persistentSession = true)
        {
            string host = GetHost(queueDef);
            int port = GetPort(queueDef);
            bool useTls = GetUseTls(queueDef);
            bool allowUntrustedCertificates = GetAllowUntrustedCertificates(queueDef);
            string username = GetUsername(queueDef);
            string password = GetPassword(queueDef);
            bool useWebSocket = GetUseWebSocket(queueDef);
            string webSocketPath = GetWebSocketPath(queueDef);
            int keepAlive = GetKeepAlive(queueDef);
            int connectionTimeout = GetConnectionTimeout(queueDef);
            var protocolVersion = GetProtocolVersion(queueDef);

            var builder = new MqttClientOptionsBuilder()
                .WithClientId(clientId)
                .WithProtocolVersion(protocolVersion)
                .WithCleanSession(!persistentSession)
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(keepAlive))
                .WithTimeout(TimeSpan.FromSeconds(connectionTimeout));

            if (useWebSocket)
            {
                string scheme = useTls ? "wss" : "ws";
                builder = builder.WithWebSocketServer(o => o.WithUri($"{scheme}://{host}:{port}{webSocketPath}"));
                if (useTls)
                    builder = builder.WithTlsOptions(o =>
                    {
                        o.UseTls();
                        if (allowUntrustedCertificates)
                            o.WithCertificateValidationHandler(_ => true);
                    });
            }
            else
            {
                builder = builder.WithTcpServer(host, port);
                if (useTls)
                    builder = builder.WithTlsOptions(o =>
                    {
                        o.UseTls();
                        if (allowUntrustedCertificates)
                            o.WithCertificateValidationHandler(_ => true);
                    });
            }

            // MQTT 5: set SessionExpiryInterval so the broker retains the session after disconnect
            if (persistentSession && protocolVersion == MqttProtocolVersion.V500)
                builder = builder.WithSessionExpiryInterval(uint.MaxValue);

            if (!string.IsNullOrEmpty(username))
                builder = builder.WithCredentials(username, password ?? string.Empty);

            // Last Will and Testament
            if (queueDef.EnableLwt && !string.IsNullOrEmpty(queueDef.LwtTopic))
            {
                builder = builder
                    .WithWillTopic(queueDef.LwtTopic)
                    .WithWillQualityOfServiceLevel((MqttQualityOfServiceLevel)queueDef.GetLwtQosInt())
                    .WithWillRetain(queueDef.LwtRetain);
                if (!string.IsNullOrEmpty(queueDef.LwtPayload))
                    builder = builder.WithWillPayload(queueDef.LwtPayload);
            }

            return builder.Build();
        }

        public static string GetClientId(MqttMessageQueue queueDef)
        {
            if (!string.IsNullOrEmpty(queueDef.ClientIdOverride))
                return queueDef.ClientIdOverride;
            return $"decisions-mqtt-{queueDef.Id}";
        }

        public static bool GetPersistentSession(MqttMessageQueue queueDef)
        {
            if (queueDef.OverrideSettings)
                return queueDef.PersistentSession;
            return GetSettingsForQueue(queueDef)?.PersistentSession ?? false;
        }

        public static int GetKeepAlive(MqttMessageQueue queueDef)
        {
            if (queueDef.OverrideSettings)
                return queueDef.KeepAliveSeconds;
            return GetSettingsForQueue(queueDef)?.KeepAliveSeconds ?? 60;
        }

        public static int GetConnectionTimeout(MqttMessageQueue queueDef)
        {
            if (queueDef.OverrideSettings)
                return queueDef.ConnectionTimeoutSeconds;
            return GetSettingsForQueue(queueDef)?.ConnectionTimeoutSeconds ?? 10;
        }

        public static bool GetUseWebSocket(MqttMessageQueue queueDef)
        {
            if (queueDef.OverrideSettings)
                return queueDef.UseWebSocket;
            return GetSettingsForQueue(queueDef)?.UseWebSocket ?? false;
        }

        public static string GetWebSocketPath(MqttMessageQueue queueDef)
        {
            if (queueDef.OverrideSettings)
                return queueDef.WebSocketPath ?? "/mqtt";
            return GetSettingsForQueue(queueDef)?.WebSocketPath ?? "/mqtt";
        }

        public static MqttProtocolVersion GetProtocolVersion(MqttMessageQueue queueDef)
        {
            string version;
            if (queueDef.OverrideSettings)
                version = queueDef.ProtocolVersion;
            else
                version = GetSettingsForQueue(queueDef)?.ProtocolVersion;

            return version == "5.0" ? MqttProtocolVersion.V500 : MqttProtocolVersion.V311;
        }

        /// <summary>
        /// Returns settings for the given project, or global settings if no project ID is provided.
        /// This respects the three-level hierarchy: global → project → per-queue override.
        /// </summary>
        internal static MqttSettings GetSettings(string projectId)
        {
            if (string.IsNullOrEmpty(projectId))
                return ModuleSettingsAccessor<MqttSettings>.Instance;
            return ProjectSettingsAccessor<MqttSettings>.GetSettings(projectId);
        }

        private static MqttSettings GetSettingsForQueue(MqttMessageQueue queueDef)
            => GetSettings(queueDef.GetProjectId());

        public static string GetHost(MqttMessageQueue queueDef)
        {
            if (queueDef.OverrideSettings && !string.IsNullOrEmpty(queueDef.Server))
                return queueDef.Server;
            string host = GetSettingsForQueue(queueDef)?.Server;
            if (string.IsNullOrEmpty(host))
                throw new InvalidOperationException(
                    $"MQTT queue '{queueDef.DisplayName}': Broker Host is not configured. " +
                    "Set it in the global MQTT Settings or enable Override Settings on the queue.");
            return host;
        }

        public static int GetPort(MqttMessageQueue queueDef)
        {
            if (queueDef.OverrideSettings)
            {
                if (!queueDef.UseDefaultPort)
                    return queueDef.Port;
                if (queueDef.UseWebSocket) return queueDef.UseTls ? 8084 : 8083;
                return queueDef.UseTls ? 8883 : 1883;
            }
            return GetSettingsForQueue(queueDef)?.GetEffectivePort() ?? 1883;
        }

        public static bool GetUseTls(MqttMessageQueue queueDef)
        {
            if (queueDef.OverrideSettings)
                return queueDef.UseTls;
            return GetSettingsForQueue(queueDef)?.UseTls ?? false;
        }

        public static bool GetAllowUntrustedCertificates(MqttMessageQueue queueDef)
        {
            if (queueDef.OverrideSettings)
                return queueDef.AllowUntrustedCertificates;
            return GetSettingsForQueue(queueDef)?.AllowUntrustedCertificates ?? false;
        }

        public static string GetUsername(MqttMessageQueue queueDef)
        {
            if (queueDef.OverrideSettings)
                return queueDef.Username;
            return GetSettingsForQueue(queueDef)?.Username;
        }

        public static string GetPassword(MqttMessageQueue queueDef)
        {
            if (queueDef.OverrideSettings)
                return queueDef.Password;
            return GetSettingsForQueue(queueDef)?.Password;
        }
    }
}
