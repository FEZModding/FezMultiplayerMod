using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Net;
using System.IO;
using System.Collections.Concurrent;
using FezSharedTools;
using System.Threading.Tasks;
using static FezMultiplayerDedicatedServer.MultiplayerServerNetcode;

namespace FezMultiplayerDedicatedServer
{
    public static class IPAddressExtensions
    {
        public static string ToCommonString(this IPAddress addr)
        {
            if (addr.IsIPv4MappedToIPv6)
            {
                addr = addr.MapToIPv4();
            }
            return (addr.AddressFamily == AddressFamily.InterNetworkV6)
                            ? "[" + addr.ToString() + "]" : addr.ToString();
        }
        public static string ToCommonString(this IPEndPoint ep)
        {
            return ep.Address.ToCommonString() + ':' + ep.Port;
        }
    }

    /// <summary>
    /// The class that contains all the networking stuff
    /// 
    /// Note: This class should only contain System usings
    /// </summary>
    public class MultiplayerServerNetcode : SharedNetcode<ServerPlayerMetadata>, IDisposable
    {
        private const string CRLF = "\r\n";
        private const char WebSocketReplyMessageSeparator = '\uE001';

        [Serializable]
        public class ServerPlayerMetadata : PlayerMetadata
        {
            public Socket client;
            public readonly DateTime joinTime = DateTime.UtcNow;
            public TimeSpan TimeSinceJoin => DateTime.UtcNow - joinTime;
            public long NetworkSpeedUp = 0;
            public long NetworkSpeedDown = 0;

            public ServerPlayerMetadata(Socket client, Guid Uuid, string CurrentLevelName, Vector3 Position, Viewpoint CameraViewpoint, ActionType Action, int AnimFrame, HorizontalDirection LookingDirection, long LastUpdateTimestamp)
            : base(Uuid, CurrentLevelName, Position, CameraViewpoint, Action, AnimFrame, LookingDirection, LastUpdateTimestamp)
            {
                this.client = client;
            }
        }

        private readonly Socket listenerSocket;
        private readonly int listenPort;
        protected readonly int overduetimeout;
        public readonly bool useAllowList;
        public readonly IPFilter AllowList;
        public readonly IPFilter BlockList;
        public bool SyncWorldState;
        public bool AllowRemoteWebInterface;

        public override ConcurrentDictionary<Guid, ServerPlayerMetadata> Players { get; } = new ConcurrentDictionary<Guid, ServerPlayerMetadata>();
        public readonly ConcurrentDictionary<Guid, long> DisconnectedPlayers = new ConcurrentDictionary<Guid, long>();
        private IEnumerable<Socket> ConnectedClients => Players.Select(p => p.Value.client);
        public EndPoint LocalEndPoint => listenerSocket?.LocalEndPoint;

        public event Action OnUpdate = () => { };
        public event Action OnDispose = () => { };

        private readonly List<ActiveLevelState> activeLevelStates = new List<ActiveLevelState>();

        private readonly ServerAdvertiser serverAdvertiser;

