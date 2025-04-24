using FezSharedTools;
using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Timers;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Text;
using System.Globalization;

namespace FezMultiplayerDedicatedServer
{
    sealed class FezDedicatedServer
    {
        private static MultiplayerServerNetcode server;
        static void Main(string[] args)
        {
            //TODO add more to this, like command line parameters and connection logs

            Console.WriteLine($"FezMultiplayerMod server starting... (protocol ver: {MultiplayerServerNetcode.ProtocolVersion})");

            Queue<string> queue = new Queue<string>();
            foreach (string item in args)
            {
                queue.Enqueue(item);
            }

            string SettingsFilePath = "FezMultiplayerServer.ini";

            //Note: to include spaces in the file path, enclose the entire path in double quotes ""
            while (queue.Count > 0)
            {
                string val;
                switch (val = queue.Dequeue().ToLower(CultureInfo.InvariantCulture))
                {
                case "--settings-file":
                    SettingsFilePath = queue.Dequeue();
                    break;
                default:
                    Console.WriteLine($"Invalid switch - \"{val}\"");
                    break;
                }
            }

            Console.WriteLine($"Loading settings from {SettingsFilePath}");
            MultiplayerServerSettings settings = IniTools.ReadSettingsFile(SettingsFilePath, new MultiplayerServerSettings());
            IniTools.WriteSettingsFile(SettingsFilePath, settings);

            Console.WriteLine("Initializing server...");
            server = new MultiplayerServerNetcode(settings);

            //MultiplayerServerSettings.WriteSettingsFile(SettingsFilePath, settings);//TODO

            //Wait for the server netcode to finish initializing
            while (server.LocalEndPoint == null && server.FatalException == null)
            {
                System.Threading.Thread.Sleep(1);
            }
            if (server.FatalException != null)
            {
                Console.WriteLine(server.ErrorMessage);
                Console.WriteLine(server.FatalException);
            }

            //Note: the following line can fail due to race conditions, since the listening thread might not be initialized yet; this is the reason of the above sleep
            Console.WriteLine("Listening on port " + ((System.Net.IPEndPoint)server.LocalEndPoint).Port);

            GetLocalIPAddresses();

            Timer myTimer = new Timer();
            myTimer.Elapsed += (a, b) => { server.Update(); };
            myTimer.Interval = 1f / 60f * 1000; // 1000 ms is one second
            myTimer.Start();

            //Note: gotta keep the program busy otherwise it'll close

            //TODO make the CLI better; see https://learn.microsoft.com/en-us/dotnet/api/system.console , particularly Console.SetCursorPosition
            //I want a nice animated one that automatically updates what it writes on the screen

            try
            {
                string line;
                bool running = true;
                var cliActions = new Dictionary<string, (string desc, Action action)>
                {
                    {
                        "exit".ToLowerInvariant(),
                        ("Stops the server and closes the program", () =>
                        {
                            running = false;
                        })
                    },
                    {
                        "players".ToLowerInvariant(),
                        ("Lists currently connected players", () =>
                        {
                            string s = "Connected players:\n";
                            string[] columns = { };
                            s += FormatPlayerDataTabular(out int count);
                            s += $"{count} players online";
                            Console.WriteLine(s);
                        })
                    },
                    {
                        "dis".ToLowerInvariant(),
                        ("Lists disconnected players", () =>
                        {
                            string s = "Disconnected players:\n";
                            int count = 0;
                            foreach (var kvpair in server.DisconnectedPlayers)
                            {
                                count++;
                                s += $"{kvpair.Key}, {(DateTime.UtcNow.Ticks - kvpair.Value) / (double)TimeSpan.TicksPerSecond}\n";
                            }
                            if(count == 0)
                            {
                                s += "None";
                            }
                            Console.WriteLine(s);
                        })
                    },
                    {
                        "appear".ToLowerInvariant(),
                        ("Lists players appearances", () =>
                        {
                            string s = "Player appearances:\n";
                            int count = 0;
                            foreach (var kvpair in server.PlayerAppearances)
                            {
                                count++;
                                s += $"{kvpair.Key}: Name=\"{kvpair.Value.PlayerName}\x1B[0m\" Appearance=\"{kvpair.Value.CustomCharacterAppearance}\"\n";
                            }
                            if(count == 0)
                            {
                                s += "None";
                            }
                            Console.WriteLine(s);
                        })
                    },
                };
                int maxCommandLength = cliActions.Max(kv => kv.Key.Length);
                (string desc, Action action) HelpCommand;
                string helpCmdName = "help".ToLowerInvariant();
                cliActions.Add(helpCmdName,
                    HelpCommand = ("Lists available commands", () =>
                    {
                        Console.WriteLine("Available commands:");
                        foreach (var kvpair in cliActions)
                        {
                            Console.WriteLine($"{kvpair.Key.PadRight(maxCommandLength, ' ')} - {kvpair.Value.desc}");
                        }
                    }
                ));
                Console.WriteLine($"Use {helpCmdName} to list available commands");

                while (running)
                {
                    line = Console.ReadLine().Trim().ToLowerInvariant();
                    bool validAction = cliActions.TryGetValue(line, out var tuple);
                    if (validAction)
                    {
                        tuple.action();
                    }
                    else
                    {
                        Console.WriteLine($"Unknown command: \"{line}\"");
                        HelpCommand.action();
                    }
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                System.Diagnostics.Debugger.Launch();
            }
            server.Dispose();
        }
        /// <summary>
        /// Formats the player information into a tabular format
        /// </summary>
        /// <param name="count">This gets incremented for each item in the collection</param>
        /// <returns>A string containing the player information in a tabular format</returns>
        private static string FormatPlayerDataTabular(out int count)
        {
            ///The string to use for padding columns
            const string T_GAP_COL = "   ";

            /// Key is column name, list contains entries in that column
            Dictionary<string, List<string>> tDat = new Dictionary<string, List<string>>();

            /// Contains the max string length of each column in tDat;
            /// tDatLen[colName] should be equivalent to tDat[colName].Max(s => s.Length)
            Dictionary<string, int> tDatLen = new Dictionary<string, int>();

            /// Contains the names of the columns (keys) in tDat and tDatLen
            List<string> colNames = new List<string>();
            
            var sb = new StringBuilder();
            count = 0;

            void AddToCol(string colName, string val)
            {
                if (!tDat.ContainsKey(colName))
                {
                    colNames.Add(colName);
                    tDat.Add(colName, new List<string>());
                }
                //Get the length of the visible portion of the string (i.e., the length excluding all non-printable characters)
                int valLen = Regex.Replace(val, "\x1B(?:\\[([0-9;+\\-]*)(?:\x20?[\x40-\x7F])|.)|\x7F|[\x00-\x1F]", "").Length;
                if (tDatLen.ContainsKey(colName))
                {
                    tDatLen[colName] = Math.Max(valLen, tDatLen[colName]);
                }
                else
                {
                    tDatLen[colName] = Math.Max(valLen, colName.Length);
                }
                tDat[colName].Add(val);
            }
            // get the data
            foreach (var kvpair in server.Players)
            {
                /*
                 * Note: we could probably move the contents of this foreach loop outside of this method
                 *       and generalize this method for use with other data, to more easily generate tables
                 */
                MultiplayerServerNetcode.ServerPlayerMetadata p = kvpair.Value;
                count++;
                AddToCol("Guid", kvpair.Key.ToString());
                AddToCol("IP address", p.client.RemoteEndPoint.ToString());
                AddToCol("Name", server.GetPlayerName(p.Uuid) + "\x1B[0m");
                AddToCol("Time since join", p.TimeSinceJoin.ToString());
                AddToCol("Level", ((p.CurrentLevelName == null || p.CurrentLevelName.Length == 0) ? "???" : p.CurrentLevelName));
                AddToCol("Action", p.Action.ToString());
                AddToCol("Viewpoint", p.CameraViewpoint.ToString());
                AddToCol("Position", p.Position.Round(3).ToString());
                AddToCol("Ping", (p.NetworkSpeedUp + p.NetworkSpeedDown) + "ms");
                AddToCol("Last update", ((DateTime.UtcNow.Ticks - p.LastUpdateTimestamp) / (double)TimeSpan.TicksPerSecond) + "s");
                //Note: if the number of entries for each column are not the same, unexpected results will occur
                //i.e., even if a cell is supposed to be empty, you still need to call AddToCol for the data to be formatted correctly
            }

            // arrange the data in a nice tabular format
            // column header row
            sb.AppendLine(string.Join(T_GAP_COL, colNames.Select(colName => colName.PadRight(tDatLen[colName]))));

            int c = tDat.First().Value.Count;
            for (int i = 0; i < c; ++i)
            {
                // data rows
                sb.AppendLine(string.Join(T_GAP_COL, colNames.Select(colName => tDat[colName][i].PadRight(tDatLen[colName]))));
            }
            return sb.ToString();
        }

        private class IPAddressComparer : IComparer<IPAddress>
        {
            public int Compare(IPAddress addr1, IPAddress addr2)
            {
                if(addr1.AddressFamily != addr2.AddressFamily)
                {
                    return addr1.AddressFamily - addr2.AddressFamily;
                }
                byte[] addr1bytes = addr1.GetAddressBytes();
                byte[] addr2bytes = addr2.GetAddressBytes();
                if (addr1bytes.Length != addr2bytes.Length)
                {
                    throw new Exception($"Address byte length mismatch ({addr1bytes.Length} vs {addr2bytes.Length})");
                }
                for (int i = 0; i < addr1bytes.Length; ++i)
                {
                    int c = (int)addr1bytes[i] - (int)addr2bytes[i];
                    if (c != 0)
                    {
                        return c;
                    }
                }
                return 0;
            }
        };
        private static readonly IPAddressComparer AddressComparer = new IPAddressComparer();
        private const bool HIDE_LOOPBACK_ADDRS = true;
        private const bool HIDE_V6LINK_LOCAL = false;
        private static void GetLocalIPAddresses()
        {
            List<IPAddress> myIpAddresses = new List<IPAddress>();
            List<IPAddress> loopbackAddresses = new List<IPAddress>();

            // Get all network interfaces on the machine
            NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface networkInterface in networkInterfaces)
            {
                // Check if the network interface is up and operational
                if (networkInterface.OperationalStatus == OperationalStatus.Up)
                {
                    // Get the IP properties of the network interface
                    var unicastAddresses = networkInterface.GetIPProperties().UnicastAddresses;

                    // Add each unicast address to myIpAddresses
                    foreach (var unicastAddress in unicastAddresses)
                    {
                        var addr = unicastAddress.Address;
                        if (IPAddress.IsLoopback(addr))
                        {
                            loopbackAddresses.Add(addr);
                        }
                        if ((!HIDE_V6LINK_LOCAL || !addr.IsIPv6LinkLocal)
                                && (!HIDE_LOOPBACK_ADDRS || !IPAddress.IsLoopback(addr)))
                        {
                            myIpAddresses.Add(addr);
                        }
                    };
                }
            }
            IEnumerable<string> IPAddressListToOrderedStrings(List<IPAddress> list)
            {
                return list.OrderBy(addr => addr, AddressComparer)
                        .Select(addr =>
                        {
                            if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                            {
                                return "[" + addr.ToString() + "]";
                            }
                            else
                            {
                                return addr.ToString();
                            }
                        });
            }
            void WriteListeningText(string addrType, List<IPAddress> addresses)
            {
                IEnumerable<string> ipStrings = IPAddressListToOrderedStrings(addresses);
                Console.WriteLine($"Listening on {addrType} address{(myIpAddresses.Count() == 1 ? "" : "es")}: {string.Join(", ", ipStrings)}");
            }
            if (myIpAddresses.Count() > 0)
            {
                WriteListeningText("Local IP", myIpAddresses);
            }
            else if (loopbackAddresses.Count() > 0)
            {
                //They should at least have loopback addresses
                WriteListeningText("loopback", loopbackAddresses);
            }
            else
            {
                Console.WriteLine("No network interfaces detected.");
            }
        }
    }
}
