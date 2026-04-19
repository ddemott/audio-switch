using System.Windows;
using AudioSwitch.Core.Models;

namespace AudioSwitch.App.Views;

public partial class CloseChoiceWindow : Window
{
    public CloseChoiceWindow()
    {
        InitializeComponent();
    }

    public CloseAction Result { get; private set; } = CloseAction.Cancel;

    public bool RememberChoice => RememberCheckBox.IsChecked == true;

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        Result = CloseAction.MinimizeToTray;
        DialogResult = true;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Result = CloseAction.Exit;
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Result = CloseAction.Cancel;
        DialogResult = false;
    }
}
