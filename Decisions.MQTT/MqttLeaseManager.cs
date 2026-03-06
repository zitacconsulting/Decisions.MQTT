using System;
using DecisionsFramework;
using DecisionsFramework.Data.ORMapper;

namespace Decisions.MqttMessageQueue
{
    public static class MqttLeaseManager
    {
        private static readonly Log Log = new Log("MQTT");
        private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(60);

        private static string LeaseId(string queueId) => $"lease_{queueId}";

        public static bool AcquireLease(string queueId, string threadId)
        {
            var orm = new ORM<MqttLease>();
            try
            {
                string leaseId = LeaseId(queueId);
                var existing = orm.Fetch(new WhereCondition[]
                {
                    new FieldWhereCondition("id", QueryMatchType.Equals, leaseId)
                }).FirstOrDefault();

                if (existing == null)
                {
                    var newLease = new MqttLease(queueId, threadId, DateTime.UtcNow.Add(LeaseDuration));
                    orm.Store(newLease, true);
                    Log.Info($"[MQTT] Acquired new lease for queue {queueId}, thread {threadId}");
                    return true;
                }

                if (existing.LeaseExpirationTime >= DateTime.UtcNow)
                {
                    if (existing.LeaseOwner == threadId)
                    {
                        existing.LeaseExpirationTime = DateTime.UtcNow.Add(LeaseDuration);
                        orm.Store(existing, false, false, "lease_expiration_time");
                        Log.Debug($"[MQTT] Renewed lease for queue {queueId}, thread {threadId}");
                        return true;
                    }

                    Log.Debug($"[MQTT] Lease for queue {queueId} is held by {existing.LeaseOwner}");
                    return false;
                }

                // Lease has expired — take ownership
                try
                {
                    orm.Delete(existing, true);
                    var newLease = new MqttLease(queueId, threadId, DateTime.UtcNow.Add(LeaseDuration));
                    orm.Store(newLease, true);
                    Log.Info($"[MQTT] Took over expired lease for queue {queueId}, thread {threadId}");
                    return true;
                }
                catch (Exception ex) when (ex.Message.Contains("unique constraint"))
                {
                    Log.Debug($"[MQTT] Concurrent lease acquisition detected for queue {queueId}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"[MQTT] Error acquiring lease for queue {queueId}, thread {threadId}");
                return false;
            }
        }

        public static void ReleaseLease(string queueId, string threadId)
        {
            var orm = new ORM<MqttLease>();
            try
            {
                string leaseId = LeaseId(queueId);
                var lease = orm.Fetch(new WhereCondition[]
                {
                    new FieldWhereCondition("id", QueryMatchType.Equals, leaseId),
                    new FieldWhereCondition("lease_owner", QueryMatchType.Equals, threadId)
                }).FirstOrDefault();

                if (lease != null)
                {
                    lease.LeaseOwner = null;
                    lease.LeaseExpirationTime = DateTime.UtcNow;
                    orm.Store(lease, false, false, "lease_owner", "lease_expiration_time");
                    Log.Info($"[MQTT] Released lease for queue {queueId}, thread {threadId}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"[MQTT] Error releasing lease for queue {queueId}, thread {threadId}");
            }
        }
    }
}
