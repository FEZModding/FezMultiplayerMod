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

        private static int port => 7777;//TODO add a way to change the port
        private readonly UdpClient udpClient = new UdpClient(port);
        public static List<IPAddress> Targets { get; } = new List<IPAddress>() { IPAddress.Loopback };//TODO add a way to change the targets
        public readonly Dictionary<Guid, PlayerMetadata> Players = new Dictionary<Guid, PlayerMetadata>();
        public readonly Guid MyUuid = Guid.NewGuid();

        private PlayerMetadata GetMyPlayer()
        {
            PlayerMetadata p;
            if(!Players.TryGetValue(MyUuid, out p))
            {
                Players.Add(MyUuid, p = new PlayerMetadata(MyUuid, null, Vector3.Zero, 0));
            }
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
        }

        private bool disposing = false;
        public void Dispose()
        {
            if (this.disposing)
                return;
            this.disposing = true;
        }

        public void Update(GameTime gameTime)
        {
            Players[MyUuid] = GetMyPlayer();
            SendToAll();
            ReceiveFromAll();
        }

        private void SendToAll()
        {
            byte[] msg = Serialize(Players[MyUuid]);//Note: could also send the info for the other players if we want

            // Send the message to all recipients
            System.Threading.Tasks.Parallel.ForEach(Targets,
                targ =>
                {
                    UdpClient Client = new UdpClient();
                    Client.Send(msg, msg.Length, new IPEndPoint(targ, port));
                    Client.Close();
                });
        }
        private void ReceiveFromAll()
        {
            //Note: should probably be async instead
            foreach (var targ in Targets)
            {
                IPEndPoint t = new IPEndPoint(IPAddress.Any, port);
                ProcessDatagram(udpClient.Receive(ref t));
            }
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

                    PlayerMetadata p;
                    if (!Players.TryGetValue(MyUuid, out p))
                    {
                        Players.Add(MyUuid, p = new PlayerMetadata(
                            uuid: uuid,
                            currentLevelName: lvl,
                            position: pos,
                            action: act
                        ));
                    }
                    else
                    {
                        //update player
                        p.currentLevelName = lvl;
                        p.position = pos;
                        p.action = act;
                    }
                    Players[uuid] = p;
                }
            }
        }
    }
}