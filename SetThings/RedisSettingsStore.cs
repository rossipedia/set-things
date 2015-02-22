using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace SetThings
{
    public sealed class RedisSettingsStore : ISettingsStore
    {
        private readonly int _databaseNum;
        private readonly string _hashName;
        private readonly ConnectionMultiplexer _redis;


        public RedisSettingsStore(ConnectionMultiplexer redis, int databaseNum, string hashName)
        {
            if (string.IsNullOrEmpty(hashName))
            {
                throw new ArgumentNullException(
                    "hashName",
                    "Hash name for Redis-backed settings cannot be null or empty");
            }

            _redis = redis;
            _databaseNum = databaseNum;
            _hashName = hashName;
        }


        public Dictionary<string, string> ReadSettings() =>
            _redis.GetDatabase(_databaseNum)
                  .HashScan(_hashName)
                  .ToDictionary(e => (string) e.Name, e => (string) e.Value);


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
                var hashEntries = db.HashScan(_hashName)
                                    .Select((e, i) => new {e, i})
                                    .GroupBy(x => x.i/100)
                                    .Select(g => g.Select(e => e.e.Name).ToArray());

                foreach (var fields in hashEntries)
                {
                    db.HashDelete(_hashName, fields);
                }
            }

            // Don't write all at once, write 100 at a time
            var chunks = settings
                .Select((kv, i) => new {kv, i})
                .GroupBy(x => x.i/100)
                .Select(
                    x =>
                    x.Select(o => new HashEntry(o.kv.Key, o.kv.Value)).ToArray());


            foreach (var chunk in chunks)
            {
                db.HashSet(_hashName, chunk);
            }
        }


        public async Task WriteSettingsAsync(Dictionary<string, string> settings, bool merge = false)
        {
            var db = _redis.GetDatabase(_databaseNum);

            // If we're not merging, nuke existing
            if (!merge)
            {
                // Don't delete all at once, delete in batches of 100
                var hashEntries = db.HashScan(_hashName)
                                    .Select((e, i) => new {e, i})
                                    .GroupBy(x => x.i/100)
                                    .Select(g => g.Select(e => e.e.Name).ToArray());

                foreach (var fields in hashEntries)
                {
                    await db.HashDeleteAsync(_hashName, fields);
                }
            }

            // Don't write all at once, write 100 at a time
            var chunks = settings
                .Select((kv, i) => new {kv, i})
                .GroupBy(x => x.i/100)
                .Select(
                    x =>
                    x.Select(o => new HashEntry(o.kv.Key, o.kv.Value)).ToArray());


            foreach (var chunk in chunks)
            {
                await db.HashSetAsync(_hashName, chunk);
            }
        }
    }
}