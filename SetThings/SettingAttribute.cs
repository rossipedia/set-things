using System;

namespace SetThings
{
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
}