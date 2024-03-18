using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Linq;
using System;
using System.ComponentModel;

namespace FezGame.MultiplayerMod
{
    /// <summary>
    /// The class that contains the settings for <see cref="MultiplayerClient"/>
    /// </summary>
    public class MultiplayerClientSettings
    {
        private const int DefaultPort = 7777;
        /// <summary>
        /// The port to listen on
        /// </summary>
        [Description("The port to listen on")]
        public int listenPort = DefaultPort;
        /// <summary>
        /// An array representing the main endpoint(s) to talk to. Note: Currently only supports IPv4
        /// </summary>
        [Description("An comma-separated list representing the main endpoint(s) to talk to. Note: Currently only supports IPv4")]
        public IPEndPoint[] mainEndpoint = new[] { new IPEndPoint(IPAddress.Loopback, DefaultPort) };
        /// <summary>
        /// The amount of times to attempt to use the next port as the port to listen to before giving up. In case of an error, see <see cref="MultiplayerClient.ErrorMessage"/>
        /// </summary>
        [Description("The amount of times to attempt to use the next port as the port to listen to before giving up.")]
        public int maxAdjustListenPortOnBindFail = 1000;
        /// <summary>
        /// Determines if the IP addresses of the players should be relayed to all the other players.
        /// </summary>
        [Description("Determines if the IP addresses of the players should be relayed to all the other players.")]
        public bool serverless = true;
        /// <summary>
        /// The amount of time, in <see cref="System.DateTime.Ticks">ticks</see>, to wait before removing a player. For reference, there are 10000000 (ten million) ticks in one second.
        /// </summary>
        [Description("The amount of time, in ticks, to wait before removing a player. For reference, there are 10000000 (ten million) ticks in one second.")]
        public long overduetimeout = 30_000_000;
        /// <summary>
        /// A string representing the name to display for this client. Must contain only printable ASCII characters
        /// </summary>
        [Description("A string representing the name to display for this client.")]
        public string myPlayerName = "Player";

        private const char IniKeyValDelimiter = '=';
        private const string FezMultiplayerModVersionName = "FezMultiplayerMod.Version";//TODO use to check the settings file version?
        public static MultiplayerClientSettings ReadSettingsFile(string filepath)
        {
            MultiplayerClientSettings settings = new MultiplayerClientSettings();

            if(!File.Exists(filepath)){
                return settings;
            }

            var fields = typeof(MultiplayerClientSettings).GetFields().ToDictionary(a => a.Name, StringComparer.InvariantCultureIgnoreCase);

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

                if (fields.ContainsKey(key))
                {
                    var f = fields[key];
                    object v = ParseObject(f.FieldType, value.Trim());
                    if (v != null)
                    {
                        f.SetValue(settings, v);
                    }
                }
            }

            return settings;
        }
        public static void WriteSettingsFile(string filepath, MultiplayerClientSettings settings)
        {
            List<string> lines = new List<string>()
            {
                "; FezMultiplayerMod settings",
                "",
                "; Note:",
                "; Everything has default values; if a setting is not in the settings file, the mod will use the default value and will add the setting with the default value to the settings file.",
                "; Also any modifications apart from the values for the settings will be erased.",
                "",
        #if FEZCLIENT
        //TODO fix FezMultiplayerMod.Version not existing on the standalone server executable
                "[Metadata]",
                $"{FezMultiplayerModVersionName}{IniKeyValDelimiter}{FezMultiplayerMod.Version}",
        #endif
                "",
                "[Settings]",
            };

            FieldInfo[] fields = typeof(MultiplayerClientSettings).GetFields();
            foreach (var field in fields)
            {
                lines.Add("; " + (field.GetCustomAttributes(typeof(DescriptionAttribute), true).FirstOrDefault() as DescriptionAttribute)?.Description);
                lines.Add(field.Name + IniKeyValDelimiter + FormatObject(field.GetValue(settings)));
                lines.Add("");
            }

            File.WriteAllLines(filepath, lines, System.Text.Encoding.UTF8);
        }
        private static string FormatObject(object obj)
        {
            if (obj == null)
            {
                return "";
            }
            if (obj.GetType().IsArray)
            {
                object[] arr = (object[])obj;
                return String.Join(", ", arr);
            }
            return "" + obj;
        }
        private static object ParseObject(Type t, string str)
        {
            try
            {
                if (typeof(long).Equals(t))
                {
                    return long.Parse(str);
                }
                if (typeof(int).Equals(t))
                {
                    return int.Parse(str);
                }
                if (typeof(bool).Equals(t))
                {
                    return str != null && str.Equals("true", StringComparison.InvariantCultureIgnoreCase);//bool.Parse(str);
                }
                if (t.IsArray)
                {
                    Type elementType = t.GetElementType();
                    if (typeof(IPEndPoint).Equals(elementType))
                    {
                        return str.Split(',').Select(a =>
                        {
                            try
                            {
                                a = a.Trim();
                                int portsepindex = a.LastIndexOf(':');
                                string addr;//Note: the replaces are for IPv6
                                string port;
                                if (portsepindex < 0)
                                {
                                    portsepindex = a.Length;
                                    port = "" + DefaultPort;
                                    string msg = $"port for endpoint \"{a}\" not found. Using default port ({DefaultPort})";
                                    #if FEZCLIENT
                                    Common.Logger.Log("MultiplayerClientSettings", Common.LogSeverity.Warning, msg);
                                    #endif
                                    Console.WriteLine("Warning: " + msg);
                                }
                                else
                                {
                                    port = a.Substring(portsepindex + 1);
                                }
                                addr = a.Substring(0, portsepindex).Replace("[", "").Replace("]", "");//Note: the replaces are for IPv6
                                return new IPEndPoint(IPAddress.Parse(addr), int.Parse(port));
                            }
                            catch (Exception e)
                            {
                                throw new ArgumentException($"String \"{a}\" is not a valid IPEndPoint.", "str", e);
                            }
                        }).ToArray();
                    }
                }
                if(typeof(string).Equals(t))
                {
                    return str;
                }
            }
            catch (Exception e)
            {
                throw e;//TODO?
            }

            throw new ArgumentException($"Type \"{t.FullName}\" is not supported.", "t");
        }
    }
}