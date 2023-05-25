using FezEngine.Components;
using FezEngine.Services;
using FezEngine.Structure;
using FezEngine.Tools;
using FezGame.Components;
using FezGame.Services;
using FezGame.Structure;
using Microsoft.Xna.Framework;
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
using FezEngine;

namespace FezGame.MultiplayerMod
{
    /// <summary>
    /// The class that contains all the networking stuff
    /// </summary>
    public class MultiplayerClient : IDisposable
    {
        public class MultiplayerClientSettings
        {
            /// <param name="listenPort">The port to listen on</param>
            /// <param name="mainEndpoint"></param>
            /// <param name="maxAdjustListenPortOnBindFail"></param>
            /// <param name="serverless"></param>
            /// <param name="overduetimeout">The</param>
            
            public int listenPort = 7777;
            /// <summary>
            /// An array representing the main endpoint(s) to talk to.
            /// </summary>
            public IPEndPoint[] mainEndpoint = null;
            /// <summary>
            /// The amount of times to attempt to use the next port as the port to listen to, or to just throw an error. In case of an error, see <see cref="ErrorMessage"/>
            /// </summary>
            public int maxAdjustListenPortOnBindFail = 1000;
            /// <summary>
            /// Determines if the IP addresses of the players should be relayed to all the other players.
            /// </summary>
            public bool serverless = true;
            /// <summary>
            ///  amount of time, in <see cref="System.DateTime.Ticks">ticks</see>, to wait before removing a player. For reference, there are 10000000 (ten million) ticks in one second.
            /// </summary>
            public long overduetimeout = 30_000_000;

        }

        [ServiceDependency]
        public IPlayerManager PlayerManager { private get; set; }

        [ServiceDependency]
        public IGameLevelManager LevelManager { private get; set; }

        [ServiceDependency]
        public IGameCameraManager CameraManager { private get; set; }

        [Serializable]
        public struct PlayerMetadata
        {
            public IPEndPoint Endpoint;
            public readonly Guid Uuid;
            public string CurrentLevelName;
            public Vector3 Position;
            public ActionType Action;
            public int AnimFrame;
            public long LastUpdateTimestamp;
            public HorizontalDirection LookingDirection;
            public Viewpoint CameraViewpoint;

            public PlayerMetadata(IPEndPoint Endpoint, Guid Uuid, string CurrentLevelName, Vector3 Position, Viewpoint CameraViewpoint, ActionType Action, int AnimFrame, HorizontalDirection LookingDirection, long LastUpdateTimestamp)
            {
                this.Endpoint = Endpoint;
                this.Uuid = Uuid;
                this.CurrentLevelName = CurrentLevelName;
                this.Position = Position;
                this.Action = Action;
                this.AnimFrame = AnimFrame;
                this.LookingDirection = LookingDirection;
                this.LastUpdateTimestamp = LastUpdateTimestamp;
                this.CameraViewpoint = CameraViewpoint;
            }
        }

        private volatile UdpClient udpListener;
        private readonly Thread listenerThread;
        private readonly Thread timeoutthread;
        private readonly IPEndPoint[] mainEndpoint;
        public readonly ConcurrentDictionary<Guid, PlayerMetadata> Players = new ConcurrentDictionary<Guid, PlayerMetadata>();
        private IEnumerable<IPEndPoint> Targets => Players.Select(p => p.Value.Endpoint).Concat(mainEndpoint);
        public bool Listening => udpListener?.Client?.IsBound ?? false;
        public readonly Guid MyUuid = Guid.NewGuid();

        //Note: it has to connect to another player before it propagates the player information
        /// <summary>
        /// for true it relays the IP endpoints of all the players to all the other players, otherwise IP addressed will only be sent to the <see cref="MultiplayerClientSettings.mainEndpoint"/>.
        /// </summary>
        private readonly bool serverless;

        public volatile string ErrorMessage = null;//Note: this gets updated in the listenerThread

        /// <summary>
        /// Creates a new instance of this class with the provided parameters.
        /// For any errors that get encountered while performing <see cref="ErrorMessage"/>
        /// </summary>
        /// <param name="listenPort">The port to listen on</param>
        /// <param name="mainEndpoint">An array representing the main endpoint(s) to talk to.</param>
        /// <param name="maxAdjustListenPortOnBindFail">The amount of times to attempt to use the next port as the port to listen to, or to just throw an error. In case of an error, see <see cref="ErrorMessage"/></param>
        /// <param name="serverless">Determines if the IP addresses of the players should be relayed to all the other players.</param>
        /// <param name="overduetimeout">The amount of time, in <see cref="System.DateTime.Ticks">ticks</see>, to wait before removing a player. For reference, there are 10000000 (ten million) ticks in one second.</param>
        internal MultiplayerClient(MultiplayerClientSettings settings)
        {
            this.serverless = settings.serverless;
            int listenPort = settings.listenPort;
            this.mainEndpoint = settings.mainEndpoint;
            if(mainEndpoint == null || mainEndpoint.Length == 0)
            {
                mainEndpoint = new[] { new IPEndPoint(IPAddress.Loopback, listenPort) };
            }
            _ = Waiters.Wait(() => ServiceHelper.FirstLoadDone, () => ServiceHelper.InjectServices(this));

            listenerThread = new Thread(() =>
            {
                bool initializing = true;
                int retries = 0;
                while (initializing)
                {
                    try
                    {
                        udpListener = new UdpClient(listenPort);
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
                            ErrorMessage = $"Failed to bind a port after {retries} tr{(retries == 1 ? "y" : "ies")}. Ports number {listenPort - settings.maxAdjustListenPortOnBindFail} to {listenPort} are already in use";
                            listenerThread.Abort(e);
                        }
                    }
                }
                while (!disposing)
                {
                    //IPEndPoint object will allow us to read datagrams sent from any source.
                    IPEndPoint t = new IPEndPoint(IPAddress.Any, listenPort);
                    ProcessDatagram(udpListener.Receive(ref t), t);
                }
                udpListener.Close();
            });
            listenerThread.Start();

