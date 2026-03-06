using DecisionsFramework.Data.ORMapper;
using Decisions.MessageQueues;

namespace Decisions.MqttMessageQueue
{
    public class MqttClusterNotification : BaseMqClusterNotification
    {
        public override BaseMqDefinition GetQueueDefinition(string queueEntityId)
        {
            return new ORM<MqttMessageQueue>().Fetch(queueEntityId);
        }
    }
}
