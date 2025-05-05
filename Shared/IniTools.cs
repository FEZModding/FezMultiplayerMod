

#if !FEZCLIENT
using FezMultiplayerDedicatedServer;
#endif
#if FEZCLIENT
using FezGame.MultiplayerMod;
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
    internal static class TypeExtensions
    {
        public static bool IsListType(this Type type)
        {
            return (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>));
        }
    }
    internal sealed class IniTools
    {
        
        /// <summary>
        /// Attempts to parse the provided string into an IP endpoint, throwing an <see cref="ArgumentException"/> if it is not in a valid format. 
        /// </summary>
        /// <remarks>
        /// Note: For IPV6 endpoints, please encase the IPV6 address in square brackets, followed by a colon and then the port. e.g. <c>[::1]:7777</c><br />
        /// For IPV4 endpoints, the typical dotted decimal notation (e.g. <c>127.0.0.1:7777</c>) is the correct format.
        /// </remarks>
        /// <param name="str">The string to attempt to parse into an IP Endpoint</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">if the provided string is not in a valid format</exception>
        public static IPEndPoint TryParseIPEndPoint(string str)
        {
            try
            {
                str = str.Trim();
                int portsepindex = str.LastIndexOf(':');
                string addr;//Note: the replaces are for IPv6
                int port;
                if (portsepindex < 0)
                {
                    portsepindex = str.Length;
                    port = SharedConstants.DefaultPort;
                    string msg = $"port for endpoint \"{str}\" not found. Using default port ({SharedConstants.DefaultPort})";
#if FEZCLIENT
                    Common.Logger.Log("MultiplayerClientSettings", Common.LogSeverity.Warning, msg);
#endif
                    Console.WriteLine("Warning: " + msg);
                }
                else
                {
                    port = int.Parse(str.Substring(portsepindex + 1));
                }
                addr = str.Substring(0, portsepindex).Replace("[", "").Replace("]", "");//Note: the replaces are for IPv6
                return new IPEndPoint(IPAddress.Parse(addr), port);
            }
            catch (Exception e)
            {
                throw new ArgumentException($"String \"{str}\" is not a valid IPEndPoint.", "str", e);
            }
        }
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

            Dictionary<string, FieldInfo> fields = typeof(T).GetFields().ToDictionary(a => a.Name, StringComparer.InvariantCultureIgnoreCase);

            string[] lines = File.ReadAllLines(filepath, System.Text.Encoding.UTF8);
            foreach (string line in lines)
            {
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith(";") || trimmed.Length <= 0)
                    continue;
                if (trimmed.StartsWith("["))
                {
                    continue;
                }
                Match kvmatch = Regex.Match(trimmed, $@"(.*?)(?:{IniKeyValDelimiter}(.*))?$");
                string key = kvmatch.Groups[1].Value.Trim();
                string value = kvmatch.Groups[2].Success ? kvmatch.Groups[2].Value : "";

                if (fields.ContainsKey(key))
                {
                    FieldInfo f = fields[key];
                    object v = ParseObject(f.FieldType, value.Trim(), f, settings);
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
                "; Everything has default values; if a setting is not in the settings file, the code will use the default value and will add the setting with the default value to the settings file.",
                "; Also any modifications apart from the values for the settings will be erased.",
                "",
                "[Settings]",
            };

            FieldInfo[] fields = typeof(T).GetFields();
            foreach (FieldInfo field in fields)
            {
                string desc = "; " + (field.GetCustomAttributes(typeof(DescriptionAttribute), true).FirstOrDefault() as DescriptionAttribute)?.Description;
                // desc = Regex.Replace(desc, @"(?<=[a-z]\. )", "\n; ");
                lines.Add(desc);
                lines.Add(field.Name + IniKeyValDelimiter + FormatObject(field.GetValue(settings)));
                lines.Add("");
            }

            File.WriteAllLines(filepath, lines, System.Text.Encoding.UTF8);
        }
        private const char RecordSeparator = '\x1E';
        private static string FormatObject(object obj)
        {
            if (obj == null)
            {
                return "";
            }
            if (obj.GetType().IsListType())
            {
                System.Collections.IList list = ((System.Collections.IList)obj);
                string s = "";
                for(int i=0;i<list.Count; ++i)
                {
                    s += list[i];
                    if(i+1<list.Count){
                        s += RecordSeparator.ToString();
                    }
                }
                return s;
            }
            return "" + obj;
        }
        private static object ParseObject(Type t, string str, FieldInfo fieldInfo, object containingObject)
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
                if (typeof(IPEndPoint).Equals(t))
                {
                    return TryParseIPEndPoint(str);
                }
                if (t.IsListType())
                {
                    Type listItemType = fieldInfo.FieldType.GetGenericArguments()[0];
                    System.Collections.IList list = (System.Collections.IList)fieldInfo.GetValue(containingObject);
                    list.Clear();
                    str.Split(RecordSeparator).Select(a =>
                    {
                        return ParseObject(listItemType, a, fieldInfo, containingObject);
                    }).Where(s => s != null).ToList().ForEach(a =>
                    {
                        list.Add(a);
                    });
                    return list;
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
#if FEZCLIENT
                if (typeof(ServerInfo).Equals(t))
                {
                    try
                    {
                        return ServerInfo.Parse(str);
                    }
                    catch(ArgumentException)
                    {
                        return null;
                    }
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