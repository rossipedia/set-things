using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;

namespace SetThings
{
    internal static class ReflectionExtensions
    {
        internal static Func<Dictionary<string, string>, TSettings> BuildSettingsReader<TSettings>(this Type type)
            where TSettings : class, new()
        {
            if (type == null)
                throw new ArgumentNullException("type");

            if (typeof (TSettings) != type)
                throw new ArgumentException("Type mismatch: \"" + typeof (TSettings).FullName + "\" != \"" + type.FullName +
                                            "\"");


            var rawSettingsArg = Expression.Parameter(typeof (Dictionary<string, string>), "raw");
            var parameterExprs = new[] {rawSettingsArg};

            var statements = new List<Expression>();

            // var settings = new TSettings();
            var defaultCtor = type.GetConstructor(Type.EmptyTypes);
            if (defaultCtor == null)
                throw new ArgumentException("Type \"" + type.FullName + "\" does not define a default constructor");

            var settingsVar = Expression.Variable(type, "settings");
            var newSettings = Expression.Assign(settingsVar, Expression.New(defaultCtor));

            statements.Add(newSettings);
            BuildReadersOrWriters(s_getRawValueMethod, rawSettingsArg, type, settingsVar, statements);
            // return settings;
            statements.Add(settingsVar);

            var block = Expression.Block(type, new[] {settingsVar}, statements);
            var lambda = Expression.Lambda<Func<Dictionary<string, string>, TSettings>>(block, "Write" + type.Name,
                parameterExprs);
            return lambda.Compile();
        }

        internal static Action<Dictionary<string, string>, TSettings> BuildSettingsWriter<TSettings>(this Type type)
            where TSettings : class, new()
        {
            if (type == null)
                throw new ArgumentNullException("type");

            if (typeof (TSettings) != type)
                throw new ArgumentException("Type mismatch: \"" + typeof (TSettings).FullName + "\" != \"" + type.FullName + "\"");
            
            var rawSettingsArg = Expression.Parameter(typeof (Dictionary<string, string>), "raw");
            var settingsVar = Expression.Parameter(type, "settings");
            var parameterExprs = new[] {rawSettingsArg, settingsVar};

            var statements = new List<Expression>();

            // var settings = new TSettings();
            var defaultCtor = type.GetConstructor(Type.EmptyTypes);
            if (defaultCtor == null)
                throw new ArgumentException("Type \"" + type.FullName + "\" does not define a default constructor");

            
            var newSettings = Expression.Assign(settingsVar, Expression.New(defaultCtor));

            statements.Add(newSettings);
            BuildReadersOrWriters(s_setRawValueMethod, rawSettingsArg, type, settingsVar, statements);
            // return settings;
            statements.Add(settingsVar);

            var block = Expression.Block(type, statements);
            var lambda = Expression.Lambda<Action<Dictionary<string, string>, TSettings>>(block, "Write" + type.Name,
                parameterExprs);
            return lambda.Compile();
        }

        private static void BuildReadersOrWriters(MethodInfo setOrGet, Expression rawSettings, Type type, Expression parent, List<Expression> statements, string prefix = null)
        {
            var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            foreach (var prop in properties)
            {
                var propRef = Expression.Property(parent, prop);
                var isDirect = !prop.PropertyType.IsDefined(typeof(SettingsAttribute));
                if (isDirect)
                {
                    var key = Expression.Constant(prop.GetSettingsKey(prefix), typeof(string));
                    var defaultVal = Expression.Constant(GetDefaultValue(prop), typeof(string));
                    var method = setOrGet.MakeGenericMethod(prop.PropertyType);
                    var getValueCall = Expression.Call(method, new[] { rawSettings, key, defaultVal, propRef });
                    statements.Add(getValueCall);
                }
                else
                {
                    // new T()
                    statements.Add(Expression.Assign(propRef, Expression.New(prop.PropertyType)));
                    BuildReadersOrWriters(setOrGet, rawSettings, prop.PropertyType, propRef, statements, prop.Name);
                }
            }
        }

        
        private static readonly MethodInfo s_getRawValueMethod = typeof(ReflectionExtensions).GetMethod("GetRawValue", BindingFlags.Static | BindingFlags.NonPublic);
        // ReSharper disable once UnusedMember.Local
        private static void GetRawValue<T>(Dictionary<string, string> rawSettings, string key, string defaultValue, ref T target)
        {
            string val;
            var converter = TypeDescriptor.GetConverter(typeof(T));
            if (rawSettings.TryGetValue(key, out val))
            {
                target = (T)converter.ConvertFromInvariantString(val);
            }
            else if (defaultValue != null)
            {
                target = (T)converter.ConvertFromInvariantString(defaultValue);
            }
        }

        private static readonly MethodInfo s_setRawValueMethod = typeof(ReflectionExtensions).GetMethod("SetRawValue", BindingFlags.Static | BindingFlags.NonPublic);
        // ReSharper disable once UnusedMember.Local
        // ReSharper disable once UnusedParameter.Local
        private static void SetRawValue<T>(Dictionary<string, string> rawSettings, string key, string defaultValue, T value)
        {
            rawSettings.Add(key, Convert.ToString(value));
        }

        internal static string GetDefaultValue(this PropertyInfo prop)
        {
            var attr = prop.GetCustomAttribute<DefaultAttribute>();
            return attr != null ? attr.Value : null;
        }

        internal static string GetSettingsKey(this PropertyInfo prop, string prefix)
        {
            var attr = prop.GetCustomAttribute<SettingAttribute>();
            return (prefix != null ? prefix + "." : "") + (attr != null ? attr.Name : prop.Name);
        }

        public static string GetSettingKey<TSettings, TProp>(this Expression<Func<TSettings, TProp>> setting)
        {
            var parts = new Stack<string>();
            var expr = setting.Body as MemberExpression;
            while (expr != null)
            {
                var prop = (PropertyInfo)expr.Member;
                parts.Push(prop.GetSettingsKey(null));
                expr = expr.Expression as MemberExpression;
            }

            return string.Join(".", parts);
        }
    }
}
