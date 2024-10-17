using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Linq;
using System;
using System.ComponentModel;

namespace FezGame.MultiplayerMod
{
    /// <summary>
    /// The class that contains the settings for <see cref="MultiplayerClient"/>
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
    }

}