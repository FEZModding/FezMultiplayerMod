﻿using FezSharedTools;
using System;
using System.Timers;

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

            Timer myTimer = new Timer();
            myTimer.Elapsed += (a, b) => { server.Update(); };
            myTimer.Interval = 1f / 60f * 1000; // 1000 ms is one second
            myTimer.Start();

            //Note: gotta keep the program busy otherwise it'll close

            //TODO make the CLI better

            string line;
            while (true)
            {
                line = Console.ReadLine().Trim().ToLowerInvariant();

                if (line.Equals("exit".ToLowerInvariant()))
                {
                    break;
                }
            }

            server.Dispose();
        }
    }
}
