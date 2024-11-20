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
using FezSharedTools;

using ActionType = System.Int32;
using HorizontalDirection = System.Int32;
using Viewpoint = System.Int32;
using System.Threading.Tasks;
using static FezMultiplayerDedicatedServer.MultiplayerServer;

namespace FezMultiplayerDedicatedServer
{
    /// <summary>
    /// The class that contains all the networking stuff
    /// 
    /// Note: This class should only contain System usings
    /// </summary>
    public class MultiplayerServer : SharedNetcode<ServerPlayerMetadata>, IDisposable
    {
        [Serializable]
        public class ServerPlayerMetadata : PlayerMetadata
        {
            public TcpClient tcpClient;
            public readonly DateTime joinTime = DateTime.UtcNow;
            public TimeSpan TimeSinceJoin => DateTime.UtcNow - joinTime;

            public ServerPlayerMetadata(TcpClient tcpClient, Guid Uuid, string CurrentLevelName, Vector3 Position, Viewpoint CameraViewpoint, ActionType Action, int AnimFrame, HorizontalDirection LookingDirection, long LastUpdateTimestamp)
            : base(Uuid, CurrentLevelName, Position, CameraViewpoint, Action, AnimFrame, LookingDirection, LastUpdateTimestamp)
            {
                this.tcpClient = tcpClient;
            }
        }

        private readonly TcpListener tcpListener;
        private readonly Task listenerTask;
        private readonly Task timeoutTask;
        private readonly int listenPort;
        protected readonly int overduetimeout;
        private readonly bool useAllowList;
        private readonly IPFilter AllowList;
        private readonly IPFilter BlockList;
        public bool SyncWorldState;

        public override ConcurrentDictionary<Guid, ServerPlayerMetadata> Players { get; } = new ConcurrentDictionary<Guid, ServerPlayerMetadata>();
        public readonly ConcurrentDictionary<Guid, long> DisconnectedPlayers = new ConcurrentDictionary<Guid, long>();
        private IEnumerable<TcpClient> ConnectedClients => Players.Select(p => p.Value.tcpClient);
        public EndPoint LocalEndPoint => tcpListener?.LocalEndpoint;

        public event Action OnUpdate = () => { };
        public event Action OnDispose = () => { };

        private readonly List<ActiveLevelState> activeLevelStates = new List<ActiveLevelState>();

        /// <summary>
        /// Creates a new instance of this class with the provided parameters.
        /// For any errors that get encountered see <see cref="ErrorMessage"/> an <see cref="FatalException"/>
        /// </summary>
        /// <param name="settings">The <see cref="MultiplayerServerSettings"/> to use to create this instance.</param>
        internal MultiplayerServer(MultiplayerServerSettings settings)
        {
            this.listenPort = settings.ListenPort;
            this.overduetimeout = settings.OverdueTimeout;
            this.useAllowList = settings.UseAllowList;
            this.AllowList = settings.AllowList;
            this.BlockList = settings.BlockList;
            this.SyncWorldState = settings.SyncWorldState;

            bool initializing = true;
            int retries = 0;
            while (initializing)
            {
                try
                {
                    tcpListener = new TcpListener(IPAddress.Any, listenPort);
                    tcpListener.Start();
                    initializing = false;
                }
                catch (Exception e)
                {
                    if (settings.MaxAdjustListenPortOnBindFail > retries++)
                    {
                        Console.WriteLine($"Port {listenPort} is already in use. Trying {listenPort + 1} instead.");
                        listenPort++;
                    }
                    else
                    {
                        //ErrorMessage = e.Message;
                        ErrorMessage = $"Failed to bind a port after {retries} tr{(retries == 1 ? "y" : "ies")}. Ports number {listenPort - retries + 1} to {listenPort} are already in use. Exiting.";
                        return;
                    }
                }
            }
            listenerTask = StartAcceptTcpClients();
            timeoutTask = Task.Factory.StartNew(RemoveOldClients, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
        private void RemoveOldClients()
        {
            while (!disposing)
            {
                Thread.Sleep(500);
                try
                {
                    foreach (var p in Players)
                    {
                        if (disposing)
                        {
                            break;
                        }
                        Thread.Sleep(10);
                        if (p.Key.Equals(Guid.Empty))
                        {
                            Thread.Sleep(1000);
                            if (p.Key.Equals(Guid.Empty))
                            {
                                p.Value.tcpClient.Close();
                                ProcessDisconnectInternal(p.Key);
                            }
                        }
                        if (!p.Value.tcpClient.Connected)
                        {
                            ProcessDisconnectInternal(p.Key);
                        }
                    }
                }
                catch
                {
                }
            }
        }
        private async Task StartAcceptTcpClients()
        {
            try
            {
                while (!disposing)
                {
                    //Note: AcceptTcpClient blocks until a connection is made
                    //Note: apparently tcpListener.AcceptTcpClient(); is so blocking, if it's in a Thread, it even blocks calls to that thread's .Abort() method 
                    //TcpClient client = tcpListener.AcceptTcpClient();
                    TcpClient client = await tcpListener.AcceptTcpClientAsync();
                    new Thread(() => {
                        try
                        {
                            IPEndPoint remoteEndpoint = (IPEndPoint)client.Client.RemoteEndPoint;
                            if (BlockList.Contains(remoteEndpoint.Address)
                                || (useAllowList && !AllowList.Contains(remoteEndpoint.Address))
                                    )
                            {
                                client.Client.Shutdown(SocketShutdown.Both);
                                //client.Client.Close();
                                client.Close();
                                return;
                            }
                            OnNewClientConnect(client);
                        }
                        catch (Exception e)
                        {
                            //TODO handle exception
                            Console.WriteLine(e);
                        }
                        finally
                        {
                            client.Close();
                        }
                    }).Start();
                }
            }
            catch (Exception e) { FatalException = e; }
        }


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
                    OnDispose();
                    Thread.Sleep(1000);//try to wait for child threads to stop on their own
                    foreach (TcpClient client in ConnectedClients)
                    {
                        client.Close();
                    }
                    tcpListener.Stop();
                }

                // Dispose unmanaged resources here

                disposed = true;
            }
        }
        ~MultiplayerServer()
        {
            Dispose(false);
        }

