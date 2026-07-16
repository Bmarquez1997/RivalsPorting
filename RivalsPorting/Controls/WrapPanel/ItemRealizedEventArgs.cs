
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace RivalsPorting.Controls.WrapPanel;

public class ItemRealizedEventArgs : RoutedEventArgs
{
    public ItemRealizedEventArgs(int index, Control container, object? item)
    {
        Index = index;
        Container = container;
        Item = item;
    }

    public int Index { get; }

    public Control Container { get; }

    public object? Item { get; }
}