using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace SetThings
{
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
}
