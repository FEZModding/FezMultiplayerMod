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
#if FEZCLIENT
using ActionType = FezGame.Structure.ActionType;
using HorizontalDirection = FezEngine.HorizontalDirection;
using Viewpoint = FezEngine.Viewpoint;
using Vector3 = Microsoft.Xna.Framework.Vector3;
#else
using ActionType = System.Int32;
using HorizontalDirection = System.Int32;
using Viewpoint = System.Int32;
public struct Vector3
{
    public float X, Y, Z;
    public Vector3(float x, float y, float z)
    {
        X = x; Y = y; Z = z;
    }
}
#endif
namespace FezGame.MultiplayerMod
{
    /// <summary>
    /// The class that contains all the networking stuff
    /// 
    /// Note: This class should only contain System usings
    /// </summary>
    public class MultiplayerServer : IDisposable
    {
        [Serializable]
        public struct PlayerMetadata
        {
            public IPEndPoint Endpoint;
            public readonly Guid Uuid;
            public string PlayerName;
            public string CurrentLevelName;
            public Vector3 Position;
            public ActionType Action;
            public int AnimFrame;
            public long LastUpdateTimestamp;
            public HorizontalDirection LookingDirection;
            public Viewpoint CameraViewpoint;
            /// <summary>
            /// for auto-disposing, since LastUpdateTimestamp shouldn't be used for that because the system clocks of the two protocols could be different
            /// </summary>
            public long LastUpdateLocalTimestamp;

            public PlayerMetadata(IPEndPoint Endpoint, Guid Uuid, string PlayerName, string CurrentLevelName, Vector3 Position, Viewpoint CameraViewpoint, ActionType Action, int AnimFrame, HorizontalDirection LookingDirection, long LastUpdateTimestamp, long LastUpdateLocalTimestamp)
            {
                this.Endpoint = Endpoint;
                this.Uuid = Uuid;
                this.PlayerName = PlayerName;
                this.CurrentLevelName = CurrentLevelName;
                this.Position = Position;
                this.Action = Action;
                this.AnimFrame = AnimFrame;
                this.LookingDirection = LookingDirection;
                this.LastUpdateTimestamp = LastUpdateTimestamp;
                this.CameraViewpoint = CameraViewpoint;
                this.LastUpdateLocalTimestamp = LastUpdateLocalTimestamp;
            }
        }

        private volatile UdpClient udpListener;
        private readonly Thread listenerThread;
        private readonly Thread timeoutthread;
        protected readonly IPEndPoint[] mainEndpoint;
        protected readonly long overduetimeout;
        private readonly bool useAllowList;
        private readonly IPFilter AllowList;
        private readonly IPFilter BlockList;

        /// <summary>
        /// How long to wait, in ticks, before stopping sending or receiving packets for this player.
        /// This is required otherwise race conditions can occur.
        /// <br /><br />Note: could make this customizable
        /// </summary>
        private readonly long preoverduetimeoutoffset = TimeSpan.TicksPerSecond * 5;

        public readonly ConcurrentDictionary<Guid, PlayerMetadata> Players = new ConcurrentDictionary<Guid, PlayerMetadata>();
        private IEnumerable<IPEndPoint> Targets => Players.Select(p => p.Value.Endpoint).Concat(mainEndpoint);
        public bool Listening => udpListener?.Client?.IsBound ?? false;
        public EndPoint LocalEndPoint => udpListener?.Client?.LocalEndPoint;
        public readonly Guid MyUuid = Guid.NewGuid();
        public string MyPlayerName = "";

        //Note: it has to connect to another player before it propagates the player information
        /// <summary>
        /// for true it relays the IP endpoints of all the players to all the other players, otherwise IP addressed will only be sent to the <see cref="MultiplayerClientSettings.mainEndpoint"/>.
        /// </summary>
        private readonly bool serverless;

        public volatile string ErrorMessage = null;//Note: this gets updated in the listenerThread
        /// <summary>
        /// If not null, contains a fatal exception that was thrown on a child Thread
        /// </summary>
        public volatile Exception FatalException = null;


        public event Action OnUpdate = () => { };
        public event Action OnDispose = () => { };

