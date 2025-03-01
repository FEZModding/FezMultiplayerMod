using FezSharedTools;
using System;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Text;
using System.Net.Sockets;
using System.Threading;

namespace FezGame.MultiplayerMod
{
    //TODO test and use this class
    internal sealed class ServerDiscoverer : IDisposable
    {
        private readonly UdpClient client = new UdpClient();
        private readonly string ProtocolSignature;
        private readonly string ProtocolVersion;
        private Thread listenerThread;
        private volatile bool disposing = false;
        private bool thisDisposed = false;

        internal ServerDiscoverer(string ProtocolSignature, string ProtocolVersion)
        {
            this.ProtocolSignature = ProtocolSignature;
            this.ProtocolVersion = ProtocolVersion;
            client.JoinMulticastGroup(SharedConstants.MulticastAddress);
            listenerThread = new Thread(() =>
            {
                try
                {
                    while (!disposing)
                    {
                        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                        byte[] receivedBytes = client.Receive(ref remoteEndPoint);
                        string receivedMessage = Encoding.UTF8.GetString(receivedBytes);
                        OnReceiveData(receivedMessage);
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
        event Action<string> OnReceiveData = (data) => { };

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
                    client.DropMulticastGroup(SharedConstants.MulticastAddress);
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
