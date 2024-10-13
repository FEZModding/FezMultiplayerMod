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
using Microsoft.Xna.Framework;
using FezEngine;
using FezGame.Structure;

namespace FezGame.MultiplayerMod
{
    /// <summary>
    /// The class that contains all the networking stuff
    /// 
    /// Note: This class should only contain System usings
    /// </summary>
    public class MultiplayerServer : IDisposable
    {

        private volatile TcpClient tcpClient;
        private volatile NetworkStream tcpStream;
        private readonly Thread listenerThread;

        /// <summary>
        /// How long to wait, in ticks, before stopping sending or receiving packets for this player.
        /// This is required otherwise race conditions can occur.
        /// <br /><br />Note: could make this customizable
        /// </summary>
        private readonly long preoverduetimeoutoffset = TimeSpan.TicksPerSecond * 5;

        public readonly ConcurrentDictionary<Guid, PlayerMetadata> Players = new ConcurrentDictionary<Guid, PlayerMetadata>();
        public bool Listening => tcpClient?.Client?.IsBound ?? false;
        public EndPoint LocalEndPoint => tcpClient?.Client?.LocalEndPoint;
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

            listenerThread = new Thread(() =>
            {
                try
                {
                    bool initializing = true;
                    int retries = 0;
                    while (initializing)
                    {
                        tcpClient = new TcpClient(AddressFamily.InterNetwork);
                        initializing = false;
                    }
                    tcpClient.Connect(settings.mainEndpoint);
                    tcpStream = tcpClient.GetStream();
                    while (!disposing)
                    {
                        //TODO read from tcpStream
                        ProcessDatagram(tcpClient.Receive(ref t), t);//Note: udpListener.Receive blocks until there is a datagram o read

                    }
                    tcpClient.Close();
                }
                catch (Exception e) { FatalException = e; }
            });
            listenerThread.Start();
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
                    if (listenerThread.IsAlive)
                    {
                        listenerThread.Abort();//assume the thread is stuck and forcibly terminate it
                    }
                    //tcpClient.EndConnect();
                    tcpClient.Close();//must be after listenerThread is stopped
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

            if (!Listening)
            {
                return;
            }

            OnUpdate();

            try
            {

                foreach (PlayerMetadata m in Players.Values)
                {
                    //TODO: update to match the UML diagram
                    SendToAll((targ) => Serialize(m, mainEndpoint.Contains(targ)));
                }
            }
            catch (KeyNotFoundException)//this can happen if an item is removed by another thread while this thread is iterating over the items
            {
            }
        }

        #region network packet stuff
        private const string ProtocolSignature = "FezMultiplayer";// Do not change
        public const string ProtocolVersion = "quince";//Update this ever time you change something that affect the packets

        private void SendTcp(byte[] msg)
        {
            (tcpStream ?? (tcpStream = tcpClient.GetStream())).Write(msg);//TODO
            return;
        }

        //TODO make these packet things more extensible somehow?

        protected static byte[] SerializeDisconnect(Guid uuid)
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
                    writer.Write((Byte)PacketType.Disconnect);
                    writer.Write((Int64)DateTime.UtcNow.Ticks);
                    writer.Write((Guid)uuid);
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
                    writer.Write((Guid)p.Uuid);
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

        //
        // For player names and whatnot
        // should be an inclusive list of all characters supported by all of the languages' game fonts
        // probably should restrict certain characters for internal use (like punctuation and symbols)
        // also need to figure out how to handle people using characters that are not in this list
        //
        // Complete list of common chars: (?<=")[A-Za-z0-9 !"#$%&'()*+,\-./:;<>=?@\[\]\\^_`{}|~]+(?=")
        //
        // Common punctuation characters: [ !"#$%&'()*+,\-./:;<>=?@\[\]^_`{}|~]
        // Potential reserved characters: [ #$&\\`]
        //
        // TODO add a universal font?
        // Note the hanzi/kanji/hanja (Chinese characters) look slightly different in Chinese vs Japanese vs Korean;
        //     Might be worth making a system that can write the player name using multiple fonts

        //unused
        //public static readonly System.Text.RegularExpressions.Regex commonCharRegex = new System.Text.RegularExpressions.Regex(@"[^\x20-\x7E]");

        //more strict so we can potentially add features (such as colors and effects) using special characters later
        public static readonly System.Text.RegularExpressions.Regex nameInvalidCharRegex = new System.Text.RegularExpressions.Regex(@"[^0-9A-Za-z]");

        private const int maxplayernamelength = 32;

        private void ProcessDatagram(byte[] data, IPEndPoint remoteHost)
        {
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
                            Guid uuid = reader.ReadGuid();
                            string playername = reader.ReadString();
                            playername = nameInvalidCharRegex.Replace(playername.Length > maxplayernamelength ? playername.Substring(0, maxplayernamelength) : playername, "");
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
                    case PacketType.Disconnect:
                        {
                            try
                            {
                                Guid puid = reader.ReadGuid();
                                if (puid != MyUuid && Players.TryGetValue(puid, out var p) && remoteHost.Address.Equals(p.Endpoint.Address))
                                {
                                    _ = Players.TryRemove(puid, out _);
                                }
                            }
                            catch (InvalidOperationException) { }
                            catch (KeyNotFoundException) { } //this can happen if an item is removed by another thread while this thread is iterating over the items

                            break;
                        }
                    case PacketType.Message:
                        {
                            //TBD
                            break;
                        }
                    case PacketType.Notice:
                        {
                            //TBD
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