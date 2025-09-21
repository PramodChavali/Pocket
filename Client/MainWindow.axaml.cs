using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using Avalonia.Controls;
using NAudio.Wave;

namespace Pocket
{
    public partial class MainWindow : Window
    {
        private WaveInEvent micInput;
        private WaveOutEvent serverOutput;
        private BufferedWaveProvider audioQueue;
        private TcpClient server;
        private NetworkStream audioStream;

        public MainWindow()
        {
            InitializeComponent();
            JoinButton.Click += OnJoinClick;
        }

        private async void OnJoinClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await StartAudioLoopback();
        }

        private async Task StartAudioLoopback()
        {
            // connect to server
            server = new TcpClient();
            await server.ConnectAsync("127.0.0.1", 5000); // later replace with Pi IP
            audioStream = server.GetStream();

            // setup mic capture
            micInput = new WaveInEvent();
            micInput.WaveFormat = new WaveFormat(44100, 16, 1); // 44.1kHz mono
            micInput.DataAvailable += async (s, a) =>
            {
                // send mic bytes to server
                await audioStream.WriteAsync(a.Buffer, 0, a.BytesRecorded);
            };
            micInput.StartRecording();

            // setup playback
            audioQueue = new BufferedWaveProvider(micInput.WaveFormat);
            serverOutput = new WaveOutEvent();
            serverOutput.Init(audioQueue);
            serverOutput.Play();

            // read echoed audio from server
            _ = Task.Run(async () =>
            {
                var buffer = new byte[4096];
                while (true)
                {
                    int bytesRead = await audioStream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                        audioQueue.AddSamples(buffer, 0, bytesRead);
                }
            });
        }
    }
}
