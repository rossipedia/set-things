// ReSharper disable UnusedAutoPropertyAccessor.Local

using SetThings;

namespace SampleApplication
{
    public class Settings
    {
        public AnalyticsSettings Analytics { get; private set; }
        
        [Settings]
        public class AnalyticsSettings
        {
            public bool NewEnabled { get; private set; }
            public bool LegacyEnabled { get; private set; }
        }

        
        public NetworkSettings Network { get; private set; }
        
        [Settings]
        public class NetworkSettings
        {
            [Default(Value="careers.stackoverflow.com")]
            public string CareersHost { get; private set; }
            public string CalculonHost { get; private set; }
            public string JoelCareersHost { get; private set; }
        }
    }
}