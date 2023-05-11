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

namespace FezGame.MultiplayerMod
{
    public class MultiplayerClient : IDisposable
    {
        [ServiceDependency]
        public IPlayerManager PlayerManager { private get; set; }

        [ServiceDependency]
        public IGameLevelManager LevelManager { private get; set; }

        [Serializable]
        public struct PlayerMetadata
        {
            public IPEndPoint endpoint;
            public readonly Guid uuid;
            public string currentLevelName;
            public Vector3 position;
            public ActionType action;
            public long lastUpdateTimestamp;

            public PlayerMetadata(IPEndPoint endpoint, Guid uuid, string currentLevelName, Vector3 position, ActionType action, long lastUpdateTimestamp)
            {
                IPAddress ip = endpoint.Address;
                if (ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any) || ip.Equals(IPAddress.None) || ip.Equals(IPAddress.IPv6None))
                {
                    endpoint.Address = IPAddress.Loopback;
                }
                this.endpoint = endpoint;
                this.uuid = uuid;
                this.currentLevelName = currentLevelName;
                this.position = position;
                this.action = action;
                this.lastUpdateTimestamp = lastUpdateTimestamp;
            }
        }
        private const int mainPort = 7777;
        private int listenPort = mainPort;//TODO add a way to change the port
        private volatile UdpClient udpListener;
        private readonly Thread listenerThread;
        private readonly Thread timeoutthread;
        public IPEndPoint[] mainEndpoint = new[] { new IPEndPoint(IPAddress.Loopback, mainPort) };//TODO add a way to change the targets
        public readonly ConcurrentDictionary<Guid, PlayerMetadata> Players = new ConcurrentDictionary<Guid, PlayerMetadata>();
        //Note: it has to connect to another player before it propagates the player information
        private IEnumerable<IPEndPoint> Targets => Players.Select(p => p.Value.endpoint).Concat(mainEndpoint);
        public readonly Guid MyUuid = Guid.NewGuid();

        internal MultiplayerClient(Game game)
        {
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
                    catch (Exception)
                    {
                        listenPort++;
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
                    const long overduetimeout = 30_000_000;//3 seconds //TODO probably should make this customizable
                    foreach (PlayerMetadata p in Players.Values)
                    {
                        if (p.lastUpdateTimestamp < Players[MyUuid].lastUpdateTimestamp - overduetimeout)
                        {
                            _ = Players.TryRemove(p.uuid, out _);
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
            SendToAll(SerializeNotice(NoticeType.Disconnect, Players[MyUuid].endpoint?.ToString() ?? ""));
        }

        public void Update(GameTime gameTime)
        {
            //UpdateMyPlayer

            PlayerMetadata p = Players.GetOrAdd(MyUuid, (guid) =>
            {
                return new PlayerMetadata((IPEndPoint)udpListener.Client.LocalEndPoint, guid, null, Vector3.Zero, 0, DateTime.UtcNow.Ticks);
            });

            //update MyPlayer
            p.currentLevelName = LevelManager?.Name;
            if (PlayerManager != null)
            {
                p.position = PlayerManager.Position;
                p.action = PlayerManager.Action;
            }
            p.lastUpdateTimestamp = DateTime.UtcNow.Ticks;
            Players[MyUuid] = p;

            //SendPlayerDataToAll
            foreach (PlayerMetadata m in Players.Values)//Note: could also send the info for the other players if we want
            {
                SendToAll(Serialize(m));
            }
        }

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
                    writer.Write((Byte)PacketType.PlayerInfo);
                    writer.Write((Int64)p.lastUpdateTimestamp);
                    writer.Write((String)((IPAddress)p.endpoint.Address).ToString());
                    writer.Write((Int32)p.endpoint.Port);
                    writer.Write((bool)p.uuid.Equals(MyUuid));
                    writer.Write((String)p.uuid.ToString());
                    writer.Write((String)p.currentLevelName ?? "");
                    writer.Write((Single)p.position.X);
                    writer.Write((Single)p.position.Y);
                    writer.Write((Single)p.position.Z);
                    writer.Write((Int32)p.action);
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
                            ActionType act = (ActionType)reader.ReadInt32();

                            PlayerMetadata p = Players.GetOrAdd(uuid, (guid) =>
                            {
                                var np = new PlayerMetadata(
                                    endpoint: endpoint,
                                    uuid: guid,
                                    currentLevelName: lvl,
                                    position: pos,
                                    action: act,
                                    lastUpdateTimestamp: timestamp
                                );
                                return np;
                            });
                            if (timestamp > p.lastUpdateTimestamp)//Ensure we're not saving old data
                            {
                                //update player
                                p.currentLevelName = lvl;
                                p.position = pos;
                                p.action = act;
                                p.lastUpdateTimestamp = timestamp;
                                p.endpoint = endpoint;
                            }
                            Players[uuid] = p;
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
                                        _ = Players.TryRemove(Players.First(p => noticeData.Equals(p.Value.endpoint.ToString())).Key, out _);
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
                                    throw new NotSupportedException("Unsupported NoticeType: " + noticeType);
                                }
                            }
                            break;
                        }
                    default:
                        {
                            //Unsupported packet type
                            throw new NotSupportedException("Unsupported PacketType: " + packetType);
                        }
                    }
                }
            }
        }
    }
}