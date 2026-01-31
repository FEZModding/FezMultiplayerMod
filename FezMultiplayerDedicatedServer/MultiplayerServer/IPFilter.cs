

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace FezMultiplayerDedicatedServer
{
    public sealed class IPFilter
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
#if DEBUG
        private readonly List<Tuple<string, IPAddressRange?>> rangesDEBUG = new List<Tuple<string, IPAddressRange?>>();
#endif
        private void ReloadFilterString()
        {
            ranges.Clear();
            string[] entries = filterString.Split(',');
            foreach (string entry in entries)
            {
                string str = entry.Trim();
                if (IPAddressRange.TryParseRange(str, out IPAddressRange range))
                {
                    ranges.Add(range);
#if DEBUG
                    rangesDEBUG.Add(Tuple.Create<string, IPAddressRange?>(str, range));
#endif
                }
                else
                {
#if DEBUG
                    rangesDEBUG.Add(Tuple.Create<string, IPAddressRange?>(str, null));
#endif
                    //TODO notify user?
                    continue;
                }
            }
        }

        private struct IPAddressRange
        {
            private readonly byte[] low;
            private readonly byte[] high;
            private readonly AddressFamily family;

            public IPAddressRange(IPAddress low, IPAddress high)
            {
                this.low = low.GetAddressBytes();
                this.high = high.GetAddressBytes();
                this.family = low.AddressFamily;
                if (low.AddressFamily != high.AddressFamily)
                {
                    throw new ArgumentException("Address family mismatch!");
                }
            }
            internal IPAddressRange(byte[] low, byte[] high, AddressFamily family)
            {
                this.low = low;
                this.high = high;
                this.family = family;
                if (low.Length != high.Length)
                {
                    throw new ArgumentException("Array length mismatch!");
                }
            }
            public bool Contains(IPAddress address)
            {
                if (family == AddressFamily.InterNetwork && address.IsIPv4MappedToIPv6)
                {
                    address = address.MapToIPv4();
                }
                if (family != address.AddressFamily)
                {
                    return false;
                }
                byte[] addr = address.GetAddressBytes();
                for (int i = 0; i < addr.Length; ++i)
                {
                    if (addr[i] < low[i] || addr[i] > high[i])
                    {
                        return false;
                    }
                }
                return true;
            }
            public override string ToString()
            {
                return new IPAddress(low).ToString() + " to " + new IPAddress(high).ToString();
            }
            /// <summary>
            /// <para>
            /// Attempts to create a new IP Address Range from the provided string, with the syntax being in according to the syntax described on
            /// <see href="https://docs.cpanel.net/cpanel/security/ip-blocker/">cPanel Docs "IP Blocker" page</see>,
            /// which should be the same as the following list.
            /// </para>
            /// <para name="desc">
            /// The IP addresses range can be in any of the following formats:
            /// <list type="bullet" name="formats">
            ///     <item>
            ///         <description><b>Single IPv4 or IPv6 address</b>, e.g., <c>10.5.3.33</c> </description>
            ///     </item>
            ///     <item>
            ///         <description><b>IPv4 or IPv6 Range</b>, e.g., <c>10.5.3.3-10.5.3.40</c> </description>
            ///     </item>
            ///     <item>
            ///         <description><b>Implied IPv4 range</b>, e.g., <c>10.5.3.3-40</c> </description>
            ///     </item>
            ///     <item>
            ///         <description><b>CIDR format</b> (for example, in <c>10.56.27.0/24</c>,
            ///         the first 24 bits are constant, and the last 8 bits are wild,
            ///         so the resulting range is <c>10.56.27.0</c> to <c>10.56.27.255</c></description>
            ///     </item>
            ///     <item>
            ///         <description><b>Implied IPv4 address</b> (for example, <c>10.</c> gets interpreted as <c>10.*.*.*</c></description>
            ///     </item>
            /// </list>
            /// </para>
            /// </summary>
            /// <param name="filterString">The string to convert to an IP Filter</param>
            /// <returns><c>true</c> if the string was successfully parsed, else <c>false</c>.</returns>
            public static bool TryParseRange(string str, out IPAddressRange range)
            {
                IPAddress lowAddress = null, highAddress = null;
                if (str.Contains(":"))
                {
                    if (str.Contains("/"))//CIDR
                    {
                        const int IPv6_Total_Bits = 128;
                        string[] parts = str.Split('/');
                        if (IPAddress.TryParse(parts[0], out var address))
                        {
                            byte[] addressBytes = address.GetAddressBytes();
                            int maskBitCount = IPv6_Total_Bits - Math.Min(int.Parse(parts[1]), addressBytes.Length * 8);
                            byte[] highBytes = (byte[])addressBytes.Clone(),
                                    lowBytes = (byte[])addressBytes.Clone();
                            int byteIndex = addressBytes.Length;
                            for (int i = 0; i < maskBitCount; ++i)
                            {
                                byte maskBitNumber = (byte)(i % 8);
                                byte mask = (byte)(1 << maskBitNumber);
                                if (maskBitNumber == 0)
                                {
                                    byteIndex -= 1;
                                }
                                highBytes[byteIndex] = (byte)(highBytes[byteIndex] | mask);
                                lowBytes[byteIndex] = (byte)(lowBytes[byteIndex] & ~mask);
                            }

                            lowAddress = new IPAddress(lowBytes);
                            highAddress = new IPAddress(highBytes);
                        }
                        else
                        {
                            range = default;
                            return false;
                        }
                    }
                    else if (str.Contains("-"))//range
                    {
                        string[] parts = str.Split('-');
                        bool lowValid = IPAddress.TryParse(parts[0], out lowAddress);
                        bool highValid = IPAddress.TryParse(parts[1], out highAddress);
                        if (lowValid && highValid)
                        {
                            range = new IPAddressRange(lowAddress, highAddress);
                            return true;
                        }
                        else
                        {
                            range = default;
                            return false;
                        }
                    }
                    else if (IPAddress.TryParse(str, out var address))
                    {
                        range = new IPAddressRange(address, address);
                        return true;
                    }
                    else
                    {
                        range = default;
                        return false;
                    }
                }
                else if (Regex.IsMatch(str, @"\A\d+\.\d+\.\d+\.\d+\Z"))
                {
                    //single IP address
                    if (IPAddress.TryParse(str, out IPAddress address))
                    {
                        lowAddress = highAddress = address;
                    }
                    else
                    {
                        range = default;
                        return false;
                    }
                }
                else if (Regex.IsMatch(str, @"\A\d+\.\d+\.\d+\.\d+/\d+\Z"))
                {
                    //CIDR format
                    string[] parts = str.Split('/');

                    //Important Note: all four of these UInt32 are in network order, not host order, so don't do comparisons with them
                    //convert IP string to UInt32
                    UInt32 address = BitConverter.ToUInt32(IPAddress.Parse(parts[0]).GetAddressBytes(), 0);
                    UInt32 mask = (UInt32)IPAddress.HostToNetworkOrder((Int32)Math.Pow(2, 32 - int.Parse(parts[1])) - 1);
                    UInt32 lowValue = (UInt32)(address & ~mask);
                    UInt32 highValue = (UInt32)(address | mask);

                    //convert Int32 back to IPAddress
                    lowAddress = new IPAddress(lowValue);
                    highAddress = new IPAddress(highValue);
                }
                else if (str.Contains("-"))
                {
                    //range or implied range ( could be "10.5.3.3-10.5.3.40" or "10.5.3.3-40" )
                    string[] parts = str.Split('-');
                    string lowstr = parts[0], highstr;
                    string highstr__end = parts[1];
                    int endpartcount = highstr__end.Count(c => c == '.') + 1;
                    if (endpartcount == 4)
                    {
                        highstr = highstr__end;
                    }
                    else
                    {
                        string rg = @"\." + String.Join(@"\.", Enumerable.Repeat(@"\d+", endpartcount)) + @"\Z";
                        highstr = Regex.Replace(lowstr, rg, "." + highstr__end);
                    }
                    lowAddress = IPAddress.Parse(lowstr);
                    highAddress = IPAddress.Parse(highstr);
                }
                else if (Regex.IsMatch(str, @"\A(\d+\.){1,3}\Z"))
                {
                    //Implied IP address (e.g., 10. )
                    string lowstr = str;
                    while (lowstr.Count(c => c == '.') < 3)
                    {
                        lowstr += "0.";
                    }
                    lowstr += "0";
                    string highstr = str;
                    while (highstr.Count(c => c == '.') < 3)
                    {
                        highstr += "255.";
                    }
                    highstr += "255";
                    lowAddress = IPAddress.Parse(lowstr);
                    highAddress = IPAddress.Parse(highstr);
                }
                else
                {
                    //unsupported syntax
                    range = default;
                    return false;
                }
                if (lowAddress == null || highAddress == null)
                {
                    //this should never happen
                    throw new Exception("A problem was encountered when parsing a well-formed IP address range");
                }
                range = new IPAddressRange(lowAddress, highAddress);
                return true;
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
        /// <inheritdoc cref='IPAddressRange.TryParseRange(string, out IPAddressRange)' path="//list[@name='formats']"/>
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
        public string[] ToStringArray()
        {
#if DEBUG
            return rangesDEBUG.Select(a => a.ToString()).ToArray();
#else
            return ranges.Select(a => a.ToString()).ToArray();
#endif
        }
        public string ToDetailedString()
        {
            const string indent = ">  ";
            return $"Raw filter string: {FilterString}\n" +
                    $"Parsed as the following ranges:\n" +
                    $"{indent}{string.Join(",\n" + indent, ToStringArray())}\nEnd";
        }
    }
}