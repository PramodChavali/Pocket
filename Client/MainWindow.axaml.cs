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
    private PocketServer _server;
    private PocketClient _client;

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
        JoinSessionButton.Click += OnJoinSessionClick;
        SettingsButton.Click += OnSettingsButtonClick;
        QuitButton.Click += OnQuitButtonClick;
        //back buttons
        HostSetupBackButton.Click += BackToMenu;
        JoinBackButton.Click += BackToMenu;
        StopSessionButton.Click += StopServer;
    }

    // Update the BackToMenu method for StopSessionButton
    private void BackToMenu(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SetCurrentView(EView.MainMenuView);
        // Clear form fields when going back
        if (sender == HostSetupBackButton)
        {
            HostUsernameTextBox.Text = "";
            LobbyNameTextBox.Text = "";
            PasswordTextBox.Text = "";
        }
        else if (sender == JoinBackButton)
        {
            ServerAddressTextBox.Text = "";
            JoinUsernameTextBox.Text = "";
            JoinSessionNameTextBox.Text = "";
            JoinPasswordTextBox.Text = "";
            _client?.Disconnect();
            _client = null;
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
        // Clean up server and client before quitting
        _server?.Stop();
        _client?.Disconnect();
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

    private async void OnJoinSessionClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string serverAddress = ServerAddressTextBox.Text?.Trim();
        string username = JoinUsernameTextBox.Text?.Trim();
        string sessionName = JoinSessionNameTextBox.Text?.Trim();
        string password = JoinPasswordTextBox.Text?.Trim();

        if (string.IsNullOrEmpty(username))
        {
            StatusText.Text = "Please enter a username";
            return;
        }

        if (string.IsNullOrEmpty(sessionName))
        {
            StatusText.Text = "Please enter a session name";
            return;
        }

        if (string.IsNullOrEmpty(serverAddress))
        {
            StatusText.Text = "Please enter the server address";
            return;
        }

        try
        {
            JoinSessionButton.IsEnabled = false;
            JoinSessionButton.Content = "Connecting...";
            StatusText.Text = "Connecting to session...";

            _client = new PocketClient();
            _client.StatusUpdate += OnClientStatusUpdate;
            _client.ParticipantJoined += OnClientParticipantJoined;
            _client.ParticipantLeft += OnClientParticipantLeft;

            bool success = await _client.ConnectAsync(serverAddress, 8080, username, password, sessionName);

            if (success)
            {
                StatusText.Text = "Connected to session";
            }
            else
            {
                StatusText.Text = "Failed to join session";
                _client = null;
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Connection failed: {ex.Message}";
            _client = null;
        }
        finally
        {
            JoinSessionButton.IsEnabled = true;
            JoinSessionButton.Content = "Join Session";
        }
    }

    private async void StartLocalServer(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Grab the name, username and password
        string hostUsername = HostUsernameTextBox.Text?.Trim();
        string lobbyName = LobbyNameTextBox.Text?.Trim();
        string lobbyPassword = PasswordTextBox.Text?.Trim();

        // Validate input
        if (string.IsNullOrEmpty(hostUsername))
        {
            hostUsername = "Host";
        }
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

            // Add host to participants list
            ParticipantsListBox.Items.Add($"🟢 {hostUsername} (Host)");

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

    // Client event handlers
    private void OnClientStatusUpdate(string status)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusText.Text = status;
        });
    }

    private void OnClientParticipantJoined(string participant)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusText.Text = $"{participant} joined the session";
        });
    }

    private void OnClientParticipantLeft(string participant)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusText.Text = $"{participant} left the session";
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
        _client?.Disconnect();
        base.OnClosed(e);
    }
}