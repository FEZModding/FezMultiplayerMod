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
using FezSharedTools;

namespace FezGame.MultiplayerMod
{
    /// <summary>
    /// The class that contains all the networking stuff
    /// 
    /// Note: This class should only contain System usings
    /// </summary>
    public abstract class MultiplayerClientNetcode : SharedNetcode<PlayerMetadata>, IDisposable
    {

        private readonly Thread listenerThread;

        public Guid MyUuid { get; private set; }
        public volatile PlayerMetadata MyPlayerMetadata = null;
        public override ConcurrentDictionary<Guid, PlayerMetadata> Players { get; } = new ConcurrentDictionary<Guid, PlayerMetadata>();
        public volatile bool Listening;
        public PlayerAppearance MyAppearance;
        public string MyPlayerName
        {
            get => MyAppearance.PlayerName;
            set => MyAppearance.PlayerName = value;
        }

        public event Action OnUpdate = () => { };
        public event Action OnDispose = () => { };

        /// <summary>
        /// Creates a new instance of this class with the provided parameters.
        /// For any errors that get encountered see <see cref="ErrorMessage"/> an <see cref="FatalException"/>
        /// </summary>
        /// <param name="settings">The <see cref="MultiplayerClientSettings"/> to use to create this instance.</param>
        internal MultiplayerClientNetcode(MultiplayerClientSettings settings)
        {
            Listening = false;
            MyAppearance = new PlayerAppearance(settings.MyPlayerName, settings.Appearance);

            listenerThread = new Thread(() =>
            {
                bool WasSucessfullyConnected = false;
                try
                {
                    TcpClient tcpClient = new TcpClient(AddressFamily.InterNetwork);
                    tcpClient.Connect(settings.MainEndpoint);
                    Listening = true;
                    while (MyPlayerMetadata == null)
                    {
                        Thread.Sleep(100);
                    }
                    using (NetworkStream tcpStream = tcpClient.GetStream())
                    using (BinaryReader reader = new BinaryReader(tcpStream))
                    using (BinaryWriter writer = new BinaryWriter(tcpStream))
                    {
                        ReadServerGameTickPacket(reader);
                        WriteClientGameTickPacket(writer, MyPlayerMetadata, null, null, MyAppearance, UnknownPlayerAppearanceGuids.Keys, false);
                        WasSucessfullyConnected = true;
                        while (true)
                        {
                            ReadServerGameTickPacket(reader);
                            if (!disposing)
                            {
                                ActiveLevelState? activeLevelState = null;
                                if (settings.SyncWorldState)
                                {
                                    activeLevelState = GetCurrentLevelState();
                                }

                                SaveDataUpdate? saveDataUpdate = null;
                                if (settings.SyncWorldState)
                                {
                                    saveDataUpdate = GetSaveDataUpdate();
                                }
                                //TODO transmit MyAppearance whenever its value changes 
                                WriteClientGameTickPacket(writer, MyPlayerMetadata, GetSaveDataUpdate(), activeLevelState, null, UnknownPlayerAppearanceGuids.Keys, false);
                            }
                            else
                            {
                                WriteClientGameTickPacket(writer, MyPlayerMetadata, null, null, null, new List<Guid>(0), true);
                                break;
                            }
                        }
                        reader.Close();
                        writer.Close();
                        tcpStream.Close();
                        tcpClient.Close();
                    }
                }
                catch (VersionMismatchException e)
                {
                    FatalException = e;
                }
                catch (InvalidDataException e)
                {
                    FatalException = e;
                }
                //catch (EndOfStreamException e)
                //{
                //    FatalException = e;
                //}
                catch (IOException e)
                {
                    if(WasSucessfullyConnected){
                        //TODO retry connection?
                        FatalException = e;
                    }
                    else
                    {
                        FatalException = e;
                    }
                }
                catch (Exception e)
                {
                    FatalException = e;
                }
                finally
                {
                    Listening = false;
                }
            });
            listenerThread.Start();
        }

        protected abstract SaveDataUpdate? GetSaveDataUpdate();
        protected abstract ActiveLevelState? GetCurrentLevelState();

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
                    Thread.Sleep(1000);//try to wait for child threads to stop on their own
                    if (listenerThread.IsAlive)
                    {
                        listenerThread.Abort();//assume the thread is stuck and forcibly terminate it
                    }
                }

                // Dispose unmanaged resources here

                disposed = true;
            }
        }
        ~MultiplayerClientNetcode()
        {
            Dispose(false);
        }

        public void Update()
        {
            if (FatalException != null)
            {
#if DEBUG
                if (!System.Diagnostics.Debugger.IsAttached)
                {
                    System.Diagnostics.Debugger.Launch();
                }
                System.Diagnostics.Debugger.Break();
#endif
                throw FatalException;//This should never happen
            }

            if (!Listening)
            {
                return;
            }

            OnUpdate();
        }

        protected override void ProcessDisconnect(Guid puid)
        {
            try
            {
                if (puid != MyUuid && Players.TryGetValue(puid, out PlayerMetadata p))
                {
                    //DisconnectedPlayers.TryAdd(puid, DateTime.UtcNow.Ticks);
                    _ = Players.TryRemove(puid, out _);
                }
            }
            catch (InvalidOperationException) { }
            catch (KeyNotFoundException) { } //this can happen if an item is removed by another thread while this thread is iterating over the items

            //this should happen after we remove the player from the Players collection, to avoid any race conditions 
            _ = PlayerAppearances.TryRemove(puid, out _);
        }

        protected override void ProcessNewClientGuid(Guid puid)
        {
            MyUuid = puid;
            MyPlayerMetadata.Uuid = puid;
        }
    }
}