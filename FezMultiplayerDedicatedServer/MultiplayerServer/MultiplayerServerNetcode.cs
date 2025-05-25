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
using System.Threading.Tasks;
using static FezMultiplayerDedicatedServer.MultiplayerServerNetcode;

namespace FezMultiplayerDedicatedServer
{
    /// <summary>
    /// The class that contains all the networking stuff
    /// 
    /// Note: This class should only contain System usings
    /// </summary>
    public class MultiplayerServerNetcode : SharedNetcode<ServerPlayerMetadata>, IDisposable
    {
        [Serializable]
        public class ServerPlayerMetadata : PlayerMetadata
        {
            public Socket client;
            public readonly DateTime joinTime = DateTime.UtcNow;
            public TimeSpan TimeSinceJoin => DateTime.UtcNow - joinTime;
            public long NetworkSpeedUp = 0;
            public long NetworkSpeedDown = 0;

            public ServerPlayerMetadata(Socket client, Guid Uuid, string CurrentLevelName, Vector3 Position, Viewpoint CameraViewpoint, ActionType Action, int AnimFrame, HorizontalDirection LookingDirection, long LastUpdateTimestamp)
            : base(Uuid, CurrentLevelName, Position, CameraViewpoint, Action, AnimFrame, LookingDirection, LastUpdateTimestamp)
            {
                this.client = client;
            }
        }

        private readonly Socket listenerSocket;
        private readonly int listenPort;
        protected readonly int overduetimeout;
        private readonly bool useAllowList;
        private readonly IPFilter AllowList;
        private readonly IPFilter BlockList;
        public bool SyncWorldState;

        public override ConcurrentDictionary<Guid, ServerPlayerMetadata> Players { get; } = new ConcurrentDictionary<Guid, ServerPlayerMetadata>();
        public readonly ConcurrentDictionary<Guid, long> DisconnectedPlayers = new ConcurrentDictionary<Guid, long>();
        private IEnumerable<Socket> ConnectedClients => Players.Select(p => p.Value.client);
        public EndPoint LocalEndPoint => listenerSocket?.LocalEndPoint;

        public event Action OnUpdate = () => { };
        public event Action OnDispose = () => { };

        private readonly List<ActiveLevelState> activeLevelStates = new List<ActiveLevelState>();

        private readonly ServerAdvertiser serverAdvertiser;

        /// <summary>
        /// Creates a new instance of this class with the provided parameters.
        /// For any errors that get encountered see <see cref="ErrorMessage"/> an <see cref="FatalException"/>
        /// </summary>
        /// <param name="settings">The <see cref="MultiplayerServerSettings"/> to use to create this instance.</param>
        internal MultiplayerServerNetcode(MultiplayerServerSettings settings)
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
                    // Create a listener socket that can accept both IPv4 and IPv6 connections
                    listenerSocket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);

