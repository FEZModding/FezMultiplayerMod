using FezEngine;
using FezEngine.Components;
using FezEngine.Services;
using FezEngine.Tools;
using FezGame.Services;
using FezGame.Structure;
using FezSharedTools;
using Microsoft.Xna.Framework;
using System;

namespace FezGame.MultiplayerMod
{
    /// <summary>
    /// The GameComponent-like class that handles updating data from the Game and updating data in the Game
    /// </summary>
    public sealed class MultiplayerClient : MultiplayerClientNetcode, IDisposable
    {
        [ServiceDependency]
        public IPlayerManager PlayerManager { private get; set; }

        [ServiceDependency]
        public IGameLevelManager LevelManager { private get; set; }

        [ServiceDependency]
        public IGameCameraManager CameraManager { private get; set; }

        [ServiceDependency]
        public IGameStateManager GameState { private get; set; }

        [ServiceDependency]
        public ITimeManager TimeManager { private get; set; }

        /// <summary>
        /// Creates a new instance of this class with the provided parameters.
        /// For any errors that get encountered see <see cref="ErrorMessage"/> and <see cref="FatalException"/>
        /// Use <c>ConnectToServerAsync(IPEndPoint)</c> to connect to a server.
        /// </summary>
        /// <param name="settings">The <see cref="MultiplayerClientSettings"/> to use to create this instance.</param>
        internal MultiplayerClient(MultiplayerClientSettings settings) : base(settings)
        {
            _ = Waiters.Wait(() => ServiceHelper.FirstLoadDone, () => ServiceHelper.InjectServices(this));

            OnUpdate += UpdateMyPlayer;
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

        protected override ActiveLevelState GetCurrentLevelState()
        {
            //TODO not yet implemented
            if (SharedConstants.TODO_Debug_EnableLevelStateSync)
            {
                System.Diagnostics.Debugger.Launch();
                System.Diagnostics.Debugger.Break();
            }
            return new ActiveLevelState();
        }

        protected override void ProcessActiveLevelState(ActiveLevelState activeLevelState)
        {
            if (SyncWorldState)
            {
                //TODO not yet implemented
                if (SharedConstants.TODO_Debug_EnableLevelStateSync)
                {
                    System.Diagnostics.Debugger.Launch();
                    System.Diagnostics.Debugger.Break();
                }
            }
        }

        protected override void SetTimeOfDay(long newTimeOfDayTicks)
        {
            if (SyncTimeOfDay)
            {
                TimeManager.CurrentTime = DateTime.Today.Add(new TimeSpan(newTimeOfDayTicks));
            }
        }

        protected override void ProcessServerSharedSaveData(SaveData saveData)
        {
            if (SyncWorldState)
            {
                if (GameState?.SaveData != null)
                {
                    //TODO not yet implemented
                    lock (SaveDataObserver.saveDataLock)
                    {
                        saveData.CloneInto(GameState.SaveData);
                    }
                }
            }
        }
    }
}