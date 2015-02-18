using System;
using System.Collections.Generic;
using StackExchange.Redis;

namespace SetThings
{
    public class RedisSettingsManager<TSettings>
        : SettingsManager<TSettings> where TSettings : class, new()
    {
        private readonly int _databaseNum;
        private readonly ConnectionMultiplexer _redis;


        public RedisSettingsManager(ConnectionMultiplexer redis, int databaseNum, string hashName)
            : base(new RedisSettingsStore(redis, databaseNum, hashName))
        {
            _redis = redis;
            _databaseNum = databaseNum;
            var subscriber = redis.GetSubscriber();
            subscriber.Subscribe(Channel, OnSettingsUpdatedMessage);
        }


        internal static string Channel
        {
            get { return string.Format("SETTINGS-{0}-UPDATED", typeof (TSettings).FullName); }
        }

        public event EventHandler SettingsUpdated;


        private void OnSettingsUpdatedMessage(RedisChannel channel, RedisValue message)
        {
            OnSettingsUpdated();
        }


        protected virtual void OnSettingsUpdated()
        {
            var handler = SettingsUpdated;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }


        internal override void WriteSettings(Dictionary<string, string> settings, bool merge = false)
        {
            base.WriteSettings(settings, merge);
            var db = _redis.GetDatabase(_databaseNum);
            db.Publish(Channel, "Update");
        }
    }
}