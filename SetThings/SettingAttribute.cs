using System;

namespace SetThings
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class SettingAttribute : Attribute
    {
        public SettingAttribute() {}


        public SettingAttribute(string name)
        {
            Name = name;
        }


        public string Name { get; set; }
    }
}