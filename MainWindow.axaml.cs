using Avalonia.Controls;
using System;
using NAudio;
using NAudio.Wave;

namespace Pocket
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            JoinButton.Click += OnJoinButtonClick;
        }

        private void OnJoinButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            StartMicrophoneStream();
            ConnectToServer();
        }

        private void StartMicrophoneStream()
        {
            System.Diagnostics.Debug.WriteLine("Starting mic stream");
            StatusText.Text = "Starting microphone...";

            
            
        }

        private void ConnectToServer()
        {
            //not yet implemented
            System.Diagnostics.Debug.WriteLine("not implemented yet");
        }
    }
}