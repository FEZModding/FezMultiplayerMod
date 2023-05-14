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
                IPAddress ip = Endpoint.Address;
                if (ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any) || ip.Equals(IPAddress.None) || ip.Equals(IPAddress.IPv6None))
                {
                    Endpoint.Address = IPAddress.Loopback;
                }
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
        public readonly Guid MyUuid = Guid.NewGuid();

        //Note: it has to connect to another player before it propagates the player information
        private readonly bool serverless;//TODO implement the case where this is false; for true it relays the IP addresses of all the players to all the other players

        public volatile string ErrorMessage = null;//Note: this gets updated in the listenerThread

        /// <summary>
        /// Creates a new instance of this class with the provided parameters.
        /// For any errors that get encountered while performing <see cref="ErrorMessage"/>
        /// </summary>
        /// <param name="listenPort">The port to listen on</param>
        /// <param name="mainEndpoint">An array representing the main endpoint(s) to talk to.</param>
        /// <param name="adjustListenPortOnBindFail">Whether to automatically use the next available port as the port to listen to, or to just throw an error. In case of an error, see <see cref="ErrorMessage"/></param>
        /// <param name="serverless">Determines if the IP addresses of the players should be relayed to all the other players.</param>
        /// <param name="overduetimeout">The amount of time, in <see cref="System.DateTime.Ticks">ticks</see>, to wait before removing a player. For reference, there are 10000000 (ten million) ticks in one second.</param>
        internal MultiplayerClient(int listenPort = 7777, IPEndPoint[] mainEndpoint = null, bool adjustListenPortOnBindFail = true, bool serverless = true, long overduetimeout = 30_000_000)
        {
            this.serverless = serverless;
            if(mainEndpoint == null || mainEndpoint.Length == 0)
            {
                mainEndpoint = new[] { new IPEndPoint(IPAddress.Loopback, listenPort) };
            }
            this.mainEndpoint = mainEndpoint;
            _ = Waiters.Wait(() => ServiceHelper.FirstLoadDone, () => ServiceHelper.InjectServices(this));

            listenerThread = new Thread(() =>
            {
                bool init = true;
                while (init)
                {
                    try
                    {
                        udpListener = new UdpClient(listenPort);
                        init = false;
                    }
                    catch (Exception e)
                    {
                        if (adjustListenPortOnBindFail)
                        {
                            listenPort++;
                        }
                        else
                        {
                            //ErrorMessage = e.Message;
                            ErrorMessage = $"Port number {listenPort} is already in use";
                            throw e;
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
                        if (p.LastUpdateTimestamp < Players[MyUuid].LastUpdateTimestamp - overduetimeout)
                        {
                            _ = Players.TryRemove(p.Uuid, out _);
                        }
                    }
                }
            });
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
            //UpdateMyPlayer

            PlayerMetadata p = Players.GetOrAdd(MyUuid, (guid) =>
            {
                return new PlayerMetadata((IPEndPoint)udpListener.Client.LocalEndPoint, guid, null, Vector3.Zero, Viewpoint.None, ActionType.None, 0, HorizontalDirection.None, DateTime.UtcNow.Ticks);
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
            foreach (PlayerMetadata m in Players.Values)
            {
                SendToAll(Serialize(m));
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

        private void SendToAll(byte[] msg)
        {
            // Send the message to all recipients
            System.Threading.Tasks.Parallel.ForEach(Targets,
                targ => SendUdp(msg, targ));
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
            Message = 9,
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

        private byte[] Serialize(PlayerMetadata p)
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
                    writer.Write((String)((IPAddress)p.Endpoint.Address).ToString());
                    writer.Write((Int32)p.Endpoint.Port);
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
                            IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse(reader.ReadString()), reader.ReadInt32());
                            bool infoFromOwner = reader.ReadBoolean();
                            if (infoFromOwner)
                            {
                                endpoint.Address = remoteHost.Address;
                            }
                            IPAddress ip = remoteHost.Address;
                            if (ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any) || ip.Equals(IPAddress.None) || ip.Equals(IPAddress.IPv6None))
                            {
                                endpoint.Address = IPAddress.Loopback;
                            }
                            Guid uuid = Guid.Parse(reader.ReadString());
                            string lvl = reader.ReadString();
                            Vector3 pos = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                            Viewpoint vp = (Viewpoint)reader.ReadInt32();
                            ActionType act = (ActionType)reader.ReadInt32();
                            int frame = reader.ReadInt32();
                            HorizontalDirection lookdir = (HorizontalDirection)reader.ReadInt32();

                            if (uuid == MyUuid)//ignore the other stuff if it's ourself
                            {
                                PlayerMetadata me = Players[MyUuid];
                                me.Endpoint = endpoint;
                                Players[uuid] = me;
                                return;
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
                                    throw new NotSupportedException(ErrorMessage = "Unsupported NoticeType: " + noticeType);
                                }
                            }
                            break;
                        }
                    default:
                        {
                            //Unsupported packet type
                            throw new NotSupportedException(ErrorMessage = "Unsupported PacketType: " + packetType);
                        }
                    }
                }
            }
        }
        #endregion
    }
}