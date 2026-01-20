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
using System.IO;

namespace FezMultiplayerDedicatedServer
{
    sealed class FezDedicatedServer
    {
        public struct CommandLineCommand
        {
            public string Description { get; }
            public Action<string[]> Action { get; }

            public CommandLineCommand(string description, Action<string[]> action)
            {
                Description = description;
                Action = action;
            }
            public static implicit operator CommandLineCommand((string description, Action<string[]> action) tuple)
            {
                return new CommandLineCommand(tuple.description, tuple.action);
            }
        }

        public static string ReadLineInvariant()
        {
            return Console.ReadLine()?.Trim()?.ToLowerInvariant() ?? "";
        }
        public static string Prompt(string text)
        {
            Console.WriteLine(text);
            return ReadLineInvariant();
        }
        public static string GetArgOrPrompt(string[] args, int index, string text)
        {
            return args.Length <= index ? Prompt(text) : args[index];
        }

        public static readonly Dictionary<string, CommandLineCommand> cliActions = new Dictionary<string, CommandLineCommand>
                {
                    {
                        "exit".ToLowerInvariant(),
                        ("Stops the server and closes the program", (_) =>
                        {
                            running = false;
                        })
                    },
                    {
                        "players".ToLowerInvariant(),
                        ("Lists currently connected players", (_) =>
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
                        ("Lists disconnected players", (_) =>
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
                        ("Lists players appearances", (_) =>
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
                    {
                        "blocklist".ToLowerInvariant(),
                        ("Prints out the blocklsit", (_) =>
                        {
                            Console.WriteLine(settings.BlockList.ToDetailedString());
                        })
                    },
                    {
                        "allowlist".ToLowerInvariant(),
                        ("Prints out the allowlist", (_) =>
                        {
                            Console.WriteLine(settings.AllowList.ToDetailedString());
                        })
                    },
                    {
                        "kick".ToLowerInvariant(),
                        ("kick a player", (args) =>
                        {
                            string arg = GetArgOrPrompt(args, 1, "Which player? (supply the Guid)");
                            if(Guid.TryParse(arg, out Guid puid) && server.Players.TryGetValue(puid, out var pdat))
                            {
                                var client = pdat.client;
                                // forcibly terminate the connection
                                client.ForceDisconnect();
                                Console.WriteLine("kicked player " + arg);
                                //TODO rn the client can reconnect immediately, so we should make it so they can't reconnect for like 30 seconds or so
                            }
                            Console.WriteLine("player Guid not found: " + arg);
                        })
                    },
                    {
                        "ban".ToLowerInvariant(),
                        ("IP ban", (args) =>
                        {
                            string arg = GetArgOrPrompt(args, 1, "Which IP?");

                            if(IPAddress.TryParse(arg, out IPAddress address))
                            {
                                if(address.IsIPv4MappedToIPv6)
                                {
                                    address = address.MapToIPv4();
                                }
                                //Note: server.BlockList and settings.BlockList point to the same object
                                settings.BlockList.FilterString += ","+address.ToString();
                                //TODO write the changes to settings to the server settings file
                                Console.WriteLine("banned IP " + address);

                                var matchingPlayers = server.Players.Where(player => {
                                    IPAddress a = ((IPEndPoint)player.Value.client?.RemoteEndPoint)?.Address;
                                    if(a.IsIPv4MappedToIPv6){
                                        a = a.MapToIPv4();
                                    }
                                    return address.Equals(a);
                                });
                                foreach(var player in matchingPlayers)
                                {
                                    // forcibly terminate the connections
                                    player.Value.client.ForceDisconnect();
                                }
                            }
                            else
                            {
                                Console.WriteLine("Invalid IP: \"" + arg + "\"");
                            }
                        })
                    },
                    #if DEBUG
                    {
                        "restart".ToLowerInvariant(),
                        ("(debug) restarts the server", (args) =>
                        {
                            #if DEBUG
                            // deferred launch so the compiler can finish compiling the application before it restarts
                            // so the exe file isn't in use so it can be updated
                            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                            {
                                System.Diagnostics.Process.Start("cmd.exe", "/c \"timeout /t 2 >nul && cd \"" + AppDomain.CurrentDomain.BaseDirectory + "\" && \""+Environment.GetCommandLineArgs()[0] + "\"\"");
                            }
                            else
                            {
                                System.Diagnostics.Process.Start("bash", "-c \"sleep 2 && cd '" + AppDomain.CurrentDomain.BaseDirectory + "' && '" + Environment.GetCommandLineArgs()[0] + "'\"");
                            }
                            #else
                            System.Diagnostics.Process.Start(Environment.GetCommandLineArgs()[0]);
                            #endif
                            Environment.Exit(0);
                        })
                    },
                    #endif
                    {
                        "netstatus".ToLowerInvariant(),
                        ("Displays the network status", NetworkStatus)
                    },
                    {
                        "cls".ToLowerInvariant(),
                        ("Clears console output screen", (args) =>
                        {
                            Console.Clear();
                        })
                    },
                    {
                        "timescale".ToLowerInvariant(),
                        ("Sets the scale for time of day speed", (args) =>
                        {
                            string arg = GetArgOrPrompt(args, 1, "How fast would you like time of day to progress? (note: 1 is normal speed, 2 is twice speed, etc.)");

                            const double MAX_TIMESCALE = 100;

                            if (double.TryParse(arg, out double newTimescale) && !double.IsNaN(newTimescale) && !double.IsInfinity(newTimescale))
                            {
                                if (Math.Abs(newTimescale) > MAX_TIMESCALE)
                                {
                                    Console.WriteLine($"Please input a value between -{MAX_TIMESCALE} and {MAX_TIMESCALE} so we don't injure anyone with flashing lights");
                                }
                                else
                                {
                                    server.TimeScale = newTimescale;
                                    Console.WriteLine("Time scale set to: " + arg + "");
                                }
                            }
                            else
                            {
                                Console.WriteLine("Invalid time scale: \"" + arg + "\"");
                            }
                        })
                    },
                };
        internal static MultiplayerServerNetcode server;
        private static MultiplayerServerSettings settings;
        private static volatile bool running;
        private static readonly CommandLineCommand HelpCommand;
        private static readonly string helpCmdName = "help".ToLowerInvariant();
        static FezDedicatedServer()
        {
            int maxCommandLength = cliActions.Max(kv => kv.Key.Length);
            cliActions.Add(helpCmdName,
                HelpCommand = ("Lists available commands", (_) =>
                        {
                            Console.WriteLine("Available commands:");
                            foreach (var kvpair in cliActions)
                            {
                                Console.WriteLine($"{kvpair.Key.PadRight(maxCommandLength, ' ')} - {kvpair.Value.Description}");
                            }
                        }
            ));
        }
        private static volatile bool IsUpdating = false;
        static void Main(string[] prog_args)
        {
            //TODO add more to this, like command line parameters and connection logs

            Console.WriteLine($"FezMultiplayerMod server starting... (protocol ver: {MultiplayerServerNetcode.ProtocolVersion})");

            Queue<string> queue = new Queue<string>();
            foreach (string item in prog_args)
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
            settings = IniTools.ReadSettingsFile(SettingsFilePath, new MultiplayerServerSettings());
            IniTools.WriteSettingsFile(SettingsFilePath, settings);

            Console.WriteLine("Initializing server...");
            server = new MultiplayerServerNetcode(settings);

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

            SaveDataObserver saveDataObserver = new SaveDataObserver();

            if (settings.SyncWorldState)
            {
                Console.WriteLine("Save data will be saved to " + Path.GetFullPath(settings.SaveFileFullPath));
            }

            if (settings.SyncWorldState)
            {
                try
                {
                    if (File.Exists(settings.SaveFileFullPath))
                    {
                        using (FileStream fs = new FileStream(settings.SaveFileFullPath, FileMode.Open))
                        using (BinaryReader reader = new BinaryReader(fs, Encoding.UTF8))
                        {
                            reader.ReadSharedSaveData().CloneInto(server.sharedSaveData);
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }

            long SaveIntervalTicks = TimeSpan.FromSeconds(5.0f).Ticks;
            Timer myTimer = new Timer();
            myTimer.Elapsed += (a, b) => {
                if (!IsUpdating)
                {
                    IsUpdating = true;
                    server.Update();
                    if (settings.SyncWorldState)
                    {
                        if (!server.sharedSaveData.SinceLastSaved.HasValue || server.sharedSaveData.SinceLastSaved > SaveIntervalTicks)
                        {
                            try
                            {
                                using (FileStream fs = new FileStream(settings.SaveFileFullPath, FileMode.Create))
                                using (BinaryWriter writer = new BinaryWriter(fs, Encoding.UTF8))
                                {
                                    writer.Write(server.sharedSaveData);
                                }
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e.Message);
                            }
                            server.sharedSaveData.SinceLastSaved = 0;
                        }
                    }
                    IsUpdating = false;
                }
            };
            myTimer.Interval = 1f / 60f * 1000; // 1000 ms is one second
            myTimer.Start();

            //Note: gotta keep the program busy otherwise it'll close

            //TODO make the CLI better; see https://learn.microsoft.com/en-us/dotnet/api/system.console , particularly Console.SetCursorPosition
            //I want a nice animated one that automatically updates what it writes on the screen

            try
            {
                string line;
                running = true;
                Console.WriteLine($"Use {helpCmdName} to list available commands");

                while (running)
                {
                    line = ReadLineInvariant();
                    MatchCollection matches = Regex.Matches(line, @"([^""'\s]+|""(?:\\.|[^""])*""|'(?:\\.|[^'])*')");
                    string[] cmd_args = matches.Cast<Match>().Select(m=>m.Value).ToArray();
                    string cmd_name = cmd_args.Length > 0 ? cmd_args[0] : "";
                    bool validAction = cliActions.TryGetValue(cmd_name, out CommandLineCommand tuple);
                    if (validAction)
                    {
                        tuple.Action(cmd_args);
                    }
                    else
                    {
                        Console.WriteLine($"Unknown command: \"{cmd_name}\"");
                        HelpCommand.Action(cmd_args);
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

            StringBuilder sb = new StringBuilder();
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
                AddToCol("Ping", (p.NetworkSpeedUpDown) + "ms");
                AddToCol("Last update", ((DateTime.UtcNow.Ticks - p.LastUpdateTimestamp) / (double)TimeSpan.TicksPerSecond) + "s");
                //Note: if the number of entries for each column are not the same, unexpected results will occur
                //i.e., even if a cell is supposed to be empty, you still need to call AddToCol for the data to be formatted correctly
            }

            // arrange the data in a nice tabular format
            // column header row
            sb.AppendLine(string.Join(T_GAP_COL, colNames.Select(colName => colName.PadRight(tDatLen[colName]))));

            if (tDat.Any())
            {
                int c = tDat.FirstOrDefault().Value.Count;
                for (int i = 0; i < c; ++i)
                {
                    // data rows
                    sb.AppendLine(string.Join(T_GAP_COL, colNames.Select(colName => tDat[colName][i].PadRight(tDatLen[colName]))));
                }
            }
            else //no players 
            {

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
        private static void NetworkStatus(string[] args)
        {
            NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface networkInterface in networkInterfaces)
            {
                if (args.Length <= 1 || !(args[1].Equals("--all") || args[1].Equals("-a")))
                {
                    if (networkInterface.OperationalStatus != OperationalStatus.Up)
                    {
                        continue;
                    }
                }
                Console.WriteLine($"[{networkInterface.NetworkInterfaceType}] \"{networkInterface.Name}\" ({networkInterface.Description})");
                Console.WriteLine($"Status: {networkInterface.OperationalStatus}");
                Console.WriteLine($"Speed: {networkInterface.Speed} bps ({networkInterface.Speed / 1000000} mbps)");
                // Check if the network interface is up and operational
                if (networkInterface.OperationalStatus == OperationalStatus.Up)
                {
                    // Get the IP properties of the network interface
                    var unicastAddresses = networkInterface.GetIPProperties().UnicastAddresses;
                    var addrs = string.Join(", ", unicastAddresses.Select(unicastAddress =>
                    {
                        return unicastAddress.Address.ToCommonString();
                    }));
                    Console.WriteLine($"IP Address{(addrs.Length == 1 ? "" : "es")}: {addrs}");
                }
                Console.WriteLine();
            }
        }
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
