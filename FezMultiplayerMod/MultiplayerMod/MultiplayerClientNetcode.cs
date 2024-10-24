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
    public class MultiplayerClientNetcode : SharedNetcode<PlayerMetadata>, IDisposable
    {

        private volatile TcpClient tcpClient;
        private readonly Thread listenerThread;

        /// <summary>
        /// How long to wait, in ticks, before stopping sending or receiving packets for this player.
        /// This is required otherwise race conditions can occur.
        /// <br /><br />Note: could make this customizable
        /// </summary>
        private readonly long preoverduetimeoutoffset = TimeSpan.TicksPerSecond * 5;

        public override ConcurrentDictionary<Guid, PlayerMetadata> Players { get; } = new ConcurrentDictionary<Guid, PlayerMetadata>();
        public bool Listening => tcpClient?.Client?.IsBound ?? false;
        public EndPoint LocalEndPoint => tcpClient?.Client?.LocalEndPoint;
        public readonly Guid MyUuid = Guid.NewGuid();
        public string MyPlayerName = "";


        public event Action OnUpdate = () => { };
        public event Action OnDispose = () => { };

        /// <summary>
        /// Creates a new instance of this class with the provided parameters.
        /// For any errors that get encountered see <see cref="ErrorMessage"/> an <see cref="FatalException"/>
        /// </summary>
        /// <param name="settings">The <see cref="MultiplayerClientSettings"/> to use to create this instance.</param>
        internal MultiplayerClientNetcode(MultiplayerClientSettings settings)
        {
            this.MyPlayerName = settings.myPlayerName;

            listenerThread = new Thread(() =>
            {
                try
                {
                    bool initializing = true;
                    while (initializing)
                    {
                        tcpClient = new TcpClient(AddressFamily.InterNetwork);
                        initializing = false;
                    }
                    tcpClient.Connect(settings.mainEndpoint);
                    NetworkStream tcpStream = tcpClient.GetStream();
                    WriteClientGameTickPacket(writer, Players[MyUuid], saveDataUpdate, levelState, appearance);
                    ReadServerGameTickPacket(reader);
                    while (!disposing)
                    {
                        //TODO read from tcpStream
                        WriteClientGameTickPacket(writer, Players[MyUuid], saveDataUpdate, levelState, null);
                        ReadServerGameTickPacket(reader);
                    }
                    tcpClient.Close();
                }
                catch (Exception e) { FatalException = e; }
            });
            listenerThread.Start();
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
                    Disconnect();
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

            if (!Listening)
            {
                return;
            }

            OnUpdate();
        }
        public void Disconnect()
        {
            tcpClient.Close();
        }


        protected override void ProcessDisconnect(Guid puid)
        {
            try
            {
                if (puid != MyUuid && Players.TryGetValue(puid, out var p))
                {
                    //DisconnectedPlayers.TryAdd(puid, DateTime.UtcNow.Ticks);
                    _ = Players.TryRemove(puid, out _);
                }
            }
            catch (InvalidOperationException) { }
            catch (KeyNotFoundException) { } //this can happen if an item is removed by another thread while this thread is iterating over the items
        }
    }
}