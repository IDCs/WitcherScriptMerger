using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using WitcherScriptMerger.Common;

namespace WitcherScriptMerger
{
    class AppSettings
    {
        Configuration _cachedConfig;
        Configuration CachedConfig
        {
            get
            {
                if (_cachedConfig == null)
                    _cachedConfig = OpenConfiguration();
                return _cachedConfig;
            }
        }

        public bool HasConfigFile => CachedConfig.HasFile;

        /// <summary>
        /// Paths the configuration may sit beside, in preference order. A
        /// release ships WitcherScriptMerger.exe.config and Vortex writes the
        /// game and mod directories into it by name, so the apphost comes
        /// first; the assembly-named config a plain build emits is the fallback.
        /// </summary>
        static IEnumerable<string> ConfigBasePaths()
        {
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(processPath) &&
                string.Equals(Path.GetFileName(processPath), KnownPaths.Executable, StringComparison.OrdinalIgnoreCase))
            {
                yield return processPath;
            }

            var assemblyPath = Assembly.GetEntryAssembly()?.Location;
            if (!string.IsNullOrEmpty(assemblyPath))
            {
                yield return assemblyPath;
            }
        }

        static Configuration OpenConfiguration()
        {
            Configuration lastTried = null;
            foreach (var basePath in ConfigBasePaths())
            {
                lastTried = ConfigurationManager.OpenExeConfiguration(basePath);
                if (lastTried.HasFile)
                {
                    return lastTried;
                }
            }
            // Nothing on disk. Returned anyway so the caller reports the miss.
            return lastTried;
        }

        public AppSettings()
        {
            if (CachedConfig == null || !CachedConfig.HasFile)
            {
                MessageBox.Show(
                    "Config file is missing.",
                    "Script Merger Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Environment.Exit(1);
            }
        }

        public void Set(string key, object value)
        {
            try
            {
                CachedConfig.AppSettings.Settings[key].Value = value.ToString();
            }
            catch
            {
                CachedConfig.AppSettings.Settings.Add(key, value.ToString());
            }
        }

        public T Get<T>(string key)
        {
            try
            {
                if (CachedConfig.HasFile)
                {
                    var valueString = CachedConfig.AppSettings.Settings[key].Value;
                    var parseMethod = typeof(T).GetMethod("Parse", new Type[] { typeof(string) });
                    var valueObject = parseMethod.Invoke(null, new object[] { valueString });
                    return (T)valueObject;
                }

                Program.MainForm.ShowError($"Config file doesn't exist:\n\n{CachedConfig.FilePath}");
                return default(T);
            }
            catch
            {
                return default(T);
            }
        }

        public string Get(string key)
        {
            try
            {
                if (CachedConfig.HasFile)
                    return CachedConfig.AppSettings.Settings[key].Value;

                Program.MainForm.ShowError($"Config file doesn't exist:\n\n{CachedConfig.FilePath}");
                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public void Save()
        {
            try
            {
                CachedConfig.Save(ConfigurationSaveMode.Minimal);
            }
            catch (Exception ex)
            {
                Program.MainForm.ShowError($"Failed to save config due to error:\n\n{ex.Message}");
            }
        }
    }
}