            timeoutthread = new Thread(() =>
            {
                while (!disposing)
                {
                    foreach (PlayerMetadata p in Players.Values)
                    {
                        if (p.LastUpdateTimestamp < DateTime.UtcNow.Ticks - settings.overduetimeout)
                        {
                            _ = Players.TryRemove(p.Uuid, out _);
                        }
                    }
                }
            });
            timeoutthread.Start();
        }

        private bool disposing = false;
        public void Dispose()
        {
            if (this.disposing)
                return;
            this.disposing = true;

            Thread.Sleep(100);
            if (listenerThread.IsAlive)
            {
                listenerThread.Abort();
            }
            if (timeoutthread.IsAlive)
            {
                timeoutthread.Abort();
            }
            udpListener.Close();
            SendToAll(SerializeNotice(NoticeType.Disconnect, Players[MyUuid].Endpoint?.ToString() ?? ""));
        }

        public void Update(GameTime gameTime)
        {
            if(!Listening)
            {
                return;
            }
            
            //UpdateMyPlayer

            PlayerMetadata p = Players.GetOrAdd(MyUuid, (guid) =>
            {
                IPEndPoint Endpoint = (IPEndPoint)udpListener?.Client?.LocalEndPoint ?? new IPEndPoint(IPAddress.Loopback, mainEndpoint[0].Port);
                IPAddress ip = Endpoint.Address;
                if (ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any) || ip.Equals(IPAddress.None) || ip.Equals(IPAddress.IPv6None))
                {
                    Endpoint.Address = IPAddress.Loopback;
                }

                return new PlayerMetadata(Endpoint, guid, null, Vector3.Zero, Viewpoint.None, ActionType.None, 0, HorizontalDirection.None, DateTime.UtcNow.Ticks);
            });

            //update MyPlayer
            p.CurrentLevelName = LevelManager?.Name;
            if (PlayerManager != null)
            {
                p.Position = PlayerManager.Position;
                p.Action = PlayerManager.Action;
                p.LookingDirection = PlayerManager.LookingDirection;
                p.AnimFrame = PlayerManager.Animation?.Timing?.Frame ?? 0;
            }
            if (CameraManager != null)
            {
                p.CameraViewpoint = CameraManager.Viewpoint;
            }
            p.LastUpdateTimestamp = DateTime.UtcNow.Ticks;
            Players[MyUuid] = p;

            //SendPlayerDataToAll
            if (serverless)
            {
                foreach (PlayerMetadata m in Players.Values)
                {
                    SendToAll(Serialize(m, false));
                }
            }
            else
            {
                foreach (PlayerMetadata m in Players.Values)
                {
                    //Note: probably should refactor these methods
                    SendToAll((targ)=>Serialize(m, mainEndpoint.Contains(targ)));
                }
            }
        }

        #region network packet stuff
        private const string ProtocolSignature = "FezMultiplayer";// Do not change
        private const string ProtocolVersion = "十";//Update this ever time you change something that affect the packets

        private static void SendUdp(byte[] msg, IPEndPoint targ)
        {
            UdpClient Client = new UdpClient();
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

        private void SendToAll(byte[] msg)
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

        private enum PacketType
        {
            //arbitrary values
            PlayerInfo = 1,
            Notice = 3,
        }

        private enum NoticeType
        {
            //arbitrary values
            Disconnect = 7,
            Message = 9,//currently unused
        }

        private static byte[] SerializeNotice(NoticeType type, string data)
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
                    writer.Write((String)p.Uuid.ToString());
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
                                endpoint = new IPEndPoint(IPAddress.Parse(reader.ReadString()), reader.ReadInt32());
                            }
                            catch(Exception)
                            {
                                endpoint = new IPEndPoint(IPAddress.None, 0);
                            }
                            bool infoFromOwner = reader.ReadBoolean();
                            if (infoFromOwner)
                            {
                                endpoint.Address = remoteHost.Address;
                            }
                            IPAddress ip = remoteHost.Address;
                            Guid uuid = Guid.Parse(reader.ReadString());
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
                                    me.Endpoint = endpoint;
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
                                        CurrentLevelName: lvl,
                                        Position: pos,
                                        CameraViewpoint: vp,
                                        Action: act,
                                        AnimFrame: frame,
                                        LookingDirection: lookdir,
                                        LastUpdateTimestamp: timestamp
                                    );
                                    return np;
                                });
                                if (timestamp > p.LastUpdateTimestamp)//Ensure we're not saving old data
                                {
                                    //update player
                                    p.CurrentLevelName = lvl;
                                    p.Position = pos;
                                    p.CameraViewpoint = vp;
                                    p.Action = act;
                                    p.AnimFrame = frame;
                                    p.LookingDirection = lookdir;
                                    p.LastUpdateTimestamp = timestamp;
                                    p.Endpoint = endpoint;
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
                                        _ = Players.TryRemove(Players.First(p => noticeData.Equals(p.Value.Endpoint.ToString())).Key, out _);
                                    }
                                    catch (InvalidOperationException) { }

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