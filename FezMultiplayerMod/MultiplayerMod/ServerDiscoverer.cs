using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;

namespace FezGame.MultiplayerMod
{
    //TODO test this class
    /// <summary>
    /// Receives simple INI-formatted data from the specified <see cref="IPAddress"/>
    /// and supplies the entries as a 
    /// <see cref="Dictionary{String, String}"/>
    /// to the <see cref="OnReceiveData"/> event.
    /// </summary>
    internal sealed class ServerDiscoverer : IDisposable
    {
        private readonly IPAddress MulticastAddress;
        private readonly UdpClient client = new UdpClient();
        private readonly Thread listenerThread;
        private volatile bool disposing = false;
        private bool thisDisposed = false;

        /// <summary>
        /// Constructs a new <see cref="ServerDiscoverer"/> with the specified <paramref name="MulticastAddress"/>
        /// </summary>
        /// <param name="MulticastAddress">The <paramref name="MulticastAddress"/> to use for this <see cref="ServerDiscoverer"/></param>
        /// <param name="ProtocolSignature"></param>
        /// <param name="ProtocolVersion"></param>
        internal ServerDiscoverer(IPAddress MulticastAddress)
        {
            this.MulticastAddress = MulticastAddress;
            client.JoinMulticastGroup(MulticastAddress);
            listenerThread = new Thread(() =>
            {
                try
                {
                    while (!disposing)
                    {
                        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                        byte[] receivedBytes = client.Receive(ref remoteEndPoint);
                        string receivedMessage = Encoding.UTF8.GetString(receivedBytes);

                        OnReceiveData(remoteEndPoint, receivedMessage.Split('\n').Select(a => a.Split(new[] { '=' }, 2)).ToDictionary(p => p[0], p => p[1]));
                    }
                }
                catch (Exception e)
                {
                    //TODO
                    throw e;
                }
            });
            listenerThread.Start();
        }
        /// <summary>
        /// Receives simple INI-formatted data from the <see cref="IPAddress"/> associated with this <see cref="ServerDiscoverer"/>
        /// </summary>
        public event Action<IPEndPoint, Dictionary<string,string>> OnReceiveData = (remoteEndPoint, data) => { };

        private void Dispose(bool disposing)
        {
            if (!thisDisposed)
            {
                if (disposing)
                {
                    this.disposing = true;//let child threads know it's disposing time
                    Thread.Sleep(1000);//try to wait for child threads to stop on their own
                    if (listenerThread.IsAlive)
                    {
                        listenerThread.Abort();//assume the thread is stuck and forcibly terminate it
                    }
                    client.DropMulticastGroup(MulticastAddress);
                    client.Close();
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                thisDisposed = true;
            }
        }

        ~ServerDiscoverer()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