        /// <summary>
        /// Creates a new instance of this class with the provided parameters.
        /// For any errors that get encountered see <see cref="ErrorMessage"/> an <see cref="FatalException"/>
        /// </summary>
        /// <param name="settings">The <see cref="MultiplayerClientSettings"/> to use to create this instance.</param>
        internal MultiplayerServer(MultiplayerClientSettings settings)
        {
            this.MyPlayerName = settings.myPlayerName;
            this.serverless = settings.serverless;
            int listenPort = settings.listenPort;
            this.mainEndpoint = settings.mainEndpoint;
            this.overduetimeout = settings.overduetimeout;
            this.useAllowList = settings.useAllowList;
            this.AllowList = settings.AllowList;
            this.BlockList = settings.BlockList;
            if(mainEndpoint == null || mainEndpoint.Length == 0)
            {
                mainEndpoint = new[] { new IPEndPoint(IPAddress.Loopback, listenPort) };
            }

            listenerThread = new Thread(() =>
            {
                try
                {
                    bool initializing = true;
                    int retries = 0;
                    while (initializing)
                    {
                        try
                        {
                            udpListener = new UdpClient(listenPort, AddressFamily.InterNetwork);
                            initializing = false;
                        }
                        catch (Exception e)
                        {
                            if (settings.maxAdjustListenPortOnBindFail > retries++)
                            {
                                listenPort++;
                            }
                            else
                            {
                                //ErrorMessage = e.Message;
                                ErrorMessage = $"Failed to bind a port after {retries} tr{(retries == 1 ? "y" : "ies")}. Ports number {listenPort - retries + 1} to {listenPort} are already in use";
                                //listenerThread.Abort(e);//does this even work?//calling Abort is a bad idea
                                return;
                            }
                        }
                    }
                    while (!disposing)
                    {
                        //IPEndPoint object will allow us to read datagrams sent from any source.
                        IPEndPoint t = new IPEndPoint(IPAddress.Any, listenPort);
                        ProcessDatagram(udpListener.Receive(ref t), t);//Note: udpListener.Receive blocks until there is a datagram o read
                    }
                    udpListener.Close();
                }
                catch (Exception e) { FatalException = e; }
            });
            listenerThread.Start();

            timeoutthread = new Thread(() =>
            {
                try
                {
                    while (!disposing)
                    {
                        try
                        {
                            foreach (PlayerMetadata p in Players.Values)
                            {
                                if ((DateTime.UtcNow.Ticks - p.LastUpdateLocalTimestamp) > overduetimeout)
                                {
                                    //it'd be bad if we removed ourselves from our own list, so we check for that, even though it shouldn't happen
                                    if (p.Uuid != MyUuid)
                                    {
                                        _ = Players.TryRemove(p.Uuid, out _);
                                    }
                                }
                            }
                        }
                        catch (KeyNotFoundException)//this can happen if an item is removed by another thread while this thread is iterating over the items
                        {
                        }
                    }
                }
                catch (Exception e) { FatalException = e; }
            });
            timeoutthread.Start();
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
                    Thread.Sleep(100);//try to wait for child threads to stop on their own
                    if (listenerThread.IsAlive)
                    {
                        listenerThread.Abort();//assume the thread is stuck and forcibly terminate it
                    }
                    if (timeoutthread.IsAlive)
                    {
                        timeoutthread.Abort();//assume the thread is stuck and forcibly terminate it
                    }
                    udpListener.Close();//must be after listenerThread is stopped
                }

                // Dispose unmanaged resources here

