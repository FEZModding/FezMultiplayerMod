using System.ComponentModel;
using FezSharedTools;

namespace FezMultiplayerDedicatedServer
{
    /// <summary>
    /// The class that contains the settings for <see cref="MultiplayerServerNetcode"/>
    /// </summary>
    public sealed class MultiplayerServerSettings
    {
        /// <summary>
        /// The port to listen on
        /// </summary>
        [Description("The port to listen on")]
        public int ListenPort = SharedConstants.DefaultPort;
        /// <summary>
        /// The amount of times to attempt to use the next port as the port to listen to before giving up. In case of an error, see <see cref="MultiplayerServerNetcode.ErrorMessage"/>
        /// </summary>
        [Description("The amount of times to attempt to use the next port as the port to listen to before giving up.")]
        public int MaxAdjustListenPortOnBindFail = 1000;
        /// <summary>
        /// The amount of time, in milliseconds, to wait before removing a player. For reference, there are 1000 (one thousand) milliseconds in one second.
        /// </summary>
        [Description("The amount of time, in milliseconds, to wait before removing a player. For reference, there are 1000 (one thousand) milliseconds in one second.")]
        public int OverdueTimeout = 5000;
        /// <summary>
        /// If true, only packets from IP addresses included in the AllowList list will be accepted.
        /// </summary>
        [Description("If true, only packets from IP addresses included in the AllowList list will be accepted.")]
        public bool UseAllowList = false;

        private const string IPFilterDesc = "Supports comma-separated entries of any combination of the following formats: " +
        "Single IP address (e.g., 10.5.3.33), " +
        "Range (e.g., 10.5.3.3-10.5.3.40), " +
        "Implied range (e.g., 10.5.3.3-40), " +
        "CIDR format, " +
        "or Implied IP address (for example, 10. filters all IP addresses that start with 10.)";
        /// <summary>
        /// If useAllowList is true, only packets from IP addresses included in this list will be accepted.
        /// <inheritdoc cref='IPFilter.IPFilter(string)'/>
        /// </summary>
        [Description("If useAllowList is true, only packets from IP addresses included in this list will be accepted. " + IPFilterDesc)]
        public readonly IPFilter AllowList = new IPFilter("");
        /// <summary>
        /// Packets from IP addresses included in this list will be ignored.
        /// <inheritdoc cref='IPFilter.IPFilter(string)' path="//para[@name='desc']"/>
        /// </summary>
        [Description("Packets from IP addresses included in this list will be ignored. " + IPFilterDesc)]
        public readonly IPFilter BlockList = new IPFilter("");

        /// <summary>
        /// If true, attempts to sync world save data, level states, and player inventories across players. Note that this setting must also be enabled on the clients for it to work.
        /// </summary>
        [Description("If true, attempts to sync world save data, level states, and player inventories across players. Note that this setting must also be enabled on the clients for it to work.")]
        public bool SyncWorldState = false;
    }
}