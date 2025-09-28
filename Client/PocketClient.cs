using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Pocket
{
    public class PocketClient
    {
        private TcpClient _tcpClient;
        private UdpClient _udpClient;
        private NetworkStream _stream;
        private bool _isConnected;
        private string _clientId;

        public event Action<string> StatusUpdate;
        public event Action<string> ParticipantJoined;
        public event Action<string> ParticipantLeft;

        public async Task<bool> ConnectAsync(string serverAddress, int serverPort,
            string username, string password, string sessionName)
        {
            try
            {
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(serverAddress, serverPort);
                _stream = _tcpClient.GetStream();
                _isConnected = true;

                // Start listening for messages
                _ = Task.Run(ListenForMessages);

                // Send join request
                var joinMessage = new ControlMessage
                {
                    Type = MessageType.Join,
                    Data = JsonSerializer.Serialize(new JoinData
                    {
                        Username = username,
                        Password = password,
                        SessionName = sessionName
                    })
                };

                await SendControlMessage(joinMessage);
                return true;
            }
            catch (Exception ex)
            {
                StatusUpdate?.Invoke($"Connection failed: {ex.Message}");
                return false;
            }
        }

        private async Task ListenForMessages()
        {
            var buffer = new byte[4096];

            try
            {
                while (_isConnected)
                {
                    var bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    var messageJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    var message = JsonSerializer.Deserialize<ControlMessage>(messageJson);

                    await ProcessMessage(message);
                }
            }
            catch (Exception ex)
            {
                StatusUpdate?.Invoke($"Connection error: {ex.Message}");
            }
            finally
            {
                Disconnect();
            }
        }

        private async Task ProcessMessage(ControlMessage message)
        {
            switch (message.Type)
            {
                case MessageType.JoinAccepted:
                    var acceptedData = JsonSerializer.Deserialize<JoinAcceptedData>(message.Data);
                    _clientId = acceptedData.ClientId;

                    // Setup UDP for audio
                    _udpClient = new UdpClient(0); // Use any available port
                    var localPort = ((IPEndPoint)_udpClient.Client.LocalEndPoint).Port;

                    // Tell server we're ready for audio
                    await SendControlMessage(new ControlMessage
                    {
                        Type = MessageType.AudioReady,
                        Data = JsonSerializer.Serialize(new AudioReadyData
                        {
                            UdpPort = localPort
                        })
                    });

                    StatusUpdate?.Invoke($"Connected! ({acceptedData.ParticipantCount} participants)");
                    break;

                case MessageType.JoinRejected:
                    StatusUpdate?.Invoke($"Join rejected: {message.Data}");
                    Disconnect();
                    break;

                case MessageType.ParticipantJoined:
                    ParticipantJoined?.Invoke(message.Data);
                    break;

                case MessageType.ParticipantLeft:
                    ParticipantLeft?.Invoke(message.Data);
                    break;
            }
        }

        private async Task SendControlMessage(ControlMessage message)
        {
            var json = JsonSerializer.Serialize(message);
            var data = Encoding.UTF8.GetBytes(json);
            await _stream.WriteAsync(data, 0, data.Length);
        }

        public void Disconnect()
        {
            _isConnected = false;
            _udpClient?.Close();
            _tcpClient?.Close();
            StatusUpdate?.Invoke("Disconnected");
        }
    }
}
