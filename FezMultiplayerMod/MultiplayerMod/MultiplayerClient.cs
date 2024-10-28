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
using FezSharedTools;

namespace FezGame.MultiplayerMod
{
    /// <summary>
    /// The class that contains all the networking stuff
    /// </summary>
    public class MultiplayerClient : MultiplayerClientNetcode, IDisposable
    {
        [ServiceDependency]
        public IPlayerManager PlayerManager { private get; set; }

        [ServiceDependency]
        public IGameLevelManager LevelManager { private get; set; }

        [ServiceDependency]
        public IGameCameraManager CameraManager { private get; set; }

        private readonly bool syncWorldState;

        /// <summary>
        /// Creates a new instance of this class with the provided parameters.
        /// For any errors that get encountered see <see cref="ErrorMessage"/> an <see cref="FatalException"/>
        /// </summary>
        /// <param name="settings">The <see cref="MultiplayerClientSettings"/> to use to create this instance.</param>
        internal MultiplayerClient(MultiplayerClientSettings settings) : base(settings)
        {
            _ = Waiters.Wait(() => ServiceHelper.FirstLoadDone, () => ServiceHelper.InjectServices(this));

            OnUpdate += UpdateMyPlayer;

            syncWorldState = settings.syncWorldState;
        }

        public void UpdateMyPlayer()
        {
            //UpdateMyPlayer

            PlayerMetadata p = MyPlayerMetadata ?? new PlayerMetadata(MyUuid, null, Vector3.Zero, Viewpoint.None, ActionType.None, 0, HorizontalDirection.None, DateTime.UtcNow.Ticks);

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
            MyPlayerMetadata = p;
        }

        protected override SaveDataUpdate? GetSaveDataUpdate()
        {
            if (!syncWorldState)
            {
                return null;
            }
            //TODO
            throw new NotImplementedException();
        }

        protected override ActiveLevelState? GetCurrentLevelState()
        {
            if (!syncWorldState)
            {
                return null;
            }
            //TODO
            throw new NotImplementedException();
        }

        protected override void ProcessSaveDataUpdate(SaveDataUpdate saveDataUpdate)
        {
            if (!syncWorldState)
            {
                return;
            }
            //TODO
            throw new NotImplementedException();
        }

        protected override void ProcessActiveLevelState(ActiveLevelState activeLevelState)
        {
            if (!syncWorldState)
            {
                return;
            }
            //TODO
            throw new NotImplementedException();
        }
    }
}