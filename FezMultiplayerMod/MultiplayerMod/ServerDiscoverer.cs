﻿using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;

namespace FezGame.MultiplayerMod
{
    /// <summary>
    /// Receives simple INI-formatted data from the specified <see cref="IPAddress"/>
    /// and supplies the entries as a 
    /// <see cref="Dictionary{String, String}"/>
    /// to the <see cref="OnReceiveData"/> event.
    /// </summary>
    internal sealed class ServerDiscoverer : IDisposable
    {
        private readonly IPEndPoint MulticastEndpoint;
        private readonly UdpClient client;
        private readonly Thread listenerThread;
        private volatile bool disposing = false;
        private bool thisDisposed = false;

        /// <summary>
        /// Constructs a new <see cref="ServerDiscoverer"/> with the specified <paramref name="MulticastEndpoint"/>
        /// </summary>
        /// <param name="MulticastEndpoint">The <paramref name="MulticastEndpoint"/> to use for this <see cref="ServerDiscoverer"/></param>
        /// <param name="ProtocolSignature"></param>
        /// <param name="ProtocolVersion"></param>
        internal ServerDiscoverer(IPEndPoint MulticastEndpoint)
        {
            this.MulticastEndpoint = MulticastEndpoint;
            //You must create the UdpClient using the multicast port number otherwise you will not be able to receive multicasted datagrams. 
            client = new UdpClient(AddressFamily.InterNetwork)
            {
                ExclusiveAddressUse = false
            };
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            client.Client.Bind(new IPEndPoint(IPAddress.Any, MulticastEndpoint.Port));
            client.JoinMulticastGroup(MulticastEndpoint.Address);
            listenerThread = new Thread(() =>
            {
                try
                {
                    IAsyncResult asyncResult = null;
                    while (!disposing)
                    {
                        if (asyncResult == null || asyncResult.IsCompleted)
                        {
                            IPEndPoint remoteEndPoint = new IPEndPoint(MulticastEndpoint.Address, MulticastEndpoint.Port);
                            asyncResult = client.BeginReceive((result) =>
                            {
                                if (this.disposing)
                                {
                                    return;
                                }
                                byte[] receivedBytes = client.EndReceive(result, ref remoteEndPoint);
                                string receivedMessage = Encoding.UTF8.GetString(receivedBytes);

                                try
                                {
                                    OnReceiveData(remoteEndPoint, receivedMessage
                                                .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                                                .Select(a => a.Split(new[] { FezSharedTools.SharedConstants.ServerDiscoveryEntrySeparator }, 2, StringSplitOptions.None))
                                                .GroupBy(p => p[0])
                                                .ToDictionary(g => g.Key, g =>
                                                {
                                                    var p = g.FirstOrDefault();
                                                    return (p.Length > 1 ? p[1].Trim() : "");
                                                })
                                            );
                                }
                                catch (Exception e)
                                {
                                    FezSharedTools.SharedTools.LogWarning(typeof(ServerDiscoverer).Name, e.ToString());
                                    System.Diagnostics.Debugger.Launch();
                                }
                            }, null);
                        }
                        else //waiting to receive data
                        {
                            Thread.Sleep(10);
                        }
                    }
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debugger.Break();
                    throw e;
                }
            })
            {
                Name = "Server Discovery Thread"
            };
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
                    client.DropMulticastGroup(MulticastEndpoint.Address);
                    client.Close();
                    // dispose managed state (managed objects)
                }

                // free unmanaged resources (unmanaged objects) and override finalizer
                // set large fields to null
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
