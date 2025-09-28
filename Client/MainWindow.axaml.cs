using Avalonia.Controls;
using Avalonia.Threading;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Pocket;

public partial class MainWindow : Window
{
    private List<Control> allViews;
    private PocketServer _server; // Add this field

    public enum EView
    {
        MainMenuView = 0,
        HostSetupView,
        HostLobbyView,
        JoinLobbyView
    }

    public MainWindow()
    {
        InitializeComponent();
        InitializeViews();
        HostButton.Click += OnHostButtonClick;
        CreateSessionButton.Click += StartLocalServer;
        JoinButton.Click += OnJoinButtonClick;
        SettingsButton.Click += OnSettingsButtonClick;
        QuitButton.Click += OnQuitButtonClick;
        //back buttons
        HostSetupBackButton.Click += BackToMenu;
        JoinBackButton.Click += BackToMenu;
        StopSessionButton.Click += StopServer; // Update this line
    }

    // Update the BackToMenu method for StopSessionButton
    private void BackToMenu(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SetCurrentView(EView.MainMenuView);
        // Clear form fields when going back
        if (sender == HostSetupBackButton)
        {
            LobbyNameTextBox.Text = "";
            PasswordTextBox.Text = "";
        }
    }

    // New method specifically for stopping the server
    private void StopServer(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _server?.Stop();
        _server = null;

        // Clear participants list
        ParticipantsListBox.Items.Clear();

        // Update status and return to main menu
        StatusText.Text = "Server stopped";
        SetCurrentView(EView.MainMenuView);
    }

    private void OnQuitButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Clean up server before quitting
        _server?.Stop();
        Close();
    }

    private void OnSettingsButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        throw new NotImplementedException();
    }

    private void OnJoinButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SetCurrentView(EView.JoinLobbyView);
    }

    private async void StartLocalServer(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Grab the name and password
        string lobbyName = LobbyNameTextBox.Text?.Trim();
        string lobbyPassword = PasswordTextBox.Text?.Trim();

        // Validate input
        if (string.IsNullOrEmpty(lobbyName))
        {
            StatusText.Text = "Please enter a session name";
            return;
        }

        try
        {
            // Disable button and show loading state
            CreateSessionButton.IsEnabled = false;
            CreateSessionButton.Content = "Starting...";
            StatusText.Text = "Starting session...";

            // Create and start server
            _server = new PocketServer(lobbyName, lobbyPassword);

            // Wire up server events
            _server.ParticipantJoined += OnParticipantJoined;
            _server.ParticipantLeft += OnParticipantLeft;
            _server.StatusUpdate += OnStatusUpdate;

            // Start the server
            await _server.StartAsync();

            // Update lobby info
            LobbyInfoText.Text = $"Session: {lobbyName}";
            if (!string.IsNullOrEmpty(lobbyPassword))
            {
                LobbyInfoText.Text += " (Password Protected)";
            }

            // Switch to lobby view
            SetCurrentView(EView.HostLobbyView);

            StatusText.Text = "Session ready - waiting for participants";
        }
        catch (Exception ex)
        {
            // Handle startup errors
            CreateSessionButton.Content = "Create Session";
            CreateSessionButton.IsEnabled = true;
            StatusText.Text = $"Failed to start session: {ex.Message}";

            // Clean up failed server
            _server?.Stop();
            _server = null;
        }
    }

    // Server event handlers
    private void OnParticipantJoined(string participant)
    {
        // Update UI on main thread
        Dispatcher.UIThread.Post(() =>
        {
            ParticipantsListBox.Items.Add($"🟢 {participant}");
        });
    }

    private void OnParticipantLeft(string participant)
    {
        // Update UI on main thread
        Dispatcher.UIThread.Post(() =>
        {
            // Find and remove the participant
            for (int i = 0; i < ParticipantsListBox.Items.Count; i++)
            {
                var item = ParticipantsListBox.Items[i]?.ToString();
                if (item?.Contains(participant) == true)
                {
                    ParticipantsListBox.Items.RemoveAt(i);
                    break;
                }
            }
        });
    }

    private void OnStatusUpdate(string status)
    {
        // Update UI on main thread
        Dispatcher.UIThread.Post(() =>
        {
            StatusText.Text = status;
        });
    }

    private void OnHostButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SetCurrentView(EView.HostSetupView);
    }

    private void SetCurrentView(EView targetView)
    {
        foreach (Control view in allViews)
        {
            if (view != allViews[(int)targetView])
            {
                view.IsVisible = false;
            }
            else
            {
                view.IsVisible = true;
            }
        }

        // Reset button state when switching views
        if (targetView == EView.HostSetupView)
        {
            CreateSessionButton.IsEnabled = true;
            CreateSessionButton.Content = "Create Session";
        }
    }

    private void InitializeViews()
    {
        allViews = new List<Control>
        {
            MainMenuView,
            HostSetupView,
            HostLobbyView,
            JoinLobbyView
        };
    }

    // Clean up when window closes
    protected override void OnClosed(EventArgs e)
    {
        _server?.Stop();
        base.OnClosed(e);
    }
}