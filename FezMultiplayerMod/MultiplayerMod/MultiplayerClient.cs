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
    public class MultiplayerClient : MultiplayerServer, IDisposable
    {
        [ServiceDependency]
        public IPlayerManager PlayerManager { private get; set; }

        [ServiceDependency]
        public IGameLevelManager LevelManager { private get; set; }

        [ServiceDependency]
        public IGameCameraManager CameraManager { private get; set; }

        /// <summary>
        /// Creates a new instance of this class with the provided parameters.
        /// For any errors that get encountered see <see cref="ErrorMessage"/> an <see cref="FatalException"/>
        /// </summary>
        /// <param name="settings">The <see cref="MultiplayerClientSettings"/> to use to create this instance.</param>
        internal MultiplayerClient(MultiplayerClientSettings settings) : base(settings)
        {
            _ = Waiters.Wait(() => ServiceHelper.FirstLoadDone, () => ServiceHelper.InjectServices(this));

            OnUpdate += UpdateMyPlayer;
            OnDispose += Disconnect;
        }

        public void Disconnect()
        {
            //check to make sure Players[MyUuid] exists, as accessing it directly could throw KeyNotFoundException
            if (Players.TryGetValue(MyUuid, out PlayerMetadata myplayer))
            {
                SendToAll(SerializeDisconnect(myplayer.Uuid));
            }
        }

        public void UpdateMyPlayer()
        {
            //UpdateMyPlayer

            PlayerMetadata p = Players.GetOrAdd(MyUuid, (guid) =>
            {
                return new PlayerMetadata(guid, null, Vector3.Zero, Viewpoint.None, ActionType.None, 0, HorizontalDirection.None, DateTime.UtcNow.Ticks);
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
        }
    }
}