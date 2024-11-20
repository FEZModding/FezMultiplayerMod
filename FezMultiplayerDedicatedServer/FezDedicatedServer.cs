using FezSharedTools;
using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Timers;
using System.Linq;

namespace FezMultiplayerDedicatedServer
{
    class FezDedicatedServer
    {
        private static MultiplayerServer server;
        static void Main(string[] args)
        {
            //TODO add more to this, like command line parameters and connection logs

            Console.WriteLine($"FezMultiplayerMod server starting... (protocol ver: {MultiplayerServer.ProtocolVersion})");

            const string SettingsFilePath = "FezMultiplayerServer.ini";//TODO: probably should use an actual path instead of just the file name
            Console.WriteLine($"Loading settings from {SettingsFilePath}");
            MultiplayerServerSettings settings = IniTools.ReadSettingsFile(SettingsFilePath, new MultiplayerServerSettings());
            IniTools.WriteSettingsFile(SettingsFilePath, settings);

            Console.WriteLine("Initializing server...");
            server = new MultiplayerServer(settings);

            //MultiplayerServerSettings.WriteSettingsFile(SettingsFilePath, settings);//TODO

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

            //TODO make the CLI better

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
                            int count = 0;
                            foreach (var kvpair in server.Players)
                            {
                                MultiplayerServer.ServerPlayerMetadata p = kvpair.Value;
                                count++;
                                s += $"{kvpair.Key}: {server.GetPlayerName(p.Uuid)}, "// + p.Uuid + ", "//{Convert.ToBase64String(p.Uuid.ToByteArray()).TrimEnd('=')}, "
                                    + $"{p.TimeSinceJoin}, "
                                    + $"{((p.CurrentLevelName == null || p.CurrentLevelName.Length == 0) ? "???" : p.CurrentLevelName)}, "
                                    + $"{p.Action}, {p.CameraViewpoint}, "
                                    + $"{p.Position/*.Round(3)*/}, {(DateTime.UtcNow.Ticks - p.LastUpdateTimestamp) / (double)TimeSpan.TicksPerSecond}\n";
                            }
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
        private static void GetLocalIPAddresses()
        {
            // Get all network interfaces on the machine
            NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface networkInterface in networkInterfaces)
            {
                // Check if the network interface is up and operational
                if (networkInterface.OperationalStatus == OperationalStatus.Up)
                {
                    // Get the IP properties of the network interface
                    var ipProperties = networkInterface.GetIPProperties();

                    // Iterate through each unicast address in the IP properties
                    foreach (var unicastAddress in ipProperties.UnicastAddresses)
                    {
                        var addr = unicastAddress.Address;
                        // Make sure we have an IPv4 address
                        if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                            && !addr.Equals(System.Net.IPAddress.Loopback))
                        {
                            Console.WriteLine($"IP Address: {addr}");
                        }
                    }
                }
            }
        }
    }
}
