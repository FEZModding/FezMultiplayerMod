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
            /// <summary>
            /// for auto-disposing, since LastUpdateTimestamp shouldn't be used for that because the system clocks of the two protocols could be different
            /// </summary>
            public long LastUpdateLocalTimestamp;

            public ServerPlayerMetadata(TcpClient tcpClient, Guid Uuid, string CurrentLevelName, Vector3 Position, Viewpoint CameraViewpoint, ActionType Action, int AnimFrame, HorizontalDirection LookingDirection, long LastUpdateTimestamp, long LastUpdateLocalTimestamp)
            : base(Uuid, CurrentLevelName, Position, CameraViewpoint, Action, AnimFrame, LookingDirection, LastUpdateTimestamp)
            {
                this.tcpClient = tcpClient;
                this.LastUpdateLocalTimestamp = LastUpdateLocalTimestamp;
            }
        }

        private volatile TcpListener tcpListener;
        private readonly Thread listenerThread;
        private readonly Thread timeoutthread;
        protected readonly long overduetimeout;
        private readonly bool useAllowList;
        private readonly IPFilter AllowList;
        private readonly IPFilter BlockList;

        /// <summary>
        /// How long to wait, in ticks, before stopping sending or receiving packets for this player.
        /// This is required otherwise race conditions can occur.
        /// <br /><br />Note: could make this customizable
        /// </summary>
        private readonly long preoverduetimeoutoffset = TimeSpan.TicksPerSecond * 5;

        public override ConcurrentDictionary<Guid, ServerPlayerMetadata> Players { get; } = new ConcurrentDictionary<Guid, ServerPlayerMetadata>();
        public readonly ConcurrentDictionary<Guid, long> DisconnectedPlayers = new ConcurrentDictionary<Guid, long>();
        private IEnumerable<TcpClient> connectedClients => Players.Select(p => p.Value.tcpClient);
        public EndPoint LocalEndPoint => tcpListener?.LocalEndpoint;

        public event Action OnUpdate = () => { };
        public event Action OnDispose = () => { };

        /// <summary>
        /// Creates a new instance of this class with the provided parameters.
        /// For any errors that get encountered see <see cref="ErrorMessage"/> an <see cref="FatalException"/>
        /// </summary>
        /// <param name="settings">The <see cref="MultiplayerServerSettings"/> to use to create this instance.</param>
        internal MultiplayerServer(MultiplayerServerSettings settings)
        {
            int listenPort = settings.listenPort;
            this.overduetimeout = settings.overduetimeout;
            this.useAllowList = settings.useAllowList;
            this.AllowList = settings.AllowList;
            this.BlockList = settings.BlockList;

            listenerThread = new Thread(() =>
            {
                try
                {
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
                            if (settings.maxAdjustListenPortOnBindFail > retries++)
                            {
                                listenPort++;
                            }
                            else
                            {
                                //ErrorMessage = e.Message;
                                ErrorMessage = $"Failed to bind a port after {retries} tr{(retries == 1 ? "y" : "ies")}. Ports number {listenPort - retries + 1} to {listenPort} are already in use";
                                //listenerThread.Abort(e);//does this even work?//calling Abort is a bad idea
                                return;
                            }
                        }
                    }
                    while (!disposing)
                    {
                        //IPEndPoint object will allow us to read datagrams sent from any source.
                        tcpListener.AcceptTcpClientAsync().ContinueWith(tcpClientTask => {
                            if (tcpClientTask.Status == TaskStatus.RanToCompletion) {
                                OnNewClientConnect(tcpClientTask.Result);
                            }
                            else if (tcpClientTask.Status == TaskStatus.Faulted)
                            {
                                // Directly accessing Exception since we assume it should not be null
                                var exception = tcpClientTask.Exception;

                                if (exception != null) // This check is mostly for safety
                                {
                                    Console.WriteLine(exception.GetBaseException().Message);
                                }
                                else
                                {
                                    // It's unlikely this else case will happen, but in case it does
                                    Console.WriteLine("No exception details available.");
                                }
                            }
                        });
                    }
                    tcpListener.Stop();
                }
                catch (Exception e) { FatalException = e; }
            });
            listenerThread.Start();

            timeoutthread = new Thread(() =>
            {
                try
                {
                    while (!disposing)
                    {
                        try
                        {
                            foreach (ServerPlayerMetadata p in Players.Values)
                            {
                                if ((DateTime.UtcNow.Ticks - p.LastUpdateLocalTimestamp) > overduetimeout || DisconnectedPlayers.ContainsKey(p.Uuid))
                                {
                                    _ = Players.TryRemove(p.Uuid, out _);
                                    if (!DisconnectedPlayers.ContainsKey(p.Uuid))
                                    {
                                        _ = DisconnectedPlayers.TryAdd(p.Uuid, p.LastUpdateLocalTimestamp);
                                    }
                                }
                            }
                            foreach (var dp in DisconnectedPlayers)
                            {
                                if ((DateTime.UtcNow.Ticks - dp.Value) > overduetimeout*2)
                                {
                                    _ = DisconnectedPlayers.TryRemove(dp.Key, out _);
                                }
                            }
                        }
                        catch (KeyNotFoundException)//this can happen if an item is removed by another thread while this thread is iterating over the items
                        {
                        }
                    }
                }
                catch (Exception e) { FatalException = e; }
            });
            timeoutthread.Start();
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
                    if (listenerThread.IsAlive)
                    {
                        listenerThread.Abort();//assume the thread is stuck and forcibly terminate it
                    }
                    if (timeoutthread.IsAlive)
                    {
                        timeoutthread.Abort();//assume the thread is stuck and forcibly terminate it
                    }
                    tcpListener.Stop();//must be after listenerThread is stopped
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

            OnUpdate();

            try
            {
                
                //SendPlayerDataToAll
                    foreach (var m in Players.Values)
                    {
                        if ((DateTime.UtcNow.Ticks - m.LastUpdateLocalTimestamp) + preoverduetimeoutoffset > overduetimeout || DisconnectedPlayers.ContainsKey(m.Uuid))
                        {
                            continue;
                        }
                        //Note: probably should refactor these methods
                        SendToAll(Serialize(m));
                    }
            }
            catch (KeyNotFoundException)//this can happen if an item is removed by another thread while this thread is iterating over the items
            {
            }
        }

        protected void SendToAll(byte[] msg)
        {
            // Send the message to all recipients
            System.Threading.Tasks.Parallel.ForEach(connectedClients,
                targ =>
                {
                    //TODO 
                });
        }

        private void OnNewClientConnect(TcpClient tcpClient)
        {
            //Get player appearance from client
            Guid puid;
            string pname;
            PlayerAppearance appearance;
            //TODO
            ServerPlayerMetadata newPlayer = new ServerPlayerMetadata(tcpClient, puid, null, new Vector3(0,0,0), 0, 0, 0, 0, DateTime.UtcNow.Ticks, DateTime.UtcNow.Ticks);
            UpdatePlayerAppearance(puid, pname, appearance);
        }

        protected override void ProcessDisconnect(Guid puid)
        {
            try
            {
                if (Players.TryGetValue(puid, out var p))
                {
                    DisconnectedPlayers.TryAdd(puid, DateTime.UtcNow.Ticks);
                    _ = Players.TryRemove(puid, out _);
                }
            }
            catch (InvalidOperationException) { }
            catch (KeyNotFoundException) { } //this can happen if an item is removed by another thread while this thread is iterating over the items
        }
    }
}