                disposed = true;
            }
        }
        ~MultiplayerServer()
        {
            Dispose(false);
        }

        public void Update()
        {
            if (FatalException != null)
            {
                throw FatalException;//This should never happen
            }
            
            if(!Listening)
            {
                return;
            }
            
            OnUpdate();

            try {

                //SendPlayerDataToAll
                if (serverless)
                {
                    foreach (PlayerMetadata m in Players.Values)
                    {
                        if ((DateTime.UtcNow.Ticks - m.LastUpdateLocalTimestamp) + preoverduetimeoutoffset > overduetimeout && m.Uuid != MyUuid)
                        {
                            continue;
                        }
                        SendToAll(Serialize(m, false));
                    }
                }
                else
                {
                    foreach (PlayerMetadata m in Players.Values)
                    {
                        if ((DateTime.UtcNow.Ticks - m.LastUpdateLocalTimestamp) + preoverduetimeoutoffset > overduetimeout && m.Uuid != MyUuid)
                        {
                            continue;
                        }
                        //Note: probably should refactor these methods
                        SendToAll((targ) => Serialize(m, mainEndpoint.Contains(targ)));
                    }
                }
            }
            catch (KeyNotFoundException)//this can happen if an item is removed by another thread while this thread is iterating over the items
            {
            }
        }

        #region network packet stuff
        private const string ProtocolSignature = "FezMultiplayer";// Do not change
        public const string ProtocolVersion = "trece";//Update this ever time you change something that affect the packets

        private static void SendUdp(byte[] msg, IPEndPoint targ)
        {
            UdpClient Client = new UdpClient();
            Client.Ttl = 3;//TODO idk about this; should probably be settings.overduetimeout
            Client.Send(msg, msg.Length, targ);
            Client.Close();
        }

        private void SendToAll(Func<IPEndPoint, byte[]> msgGenerator)
        {
            IEnumerable<IPEndPoint> targets = serverless || mainEndpoint.Contains(Players[MyUuid].Endpoint) ? Targets : mainEndpoint;
            // Send the message to all recipients
            System.Threading.Tasks.Parallel.ForEach(targets,
                targ => {
                    if (targ.Address != IPAddress.None && targ.Port > 0)
                    {
                        SendUdp(msgGenerator.Invoke(targ), targ);
                    }
                });
        }

        protected void SendToAll(byte[] msg)
        {
            IEnumerable<IPEndPoint> targets = serverless || mainEndpoint.Contains(Players[MyUuid].Endpoint) ? Targets : mainEndpoint;
            // Send the message to all recipients
            System.Threading.Tasks.Parallel.ForEach(targets,
                targ => {
                    if (targ.Address != IPAddress.None && targ.Port > 0)
                    {
                        SendUdp(msg, targ);
                    }
                });
        }
        //TODO make these packet things more extensible somehow?
        private enum PacketType
        {
            //arbitrary values
            PlayerInfo = 1,
            Notice = 3,
        }

        protected enum NoticeType //Honestly, these could probably be merged into PacketType
        {
            //arbitrary values
            Disconnect = 7,
            Message = 9,//currently unused
        }

        protected static byte[] SerializeNotice(NoticeType type, string data)
        {
            using (MemoryStream m = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(m))
                {
                    /* 
                     * Note: these pragma things are here because I feel 
                     * it's not clear which Read method should be called
                     * when all the Write methods have the same name.
                     */
#pragma warning disable IDE0004
#pragma warning disable IDE0049
                    writer.Write((String)ProtocolSignature);
                    writer.Write((String)ProtocolVersion);
                    writer.Write((Byte)PacketType.Notice);
                    writer.Write((Int64)DateTime.UtcNow.Ticks);
                    writer.Write((Byte)type);
                    writer.Write((String)data);
#pragma warning restore IDE0004
#pragma warning restore IDE0049
                    writer.Flush();
                    return m.ToArray();
                }
            }
        }

        private byte[] Serialize(PlayerMetadata p, bool mainTarget)
        {
            using (MemoryStream m = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(m))
                {
                    /* 
                     * Note: these pragma things are here because I feel 
                     * it's not clear which Read method should be called
                     * when all the Write methods have the same name.
                     */
#pragma warning disable IDE0004
#pragma warning disable IDE0049
                    writer.Write((String)ProtocolSignature);
                    writer.Write((String)ProtocolVersion);
                    writer.Write((Byte)PacketType.PlayerInfo);
                    writer.Write((Int64)p.LastUpdateTimestamp);
                    if (serverless || mainTarget)
                    {
                        writer.Write((String)((IPAddress)p.Endpoint.Address).ToString());
                        writer.Write((Int32)p.Endpoint.Port);
                    }
                    else
                    {
                        writer.Write((String)IPAddress.None.ToString());
                        writer.Write((Int32)0);
                    }
                    writer.Write((bool)p.Uuid.Equals(MyUuid));
                    writer.Write(p.Uuid.ToByteArray());
                    writer.Write((String)p.Uuid.ToString());
                    writer.Write((String)p.PlayerName ?? "");
                    writer.Write((String)p.CurrentLevelName ?? "");
                    writer.Write((Single)p.Position.X);
                    writer.Write((Single)p.Position.Y);
                    writer.Write((Single)p.Position.Z);
                    writer.Write((Int32)p.CameraViewpoint);
                    writer.Write((Int32)p.Action);
                    writer.Write((Int32)p.AnimFrame);
                    writer.Write((Int32)p.LookingDirection);
#pragma warning restore IDE0004
#pragma warning restore IDE0049
                    writer.Flush();
                    return m.ToArray();
                }
            }
        }

        private void ProcessDatagram(byte[] data, IPEndPoint remoteHost)
        {
            if (BlockList.Contains(remoteHost.Address)
                || (useAllowList && !AllowList.Contains(remoteHost.Address)))
            {
                return;
            }
            using (MemoryStream m = new MemoryStream(data))
            {
                using (BinaryReader reader = new BinaryReader(m))
                {
                    if (!ProtocolSignature.Equals(reader.ReadString()))
                    {
                        //Not a FezMultiplayer packet
                        return;
                    }
                    if (!ProtocolVersion.Equals(reader.ReadString()))
                    {
                        //Not the right version of the FezMultiplayer protocol
                        //TODO notify the user?
                        return;
                    }

                    PacketType packetType = (PacketType)reader.ReadByte();
                    long timestamp = reader.ReadInt64();

                    switch (packetType)
                    {
                    case PacketType.PlayerInfo:
                        {
                            IPEndPoint endpoint;
                            try
                            {
                                string endip = reader.ReadString();
                                int endport = reader.ReadInt32();
                                // Note: the above reads from the binary reader are there to ensure they both get called so the reader doesn't skip a value
                                endpoint = new IPEndPoint(IPAddress.Parse(endip), endport);
                            }
                            catch (Exception)//catches exceptions for when the IP endpoint received is invalid
                                             // probably should be changed so it doesn't catch the exceptions thrown by the reader, but it's probably fine
                            {
                                endpoint = new IPEndPoint(IPAddress.None, 0);
                            }
                            bool infoFromOwner = reader.ReadBoolean();
                            if (infoFromOwner)
                            {
                                endpoint.Address = remoteHost.Address;
                            }
                            IPAddress ip = remoteHost.Address;
                            Guid uuid = new Guid(reader.ReadInt32(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
                            string playername = reader.ReadString();
                            string lvl = reader.ReadString();
                            Vector3 pos = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                            Viewpoint vp = (Viewpoint)reader.ReadInt32();
                            ActionType act = (ActionType)reader.ReadInt32();
                            int frame = reader.ReadInt32();
                            HorizontalDirection lookdir = (HorizontalDirection)reader.ReadInt32();

                            if (uuid == MyUuid)//ignore the other stuff if it's ourself
                            {
                                if (serverless)
                                {
                                    PlayerMetadata me = Players[MyUuid];
                                    if (!endpoint.Address.Equals(IPAddress.Loopback))
                                    {
                                        me.Endpoint = endpoint;
                                    }
                                    Players[uuid] = me;
                                    return;
                                }
                            }
                            else
                            {
                                if (Players.ContainsKey(uuid)
                                        && (DateTime.UtcNow.Ticks - Players[uuid].LastUpdateLocalTimestamp) + preoverduetimeoutoffset > overduetimeout
                                        && uuid != MyUuid)
                                {
                                    return;//ignore packets for players that should be disconnected; if they want to reconnect they can send another datagram
                                }

                                PlayerMetadata p = Players.GetOrAdd(uuid, (guid) =>
                                {
                                    var np = new PlayerMetadata(
                                        Endpoint: endpoint,
                                        Uuid: guid,
                                        PlayerName: playername,
                                        CurrentLevelName: lvl,
                                        Position: pos,
                                        CameraViewpoint: vp,
                                        Action: act,
                                        AnimFrame: frame,
                                        LookingDirection: lookdir,
                                        LastUpdateTimestamp: timestamp,
                                        LastUpdateLocalTimestamp: DateTime.UtcNow.Ticks
                                    );
                                    return np;
                                });
                                if (timestamp > p.LastUpdateTimestamp)//Ensure we're not saving old data
                                {
                                    //update player
                                    p.PlayerName = playername;
                                    p.CurrentLevelName = lvl;
                                    p.Position = pos;
                                    p.CameraViewpoint = vp;
                                    p.Action = act;
                                    p.AnimFrame = frame;
                                    p.LookingDirection = lookdir;
                                    p.LastUpdateTimestamp = timestamp;
                                    if (!endpoint.Address.Equals(IPAddress.Loopback))
                                    {
                                        p.Endpoint = endpoint;
                                    }
                                    p.LastUpdateLocalTimestamp = DateTime.UtcNow.Ticks;//for auto-dispose
                                }
                                Players[uuid] = p;
                            }
                            break;
                        }
                    case PacketType.Notice:
                        {
                            NoticeType noticeType = (NoticeType)reader.ReadByte();
                            String noticeData = reader.ReadString();
                            switch (noticeType)
                            {
                            case NoticeType.Disconnect:
                                {
                                    try
                                    {
                                        //TODO this might have inintended effects if two players have the same endpoint; might be a better idea to use GUID instead
                                        //it'd be bad if we removed ourselves from our own list, so we check for that with p.Key != MyUuid
                                        _ = Players.TryRemove(Players.First(p => p.Key != MyUuid && noticeData.Equals(p.Value.Endpoint.ToString())).Key, out _);
                                    }
                                    catch (InvalidOperationException) { }
                                    catch (KeyNotFoundException) { } //this can happen if an item is removed by another thread while this thread is iterating over the items

                                    break;
                                }
                            case NoticeType.Message:
                                {
                                    break;
                                }
                            default:
                                {
                                    //Unsupported packet type
                                    ErrorMessage = "Unsupported NoticeType: " + noticeType;
                                    return;
                                }
                            }
                            break;
                        }
                    default:
                        {
                            //Unsupported packet type
                            ErrorMessage = "Unsupported PacketType: " + packetType;
                            return;
                        }
                    }
                }
            }
        }
        #endregion
    }
}