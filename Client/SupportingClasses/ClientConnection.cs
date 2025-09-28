using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Pocket
{
    public class ClientConnection
    {
        public string Id { get; }
        public TcpClient TcpClient { get; }
        public NetworkStream Stream { get; }
        public string DisplayName { get; set; }
        public IPEndPoint UdpEndpoint { get; set; }
        public bool IsConnected { get; set; } = true;

        public ClientConnection(string id, TcpClient tcpClient)
        {
            Id = id;
            TcpClient = tcpClient;
            Stream = tcpClient.GetStream();
            DisplayName = $"User{id[..8]}";
        }
    }
}
