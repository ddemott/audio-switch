using System.Windows;
using System.Windows.Input;

namespace AudioSwitch.App.Views;

public partial class NameEditorWindow : Window
{
    public NameEditorWindow(string title, string prompt, string initial)
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
        NameBox.Text = initial;
        Loaded += OnLoaded;
    }

    public string EnteredName { get; private set; } = string.Empty;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Activate();
        NameBox.Focus();
        Keyboard.Focus(NameBox);
        NameBox.SelectAll();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        EnteredName = NameBox.Text.Trim();
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
