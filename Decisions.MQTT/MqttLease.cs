using System;
using System.Runtime.Serialization;
using DecisionsFramework.Data.ORMapper;
using DecisionsFramework.ServiceLayer;
using DecisionsFramework.Design.ConfigurationStorage.Attributes;
using DecisionsFramework.Design.Properties;
using DecisionsFramework.ServiceLayer.Actions;
using DecisionsFramework.ServiceLayer.Utilities;

namespace Decisions.MqttMessageQueue
{
    [DataContract]
    [ORMEntity("mqtt_lease")]
    public class MqttLease : AbstractEntity
    {
        [ORMPrimaryKeyField]
        [PropertyHidden]
        [DataMember]
        public string Id { get; set; }

        [ORMField("lease_owner")]
        [DataMember]
        [WritableValue]
        public string LeaseOwner { get; set; }

        [ORMField("lease_expiration_time")]
        [DataMember]
        [WritableValue]
        public DateTime LeaseExpirationTime { get; set; }

        [PropertyHidden]
        [DataMember]
        public override string EntityName
        {
            get { return $"MQTT Lease: {Id}"; }
            set { }
        }

        [PropertyHidden]
        [DataMember]
        public override string EntityDescription
        {
            get { return $"Owner: {LeaseOwner}, Expires: {LeaseExpirationTime:u}"; }
            set { }
        }

        public MqttLease() { }

        public MqttLease(string queueId, string leaseOwner, DateTime leaseExpirationTime)
        {
            Id = $"lease_{queueId}";
            LeaseOwner = leaseOwner;
            LeaseExpirationTime = leaseExpirationTime;
        }

        public override BaseActionType[] GetActions(AbstractUserContext userContext, EntityActionType[] types)
            => Array.Empty<BaseActionType>();
    }
}
