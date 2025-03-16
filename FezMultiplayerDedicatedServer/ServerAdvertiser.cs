using System;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace FezMultiplayerDedicatedServer
{
    //TODO test this class
    /// <summary>
    /// Transmits simple INI-formatted data to the specified <see cref="IPAddress"/>
    /// in the following format:
    /// <code>Protocol=ProtocolInfo
    /// Endpoint={EndpointInfo}
    /// </code>
    /// </summary>
    internal sealed class ServerAdvertiser : IDisposable
    {
        private readonly IPEndPoint MulticastEndpoint;
        private readonly UdpClient client;
        private readonly System.Timers.Timer myTimer = new System.Timers.Timer();
        private volatile byte[] dataToSend;

        /// <summary>
        /// The amount of time, in seconds, to reshare our server info.
        /// </summary>
        private const float Interval = 10f;

        /// <summary>
        /// Constructs a new <see cref="ServerAdvertiser"/> with the specified <paramref name="MulticastEndpoint"/>
        /// </summary>
        /// <param name="MulticastEndpoint">The <paramref name="MulticastEndpoint"/> to use for this <see cref="ServerAdvertiser"/></param>
        /// <param name="MulticastData">The data to be transmitted in INI format</param>
        internal ServerAdvertiser(IPEndPoint MulticastEndpoint, string message)
        {
            this.MulticastEndpoint = MulticastEndpoint;
            client = new UdpClient(new IPEndPoint(IPAddress.Any, 0));//AddressFamily.InterNetwork
            //You do not need to belong to a multicast group to send datagrams to a multicast IP address.
            //client.JoinMulticastGroup(MulticastEndpoint.Address);

            SetMessage(message);

            myTimer.Elapsed += (a, b) => { this.AdvertiseServer(); };
            myTimer.Interval = Interval * 1000; // 1000 ms is one second
            myTimer.Start();
        }

        internal void SetMessage(string message)
        {
            dataToSend = Encoding.UTF8.GetBytes(message);
        }

        private void AdvertiseServer()
        {
            try
            {
                client.Send(dataToSend, dataToSend.Length, MulticastEndpoint);
            }
            catch(Exception e)
            {
                //TODO
                throw e;
            }
        }

        private bool thisDisposed = false;
        private void Dispose(bool disposing)
        {
            if (!thisDisposed)
            {
                if (disposing)
                {
                    //Note: Important to stop the timer before closing the client, so Elapsed doesn't get called
                    myTimer.Stop();
                    myTimer.Dispose();
                    //client.DropMulticastGroup(MulticastEndpoint.Address);
                    client.Close();
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                thisDisposed = true;
            }
        }

        ~ServerAdvertiser()
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
