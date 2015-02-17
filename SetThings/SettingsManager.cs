using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace SetThings
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class SettingsAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class SettingAttribute : Attribute
    {
        public string Name { get; set; }
        public SettingAttribute() { }
        public SettingAttribute(string name)
        {
            Name = name;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class DefaultAttribute : Attribute
    {
        public string Value { get; set; }
    }

    public abstract class SettingsManager<TSettings>
        where TSettings : class, new()
    {
        private static readonly Func<Dictionary<string, string>, TSettings> s_loadFromRaw;

        static SettingsManager()
        {
            s_loadFromRaw = typeof(TSettings).BuildSettingsReader<TSettings>();
        }

        private readonly ISettingsStore _store;
        

        protected SettingsManager(ISettingsStore store)
        {
            _store = store;
        }

        public TSettings Load()
        {
            var raw = _store.ReadSettings();
            return s_loadFromRaw(raw);
        }

        public async Task<TSettings> LoadAsync()
        {
            var raw = await _store.ReadSettingsAsync();
            return s_loadFromRaw(raw);
        }
                
        public virtual IUpdateSettings BeginUpdate()
        {
            return new Updater(this);
        }

        public interface IUpdateSettings
        {
            IUpdateSettings Set<TProp>(Expression<Func<TSettings, TProp>> setting, TProp newValue) where TProp : IConvertible;
            IUpdateSettings Reset<TProp>(Expression<Func<TSettings, TProp>> setting) where TProp : IConvertible;
            void Commit();
        }

        internal virtual void WriteSettings(Dictionary<string, string> settings, bool merge = false)
        {
            _store.WriteSettings(settings, merge);
        }

        protected class Updater : IUpdateSettings
        {
            private readonly SettingsManager<TSettings> _owner;
            private readonly Dictionary<string, string> _pendingUpdates;

            public Updater(SettingsManager<TSettings> owner)
            {
                _owner = owner;

                _pendingUpdates = new Dictionary<string, string>();
            }


            public IUpdateSettings Set<TProp>(Expression<Func<TSettings, TProp>> setting, TProp newValue)
                where TProp : IConvertible
            {
                // Get the property info
                if (setting.Body.NodeType != ExpressionType.MemberAccess)
                    throw new ArgumentException("setting expression must be a property access");

                var prop = ((MemberExpression)setting.Body).Member as PropertyInfo;
                if (prop == null)
                    throw new ArgumentException("setting expression must be a property access");


                var key = setting.GetSettingKey();
                _pendingUpdates.Add(key, newValue as string ?? Convert.ToString(newValue, CultureInfo.InvariantCulture));

                return this;
            }
            
            public IUpdateSettings Reset<TProp>(Expression<Func<TSettings, TProp>> setting)
                where TProp : IConvertible
            {
                // Get the property info
                if (setting.Body.NodeType != ExpressionType.MemberAccess)
                    throw new ArgumentException("setting expression must be a property access");

                var prop = ((MemberExpression)setting.Body).Member as PropertyInfo;
                if (prop == null)
                    throw new ArgumentException("setting expression must be a property access");


                var key = setting.GetSettingKey();
                var val = prop.GetDefaultValue();
                _pendingUpdates.Add(key, val);
                return this;
            }

            public virtual void Commit()
            {
                _owner.WriteSettings(_pendingUpdates, merge: true);
            }
        }
    }

    public class RedisSettingsManager<TSettings> : SettingsManager<TSettings> where TSettings : class, new()
    {
        private readonly ConnectionMultiplexer _redis;
        private readonly int _databaseNum;
        public event EventHandler SettingsUpdated;

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
            get { return string.Format("SETTINGS-{0}-UPDATED", typeof(TSettings).FullName); }
        }

        private void OnSettingsUpdatedMessage(RedisChannel channel, RedisValue message)
        {
            OnSettingsUpdated();
        }

        protected virtual void OnSettingsUpdated()
        {
            var handler = SettingsUpdated;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        internal override void WriteSettings(Dictionary<string, string> settings, bool merge = false)
        {
            base.WriteSettings(settings, merge);
            var db = _redis.GetDatabase(_databaseNum);
            db.Publish(Channel, "Update");
        }
    }

    public interface ISettingsStore
    {
        Dictionary<string, string> ReadSettings();
        Task<Dictionary<string, string>> ReadSettingsAsync();
        void WriteSettings(Dictionary<string, string> settings, bool merge = false);
        Task WriteSettingsAsync(Dictionary<string, string> settings, bool merge = false);
    }

    public sealed class RedisSettingsStore : ISettingsStore
    {
        private readonly ConnectionMultiplexer _redis;
        private readonly int _databaseNum;
        private readonly string _hashName;

        public RedisSettingsStore(ConnectionMultiplexer redis, int databaseNum, string hashName)
        {
            if (string.IsNullOrEmpty(hashName))
                throw new ArgumentNullException("hashName", "Hash name for Redis-backed settings cannot be null or empty");

            _redis = redis;
            _databaseNum = databaseNum;
            _hashName = hashName;

        }

        public Dictionary<string, string> ReadSettings()
        {
            var db = _redis.GetDatabase(_databaseNum);
            return db.HashScan(_hashName).ToDictionary(e => (string)e.Name, e => (string)e.Value);
        }

        public Task<Dictionary<string, string>> ReadSettingsAsync()
        {
            throw new NotImplementedException();
        }

        public void WriteSettings(Dictionary<string, string> settings, bool merge = false)
        {
            var db = _redis.GetDatabase(_databaseNum);
            
            // If we're not merging, nuke existing
            if (!merge)
            {
                // Don't delete all at once, delete in batches of 100
                var hashEntries = db.HashScan(_hashName).Select((e, i) => new { e, i }).GroupBy(x => x.i / 100).Select(g => g.Select(e => e.e.Name).ToArray());
                foreach (var fields in hashEntries)
                {
                    db.HashDelete(_hashName, fields);
                }
            }

            // Don't write all at once, write 100 at a time
            var chunks = settings.Select((kv, i) => new { kv, i }).GroupBy(x => x.i / 100).Select(x => x.Select(o => new HashEntry(o.kv.Key, o.kv.Value)).ToArray());

            
            foreach (var chunk in chunks)
            {
                db.HashSet(_hashName, chunk);
            }
        }

        public Task WriteSettingsAsync(Dictionary<string, string> settings, bool merge = false)
        {
            throw new NotImplementedException();
        }
    }
}
