using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GSYNC.App.Infrastructure;

/// <summary>Keeps both panes reachable when a desktop window is too narrow for a split view.</summary>
internal static class ResponsiveSplitLayout
{
    public static void Attach(Grid grid, FrameworkElement secondPane, double breakpoint, GridLength firstWidth, GridLength secondWidth)
    {
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0) });
        bool? previousNarrow = null;
        grid.SizeChanged += (_, args) =>
        {
            var narrow = args.NewSize.Width < breakpoint;
            if (previousNarrow == narrow) return;
            previousNarrow = narrow;
            grid.ColumnDefinitions[0].Width = narrow ? new GridLength(1, GridUnitType.Star) : firstWidth;
            grid.ColumnDefinitions[1].Width = narrow ? new GridLength(0) : secondWidth;
            grid.RowDefinitions[1].Height = narrow ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            Grid.SetColumn(secondPane, narrow ? 0 : 1);
            Grid.SetRow(secondPane, narrow ? 1 : 0);
        };
    }
}
