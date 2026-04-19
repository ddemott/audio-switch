using System.Windows;
using System.Windows.Media;

namespace AudioSwitch.App.Controls;

public static class ListBoxAssist
{
    public static readonly DependencyProperty SelectedBorderBrushProperty =
        DependencyProperty.RegisterAttached(
            "SelectedBorderBrush",
            typeof(Brush),
            typeof(ListBoxAssist),
            new PropertyMetadata(null));

    public static Brush? GetSelectedBorderBrush(DependencyObject element) =>
        (Brush?)element.GetValue(SelectedBorderBrushProperty);

    public static void SetSelectedBorderBrush(DependencyObject element, Brush? value) =>
        element.SetValue(SelectedBorderBrushProperty, value);
}
