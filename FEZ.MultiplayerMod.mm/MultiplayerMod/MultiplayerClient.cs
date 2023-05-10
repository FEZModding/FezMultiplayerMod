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
            public readonly Guid uuid;
            public string currentLevelName;
            public Vector3 position;
            public ActionType action;

            public PlayerMetadata(Guid uuid, string currentLevelName, Vector3 position, ActionType action)
            {
                this.uuid = uuid;
                this.currentLevelName = currentLevelName;
                this.position = position;
                this.action = action;
            }
        }

        private static int listenPort => 7777;//TODO add a way to change the port
        private Thread listenerThread;
        public static List<IPEndPoint> Targets { get; } = new List<IPEndPoint>() { new IPEndPoint(IPAddress.Loopback, 7777) };//TODO add a way to change the targets
        public readonly ConcurrentDictionary<Guid, PlayerMetadata> Players = new ConcurrentDictionary<Guid, PlayerMetadata>();
        public readonly Guid MyUuid = Guid.NewGuid();

        private PlayerMetadata GetMyPlayer()
        {

            PlayerMetadata p = Players.GetOrAdd(MyUuid, (guid) => new PlayerMetadata(guid, null, Vector3.Zero, 0));

            //update MyPlayer
            p.currentLevelName = LevelManager?.Name;
            if (PlayerManager != null)
            {
                p.position = PlayerManager.Position;
                p.action = PlayerManager.Action;
            }
            Players[MyUuid] = p;
            return p;
        }

        internal MultiplayerClient(Game game)
        {
            _ = Waiters.Wait(() => ServiceHelper.FirstLoadDone, () => ServiceHelper.InjectServices(this));


            listenerThread = new Thread(() =>
            {
                using (UdpClient udpListener = new UdpClient(listenPort))
                {
                    while (!disposing)
                    {
                        //IPEndPoint object will allow us to read datagrams sent from any source.
                        IPEndPoint t = new IPEndPoint(IPAddress.Any, listenPort);
                        ProcessDatagram(udpListener.Receive(ref t));
                    }
                }
            });
            listenerThread.Start();
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
        }

        public void Update(GameTime gameTime)
        {
            Players[MyUuid] = GetMyPlayer();
            SendToAll();
        }

        private static void SendUdp(byte[] msg, IPEndPoint targ)
        {
            UdpClient Client = new UdpClient();
            Client.Send(msg, msg.Length, targ);
            Client.Close();
        }

        private void SendToAll()
        {
            byte[] msg = Serialize(Players[MyUuid]);//Note: could also send the info for the other players if we want

            // Send the message to all recipients
            System.Threading.Tasks.Parallel.ForEach(Targets,
                targ => SendUdp(msg, targ));
        }

        private static byte[] Serialize(PlayerMetadata p)
        {
            using (MemoryStream m = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(m))
                {
                    writer.Write(p.uuid.ToString());
                    writer.Write(p.currentLevelName ?? "");
#pragma warning disable IDE0004
#pragma warning disable IDE0049
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

        private void ProcessDatagram(byte[] data)
        {
            using (MemoryStream m = new MemoryStream(data))
            {
                using (BinaryReader reader = new BinaryReader(m))
                {
                    Guid uuid = Guid.Parse(reader.ReadString());
                    string lvl = reader.ReadString();
                    Vector3 pos = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    ActionType act = (ActionType)reader.ReadInt32();

                    PlayerMetadata p = Players.GetOrAdd(uuid, (guid) => new PlayerMetadata(
                            uuid: guid,
                            currentLevelName: lvl,
                            position: pos,
                            action: act
                        ));
                    //update player
                    p.currentLevelName = lvl;
                    p.position = pos;
                    p.action = act;

                    Players[uuid] = p;//Note: dunno if we need this
                }
            }
        }
    }
}