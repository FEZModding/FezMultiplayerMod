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
        /// <summary>
        /// TODO add description
        /// </summary>
        [Description("To do: add description")]
        public bool useAllowList = false;
        /// <summary>
        /// If useAllowList is true, only packets from IP addresses included in this list will be accepted.
        /// <inheritdoc cref='IPFilter.IPFilter(string)'/>
        /// </summary>
        [Description("To do: add description")]
        public IPFilter AllowList = new IPFilter("");
        /// <summary>
        /// Packets from IP addresses included in this list will be ignored.
        /// <inheritdoc cref='IPFilter.IPFilter(string)' path="//para[@name='desc']"/>
        /// </summary>
        [Description("To do: add description")]
        public IPFilter BlockList = new IPFilter("");

        private const char IniKeyValDelimiter = '=';
        private const string FezMultiplayerModVersionName = "FezMultiplayerMod.Version";//TODO use to check the settings file version?
        public static MultiplayerClientSettings ReadSettingsFile(string filepath)
        {
            MultiplayerClientSettings settings = new MultiplayerClientSettings();

            if (!File.Exists(filepath))
            {
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
                if (typeof(string).Equals(t))
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

    public class IPFilter
    {
        private string filterString;
        public string FilterString
        {
            get => filterString;
            set
            {
                filterString = value;
                ReloadFilterString();
            }
        }

        private List<IPAddressRange> ranges = new List<IPAddressRange>();
        private void ReloadFilterString()
        {
            ranges.Clear();
            var entries = filterString.Split(',');
            foreach (string entry in entries)
            {
                var str = entry.Trim();
                if (str.Contains(":"))
                {
                    throw new NotImplementedException("IPv6 is currently not supported");
                }
                IPAddress low = IPAddress.Any, high = IPAddress.Any;
                if (Regex.IsMatch(@"\d+\.\d+\.\d+\.\d+", str))
                {
                    //single IP address
                    low = high = IPAddress.Parse(str);
                }
                else if (Regex.IsMatch(@"\A\d+\.\d+\.\d+\.\d+/\d+\Z", str))
                {
                    //CIDR format
                    var parts = str.Split('/');

                    //Important Note: all four of these UInt32 are in network order, not host order, so don't do comparisons with them
                    //convert IP string to UInt32
                    UInt32 b = BitConverter.ToUInt32(IPAddress.Parse(parts[0]).GetAddressBytes(), 0);
                    UInt32 mask = (UInt32)IPAddress.HostToNetworkOrder((Int32)Math.Pow(2, 32 - int.Parse(parts[1])) - 1);
                    UInt32 lowb = (UInt32)(b & ~mask);
                    UInt32 highb = (UInt32)(b | mask);

                    //convert Int32 back to IPAddress
                    low = new IPAddress(lowb);
                    high = new IPAddress(highb);
                }
                else if (str.Contains("-"))
                {
                    //range or implied range ( could be "10.5.3.3-10.5.3.40" or "10.5.3.3-40" )
                    //TODO
                }
                else if (Regex.IsMatch(@"\A(\d+\.){1,3}\Z", str))
                {
                    //Implied IP address (e.g., 10. )
                    //TODO
                }
                else
                {
                    //unsupported syntax
                    continue;
                }
                //TODO
                ranges.Add(new IPAddressRange(low, high));
            }
        }

        private static UInt32 IPAddressToHostUInt32(IPAddress address)
        {
            if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                throw new ArgumentException("Expected an IPv4 address, got " + address + " instead");
            }
            return (UInt32)IPAddress.NetworkToHostOrder((Int32)BitConverter.ToUInt32(address.GetAddressBytes(), 0));
        }
        private struct IPAddressRange
        {
            private readonly UInt32 low;
            private readonly UInt32 high;

            public IPAddressRange(IPAddress low, IPAddress high)
            {
                this.low = IPAddressToHostUInt32(low);
                this.high = IPAddressToHostUInt32(high);
            }
            public bool Contains(IPAddress address)
            {
                var val = IPAddressToHostUInt32(address);
                return val >= low && val <= high;
            }
        }

        /// <summary>
        /// <para>
        /// Creates a new IP Filter from the provided string, with the syntax being comma-separated entries according to the syntax described on
        /// <see href="https://docs.cpanel.net/cpanel/security/ip-blocker/">cPanel Docs "IP Blocker" page</see>,
        /// which should be the same as the following list.
        /// </para>
        /// <para name="desc">
        /// You can enter IP addresses as comma-separated entries of any combination of the following formats:
        /// <list type="bullet">
        ///     <item>
        ///         <description>Single IP address, e.g., <c>10.5.3.33</c> </description>
        ///     </item>
        ///     <item>
        ///         <description>Range, e.g., <c>10.5.3.3-10.5.3.40</c> </description>
        ///     </item>
        ///     <item>
        ///         <description>Implied range, e.g., <c>10.5.3.3-40</c> </description>
        ///     </item>
        ///     <item>
        ///         <description>CIDR format (for example, in <c>10.56.27.0/24</c>,
        ///         the first 24 bits are constant, and the last 8 bits are wild,
        ///         so the resulting range is <c>10.56.27.0</c> to <c>10.56.27.255</c></description>
        ///     </item>
        ///     <item>
        ///         <description>Implied IP address (for example, <c>10.</c> gets interpreted as <c>10.*.*.*</c></description>
        ///     </item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="filterString">The string to convert to an IP Filter</param>
        public IPFilter(string filterString)
        {
            FilterString = filterString;
        }

        public bool Contains(IPAddress address)
        {
            return ranges.Any(range => range.Contains(address));
        }

        public override string ToString()
        {
            return FilterString;
        }
    }
}