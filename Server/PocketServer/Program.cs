using Microsoft.VisualBasic;
using NAudio.Wave;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        var listener = new TcpListener(IPAddress.Loopback, 5000);
        listener.Start();
        Console.WriteLine("Server listening on port 5000...");

        using var client = await listener.AcceptTcpClientAsync();
        Console.WriteLine("Client connected.");

        using var networkStream = client.GetStream();

        // Setup audio playback
        var waveFormat = new WaveFormat(44100, 16, 1); // 44.1kHz, 16-bit, mono
        var bufferProvider = new BufferedWaveProvider(waveFormat);
        using var waveOut = new WaveOutEvent();
        waveOut.Init(bufferProvider);
        waveOut.Play();

        byte[] buffer = new byte[4096];
        int bytesRead;
        while ((bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            bufferProvider.AddSamples(buffer, 0, bytesRead);
        }
    }
}
