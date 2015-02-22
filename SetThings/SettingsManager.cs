using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace SetThings
{
    public abstract class SettingsManager<TSettings>
        where TSettings : class, new()
    {
        private static readonly Func<Dictionary<string, string>, TSettings> s_loadFromRaw;
        private readonly ISettingsStore _store;


        static SettingsManager()
        {
            s_loadFromRaw = typeof (TSettings).BuildSettingsReader<TSettings>();
        }


        protected SettingsManager(ISettingsStore store)
        {
            _store = store;
        }


        public TSettings Load() => s_loadFromRaw(_store.ReadSettings());
        public async Task<TSettings> LoadAsync() => s_loadFromRaw(await _store.ReadSettingsAsync());

        public virtual IUpdateSettings BeginUpdate() => new Updater(this);

        internal virtual void WriteSettings(Dictionary<string, string> settings, bool merge = false) => _store.WriteSettings(settings, merge);

        public interface IUpdateSettings
        {
            IUpdateSettings Set<TProp>(Expression<Func<TSettings, TProp>> setting, TProp newValue)
                where TProp : IConvertible;


            IUpdateSettings Reset<TProp>(Expression<Func<TSettings, TProp>> setting) where TProp : IConvertible;
            void Commit();
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
                const string invalidSettingMessage = "setting expression must be a property access";
                if (setting.Body.NodeType != ExpressionType.MemberAccess)
                {
                    throw new ArgumentException(invalidSettingMessage);
                }

                var prop = ((MemberExpression) setting.Body).Member as PropertyInfo;
                if (prop == null)
                {
                    throw new ArgumentException(invalidSettingMessage);
                }

                var key = setting.GetSettingKey();
                var converter = TypeDescriptor.GetConverter(typeof (TProp));
                var value = newValue as string ?? converter.ConvertToInvariantString(newValue);
                _pendingUpdates.Add(key, value);

                return this;
            }


            public IUpdateSettings Reset<TProp>(Expression<Func<TSettings, TProp>> setting)
                where TProp : IConvertible
            {
                // Get the property info
                if (setting.Body.NodeType != ExpressionType.MemberAccess)
                {
                    throw new ArgumentException("setting expression must be a property access");
                }

                var prop = ((MemberExpression) setting.Body).Member as PropertyInfo;
                if (prop == null)
                {
                    throw new ArgumentException("setting expression must be a property access");
                }

                var key = setting.GetSettingKey();
                var val = prop.GetDefaultValue();
                _pendingUpdates.Add(key, val);
                return this;
            }


            public virtual void Commit() => 
                _owner.WriteSettings(_pendingUpdates, merge: true);
        }
    }
}