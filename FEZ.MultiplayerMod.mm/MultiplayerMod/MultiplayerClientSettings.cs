using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Linq;
using System;

namespace FezGame.MultiplayerMod
{
    /// <summary>
    /// The class that contains the settings for <see cref="MultiplayerClient"/>
    /// </summary>
    public class MultiplayerClientSettings
    {
        /// <summary>
        /// The port to listen on
        /// </summary>
        public int listenPort = 7777;
        /// <summary>
        /// An array representing the main endpoint(s) to talk to.
        /// </summary>
        public IPEndPoint[] mainEndpoint = null;
        /// <summary>
        /// The amount of times to attempt to use the next port as the port to listen to, or to just throw an error. In case of an error, see <see cref="MultiplayerClient.ErrorMessage"/>
        /// </summary>
        public int maxAdjustListenPortOnBindFail = 1000;
        /// <summary>
        /// Determines if the IP addresses of the players should be relayed to all the other players.
        /// </summary>
        public bool serverless = true;
        /// <summary>
        /// The amount of time, in <see cref="System.DateTime.Ticks">ticks</see>, to wait before removing a player. For reference, there are 10000000 (ten million) ticks in one second.
        /// </summary>
        public long overduetimeout = 30_000_000;

        private const char IniKeyValDelimiter = '=';
        public static MultiplayerClientSettings ReadSettingsFile(string filepath)
        {
            MultiplayerClientSettings settings = new MultiplayerClientSettings();

            var fields = typeof(MultiplayerClientSettings).GetFields().ToDictionary(a => a.Name);

            string[] lines = File.ReadAllLines(filepath, System.Text.Encoding.UTF8);
            foreach (var line in lines)
            {
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith(";") || trimmed.Length <= 0)
                    continue;
                if (trimmed.StartsWith("["))
                {
                    continue;
                }
                var kvmatch = Regex.Match(trimmed, $@"(.*?)(?:{IniKeyValDelimiter}(.*))?$");
                string key = kvmatch.Groups[1].Value.Trim();
                string value = kvmatch.Groups[2].Success ? kvmatch.Groups[2].Value : "";

                if(fields.ContainsKey(key))
                {
                    var f = fields[key];
                    f.SetValue(settings, ParseObject(f.FieldType, value));
                }
            }

            return settings;
        }
        public static void WriteSettingsFile(string filepath, MultiplayerClientSettings settings)
        {
            List<string> lines = new List<string>();

            FieldInfo[] fields = typeof(MultiplayerClientSettings).GetFields();
            foreach(var field in fields)
            {
                lines.Add(field.Name + IniKeyValDelimiter + FormatObject(field.GetValue(settings)));
            }

            File.WriteAllLines(filepath, lines, System.Text.Encoding.UTF8);
        }
        private static string FormatObject(object obj)
        {
            //TODO
            return ""+obj;
        }
        private static object ParseObject(Type t, string str)
        {
            object obj = str;
            //TODO
            return obj;
        }
    }
}