        public void Update()
        {
            if (FatalException != null)
            {
                throw FatalException;//This should never happen
            }
            long curtime = DateTime.UtcNow.Ticks;
            List<Guid> KeysToRemove = new List<Guid>();
            foreach (var kvpair in DisconnectedPlayers)
            {
                if ((curtime - kvpair.Value) / (double)TimeSpan.TicksPerMillisecond >= overduetimeout)
                {
                    KeysToRemove.Add(kvpair.Key);
                    //Don't remove the keys while iterating over the ConcurrentDictionary
                }
            }
            foreach (var k in KeysToRemove)
            {
                _ = DisconnectedPlayers.TryRemove(k, out _);
            }

            OnUpdate();
        }

        private static readonly TimeSpan NewPlayerTimeSpan = TimeSpan.FromSeconds(1);
        private void OnNewClientConnect(TcpClient tcpClient)
        {
            Guid uuid = Guid.NewGuid();
            try
            {
                Console.WriteLine($"Incoming connection from {tcpClient.Client.RemoteEndPoint}...");
                using (NetworkStream stream = tcpClient.GetStream())
                {
                    stream.ReadTimeout = overduetimeout;
                    stream.WriteTimeout = overduetimeout;
                    using (NetworkStream tcpStream = tcpClient.GetStream())
                    using (BinaryNetworkReader reader = new BinaryNetworkReader(tcpStream))
                    using (BinaryNetworkWriter writer = new BinaryNetworkWriter(tcpStream))
                    {
                        try
                        {
                            //send them our data and get player appearance from client
                            WriteServerGameTickPacket(writer, Players.Values.Cast<PlayerMetadata>().ToList(), null, GetActiveLevelStates(), DisconnectedPlayers.Keys, PlayerAppearances, uuid, false, sharedSaveData);
                            MiscClientData clientData = new MiscClientData(null, false, new HashSet<Guid>(MiscClientData.MaxRequestedAppearancesSize));
                            ReadClientGameTickPacket(reader, ref clientData, uuid);
                            bool Disconnecting = clientData.Disconnecting;
                            PlayerMetadata playerMetadata = clientData.Metadata;

                            ServerPlayerMetadata addValueFactory(Guid guid)
                            {
                                return new ServerPlayerMetadata(tcpClient, uuid, playerMetadata.CurrentLevelName, playerMetadata.Position, playerMetadata.CameraViewpoint,
                                            playerMetadata.Action, playerMetadata.AnimFrame, playerMetadata.LookingDirection, playerMetadata.LastUpdateTimestamp);
                            }
                            ServerPlayerMetadata updateValueFactory(Guid guid, ServerPlayerMetadata currentval)
                            {
                                currentval.tcpClient = tcpClient;
                                //Note: the value of playerMetadata.Uuid received from the client is never used
                                //We use the Guid that we assigned instead, for security reasons. 
                                currentval.Uuid = guid;
                                if (currentval.LastUpdateTimestamp < playerMetadata.LastUpdateTimestamp)
                                {
                                    currentval.CopyValuesFrom(playerMetadata);
                                }
                                return currentval;
                            }

                            Players.AddOrUpdate(uuid, addValueFactory, updateValueFactory);
                            Console.WriteLine($"Player connected from {tcpClient.Client.RemoteEndPoint}. Assigned uuid {uuid}.");

                            bool PlayerAppearancesFilter(KeyValuePair<Guid, ServerPlayerMetadata> p)
                            {
                                //get the requested PlayerAppearances from PlayerAppearances, and players that have recently joined
                                return clientData.RequestedAppearances.Contains(p.Key) || p.Value.TimeSinceJoin < NewPlayerTimeSpan;
                            }
                            
                            while (tcpClient.Connected && !disposing)
                            {
                                if (Disconnecting)
                                {
                                    break;
                                }
                                //if UnknownPlayerAppearanceGuids contains uuid, ask client to retransmit their PlayerAppearance
                                bool requestAppearance = UnknownPlayerAppearanceGuids.ContainsKey(uuid);
                                //repeat until the client disconnects or times out
                                WriteServerGameTickPacket(writer, Players.Values.Cast<PlayerMetadata>().ToList(), GetSaveDataUpdate(), GetActiveLevelStates(), DisconnectedPlayers.Keys,
                                        GetPlayerAppearances(PlayerAppearancesFilter), null, requestAppearance, null);
                                ReadClientGameTickPacket(reader, ref clientData, uuid);
                                Disconnecting = clientData.Disconnecting;
                                playerMetadata = clientData.Metadata;
                                Players.AddOrUpdate(uuid, addValueFactory, updateValueFactory);
                                Thread.Sleep(10);
                            }
                        }
                        catch (Exception e)
                        {
                            //TODO handle exception
                            Console.WriteLine(e);
                        }
                        finally
                        {
                            reader.Close();
                            writer.Close();
                            tcpStream.Close();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                //TODO handle exception
                Console.WriteLine(e);
            }
            finally
            {
                long disconnectTime = DateTime.UtcNow.Ticks;
                DisconnectedPlayers.AddOrUpdate(uuid, disconnectTime, (puid, oldTime) => disconnectTime);
                ProcessDisconnect(uuid);
                tcpClient.Close();
                tcpClient.Dispose();
            }
        }

        protected override void ProcessDisconnect(Guid puid)
        {
            Console.WriteLine($"Disconnecting player {puid}.");
            ProcessDisconnectInternal(puid);
        }
        private void ProcessDisconnectInternal(Guid puid)
        {
            try
            {
                if (Players.TryGetValue(puid, out ServerPlayerMetadata p))// && !DisconnectedPlayers.ContainsKey(puid))
                {
                    long disconnectTime = DateTime.UtcNow.Ticks;
                    DisconnectedPlayers.AddOrUpdate(puid, disconnectTime, (lpuid, oldTime) => disconnectTime);
                    _ = Players.TryRemove(puid, out _);
                }
            }
            catch (InvalidOperationException) { }
            catch (KeyNotFoundException) { } //this can happen if an item is removed by another thread while this thread is iterating over the items

            //this should happen after we remove the player from the Players collection, to avoid any race conditions 
            _ = PlayerAppearances.TryRemove(puid, out _);
        }

        private static readonly List<ActiveLevelState> empty = new List<ActiveLevelState>();
        private List<ActiveLevelState> GetActiveLevelStates()
        {
            return SyncWorldState ? activeLevelStates : empty;
        }

        private readonly SharedSaveData sharedSaveData = new SharedSaveData();

        protected override void ProcessSaveDataUpdate(SaveDataUpdate saveDataUpdate)
        {
            if (!SyncWorldState)
            {
                return;
            }
            //TODO not yet implemented
            throw new NotImplementedException();
        }
        private SaveDataUpdate? GetSaveDataUpdate()
        {
            if (!SyncWorldState)
            {
                return null;
            }
            //TODO not yet implemented
            throw new NotImplementedException();
        }

        protected override void ProcessActiveLevelState(ActiveLevelState activeLevelState)
        {
            if (!SyncWorldState)
            {
                return;
            }
            //TODO not yet implemented
            throw new NotImplementedException();
        }
        /// <summary>
        /// Returns a collection of PlayerAppearances for players that match <paramref name="where"/>
        /// </summary>
        /// <param name="where">The filter to use to specify which PlayerAppearances to return</param>
        /// <returns></returns>
        private Dictionary<Guid, PlayerAppearance> GetPlayerAppearances(Func<KeyValuePair<Guid, ServerPlayerMetadata>, bool> where)
        {
            //IEnumerable<Guid> recentlyJoinedPlayers = Players.Values.Where(meta => meta.TimeSinceJoin < NewPlayerTimeSpan)
            //        .Select(p => p.Uuid).ToHashSet();
            //idk which of these is better
            IEnumerable<Guid> recentlyJoinedPlayers = Players.Where(where)
                    .Select(p => p.Key).ToHashSet();

            return PlayerAppearances
                    .Where(entry => recentlyJoinedPlayers.Contains(entry.Key))
                    .ToDictionary(entry => entry.Key, entry => entry.Value);
        }
    }
}