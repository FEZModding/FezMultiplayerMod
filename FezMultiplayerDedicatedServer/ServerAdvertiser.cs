using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace FezMultiplayerDedicatedServer
{
    //TODO test and use this class
    /// <summary>
    /// Transmits simple INI-formatted data to the specified <see cref="IPAddress"/>
    /// in the following format:
    /// <code>Protocol=ProtocolInfo
    /// Endpoint={EndpointInfo}
    /// </code>
    /// </summary>
    internal sealed class ServerAdvertiser : IDisposable
    {
        private readonly IPAddress MulticastAddress;
        private readonly UdpClient client = new UdpClient();
        private readonly System.Timers.Timer myTimer = new System.Timers.Timer();
        private readonly byte[] dataToSend;

        /// <summary>
        /// The amount of time, in seconds, to reshare our server info.
        /// </summary>
        private const float Interval = 10f;

        /// <summary>
        /// Constructs a new <see cref="ServerAdvertiser"/> with the specified <paramref name="MulticastAddress"/>
        /// </summary>
        /// <param name="MulticastAddress">The <paramref name="MulticastAddress"/> to use for this <see cref="ServerAdvertiser"/></param>
        /// <param name="ProtocolInfo"></param>
        /// <param name="EndpointInfo"></param>
        internal ServerAdvertiser(IPAddress MulticastAddress, string ProtocolInfo, string EndpointInfo)
        {
            this.MulticastAddress = MulticastAddress;
            client.JoinMulticastGroup(MulticastAddress);

            // Prepare the message to be sent.
            string message = $"Protocol={ProtocolInfo}\n" +
                    $"Endpoint={EndpointInfo}\n";
            dataToSend = Encoding.UTF8.GetBytes(message);

            myTimer.Elapsed += (a, b) => { this.AdvertiseServer(); };
            myTimer.Interval = Interval * 1000; // 1000 ms is one second
            myTimer.Start();

        }

        private void AdvertiseServer()
        {
            try
            {
                client.Send(dataToSend, dataToSend.Length, new IPEndPoint(MulticastAddress, 0));
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
                    client.DropMulticastGroup(MulticastAddress);
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
