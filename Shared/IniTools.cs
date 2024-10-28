

#if !FEZCLIENT
using FezMultiplayerDedicatedServer;
#endif
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;

namespace FezSharedTools
{
    class IniTools
    {
        private const char IniKeyValDelimiter = '=';
        /// <summary>
        /// Reads the values from a file in INI format and assigns the values to the public fields of the same names in object <c>settings</c>.
        /// </summary>
        /// <typeparam name="T">The type of the settings object</typeparam>
        /// <param name="filepath">The file to read from.</param>
        /// <param name="settings">The object to put the values into.</param>
        /// <returns><c>settings</c>, with updated values</returns>
        /// <remarks>
        /// See also <seealso cref="IniTools.WriteSettingsFile{T}(string, T)"/>
        /// </remarks>
        public static T ReadSettingsFile<T>(string filepath, T settings)
        {
            if (!File.Exists(filepath))
            {
                return settings;
            }

            var fields = typeof(T).GetFields().ToDictionary(a => a.Name, StringComparer.InvariantCultureIgnoreCase);

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
        /// <summary>
        /// Writes the names and <c>DescriptionAttributes</c> of all public fields in class <c>T</c> to a file at location filepath in INI format.
        /// </summary>
        /// <typeparam name="T">The type of the settings object</typeparam>
        /// <param name="filepath">The file to write to.</param>
        /// <param name="settings">The object to write to the file.</param>
        /// <remarks>
        /// See also <seealso cref="IniTools.ReadSettingsFile{T}(string, T)"/>
        /// </remarks>
        public static void WriteSettingsFile<T>(string filepath, T settings)
        {
            Type TClass = typeof(T);
            List<string> lines = new List<string>()
            {
                "; "+TClass.Name+" settings",
                "",
                "; Note:",
                "; Everything has default values; if a setting is not in the settings file, the mod will use the default value and will add the setting with the default value to the settings file.",
                "; Also any modifications apart from the values for the settings will be erased.",
                "",
                "[Settings]",
            };

            FieldInfo[] fields = typeof(T).GetFields();
            foreach (var field in fields)
            {
                var desc = "; " + (field.GetCustomAttributes(typeof(DescriptionAttribute), true).FirstOrDefault() as DescriptionAttribute)?.Description;
                // desc = Regex.Replace(desc, @"(?<=[a-z]\. )", "\n; ");
                lines.Add(desc);
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
                                int port;
                                if (portsepindex < 0)
                                {
                                    portsepindex = a.Length;
                                    port = SharedConstants.DefaultPort;
                                    string msg = $"port for endpoint \"{a}\" not found. Using default port ({SharedConstants.DefaultPort})";
#if FEZCLIENT
                                    Common.Logger.Log("MultiplayerClientSettings", Common.LogSeverity.Warning, msg);
#endif
                                    Console.WriteLine("Warning: " + msg);
                                }
                                else
                                {
                                    port = int.Parse(a.Substring(portsepindex + 1));
                                }
                                addr = a.Substring(0, portsepindex).Replace("[", "").Replace("]", "");//Note: the replaces are for IPv6
                                return new IPEndPoint(IPAddress.Parse(addr), port);
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
#if !FEZCLIENT
                if (typeof(IPFilter).Equals(t))
                {
                    return new IPFilter(str);
                }
#endif
            }
            catch (Exception e)
            {
                throw e;//TODO?
            }

            throw new ArgumentException($"Type \"{t.FullName}\" is not supported.", "t");
        }
    }
}