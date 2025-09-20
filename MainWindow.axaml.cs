using Avalonia.Controls;

namespace Pocket
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            JoinButton.Click += (s, e) => StatusText.Text = "Connecting...";
        }
    }
}