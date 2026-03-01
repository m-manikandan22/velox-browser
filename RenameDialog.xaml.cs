using System.Windows;
using System.Windows.Input;

namespace VeloxBrowser
{
    public partial class RenameDialog : Window
    {
        public string Result { get; private set; } = string.Empty;

        public RenameDialog(string currentName, string dialogTitle = "Rename Profile", string buttonLabel = "Rename")
        {
            InitializeComponent();
            NameBox.Text = currentName;
            Loaded += (_, _) =>
            {
                this.Title = dialogTitle;
                DialogTitle.Text = dialogTitle;
                OkButton.Content = buttonLabel;
                NameBox.Focus();
                NameBox.SelectAll();
            };
        }

        private void Rename_Click(object sender, RoutedEventArgs e)
        {
            Result = NameBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(Result))
            {
                DialogResult = true;
                Close();
            }
        }

        private void NameBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Rename_Click(sender, e);
            }
        }
    }
}
