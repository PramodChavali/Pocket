using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;

namespace Pocket
{
    internal class PocketServer
    {
        private TcpListener _listener;
        private UdpClient _udpServer;
        private bool _isRunning;
        private readonly string _sessionName;
        private readonly string _password;
        private readonly ConcurrentDictionary<string, ClientConnection> _clients = new();
        private readonly int _tcpPort = 8080;
        private readonly int _udpPort = 8081;

        public event Action<string> ParticipantJoined;
        public event Action<string> ParticipantLeft;
        public event Action<string> StatusUpdate;

        public PocketServer(string sessionName, string password)
        {
            _sessionName = sessionName;
            _password = password;
        }

        public async Task StartAsync()
        {
            try
            {
                // Start TCP server for control messages (join/leave/etc)
                _listener = new TcpListener(IPAddress.Any, _tcpPort);
                _listener.Start();

                // Start UDP server for audio data
                _udpServer = new UdpClient(_udpPort);

                _isRunning = true;

                StatusUpdate?.Invoke($"Server started - TCP:{_tcpPort}, UDP:{_udpPort}");
                StatusUpdate?.Invoke($"Session: {_sessionName}");

                // Start accepting connections
                _ = Task.Run(AcceptTcpClientsAsync);
                _ = Task.Run(HandleUdpAudioAsync);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to start server: {ex.Message}");
            }
        }

        private async Task AcceptTcpClientsAsync()
        {
            while (_isRunning)
            {
                try
                {
                    var tcpClient = await _listener.AcceptTcpClientAsync();
                    var clientId = Guid.NewGuid().ToString();

                    var clientConnection = new ClientConnection(clientId, tcpClient);

                    // Handle client in background
                    _ = Task.Run(() => HandleTcpClient(clientConnection));
                }
                catch (Exception ex) when (_isRunning)
                {
                    StatusUpdate?.Invoke($"Error accepting client: {ex.Message}");
                }
            }
        }

        private async Task HandleTcpClient(ClientConnection client)
        {
            var buffer = new byte[4096];

            try
            {
                while (client.IsConnected && _isRunning)
                {
                    var bytesRead = await client.Stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    var messageJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    var message = JsonSerializer.Deserialize<ControlMessage>(messageJson);

                    await ProcessControlMessage(client, message);
                }
            }
            catch (Exception ex)
            {
                StatusUpdate?.Invoke($"Client {client.DisplayName} disconnected: {ex.Message}");
            }
            finally
            {
                DisconnectClient(client);
            }
        }

        private async Task ProcessControlMessage(ClientConnection client, ControlMessage message)
        {
            switch (message.Type)
            {
                case MessageType.Join:
                    var joinData = JsonSerializer.Deserialize<JoinData>(message.Data);

                    // Validate password
                    if (!string.IsNullOrEmpty(_password) && joinData.Password != _password)
                    {
                        await SendControlMessage(client, new ControlMessage
                        {
                            Type = MessageType.JoinRejected,
                            Data = "Invalid password"
                        });
                        client.TcpClient.Close();
                        return;
                    }

                    // Add client
                    client.DisplayName = joinData.Username ?? $"User{client.Id[..8]}";
                    _clients[client.Id] = client;

                    // Send join confirmation
                    await SendControlMessage(client, new ControlMessage
                    {
                        Type = MessageType.JoinAccepted,
                        Data = JsonSerializer.Serialize(new JoinAcceptedData
                        {
                            ClientId = client.Id,
                            UdpPort = _udpPort,
                            ParticipantCount = _clients.Count
                        })
                    });

                    ParticipantJoined?.Invoke(client.DisplayName);
                    StatusUpdate?.Invoke($"{client.DisplayName} joined ({_clients.Count} total)");

                    // Notify other clients
                    await BroadcastMessage(new ControlMessage
                    {
                        Type = MessageType.ParticipantJoined,
                        Data = client.DisplayName
                    }, client.Id);
                    break;

                case MessageType.AudioReady:
                    var audioData = JsonSerializer.Deserialize<AudioReadyData>(message.Data);
                    client.UdpEndpoint = new IPEndPoint(
                        ((IPEndPoint)client.TcpClient.Client.RemoteEndPoint).Address,
                        audioData.UdpPort);

                    StatusUpdate?.Invoke($"{client.DisplayName} audio ready");
                    break;

                case MessageType.Leave:
                    DisconnectClient(client);
                    break;
            }
        }

        private async Task HandleUdpAudioAsync()
        {
            while (_isRunning)
            {
                try
                {
                    var result = await _udpServer.ReceiveAsync();
                    var audioPacket = AudioPacket.Deserialize(result.Buffer);
                    audioPacket.SenderEndpoint = result.RemoteEndPoint;

                    // Find sender and relay to all other clients
                    var sender = _clients.Values.FirstOrDefault(c =>
                        c.UdpEndpoint?.Equals(result.RemoteEndPoint) == true);

                    if (sender != null)
                    {
                        await RelayAudioToOthers(audioPacket, sender.Id);
                    }
                }
                catch (Exception ex) when (_isRunning)
                {
                    StatusUpdate?.Invoke($"UDP audio error: {ex.Message}");
                }
            }
        }

        private async Task RelayAudioToOthers(AudioPacket audioPacket, string senderId)
        {
            foreach (var client in _clients.Values.Where(c =>
                c.IsConnected && c.Id != senderId && c.UdpEndpoint != null))
            {
                try
                {
                    var serialized = audioPacket.Serialize();
                    await _udpServer.SendAsync(serialized, serialized.Length, client.UdpEndpoint);
                }
                catch (Exception ex)
                {
                    StatusUpdate?.Invoke($"Failed to relay audio to {client.DisplayName}: {ex.Message}");
                }
            }
        }

        private async Task SendControlMessage(ClientConnection client, ControlMessage message)
        {
            try
            {
                var json = JsonSerializer.Serialize(message);
                var data = Encoding.UTF8.GetBytes(json);
                await client.Stream.WriteAsync(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                StatusUpdate?.Invoke($"Failed to send message to {client.DisplayName}: {ex.Message}");
            }
        }

        private async Task BroadcastMessage(ControlMessage message, string excludeClientId = null)
        {
            foreach (var client in _clients.Values.Where(c =>
                c.IsConnected && c.Id != excludeClientId))
            {
                await SendControlMessage(client, message);
            }
        }

        private void DisconnectClient(ClientConnection client)
        {
            client.IsConnected = false;
            client.TcpClient?.Close();

            if (_clients.TryRemove(client.Id, out _))
            {
                ParticipantLeft?.Invoke(client.DisplayName);
                StatusUpdate?.Invoke($"{client.DisplayName} left ({_clients.Count} total)");

                // Notify other clients
                _ = Task.Run(() => BroadcastMessage(new ControlMessage
                {
                    Type = MessageType.ParticipantLeft,
                    Data = client.DisplayName
                }));
            }
        }

        public void Stop()
        {
            _isRunning = false;

            // Disconnect all clients
            foreach (var client in _clients.Values.ToList())
            {
                DisconnectClient(client);
            }

            _listener?.Stop();
            _udpServer?.Close();

            StatusUpdate?.Invoke("Server stopped");
        }
    }
}
