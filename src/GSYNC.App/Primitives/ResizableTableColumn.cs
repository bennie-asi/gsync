using Microsoft.UI.Xaml;

namespace GSYNC.App.Primitives;

public sealed class ResizableTableColumn
{
    public string Key { get; set; } = string.Empty;

    public string Header { get; set; } = string.Empty;

    public string BindingPath { get; set; } = string.Empty;

    public double Width { get; set; } = 120;

    public double MinWidth { get; set; } = 72;

    public bool IsBold { get; set; }

    public bool IsFillColumn { get; set; }

    public DataTemplate? CellTemplate { get; set; }
}
