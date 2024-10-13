

namespace FezMultiplayer
{
    public class IPFilter
    {
        private string filterString;
        public string FilterString
        {
            get => filterString;
            set
            {
                filterString = value;
                ReloadFilterString();
            }
        }

        private readonly List<IPAddressRange> ranges = new List<IPAddressRange>();
        private void ReloadFilterString()
        {
            ranges.Clear();
            var entries = filterString.Split(',');
            foreach (string entry in entries)
            {
                var str = entry.Trim();
                if (str.Contains(":"))
                {
                    throw new NotImplementedException("IPv6 is currently not supported");
                }
                IPAddress low = null, high = null;
                if (Regex.IsMatch(str, @"\A\d+\.\d+\.\d+\.\d+\Z"))
                {
                    //single IP address
                    low = high = IPAddress.Parse(str);
                }
                else if (Regex.IsMatch(str, @"\A\d+\.\d+\.\d+\.\d+/\d+\Z"))
                {
                    //CIDR format
                    var parts = str.Split('/');

                    //Important Note: all four of these UInt32 are in network order, not host order, so don't do comparisons with them
                    //convert IP string to UInt32
                    UInt32 b = BitConverter.ToUInt32(IPAddress.Parse(parts[0]).GetAddressBytes(), 0);
                    UInt32 mask = (UInt32)IPAddress.HostToNetworkOrder((Int32)Math.Pow(2, 32 - int.Parse(parts[1])) - 1);
                    UInt32 lowb = (UInt32)(b & ~mask);
                    UInt32 highb = (UInt32)(b | mask);

                    //convert Int32 back to IPAddress
                    low = new IPAddress(lowb);
                    high = new IPAddress(highb);
                }
                else if (str.Contains("-"))
                {
                    //range or implied range ( could be "10.5.3.3-10.5.3.40" or "10.5.3.3-40" )
                    var parts = str.Split('-');
                    string lowstr = parts[0], highstr;
                    var highstr__end = parts[1];
                    var endpartcount = highstr__end.Count(c => c == '.') + 1;
                    if (endpartcount == 4)
                    {
                        highstr = highstr__end;
                    }
                    else
                    {
                        var rg = @"\." + String.Join(@"\.", Enumerable.Repeat(@"\d+", endpartcount)) + @"\Z";
                        highstr = Regex.Replace(lowstr, rg, "." + highstr__end);
                    }
                    low = IPAddress.Parse(lowstr);
                    high = IPAddress.Parse(highstr);
                }
                else if (Regex.IsMatch(str, @"\A(\d+\.){1,3}\Z"))
                {
                    //Implied IP address (e.g., 10. )
                    var lowstr = str;
                    while (lowstr.Count(c => c == '.') < 3)
                    {
                        lowstr += "0.";
                    }
                    lowstr += "0";
                    var highstr = str;
                    while (highstr.Count(c => c == '.') < 3)
                    {
                        highstr += "255.";
                    }
                    highstr += "255";
                    low = IPAddress.Parse(lowstr);
                    high = IPAddress.Parse(highstr);
                }
                else
                {
                    //unsupported syntax
                    //TODO notify user?
                    continue;
                }
                if (low == null || high == null)
                {
                    throw new Exception("A problem was encountered when parsing a well-formed IP address range");
                }
                ranges.Add(new IPAddressRange(low, high));
            }
        }

        private static UInt32 IPAddressToHostUInt32(IPAddress address)
        {
            if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                throw new ArgumentException("Expected an IPv4 address, got " + address + " instead");
            }
            return (UInt32)IPAddress.NetworkToHostOrder((Int32)BitConverter.ToUInt32(address.GetAddressBytes(), 0));
        }
        private struct IPAddressRange
        {
            private readonly UInt32 low;
            private readonly UInt32 high;

            public IPAddressRange(IPAddress low, IPAddress high)
            {
                this.low = IPAddressToHostUInt32(low);
                this.high = IPAddressToHostUInt32(high);
            }
            public bool Contains(IPAddress address)
            {
                var val = IPAddressToHostUInt32(address);
                return val >= low && val <= high;
            }
        }

        /// <summary>
        /// <para>
        /// Creates a new IP Filter from the provided string, with the syntax being comma-separated entries according to the syntax described on
        /// <see href="https://docs.cpanel.net/cpanel/security/ip-blocker/">cPanel Docs "IP Blocker" page</see>,
        /// which should be the same as the following list.
        /// </para>
        /// <para name="desc">
        /// You can enter IP addresses as comma-separated entries of any combination of the following formats:
        /// <list type="bullet">
        ///     <item>
        ///         <description>Single IP address, e.g., <c>10.5.3.33</c> </description>
        ///     </item>
        ///     <item>
        ///         <description>Range, e.g., <c>10.5.3.3-10.5.3.40</c> </description>
        ///     </item>
        ///     <item>
        ///         <description>Implied range, e.g., <c>10.5.3.3-40</c> </description>
        ///     </item>
        ///     <item>
        ///         <description>CIDR format (for example, in <c>10.56.27.0/24</c>,
        ///         the first 24 bits are constant, and the last 8 bits are wild,
        ///         so the resulting range is <c>10.56.27.0</c> to <c>10.56.27.255</c></description>
        ///     </item>
        ///     <item>
        ///         <description>Implied IP address (for example, <c>10.</c> gets interpreted as <c>10.*.*.*</c></description>
        ///     </item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="filterString">The string to convert to an IP Filter</param>
        public IPFilter(string filterString)
        {
            FilterString = filterString;
        }

        public bool Contains(IPAddress address)
        {
            return ranges.Any(range => range.Contains(address));
        }

        public override string ToString()
        {
            return FilterString;
        }
    }
}