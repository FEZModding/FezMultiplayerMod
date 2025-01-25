using System.Net;
using System.ComponentModel;

namespace FezGame.MultiplayerMod
{
    /// <summary>
    /// The class that contains the settings for <see cref="MultiplayerClientNetcode"/>
    /// </summary>
    public sealed class MultiplayerClientSettings
    {
        private const int DefaultPort = 7777;
        /// <summary>
        /// The endpoint to connect to. Note: IPv6 must be in brackets like [::1]:7777
        /// </summary>
        [Description("The endpoint to connect to. Note: IPv6 must be in brackets like [::1]:7777")]
        public IPEndPoint MainEndpoint = new IPEndPoint(IPAddress.Loopback, DefaultPort);
        /// <summary>
        /// A string representing the name to display for this client.
        /// </summary>
        [Description("A string representing the name to display for this client.")]
        public string MyPlayerName = "Player";
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