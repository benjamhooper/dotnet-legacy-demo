using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace Tallgrass.Common
{
    /// <summary>
    /// Replacement for ConfigurationManager.AppSettings, written during the 2023 port
    /// so that nothing else had to change. It reads tallgrass.config next to the
    /// executable, then TALLGRASS_* environment variables.
    ///
    /// It is static, it is global, and every layer of the system calls it.
    /// </summary>
    public static class AppSettings
    {
        private static Dictionary<string, string> _settings;
        private static readonly object _lock = new object();

        public static string Get(string key)
        {
            EnsureLoaded();
            string value;
            if (_settings.TryGetValue(key, out value)) return value;
            return Environment.GetEnvironmentVariable("TALLGRASS_" + key.Replace(":", "_").ToUpperInvariant());
        }

        public static int GetInt(string key, int defaultValue)
        {
            int result;
            return int.TryParse(Get(key), out result) ? result : defaultValue;
        }

        public static bool GetBool(string key)
        {
            return string.Equals(Get(key), "true", StringComparison.OrdinalIgnoreCase);
        }

        public static string ConnectionString(string name)
        {
            var value = Get("ConnectionStrings:" + name);
            if (string.IsNullOrEmpty(value))
            {
                throw new InvalidOperationException(
                    "Connection string '" + name + "' is not configured. " +
                    "Set it in tallgrass.config or TALLGRASS_CONNECTIONSTRINGS_" + name.ToUpperInvariant() + ".");
            }
            return value;
        }

        private static void EnsureLoaded()
        {
            if (_settings != null) return;
            lock (_lock)
            {
                if (_settings != null) return;
                var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var path = Path.Combine(AppContext.BaseDirectory, "tallgrass.config");
                if (File.Exists(path))
                {
                    foreach (var add in XDocument.Load(path).Descendants("add"))
                    {
                        settings[(string)add.Attribute("key")] = (string)add.Attribute("value");
                    }
                }
                _settings = settings;
            }
        }
    }
}
