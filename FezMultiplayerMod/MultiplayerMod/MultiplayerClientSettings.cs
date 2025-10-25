using System.Net;
using System.ComponentModel;
using System.Collections.Generic;
using static FezGame.MultiplayerMod.ServerListMenu;
using System;

namespace FezGame.MultiplayerMod
{
    /// <summary>
    /// The class that contains the settings for <see cref="MultiplayerClientNetcode"/>
    /// </summary>
    public sealed class MultiplayerClientSettings
    {
        private const int DefaultPort = 7777;
        /// <summary>
        /// The list of endpoints in the Server List. Note: IPv6 should be in brackets like [::1]:7777
        /// </summary>
        [Description("The list of endpoints in the Server List. Note: IPv6 should be in brackets like [::1]:7777")]
        public List<ServerInfo> ServerList = new List<ServerInfo>(){
            new ServerInfo("localhost", new IPEndPoint(IPAddress.Loopback, DefaultPort))
        };
        /// <summary>
        /// A string representing the name to display for this client.
        /// </summary>
        [Description("A string representing the name to display for this client.")]
        public string MyPlayerName = ((Func<string>)(() =>
        {
            const string DefaultPlayerName = "Player";
            string username = Environment.UserName.Trim();
            HashSet<string> rootNames = new HashSet<string> {
                "root", "system", "sudo", "admin", "administrator", "test", "pi", "ubuntu", "default", "home", "public",
                "guest", "nobody", "user", "username", "macuser", "defaultuser"
            };
            if (string.IsNullOrEmpty(username) || rootNames.Contains(username.ToLower()))
            {
                return DefaultPlayerName;
            }
            rootNames.Clear();
            rootNames = null;
            return username;
        }))();
        /// <summary>
        /// Format currently TBD. The custom player skin/appearance to use
        /// </summary>
        [Description("Format currently TBD. The custom player skin/appearance to use")]
        public string Appearance = "";
        /// <summary>
        /// If true, attempts to sync world save data, level states, and player inventories across players. Note that this setting must also be enabled on the server for it to work.
        /// </summary>
        [Description("If true, attempts to sync world save data, level states, and player inventories across players. Note that this setting must also be enabled on the server for it to work.")]
        public bool SyncWorldState = false;
    }
}