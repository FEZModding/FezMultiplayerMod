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
using Common;

namespace FezGame.MultiplayerMod
{
    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected
    }
    public abstract class MultiplayerClientNetcode : SharedNetcode<PlayerMetadata>, IDisposable
    {
        protected static void LogStatus(LogSeverity severity, string message)
        {
            string ComponentName = nameof(MultiplayerClientNetcode);
            string ThreadName = Thread.CurrentThread.Name ?? String.Empty;
            if(ThreadName.Length > 0)
            {
                message = $"({ThreadName}) " + message;
            }
            Logger.Log(ComponentName, severity, message);
        }

        private Thread listenerThread;

        public ConnectionState ActiveConnectionState
        {
            get
            {
                //I think this looks nicer than a long sequence of ternary operators 
                if (listening)
                {
                    return ConnectionState.Connected;
                }
                else if (listenerThread != null && listenerThread.IsAlive)
                {
                    return ConnectionState.Connecting;
                }
                else 
                {
                    return ConnectionState.Disconnected;
                }
            }
        }

        public IPEndPoint RemoteEndpoint = null;

        public Guid MyUuid { get; private set; }
        public volatile PlayerMetadata MyPlayerMetadata = null;

        public override ConcurrentDictionary<Guid, PlayerMetadata> Players { get; } = new ConcurrentDictionary<Guid, PlayerMetadata>();

        private volatile bool listening;

        public PlayerAppearance MyAppearance;
        private volatile bool MyAppearanceChanged = false;

        public volatile uint ConnectionLatencyUp = 0;
        public volatile uint ConnectionLatencyDown = 0;

        public string MyPlayerName
        {
            get => MyAppearance.PlayerName;
            set
            {
                MyAppearance.PlayerName = value;
                MyAppearanceChanged = true;
            }
        }


        public volatile bool SyncWorldState;
        /// <summary>
        /// The amount of time, in ticks, to retry reconnecting to the server if the connection is somehow lost
        /// </summary>
        private static readonly long reconnectTimeout = TimeSpan.FromSeconds(10).Ticks;

        public event Action OnUpdate = () => { };
        public event Action OnDispose = () => { };
        public event Action OnConnect = () => { };
        public event Action OnDisconnect = () => { };

        /// <summary>
        /// Creates a new instance of this class with the provided parameters.
        /// For any errors that get encountered see <see cref="ErrorMessage"/> an <see cref="FatalException"/>
        /// </summary>
        /// <param name="settings">The <see cref="MultiplayerClientSettings"/> to use to create this instance.</param>
        internal MultiplayerClientNetcode(MultiplayerClientSettings settings)
        {
            listening = false;
            SyncWorldState = settings.SyncWorldState;
            MyAppearance = new PlayerAppearance(settings.MyPlayerName, settings.Appearance);
        }

        public void ConnectToServerAsync(IPEndPoint endpoint)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(this.GetType().Name);
            }
            if ((listenerThread != null && listenerThread.IsAlive) || (RemoteEndpoint != null))
            {
                //TODO already connected to somewhere
                return;
                throw new InvalidOperationException("Already connected to " + RemoteEndpoint);
            }
            RemoteEndpoint = endpoint;
            listenerThread = new Thread(() =>
            {
                void ConnectToServerInternal(out bool ConnectionSuccessful)
                {
                    LogStatus(LogSeverity.Information, $"Connecting to {endpoint} ...");
                    ConnectionSuccessful = false;
                    TcpClient tcpClient = new TcpClient(endpoint.AddressFamily);
                    tcpClient.Connect(endpoint);
                    listening = true;
                    while (MyPlayerMetadata == null)
                    {
                        Thread.Sleep(100);
                    }
                    using (NetworkStream tcpStream = tcpClient.GetStream())
                    using (BinaryNetworkReader reader = new BinaryNetworkReader(tcpStream))
                    using (BinaryNetworkWriter writer = new BinaryNetworkWriter(tcpStream))
                    {
                        bool retransmitAppearanceRequested = false;
                        try
                        {
                            ConnectionLatencyDown = (uint)ReadServerGameTickPacket(reader, ref retransmitAppearanceRequested);
                            ConnectionLatencyUp = (uint)WriteClientGameTickPacket(writer, MyPlayerMetadata, null, null, MyAppearance, UnknownPlayerAppearanceGuids.Keys, false);
                        }
                        catch (System.IO.EndOfStreamException e)
                        {
                            throw new Exception("Could not connect to server", e);
                        }
                        ConnectionSuccessful = true;
                        LogStatus(LogSeverity.Information, $"Connection to {endpoint} successful");
                        OnConnect();
                        while (true)
                        {
                            ConnectionLatencyDown = (uint)ReadServerGameTickPacket(reader, ref retransmitAppearanceRequested);
                            if (!disconnectRequested)
                            {
                                ActiveLevelState? activeLevelState = null;
                                if (SyncWorldState)
                                {
                                    activeLevelState = GetCurrentLevelState();
                                }

                                SaveDataUpdate? saveDataUpdate = null;
                                if (SyncWorldState)
                                {
                                    saveDataUpdate = GetSaveDataUpdate();
                                }
                                //transmit MyAppearance whenever its value changes 
                                PlayerAppearance? appearance = null;
                                if (retransmitAppearanceRequested || MyAppearanceChanged)
                                {
                                    appearance = MyAppearance;
                                    MyAppearanceChanged = false;
                                }
                                ConnectionLatencyUp = (uint)WriteClientGameTickPacket(writer, MyPlayerMetadata, saveDataUpdate, activeLevelState, appearance, UnknownPlayerAppearanceGuids.Keys, false);
                            }
                            else
                            {
                                LogStatus(LogSeverity.Information, $"Disconnecting from {RemoteEndpoint} ...");
                                //tell the server we're disconnecting
                                WriteClientGameTickPacket(writer, MyPlayerMetadata, null, null, null, new List<Guid>(0), true);
                                break;
                            }
                        }
                        reader.Close();
                        writer.Close();
                        tcpStream.Close();
                        tcpClient.Close();
                    }
                    LogStatus(LogSeverity.Information, $"Disconnected cleanly from {RemoteEndpoint}");
                }
                bool wasSuccessfullyConnected = false;
                long? disconnectTime = null;
                while (!disconnectRequested) // Infinite loop will allow us to retry connection
                {
                    try
                    {
                        ConnectToServerInternal(out wasSuccessfullyConnected);
                        if (wasSuccessfullyConnected)
                        {
                            break; // Successfully connected
                        }
                    }
                    //catch (EndOfStreamException e)
                    //{
                    //    FatalException = e;
                    //}
                    catch (Exception e)//Connection failed, data read error, connection terminated by server, etc.
                    {
                        LogStatus(LogSeverity.Warning, $"Lost connection to {RemoteEndpoint}");
                        //TODO this does not properly handle scenarios where the connection is successful but an error occurs consistently after the initial connection.
                        if (wasSuccessfullyConnected)
                        {
                            LogStatus(LogSeverity.Information, $"Attempting to reconnect to {RemoteEndpoint} ...");
                            disconnectTime = DateTime.UtcNow.Ticks;
                        }
                        else if (DateTime.UtcNow.Ticks - disconnectTime > reconnectTimeout)
                        {
                            //reconnection failed
                            LogStatus(LogSeverity.Warning, $"Failed to reconnect to {RemoteEndpoint}");
                            FatalException = e; // Record the fatal exception on failed connection attempts
                            break; // Exit the loop on persistent failures
                        }
                        else if (disconnectTime == null && !wasSuccessfullyConnected)
                        {
                            LogStatus(LogSeverity.Warning, $"Failed to connect to {endpoint}");
                            FatalException = e; // Record the fatal exception on failed connection attempts
                            break; // Exit the loop on persistent failures
                        }
                        // If previously connected, just retry
                    }
                    finally
                    {
                        listening = false;
                        Players.Clear();
                        OnDisconnect();
                    }
                }
                LogStatus(LogSeverity.Information, $"Connection with {RemoteEndpoint} terminated");
                RemoteEndpoint = null;
            })
            {
                Name = "Listener Thread"
            };
            listenerThread.Start();
        }

        /// <summary>
        /// used to tell notify the listener thread to stop
        /// </summary>
        private volatile bool disconnectRequested = false;
        public void Disconnect()
        {
            LogStatus(LogSeverity.Information, "Disconnect requested");
            this.disconnectRequested = true;//let listener thread know it should disconnect
            if (listenerThread != null && listenerThread.IsAlive)
            {
                Thread.Sleep(1000);//try to wait for child threads to stop on their own
                if (listenerThread != null && listenerThread.IsAlive)
                {
                    LogStatus(LogSeverity.Warning, "Forcibly terminated listening thread");
                    listenerThread.Abort();//assume the thread is stuck and forcibly terminate it
                }
            }
            //ensure RemoteEndpoint is reset
            RemoteEndpoint = null;
            listenerThread = null;
            this.disconnectRequested = false;//reset for next use
            LogStatus(LogSeverity.Information, "Disconnect complete");
        }

        protected abstract SaveDataUpdate GetSaveDataUpdate();
        protected abstract ActiveLevelState GetCurrentLevelState();

        // I was told "Your Dispose implementation needs work https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose#implement-the-dispose-pattern"
        // and stuff like "It technically works but is dangerous" and "always use an internal protected Dispose method" and "always call GC.SuppressFinalize(this) in the public Dispose method"
        // so I added all this extra stuff even though it technically already worked fine, so hopefully this works fine
        private bool disposed = false;
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
                    Disconnect();
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
            if (!listening)
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