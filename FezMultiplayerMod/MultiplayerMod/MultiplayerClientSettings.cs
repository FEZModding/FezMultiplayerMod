using System.Net;
using System.ComponentModel;

namespace FezGame.MultiplayerMod
{
    /// <summary>
    /// The class that contains the settings for <see cref="MultiplayerClientNetcode"/>
    /// </summary>
    public class MultiplayerClientSettings
    {
        private const int DefaultPort = 7777;
        /// <summary>
        /// The endpoint to connect to. Note: Currently only supports IPv4
        /// </summary>
        [Description("The endpoint to connect to. Note: Currently only supports IPv4")]
        public IPEndPoint mainEndpoint = new IPEndPoint(IPAddress.Loopback, DefaultPort);
        /// <summary>
        /// A string representing the name to display for this client. Note: currently only supports alphanumeric ASCII characters.
        /// </summary>
        [Description("A string representing the name to display for this client.")]
        public string myPlayerName = "Player";
        /// <summary>
        /// Format currently TBD. The custom player skin/appearance to use
        /// </summary>
        [Description("Format currently TBD. The custom player skin/appearance to use")]
        public string appearance = "";
        /// <summary>
        /// If true, attempts to sync world save data, level states, and player inventories across players. Note that this setting must also be enabled on the server for it to work.
        /// </summary>
        [Description("If true, attempts to sync world save data, level states, and player inventories across players. Note that this setting must also be enabled on the server for it to work.")]
        public bool syncWorldState = false;
    }
}