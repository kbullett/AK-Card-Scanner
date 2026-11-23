using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace AKCardScanner;

internal static class MessageDialog
{
    public static Task Show(Window owner, string text, string title)
    {
        var ok = new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Right, IsDefault = true, MinWidth = 80 };
        var win = new Window
        {
            Title = title,
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 16,
                MaxWidth = 460,
                Children = { new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }, ok }
            }
        };
        ok.Click += (_, _) => win.Close();
        return win.ShowDialog(owner);
    }
}