        /// <summary>
        /// Creates a new instance of this class with the provided parameters.
        /// For any errors that get encountered see <see cref="ErrorMessage"/> an <see cref="FatalException"/>
        /// </summary>
        /// <param name="settings">The <see cref="MultiplayerServerSettings"/> to use to create this instance.</param>
        internal MultiplayerServerNetcode(MultiplayerServerSettings settings)
        {
            this.listenPort = settings.ListenPort;
            this.overduetimeout = settings.OverdueTimeout;
            this.useAllowList = settings.UseAllowList;
            this.AllowList = settings.AllowList;
            this.BlockList = settings.BlockList;
            this.SyncWorldState = settings.SyncWorldState;
            this.AllowRemoteWebInterface = settings.AllowRemoteWebInterface;

            bool initializing = true;
            int retries = 0;
            while (initializing)
            {
                try
                {
                    // Create a listener socket that can accept both IPv4 and IPv6 connections
                    listenerSocket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);

                    // Set the socket to accept both IPv4 and IPv6 connections
                    listenerSocket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);

                    // Bind the socket to any address and the specified port
                    listenerSocket.Bind(new IPEndPoint(IPAddress.IPv6Any, listenPort));
                    listenerSocket.Listen(10); // Listen for incoming connections, with a specified backlog
                    initializing = false;
                }
                catch (Exception)
                {
                    if (settings.MaxAdjustListenPortOnBindFail > retries++)
                    {
                        Console.WriteLine($"Port {listenPort} is already in use. Trying {listenPort + 1} instead.");
                        listenPort++;
                    }
                    else
                    {
                        //ErrorMessage = e.Message;
                        ErrorMessage = $"Failed to bind a port after {retries} tr{(retries == 1 ? "y" : "ies")}. Ports number {listenPort - retries + 1} to {listenPort} are already in use. Exiting.";
                        return;
                    }
                }
            }
            _ = StartAcceptTcpClients();
            _ = Task.Factory.StartNew(RemoveOldClients, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            if (settings.DoAdvertiseServer)
            {
                Dictionary<string, string> multicastData = new Dictionary<string, string>()
                {
                    { "Protocol", ProtocolSignature },
                    { "Version", ProtocolVersion },
                    { "Endpoint", listenPort.ToString() },
                };
                string message = string.Join("\n", multicastData.Select(kv => kv.Key + SharedConstants.ServerDiscoveryEntrySeparator + kv.Value));
                serverAdvertiser = new ServerAdvertiser(SharedConstants.MulticastAddress, message);
                //serverAdvertiser.SetMessage(message);
                OnDispose += serverAdvertiser.Dispose;
            }
        }
        private void RemoveOldClients()
        {
            while (!disposing)
            {
                Thread.Sleep(500);
                try
                {
                    foreach (var p in Players)
                    {
                        if (disposing)
                        {
                            break;
                        }
                        Thread.Sleep(10);
                        if (p.Key.Equals(Guid.Empty))
                        {
                            Thread.Sleep(1000);
                            if (p.Key.Equals(Guid.Empty))
                            {
                                p.Value.client.Close();
                                ProcessDisconnectInternal(p.Key);
                            }
                        }
                        if (!p.Value.client.Connected)
                        {
                            ProcessDisconnectInternal(p.Key);
                        }
                    }
                }
                catch
                {
                }
            }
        }
        private async Task StartAcceptTcpClients()
        {
            try
            {
                while (!disposing)
                {
                    //Note: AcceptTcpClient blocks until a connection is made
                    //Note: apparently tcpListener.AcceptTcpClient(); is so blocking, if it's in a Thread, it even blocks calls to that thread's .Abort() method 
                    //TcpClient client = tcpListener.AcceptTcpClient();
                    Socket client = await Task.Factory.StartNew(() => listenerSocket.Accept());
                    new Thread(() =>
                    {
                        try
                        {
                            IPEndPoint remoteEndpoint = (IPEndPoint)client.RemoteEndPoint;
                            if (BlockList.Contains(remoteEndpoint.Address)
                                || (useAllowList && !AllowList.Contains(remoteEndpoint.Address))
                                    )
                            {
                                client.ForceDisconnect();
                                return;
                            }
                            OnNewClientConnect(client);
                        }
                        catch (Exception e)
                        {
                            //TODO handle exception
                            Console.WriteLine(e);
                        }
                        finally
                        {
                            client.Close();
                        }
                    }).Start();
                }
            }
            catch (Exception e) { FatalException = e; }
        }


        // I was told "Your Dispose implementation needs work https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose#implement-the-dispose-pattern"
        // and stuff like "It technically works but is dangerous" and "always use an internal protected Dispose method" and "always call GC.SuppressFinalize(this) in the public Dispose method"
        // so I added all this extra stuff even though it technically already worked fine, so hopefully this works fine
        private bool disposed = false;
        /// <summary>
        /// used to tell notify the child threads to stop
        /// </summary>
        private volatile bool disposing = false;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources here

