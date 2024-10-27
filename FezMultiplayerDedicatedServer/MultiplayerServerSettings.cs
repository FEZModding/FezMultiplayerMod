using System.ComponentModel;
using FezSharedTools;

namespace FezMultiplayerDedicatedServer
{
    /// <summary>
    /// The class that contains the settings for <see cref="MultiplayerServer"/>
    /// </summary>
    public class MultiplayerServerSettings
    {
        /// <summary>
        /// The port to listen on
        /// </summary>
        [Description("The port to listen on")]
        public int listenPort = SharedConstants.DefaultPort;
        /// <summary>
        /// The amount of times to attempt to use the next port as the port to listen to before giving up. In case of an error, see <see cref="MultiplayerServer.ErrorMessage"/>
        /// </summary>
        [Description("The amount of times to attempt to use the next port as the port to listen to before giving up.")]
        public int maxAdjustListenPortOnBindFail = 1000;
        /// <summary>
        /// The amount of time, in <see cref="System.DateTime.Ticks">ticks</see>, to wait before removing a player. For reference, there are 10000000 (ten million) ticks in one second.
        /// </summary>
        [Description("The amount of time, in milliseconds, to wait before removing a player. For reference, there are 1000 (one thousand) milliseconds in one second.")]
        public int overduetimeout = 5000;
        /// <summary>
        /// If true, only packets from IP addresses included in the AllowList list will be accepted.
        /// </summary>
        [Description("If true, only packets from IP addresses included in the AllowList list will be accepted.")]
        public bool useAllowList = false;

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
        [Description("Packets from IP addresses included in this list will be ignored. "+ IPFilterDesc)]
        public readonly IPFilter BlockList = new IPFilter("");
    }
}