                    // Set the socket to accept both IPv4 and IPv6 connections
                    listenerSocket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);

                    // Bind the socket to any address and the specified port
                    listenerSocket.Bind(new IPEndPoint(IPAddress.IPv6Any, listenPort));
                    listenerSocket.Listen(10); // Listen for incoming connections, with a specified backlog
                    initializing = false;
                }
                catch (Exception)
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
            _ = StartAcceptTcpClients();
            _ = Task.Factory.StartNew(RemoveOldClients, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            if (settings.DoAdvertiseServer)
            {
                Dictionary<string, string> multicastData = new Dictionary<string, string>()
                {
                    { "Protocol", ProtocolSignature },
                    { "Version", ProtocolVersion },
                    { "Endpoint", listenPort.ToString() },
                };
                string message = string.Join("\n", multicastData.Select(kv => kv.Key + SharedConstants.ServerDiscoveryEntrySeparator + kv.Value));
                serverAdvertiser = new ServerAdvertiser(SharedConstants.MulticastAddress, message);
                //serverAdvertiser.SetMessage(message);
                OnDispose += serverAdvertiser.Dispose;
            }
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
                                p.Value.client.Close();
                                ProcessDisconnectInternal(p.Key);
                            }
                        }
                        if (!p.Value.client.Connected)
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
                    Socket client = await Task.Factory.StartNew(() => listenerSocket.Accept());
                    new Thread(() =>
                    {
                        try
                        {
                            IPEndPoint remoteEndpoint = (IPEndPoint)client.RemoteEndPoint;
                            if (BlockList.Contains(remoteEndpoint.Address)
                                || (useAllowList && !AllowList.Contains(remoteEndpoint.Address))
                                    )
                            {
                                client.Shutdown(SocketShutdown.Both);
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
                    if (listenerSocket.Connected)
                    {
                        listenerSocket.Shutdown(SocketShutdown.Both);
                    }
                    foreach (Socket client in ConnectedClients)
                    {
                        client.Close();
                        client.Dispose();
                    }
                    listenerSocket.Close();
                    listenerSocket.Dispose();
                }

                // Dispose unmanaged resources here

                disposed = true;
            }
        }
        ~MultiplayerServerNetcode()
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
        private void OnNewClientConnect(Socket client)
        {
            Guid uuid = Guid.NewGuid();
            try
            {
                Console.WriteLine($"Incoming connection from {client.RemoteEndPoint}...");
                using (NetworkStream stream = new NetworkStream(client))
                {
                    stream.ReadTimeout = overduetimeout;
                    stream.WriteTimeout = overduetimeout;
                    using (BinaryNetworkReader reader = new BinaryNetworkReader(stream))
                    using (BinaryNetworkWriter writer = new BinaryNetworkWriter(stream))
                    {
                        try
                        {
                            Queue<long> SpeedUp = new Queue<long>(100);
                            Queue<long> SpeedDown = new Queue<long>(100);
                            //send them our data and get player appearance from client
                            SpeedUp.Enqueue(WriteServerGameTickPacket(writer, Players.Values.Cast<PlayerMetadata>().ToList(),
                                    null, GetActiveLevelStates(), DisconnectedPlayers.Keys,
                                    PlayerAppearances, uuid, false, sharedSaveData));
                            MiscClientData clientData = new MiscClientData(null, false, new HashSet<Guid>(MiscClientData.MaxRequestedAppearancesSize));
                            SpeedDown.Enqueue(ReadClientGameTickPacket(reader, ref clientData, uuid));
                            bool Disconnecting = clientData.Disconnecting;
                            PlayerMetadata playerMetadata = clientData.Metadata;

                            ServerPlayerMetadata addValueFactory(Guid guid)
                            {
                                return new ServerPlayerMetadata(client, uuid, playerMetadata.CurrentLevelName, playerMetadata.Position, playerMetadata.CameraViewpoint,
                                            playerMetadata.Action, playerMetadata.AnimFrame, playerMetadata.LookingDirection, playerMetadata.LastUpdateTimestamp);
                            }
                            ServerPlayerMetadata updateValueFactory(Guid guid, ServerPlayerMetadata currentval)
                            {
                                currentval.client = client;
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
                            Console.WriteLine($"Player connected from {client.RemoteEndPoint}. Assigned uuid {uuid}.");

                            bool PlayerAppearancesFilter(KeyValuePair<Guid, ServerPlayerMetadata> p)
                            {
                                //get the requested PlayerAppearances from PlayerAppearances, and players that have recently joined
                                return clientData.RequestedAppearances.Contains(p.Key) || p.Value.TimeSinceJoin < NewPlayerTimeSpan;
                            }

                            while (client.Connected && !disposing)
                            {
                                if (Disconnecting)
                                {
                                    break;
                                }
                                if (Players.TryGetValue(uuid, out ServerPlayerMetadata serverPlayerMetadata))
                                {
                                    //Note: does not produce a meaningful number for connections to loopback addresses
                                    serverPlayerMetadata.NetworkSpeedUp = (long)Math.Round(SpeedUp.Average()) / TimeSpan.TicksPerMillisecond;
                                    serverPlayerMetadata.NetworkSpeedDown = (long)Math.Round(SpeedDown.Average()) / TimeSpan.TicksPerMillisecond;
                                }
                                //if UnknownPlayerAppearanceGuids contains uuid, ask client to retransmit their PlayerAppearance
                                bool requestAppearance = UnknownPlayerAppearanceGuids.ContainsKey(uuid);
                                //repeat until the client disconnects or times out
                                if (SpeedUp.Count >= 100)
                                {
                                    _ = SpeedUp.Dequeue();
                                }
                                if (SpeedDown.Count >= 100)
                                {
                                    _ = SpeedDown.Dequeue();
                                }
                                SpeedUp.Enqueue(WriteServerGameTickPacket(writer, Players.Values.Cast<PlayerMetadata>().ToList(),
                                        GetSaveDataUpdate(), GetActiveLevelStates(), DisconnectedPlayers.Keys,
                                        GetPlayerAppearances(PlayerAppearancesFilter), null, requestAppearance, null));
                                SpeedDown.Enqueue(ReadClientGameTickPacket(reader, ref clientData, uuid));
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
                            stream.Close();
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
                client.Close();
                client.Dispose();
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