                    this.disposing = true;//let child threads know it's disposing time
                    OnDispose();
                    Thread.Sleep(1000);//try to wait for child threads to stop on their own
                    if (listenerSocket.Connected)
                    {
                        listenerSocket.Shutdown(SocketShutdown.Both);
                    }
                    foreach (Socket client in ConnectedClients)
                    {
                        client.Close();
                        client.Dispose();
                    }
                    listenerSocket.Close();
                    listenerSocket.Dispose();
                }

                // Dispose unmanaged resources here

                disposed = true;
            }
        }
        ~MultiplayerServerNetcode()
        {
            Dispose(false);
        }

        private static readonly Stopwatch timer = Stopwatch.StartNew();
        private static float DefaultTimeFactor => 260f;
        public double TimeScale = 1;
        private static double lastTimeUpdate = 0;
        public void Update()
        {
            if (FatalException != null)
            {
                throw FatalException;//This should never happen
            }
            long curtime = DateTime.UtcNow.Ticks;
            List<Guid> KeysToRemove = new List<Guid>();
            foreach (var kvpair in DisconnectedPlayers)
            {
                if ((curtime - kvpair.Value) / (double)TimeSpan.TicksPerMillisecond >= overduetimeout)
                {
                    KeysToRemove.Add(kvpair.Key);
                    //Don't remove the keys while iterating over the ConcurrentDictionary
                }
            }
            foreach (var k in KeysToRemove)
            {
                _ = DisconnectedPlayers.TryRemove(k, out _);
            }
            double curTime = timer.ElapsedMilliseconds;
            sharedSaveData.TimeOfDay += TimeSpan.FromMilliseconds((curTime - lastTimeUpdate) * DefaultTimeFactor * TimeScale);
            lastTimeUpdate = curTime;
            OnUpdate();
        }

        private static readonly TimeSpan NewPlayerTimeSpan = TimeSpan.FromSeconds(1);
        private void OnNewClientConnect(Socket client)
        {
            Guid uuid = Guid.NewGuid();
            bool isPlayer = false;
            bool isLoopback = IPAddress.IsLoopback(((IPEndPoint)client.RemoteEndPoint).Address);
            try
            {
                //Console.WriteLine($"Incoming connection from {client.RemoteEndPoint}...");
                using (NetworkStream stream = new NetworkStream(client))
                {
                    stream.ReadTimeout = overduetimeout;
                    stream.WriteTimeout = overduetimeout;
                    using (BinaryNetworkReader reader = new BinaryNetworkReader(stream))
                    using (BinaryNetworkWriter writer = new BinaryNetworkWriter(stream))
                    {
                        try
                        {
                            if (client.Available > 0)
                            {
                                //if it's a web request but the web interface is disabled, close the connection without sending back any data
                                if (isLoopback || AllowRemoteWebInterface)
                                {
                                    string request = Encoding.UTF8.GetString(reader.ReadBytes(client.Available));
                                    string[] lines = request.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                                    if (lines.Length > 0)
                                    {
                                        isPlayer = false;

                                        string[] line1 = lines[0].Split(' ');

                                        if (line1.Length >= 3)
                                        {
                                            string method = line1[0];
                                            string uri = line1[1];
                                            string protocol = line1[2];

                                            Dictionary<string, string> parseHeaders(IEnumerable<string> headerLines)
                                            {
                                                var h = headerLines.Select(line => line.Split(new char[] { ':' }, 2));
                                                Dictionary<string, string> headerDict = new Dictionary<string, string>();
                                                foreach (string[] entry in h)
                                                {
                                                    string k = entry[0];
                                                    string v = entry.Length > 1 ? entry[1] : "";
                                                    if (v.StartsWith(" "))
                                                    {
                                                        v = v.Substring(1);
                                                    }
                                                    if (headerDict.TryGetValue(k, out string headerval) && headerval.Length > 0)
                                                    {
                                                        headerDict[k] += ", " + headerval;
                                                    }
                                                    else
                                                    {
                                                        headerDict[k] = v;
                                                    }
                                                }
                                                return headerDict;
                                            }

                                            var headers = parseHeaders(lines.Skip(1).TakeWhile(line => line.Length > 0));

                                            if (headers.TryGetValue("Upgrade", out string ug) && ug.Equals("websocket")
                                                    && headers.TryGetValue("Sec-WebSocket-Key", out string wskey))
                                            {
                                                writer.Write(Encoding.UTF8.GetBytes("HTTP/1.1 101 Switching Protocols" + CRLF
                                                    + "Connection: Upgrade" + CRLF
                                                    + $"Date: {DateTime.UtcNow:R}{CRLF}"
                                                    + "Upgrade: websocket" + CRLF
                                                    + "Sec-WebSocket-Accept: " + Convert.ToBase64String(
                                                        System.Security.Cryptography.SHA1.Create().ComputeHash(
                                                            Encoding.UTF8.GetBytes(wskey + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"
                                                            )
                                                        )
                                                    ) + CRLF
                                                    + CRLF));
                                                Stopwatch timeoutTimer = Stopwatch.StartNew();
                                                bool closing = false;
                                                while (client.Connected && !disposing && !closing)
                                                {
                                                    if (timeoutTimer.ElapsedMilliseconds > 5000)
                                                    {
                                                        break;//terminate connection
                                                    }
                                                    if (client.Available <= 0)
                                                    {
                                                        Thread.Sleep(10);
                                                        continue;
                                                    }
                                                    timeoutTimer.Restart();
                                                    string message = DecodeWebSocketMessage(reader.ReadBytes(client.Available), out closing, out bool ping);
                                                    if (closing)
                                                    {
                                                        break;
                                                    }
                                                    else if (ping)
                                                    {
                                                        System.Diagnostics.Debugger.Launch();
                                                        System.Diagnostics.Debugger.Break();
                                                        SendWebSocketMessage(writer, message, opcode: 0xA);
                                                    }
                                                    else if (message.Length > 0)
                                                    {
                                                        SendWebSocketMessage(writer, message + WebSocketReplyMessageSeparator + GenerateWebResponse(method, message, isLoopback: isLoopback, includeHttpHeaders: false));
                                                    }
                                                }
                                                if (client.Connected)
                                                {
                                                    //send close message
                                                    short closeStatus = (short)(disposing ? 1001 : 1000);
                                                    byte[] bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)closeStatus));
                                                    SendWebSocketMessage(writer, "", opcode: 0x8, bytes);
                                                }
                                            }
                                            else
                                            {
                                                //Console.WriteLine($"Web browser {method} {uri} from {client.RemoteEndPoint}. Sending response...");
                                                writer.Write(Encoding.UTF8.GetBytes(GenerateWebResponse(method, uri, isLoopback: isLoopback, includeHttpHeaders: true, closing: true)));
                                                //Console.WriteLine($"Responded to {method} {uri} from {client.RemoteEndPoint}. Terminating connection.");
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {

                                Queue<long> SpeedUp = new Queue<long>(100);
                                Queue<long> SpeedDown = new Queue<long>(100);
                                //send them our data and get player appearance from client
                                SpeedUp.Enqueue(WriteServerGameTickPacket(writer, Players.Values.Cast<PlayerMetadata>().ToList(),
                                        null, GetActiveLevelStates(), DisconnectedPlayers.Keys,
                                        PlayerAppearances, uuid, false, sharedSaveData, sharedSaveData.TimeOfDay));
                                MiscClientData clientData = new MiscClientData(null, false, new HashSet<Guid>(MiscClientData.MaxRequestedAppearancesSize));
                                SpeedDown.Enqueue(ReadClientGameTickPacket(reader, ref clientData, uuid));
                                bool Disconnecting = clientData.Disconnecting;
                                PlayerMetadata playerMetadata = clientData.Metadata;

                                ServerPlayerMetadata addValueFactory(Guid guid)
                                {
                                    return new ServerPlayerMetadata(client, uuid, playerMetadata.CurrentLevelName, playerMetadata.Position, playerMetadata.CameraViewpoint,
                                                playerMetadata.Action, playerMetadata.AnimFrame, playerMetadata.LookingDirection, playerMetadata.LastUpdateTimestamp);
                                }
                                ServerPlayerMetadata updateValueFactory(Guid guid, ServerPlayerMetadata currentval)
                                {
                                    currentval.client = client;
                                    //Note: the value of playerMetadata.Uuid received from the client is never used
                                    //We use the Guid that we assigned instead, for security reasons. 
                                    currentval.Uuid = guid;
                                    if (currentval.LastUpdateTimestamp < playerMetadata.LastUpdateTimestamp)
                                    {
                                        currentval.CopyValuesFrom(playerMetadata);
                                    }
                                    return currentval;
                                }

                                isPlayer = true;
                                Players.AddOrUpdate(uuid, addValueFactory, updateValueFactory);
                                Console.WriteLine($"Player connected from {client.RemoteEndPoint}. Assigned uuid {uuid}.");

                                bool PlayerAppearancesFilter(KeyValuePair<Guid, ServerPlayerMetadata> p)
                                {
                                    //get the requested PlayerAppearances from PlayerAppearances, and players that have recently joined
                                    return clientData.RequestedAppearances.Contains(p.Key) || p.Value.TimeSinceJoin < NewPlayerTimeSpan;
                                }

                                while (client.Connected && !disposing)
                                {
                                    if (Disconnecting)
                                    {
                                        break;
                                    }
                                    if (Players.TryGetValue(uuid, out ServerPlayerMetadata serverPlayerMetadata))
                                    {
                                        //Note: does not produce a meaningful number for connections to loopback addresses
                                        serverPlayerMetadata.NetworkSpeedUp = (long)Math.Round(SpeedUp.Average()) / TimeSpan.TicksPerMillisecond;
                                        serverPlayerMetadata.NetworkSpeedDown = (long)Math.Round(SpeedDown.Average()) / TimeSpan.TicksPerMillisecond;
                                    }
                                    //if UnknownPlayerAppearanceGuids contains uuid, ask client to retransmit their PlayerAppearance
                                    bool requestAppearance = UnknownPlayerAppearanceGuids.ContainsKey(uuid);
                                    //repeat until the client disconnects or times out
                                    if (SpeedUp.Count >= 100)
                                    {
                                        _ = SpeedUp.Dequeue();
                                    }
                                    if (SpeedDown.Count >= 100)
                                    {
                                        _ = SpeedDown.Dequeue();
                                    }
                                    SpeedUp.Enqueue(WriteServerGameTickPacket(writer, Players.Values.Cast<PlayerMetadata>().ToList(),
                                            GetSaveDataUpdate(), GetActiveLevelStates(), DisconnectedPlayers.Keys,
                                            GetPlayerAppearances(PlayerAppearancesFilter), null, requestAppearance, null, sharedSaveData.TimeOfDay));
                                    SpeedDown.Enqueue(ReadClientGameTickPacket(reader, ref clientData, uuid));
                                    Disconnecting = clientData.Disconnecting;
                                    playerMetadata = clientData.Metadata;
                                    Players.AddOrUpdate(uuid, addValueFactory, updateValueFactory);
                                    Thread.Sleep(10);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            //ignore exceptions that aren't from players
                            if (isPlayer)
                            {
                                SocketException se = e.InnerException as SocketException;
                                if (se != null)
                                {
#pragma warning disable IDE0010 // Add missing cases
                                    switch (se.SocketErrorCode)
                                    {
                                    case SocketError.TimedOut:
                                        break;
                                    default:
                                        Console.WriteLine(e);
                                        Console.WriteLine(e.InnerException);
                                        break;
                                    }
#pragma warning restore IDE0010 // Add missing cases
                                }
                                else
                                {
                                    Console.WriteLine(e);
                                    Console.WriteLine(e.InnerException);
                                }
                            }
                        }
                        finally
                        {
                            reader.Close();
                            writer.Close();
                            stream.Close();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                //TODO handle exception
                Console.WriteLine(e);
            }
            finally
            {
                if (isPlayer)
                {
                    long disconnectTime = DateTime.UtcNow.Ticks;
                    DisconnectedPlayers.AddOrUpdate(uuid, disconnectTime, (puid, oldTime) => disconnectTime);
                    ProcessDisconnect(uuid);
                }
                client.Close();
                client.Dispose();
            }
        }

        private static string DecodeWebSocketMessage(byte[] bytes, out bool closing, out bool ping)
        {

            bool fin = (bytes[0] & 0b10000000) != 0,
                mask = (bytes[1] & 0b10000000) != 0; // must be true, "All messages from the client to the server have this bit set"
            int opcode = bytes[0] & 0b00001111; // expecting 1 - text message
            ulong offset = 2,
                  msgLen = bytes[1] & (ulong)0b01111111;

            if (msgLen == 126)
            {
                // bytes are reversed because websocket will print them in Big-Endian, whereas
                // BitConverter will want them arranged in little-endian on windows
                msgLen = BitConverter.ToUInt16(new byte[] { bytes[3], bytes[2] }, 0);
                offset = 4;
            }
            else if (msgLen == 127)
            {
                // To test the below code, we need to manually buffer larger messages — since the NIC's autobuffering
                // may be too latency-friendly for this code to run (that is, we may have only some of the bytes in this
                // websocket frame available through client.Available).
                msgLen = BitConverter.ToUInt64(new byte[] { bytes[9], bytes[8], bytes[7], bytes[6], bytes[5], bytes[4], bytes[3], bytes[2] }, 0);
                offset = 10;
            }
            closing = opcode == 8;
            ping = opcode == 9;
            if (mask)
            {
                byte[] decoded = new byte[msgLen];
                byte[] masks = new byte[4] { bytes[offset], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3] };
                offset += 4;

                for (ulong i = 0; i < msgLen; ++i)
                    decoded[i] = (byte)(bytes[offset + i] ^ masks[i % 4]);

                string text = Encoding.UTF8.GetString(decoded);
                return text;
            }
            return null;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="message"></param>
        /// <param name="opcode">Defaults to 1 (text message). For more options, see <see href="https://datatracker.ietf.org/doc/html/rfc6455#section-11.8"/></param>
        private static void SendWebSocketMessage(BinaryNetworkWriter writer, string message, byte opcode = 1, byte[] rawBytes = null)
        {
            byte[] rawData = rawBytes ?? Encoding.UTF8.GetBytes(message);
            int length = rawData.Length;
            byte[] frame;
            int indexStartData = 0;

            // A text message frame starts with opcode 0x81 (FIN bit set + Text opcode)
            byte opcodeByte = (byte)(0x80 | opcode);

            // A text message frame starts with opcode 0x81 (FIN bit set + Text opcode)
            if (length <= 125)
            {
                frame = new byte[2 + length];
                frame[0] = opcodeByte;
                frame[1] = (byte)length;
                indexStartData = 2;
            }
            else if (length >= 126 && length <= 65535)
            {
                frame = new byte[4 + length];
                frame[0] = opcodeByte;
                frame[1] = 126; // Extended payload length marker
                frame[2] = (byte)((length >> 8) & 255);
                frame[3] = (byte)(length & 255);
                indexStartData = 4;
            }
            else
            {
                // For very large messages, use 8-byte extended payload length
                // (omitted for brevity, but follows the same pattern as above)
                throw new NotSupportedException("Messages larger than 65535 bytes not implemented in this example.");
            }

            // Copy the raw message data into the frame
            for (int i = 0; i < length; i++)
            {
                frame[i + indexStartData] = rawData[i];
            }

            // Send the framed data over the TCP stream
            writer.Write(frame);
            writer.Flush();
        }

        private string GenerateWebResponse(string method, string uri, bool isLoopback, bool closing = false, bool includeHttpHeaders = true)
        {
            // see https://datatracker.ietf.org/doc/html/rfc2616

            string statusText = "200 OK";
            string title = nameof(FezMultiplayerDedicatedServer);

            if (uri.StartsWith("/"))
            {
                uri = uri.Substring(1);
            }

            const string Uri_players = "players.dat";
            const string Uri_appearances = "appearances.dat";
            const string Uri_disconnects = "disconnects.dat";
            Dictionary<string, (string ContentType, Func<string> Generator)> uriProviders = new Dictionary<string, (string, Func<string>)>(){
                {"favicon.ico", ("image/png", ()=>"") },
                {Uri_players, ("text/plain", ()=>DateTime.UtcNow.Ticks+"\n"+string.Join("\n", Players.Select(kv => {
                    string str = string.Join("\t", IniTools.GenerateIni(kv.Value, false, false));
                    if (isLoopback)
                    {
                        str = str.Replace("client=System.Net.Sockets.Socket", "client="+((IPEndPoint)kv.Value.client.RemoteEndPoint).ToCommonString());
                    }
                    return str;
                }))) },
                {Uri_appearances, ("text/plain", ()=>string.Join("\n", PlayerAppearances.Select(kv => kv.Key+"\t"+kv.Value.PlayerName))) },
                {Uri_disconnects, ("text/plain", ()=>string.Join("\n", DisconnectedPlayers.Keys)) },
                //TODO
            };
            string body;
            string contentType;
            if (uriProviders.TryGetValue(uri, out var provider))
            {
                body = provider.Generator();
                contentType = provider.ContentType;
            }
            else
            {
                body = $"<!DOCTYPE html>{CRLF}<html lang=\"en\">" +
                $"<head>" +
                $"<meta name=\"generator\" content=\"FezMultiplayerMod via https://github.com/FEZModding/FezMultiplayerMod\" />" +
                $"<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />" +
                $"<meta name=\"color-scheme\" content=\"dark light\" />" +
                $"<title>{title}</title>" +
                $"<style>" +
                $@"
                #wrapper {{
                    resize: horizontal;
                    overflow: auto;
                }}
                table, thead, tbody, tr, th, td {{
                    table-layout: fixed;
                    border: 1px solid black;
                    border: 1px solid CurrentColor;
                    border-collapse: collapse;
                    th, td {{
                        padding: 0.3ex;
                        font-variant-numeric: tabular-nums;
                        font-family: monospace;
                    }}
                    th[data-col-name=""Uuid""], td[data-col-name=""Uuid""] {{/*uuid*/
                        overflow: hidden;
                        max-width: 6ch;
                        text-wrap: nowrap;
                    }}
                    td[data-col-name=""PlayerName""] {{/*player name*/
                        overflow: hidden;
                        max-width: 16ch;
                        text-wrap: nowrap;
                    }}
                    th[data-col-name=""client""], td[data-col-name=""client""] {{/*client*/
                        {(isLoopback ? "" : "display: none;")}
                    }}
                    td[data-col-name=""CurrentLevelName""] {{/*level name*/
                    }}
                    td[data-col-name=""Action""] {{/*action*/
                        overflow: hidden;
                        min-width: 15ch;
                        max-width: 15ch;
                        text-wrap: nowrap;
                    }}
                    th[data-col-name=""CameraViewpoint""], td[data-col-name=""CameraViewpoint""] {{/*viewpoint*/
                        overflow: hidden;
                        max-width: 6ch;
                        text-wrap: nowrap;
                    }}
                }}
                tbody:empty::after {{
                    content: 'No data';
                    color: gray;
                    color: color-mix(in srgb, currentColor 33%, transparent);
                }}
                td:empty::after {{
                    content: 'null';
                    color: gray;
                    color: color-mix(in srgb, currentColor 33%, transparent);
                }}
                span[data-status] {{
                    --status-color: #FF0;
                    background-color: color-mix(in srgb, var(--status-color), transparent 60%);
                    color: color-mix(in srgb, var(--status-color), currentColor 77%);
                    border: 1px solid currentColor;
                    padding-inline: calc(1lh - 1ic);
                    text-transform: uppercase;
                    display: inline-block;
                }}
                span[data-status=""CONNECTED""] {{
                    --status-color: #0F0;
                }}
                span[data-status=""ERROR""] {{
                    --status-color: #F00;
                }}
                #reconnectButton {{
                    vertical-align: middle;
                }}
                " +
                $"</style>" +
                $"<script src=\"https://jenna1337.github.io/tools/RichTextRenderer.js\"></script>\n" +
                $"<script>" +
                $@"
                const colNames=['Uuid','PlayerName','client','CurrentLevelName','Action','CameraViewpoint','Position','joinTime','LastUpdateTimestamp','NetworkSpeedUp','NetworkSpeedDown','ping'];
                const constColumns = 2;
                const nameIndex = colNames.indexOf('PlayerName') ;
                const pingIndex = colNames.indexOf('ping') ;
                document.addEventListener('DOMContentLoaded',()=>{{
                    var pdat=document.getElementById('playerData');
	                var thr=pdat.createTHead().insertRow();
                    colNames.forEach(a=>{{
                        var th=document.createElement('th');
	                    th.textContent=a.split(/(?=[A-Z])/).join(' ').toLowerCase().replace(/\b\w/g, s => s.toUpperCase());
                        th.dataset.colName = a;
                        th.scope='col';
	                    thr.appendChild(th);
                    }});
                    connect();
                }});
                let consecutiveErrors = 0;
                let websocket;
                function connect(){{
                    const pCountElem = document.getElementById('playerCount');
                    const pdat=document.getElementById('playerData');
                    const connStatus=document.getElementById('connStatus');
                    const connStatusDesc=document.getElementById('connStatusDesc');
                    const reconnectButton=document.getElementById('reconnectButton');
                    const tbod = pdat.tBodies[0] ?? pdat.createTBody();
                    const wsUri = 'ws://'+location.host+'/{Uri_players}';
                    reconnectButton.style.display = 'none';
                    connStatus.textContent = 'CONNECTING...';
                    websocket = new WebSocket(wsUri);
                    let errored = false;
                    websocket.addEventListener('open', () => {{
                        errored = false;
                        connStatusDesc.textContent = '';
                        consecutiveErrors = 0;
                        tbod.innerHTML = '';
                        console.log(connStatus.textContent = 'CONNECTED');
                        connStatus.dataset.status = 'CONNECTED';
                        reconnectButton.style.display = 'none';
                        pCountElem.textContent = 'Loading...';
                        websocket.send('{Uri_players}');
                    }});
                    const MAX_RETRIES = 3;
                    websocket.addEventListener('error', (e) => {{
                        errored = true;
                        consecutiveErrors += 1;
                        console.log(connStatus.textContent = 'ERROR');
                        connStatus.dataset.status = 'ERROR';
                        console.log(e);
                        pCountElem.textContent = '???';
                        tbod.innerHTML = '';
                        if(consecutiveErrors >= MAX_RETRIES){{
                            connStatus.textContent = 'DISCONNECTED';
                            connStatusDesc.textContent = 'Failed to connect after '+consecutiveErrors+' attempts';
                            console.log('Failed to connect to server');
                            tbod.innerHTML = 'Failed to connect to server';
                            reconnectButton.style.display = '';
                        }}else{{
                            connStatus.textContent = 'DISCONNECTED';
                            connStatusDesc.textContent = 'Retrying... (attempt '+consecutiveErrors+'/'+MAX_RETRIES+')';
                            window.setTimeout(()=>connect(),1000);
                        }}
                    }});
                    websocket.addEventListener('close', (e) => {{
                        if (websocket.readyState == WebSocket.CLOSING || websocket.readyState == WebSocket.CLOSED){{
                            console.log(connStatus.textContent = 'DISCONNECTED');
                            connStatus.dataset.status = 'DISCONNECTED';
                            pCountElem.textContent = '???';
                            tbod.innerHTML = '';
                            if(consecutiveErrors >= MAX_RETRIES || !errored)
                                reconnectButton.style.display = '';
                        }}
                    }});
                    document.onvisibilitychange = (event) => {{
                        if (document.visibilityState === 'hidden') {{
                            websocket.close();
                            console.log('Page is hidden.');
                        }} else {{
                            if (websocket.readyState == WebSocket.CLOSING || websocket.readyState == WebSocket.CLOSED) 
                                connect();
                        }}
                    }};
                    var lastAppearenceCheck = -Infinity;
                    var lastDisconnectCheck = -Infinity;
                    websocket.addEventListener('message', (e) => {{
                        try{{
                        var dat = e.data;
                        var sepLoc = dat.indexOf('\u{(int)WebSocketReplyMessageSeparator:X4}');
                        var responseTo = dat.slice(0,sepLoc);
                        message = dat.slice(sepLoc + 1);
                        switch(responseTo){{
                            case '{Uri_players}':
                                var p=null;
                                let servertime = 0;
                                (p=message.split(""\n"").filter(a=>a.length>0)).forEach((m,mi)=>{{
                                    if(mi==0){{
                                        servertime=Number(m);
                                        return;
                                    }}
                                    var o=Object.fromEntries(m.split(""\t"").map(aa=>aa.split('=')));
                                    var tr = tbod.querySelector('[data-uuid=""'+o.Uuid+'""]');
                                    function setData(cell,c){{
                                        if(cell.textContent!=o[c]){{
                                            if(c=='Position'){{
                                                const fixed = '<'+o[c].match(/\d+(\.\d+)?/g).map(a=>parseFloat(a).toFixed(3)).join(', ')+'>';
                                                if(cell.textContent!=fixed)
                                                    cell.textContent=fixed;
                                            }}else if(c=='Action'){{
                                                cell.innerHTML=o[c].split(/(?=[A-Z])/g).join('<wbr />');
                                            }}else{{
                                                cell.textContent=o[c];
                                            }}
                                            if(c=='LastUpdateTimestamp'){{
                                                if(tr.cells[pingIndex])
                                                    tr.cells[pingIndex].textContent = ((servertime - o[c])/{TimeSpan.TicksPerSecond}).toFixed(6);
                                            }}
                                            cell.title = cell.textContent;
                                        }}
                                    }}
                                    if(!tr){{
                                        tr = tbod.insertRow();
	                                    tr.dataset.uuid=o.Uuid;
                                        colNames.forEach(c=>{{
                                            const td=tr.insertCell();
                                            td.dataset.colName = c;
                                            setData(td,c);
                                        }});
                                    }}
                                    else colNames.forEach((c,i)=>{{
                                        if(i!=nameIndex && i!=pingIndex){{
                                            setData(tr.cells[i],c);
                                        }}
                                    }});
                                }});
                                if(pCountElem.textContent != p.length.toString())
                                    pCountElem.textContent = p.length - 1;
                                break;
                            case '{Uri_disconnects}':
                                message.split(""\n"").forEach(uuid=>tbod.querySelector('[data-uuid=""'+uuid+'""]')?.remove());
                                break;
                            case '{Uri_appearances}':
                                message.split(""\n"").forEach(a=>{{
                                    var sepLoc = a.indexOf(""\t"");
                                    var uuid = a.slice(0,sepLoc);
                                    var name = a.slice(sepLoc + 1);
                                    var q=tbod.querySelector('[data-uuid=""'+uuid+'""]');
                                    if(!q)return;
                                    var c=q.cells[nameIndex];
                                    if(c.dataset.name!=name){{
                                        c.dataset.name=name;
                                        if(RenderRichText){{
                                            c.innerHTML = '';
                                            c.appendChild(RenderRichText(name).elemTree);
                                            c.title = c.textContent;
                                        }}else{{
                                            c.textContent=name;
                                        }}
                                    }}
                                }});
                                break;
                            default:
                                document.getElementById(responseTo).textContent=message;
                        }}
                        }}catch(e){{
                            console.log(e);
                        }}finally{{
                            if(performance.now() - lastAppearenceCheck > 1000){{
                                lastAppearenceCheck = performance.now();
                                websocket.send('{Uri_appearances}');
                            }}else if(performance.now() - lastDisconnectCheck > 1000){{
                                lastDisconnectCheck = performance.now();
                                websocket.send('{Uri_disconnects}');
                            }}else{{
                                websocket.send('{Uri_players}');
                            }}
                        }}
                    }});
                }}
                " +
                $"</script>" +
                $"</head>" +
                $"<body>TODO: if this page is opened on localhost, add buttons do kick/ban players?" +
                $"<pre>{ProtocolSignature} netcode version \"{ProtocolVersion}\"</pre>" +
                $"Connection status:<span id=\"connStatus\" data-status=\"\">Unknown</span> <span id=\"connStatusDesc\"></span> " +
                $"<button style=\"display: none\" id=\"reconnectButton\" type=\"button\" onclick=\"connect()\">Reconnect</button><br />" +
                $"Player Count: <span id=\"playerCount\">Unknown</span><br />" +
                $"Player Data:<div id=\"wrapper\"><table id=\"playerData\"></table></div>" +
                $"</body>" +
                $"</html>";
                contentType = "text/html";
            }
            if (!includeHttpHeaders)
            {
                return body;
            }

            string[] headersArr = new string[]{
                $"Date: {DateTime.UtcNow:R}",
                $"Cache-Control: no-store, no-cache, must-revalidate, max-age=0",
                $"Pragma: no-cache",
                $"Content-Type: {contentType}",
                $"Content-Length: {Encoding.UTF8.GetByteCount(body)}",
            };
            if (closing)
            {
                headersArr = headersArr.Append("Connection: close").ToArray();
            }
            string headers = string.Join(CRLF, headersArr) + CRLF;

            return $"HTTP/1.1 {statusText}{CRLF}{headers}{CRLF}{body}";
        }

        protected override void ProcessDisconnect(Guid puid)
        {
            Console.WriteLine($"Disconnecting player {puid}.");
            ProcessDisconnectInternal(puid);
        }
        private void ProcessDisconnectInternal(Guid puid)
        {
            try
            {
                if (Players.TryGetValue(puid, out ServerPlayerMetadata p))// && !DisconnectedPlayers.ContainsKey(puid))
                {
                    long disconnectTime = DateTime.UtcNow.Ticks;
                    DisconnectedPlayers.AddOrUpdate(puid, disconnectTime, (lpuid, oldTime) => disconnectTime);
                    _ = Players.TryRemove(puid, out _);
                }
            }
            catch (InvalidOperationException) { }
            catch (KeyNotFoundException) { } //this can happen if an item is removed by another thread while this thread is iterating over the items

            //this should happen after we remove the player from the Players collection, to avoid any race conditions 
            _ = PlayerAppearances.TryRemove(puid, out _);
        }

        private static readonly List<ActiveLevelState> empty = new List<ActiveLevelState>();
        private List<ActiveLevelState> GetActiveLevelStates()
        {
            return SyncWorldState ? activeLevelStates : empty;
        }

        private readonly SaveData sharedSaveData = new SaveData();

        protected override void ProcessSaveDataUpdate(SaveDataUpdate saveDataUpdate)
        {
            if (!SyncWorldState)
            {
                return;
            }
            //TODO not yet implemented
            throw new NotImplementedException();
        }
        private SaveDataUpdate? GetSaveDataUpdate()
        {
            if (!SyncWorldState)
            {
                return null;
            }
            //TODO not yet implemented
            throw new NotImplementedException();
        }

        protected override void ProcessActiveLevelState(ActiveLevelState activeLevelState)
        {
            if (!SyncWorldState)
            {
                return;
            }
            //TODO not yet implemented
            throw new NotImplementedException();
        }
        /// <summary>
        /// Returns a collection of PlayerAppearances for players that match <paramref name="where"/>
        /// </summary>
        /// <param name="where">The filter to use to specify which PlayerAppearances to return</param>
        /// <returns></returns>
        private Dictionary<Guid, PlayerAppearance> GetPlayerAppearances(Func<KeyValuePair<Guid, ServerPlayerMetadata>, bool> where)
        {
            //IEnumerable<Guid> recentlyJoinedPlayers = Players.Values.Where(meta => meta.TimeSinceJoin < NewPlayerTimeSpan)
            //        .Select(p => p.Uuid).ToHashSet();
            //idk which of these is better
            IEnumerable<Guid> recentlyJoinedPlayers = Players.Where(where)
                    .Select(p => p.Key).ToHashSet();

            return PlayerAppearances
                    .Where(entry => recentlyJoinedPlayers.Contains(entry.Key))
                    .ToDictionary(entry => entry.Key, entry => entry.Value);
        }
    }
}