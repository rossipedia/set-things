using System;
using Jil;
using SetThings;
using StackExchange.Redis;

namespace SampleApplication
{
    public static class App
    {
        private static void Main()
        {
            var multiplexer = ConnectionMultiplexer.Connect("localhost");
            var manager = new RedisSettingsManager<Settings>(multiplexer, 5, "settings");

            var settings = manager.Load();

            Console.WriteLine(JSON.Serialize(settings, Options.PrettyPrint));
            Console.WriteLine();

            var e = new System.Threading.ManualResetEvent(false);

            manager.SettingsUpdated += (sender, args) =>
            {
                settings = manager.Load();
                Console.WriteLine(JSON.Serialize(settings, Options.PrettyPrint));
                e.Set();
            };

            manager.BeginUpdate()
                   .Set(s => s.Analytics.LegacyEnabled, true)
                   .Set(s => s.Analytics.NewEnabled, true)
                   .Set(s => s.Network.CareersHost, "local.careers.stackoverflow.com")
                   .Set(s => s.Network.CalculonHost, "local.clc.stackoverflow.com")
                   .Set(s => s.Network.JoelCareersHost, "local.joeltest.com")
                   .Commit();

            e.WaitOne();
        }
    }
}