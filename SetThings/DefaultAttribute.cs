using System;

namespace SetThings
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class DefaultAttribute : Attribute
    {
        public string Value { get; set; }
    }
}