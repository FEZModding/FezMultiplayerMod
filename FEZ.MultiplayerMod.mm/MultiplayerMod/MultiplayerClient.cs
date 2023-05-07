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
    internal class MultiplayerClient : GameComponent
    {
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

        #region ServiceDependencies
        [ServiceDependency]
        public IPlayerManager PlayerManager { private get; set; }

        [ServiceDependency]
        public IGameLevelManager LevelManager { private get; set; }
        #endregion

        private static int port => 7777;//TODO add a way to change the port
        private readonly UdpClient udpClient = new UdpClient();
        public static List<IPAddress> Targets { get; } = new List<IPAddress>() { IPAddress.Loopback };//TODO add a way to change the targets
        public readonly Dictionary<Guid, PlayerMetadata> Players = new Dictionary<Guid, PlayerMetadata>();
        public readonly Guid MyUuid = Guid.NewGuid();
        private PlayerMetadata MyPlayer
        {
            get
            {
                PlayerMetadata p;
                try
                {
                    p = Players[MyUuid];
                }
                catch (KeyNotFoundException)
                {
                    p = (Players[MyUuid] = new PlayerMetadata(MyUuid, null, Vector3.Zero, 0));
                }
                //update MyPlayer
                p.currentLevelName = LevelManager.Name;
                p.position = PlayerManager.Position;
                p.action = PlayerManager.Action;
                return p;
            }
        }

        internal MultiplayerClient(Game game)
            : base(game)
        {
        }

        private bool disposing = false;
        protected override void Dispose(bool disposing)
        {
            if (this.disposing)
                return;
            this.disposing = true;

            base.Dispose();
        }

        public override void Initialize()
        {
        }
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            Players[MyUuid] = MyPlayer;
            SendToAll();
            ReceiveFromAll();
        }

        private void SendToAll()
        {
            byte[] msg = Serialize(MyPlayer);//Note: could also send the info for the other players if we want

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
                PlayerMetadata p = Deserialize(udpClient.Receive(ref t));
                Players[p.uuid] = p;
            }
        }

        private static byte[] Serialize(PlayerMetadata p)
        {
            using (MemoryStream m = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(m))
                {
                    writer.Write(p.uuid.ToString());
                    writer.Write(p.currentLevelName);
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

        private static PlayerMetadata Deserialize(byte[] data)
        {
            using (MemoryStream m = new MemoryStream(data))
            {
                using (BinaryReader reader = new BinaryReader(m))
                {
                    return new PlayerMetadata(
                        uuid: Guid.Parse(reader.ReadString()),
                        currentLevelName: reader.ReadString(),
                        position: new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                        (ActionType)reader.ReadInt32()
                        );
                }
            }
        }
    }
}