using System.Collections;
using System.Reflection;
using System.Text.Json;
using GSYNC.Data.Services;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace GSYNC.App.Primitives;

public sealed partial class ResizableTableView : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(ResizableTableView), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(
        nameof(Subtitle), typeof(string), typeof(ResizableTableView), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty FooterTextProperty = DependencyProperty.Register(
        nameof(FooterText), typeof(string), typeof(ResizableTableView), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable), typeof(ResizableTableView), new PropertyMetadata(null, OnDataChanged));

    public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(
        nameof(Columns), typeof(IList<ResizableTableColumn>), typeof(ResizableTableView), new PropertyMetadata(null, OnDataChanged));

    public static readonly DependencyProperty PersistedStateKeyProperty = DependencyProperty.Register(
        nameof(PersistedStateKey), typeof(string), typeof(ResizableTableView), new PropertyMetadata(string.Empty, OnDataChanged));

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly AppPathService _appPaths = new();
    private int _activeResizeIndex = -1;
    private double _lastPointerX;
    private bool _isDragging;

    public ResizableTableView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            UpdateTextVisibility();
            Rebuild();
        };
        SizeChanged += (_, _) => ApplyColumnWidths();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public string FooterText
    {
        get => (string)GetValue(FooterTextProperty);
        set => SetValue(FooterTextProperty, value);
    }

    public IEnumerable ItemsSource
    {
        get => (IEnumerable)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public IList<ResizableTableColumn> Columns
    {
        get => (IList<ResizableTableColumn>)GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    public string PersistedStateKey
    {
        get => (string)GetValue(PersistedStateKeyProperty);
        set => SetValue(PersistedStateKeyProperty, value);
    }

    private string StateFilePath => Path.Combine(_appPaths.GetAppDataRoot(), "ui-table-state.json");

    private void UpdateTextVisibility()
    {
        if (TitleTextBlock is not null)
        {
            TitleTextBlock.Visibility = string.IsNullOrWhiteSpace(Title) ? Visibility.Collapsed : Visibility.Visible;
        }

        if (SubtitleTextBlock is not null)
        {
            SubtitleTextBlock.Visibility = string.IsNullOrWhiteSpace(Subtitle) ? Visibility.Collapsed : Visibility.Visible;
        }

        if (FooterTextBlock is not null)
        {
            FooterTextBlock.Visibility = string.IsNullOrWhiteSpace(FooterText) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ResizableTableView table)
        {
            table.UpdateTextVisibility();
            table.Rebuild();
        }
    }

    private void Rebuild()
    {
        HeaderGrid.ColumnDefinitions.Clear();
        HeaderGrid.Children.Clear();
        RowsPanel.Children.Clear();

        if (Columns is null)
        {
            return;
        }

        RestoreColumnWidths();

        for (var i = 0; i < Columns.Count; i++)
        {
            HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition());
            var headerCell = CreateHeaderCell(Columns[i], i);
            Grid.SetColumn(headerCell, i);
            HeaderGrid.Children.Add(headerCell);
        }

        if (ItemsSource is not null)
        {
            foreach (var item in ItemsSource)
            {
                var rowBorder = new Border
                {
                    Padding = new Thickness(0, 9, 0, 9),
                    BorderBrush = (Brush)Application.Current.Resources["AppDividerBrush"],
                    BorderThickness = new Thickness(0, 0, 0, 1),
                };

                var rowGrid = new Grid { ColumnSpacing = 12 };
                for (var i = 0; i < Columns.Count; i++)
                {
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition());
                    var cell = CreateCell(item, Columns[i]);
                    Grid.SetColumn(cell, i);
                    rowGrid.Children.Add(cell);
                }

                rowBorder.Child = rowGrid;
                RowsPanel.Children.Add(rowBorder);
            }
        }

        ApplyColumnWidths();
    }

    private void ApplyColumnWidths()
    {
        if (Columns is null || Columns.Count == 0)
        {
            return;
        }

        var widths = GetEffectiveWidths();

        for (var i = 0; i < Columns.Count; i++)
        {
            var width = new GridLength(widths[i]);
            if (i < HeaderGrid.ColumnDefinitions.Count)
            {
                HeaderGrid.ColumnDefinitions[i].Width = width;
            }
        }

        foreach (var child in RowsPanel.Children)
        {
            if (child is Border { Child: Grid rowGrid })
            {
                for (var i = 0; i < Columns.Count && i < rowGrid.ColumnDefinitions.Count; i++)
                {
                    rowGrid.ColumnDefinitions[i].Width = new GridLength(widths[i]);
                }
            }
        }
    }

    private double[] GetEffectiveWidths()
    {
        var widths = Columns.Select(column => Math.Max(column.MinWidth, column.Width)).ToArray();
        var fillIndexes = Columns
            .Select((column, index) => new { column, index })
            .Where(item => item.column.IsFillColumn)
            .Select(item => item.index)
            .ToArray();

        if (fillIndexes.Length == 0)
        {
            return widths;
        }

        var spacingTotal = Math.Max(0, Columns.Count - 1) * HeaderGrid.ColumnSpacing;
        var totalWidth = widths.Sum() + spacingTotal;
        var viewportWidth = Math.Max(0, TableScrollViewer.ActualWidth - 8);
        var extraWidth = viewportWidth - totalWidth;
        if (extraWidth <= 0)
        {
            return widths;
        }

        var extraPerColumn = extraWidth / fillIndexes.Length;
        foreach (var index in fillIndexes)
        {
            widths[index] += extraPerColumn;
        }

        return widths;
    }

    private void RestoreColumnWidths()
    {
        if (Columns is null || string.IsNullOrWhiteSpace(PersistedStateKey))
        {
            return;
        }

        var state = ReadState();
        if (!state.TryGetValue(PersistedStateKey, out var persistedColumns))
        {
            return;
        }

        foreach (var column in Columns)
        {
            if (!TryGetColumnKey(column, out var columnKey) ||
                !persistedColumns.TryGetValue(columnKey, out var width))
            {
                continue;
            }

            column.Width = Math.Max(column.MinWidth, width);
        }
    }

    private void PersistColumnWidths()
    {
        if (Columns is null || string.IsNullOrWhiteSpace(PersistedStateKey))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(_appPaths.GetAppDataRoot());

            var state = ReadState();
            var widths = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var column in Columns)
            {
                if (TryGetColumnKey(column, out var columnKey))
                {
                    widths[columnKey] = Math.Max(column.MinWidth, column.Width);
                }
            }

            state[PersistedStateKey] = widths;
            var json = JsonSerializer.Serialize(state, JsonOptions);
            File.WriteAllText(StateFilePath, json);
        }
        catch
        {
        }
    }

    private Dictionary<string, Dictionary<string, double>> ReadState()
    {
        if (!File.Exists(StateFilePath))
        {
            return new Dictionary<string, Dictionary<string, double>>(StringComparer.Ordinal);
        }

        try
        {
            var json = File.ReadAllText(StateFilePath);
            return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, double>>>(json, JsonOptions)
                ?? new Dictionary<string, Dictionary<string, double>>(StringComparer.Ordinal);
        }
        catch
        {
            return new Dictionary<string, Dictionary<string, double>>(StringComparer.Ordinal);
        }
    }

    private static bool TryGetColumnKey(ResizableTableColumn column, out string key)
    {
        key = !string.IsNullOrWhiteSpace(column.Key)
            ? column.Key
            : !string.IsNullOrWhiteSpace(column.BindingPath)
                ? column.BindingPath
                : column.Header;

        return !string.IsNullOrWhiteSpace(key);
    }

    private FrameworkElement CreateHeaderCell(ResizableTableColumn column, int index)
    {
        var grid = new Grid();
        var text = new TextBlock
        {
            Text = column.Header,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources["AppTextDimBrush"],
            VerticalAlignment = VerticalAlignment.Center,
        };
        grid.Children.Add(text);

        if (index < Columns.Count - 1)
        {
            var handle = new Border
            {
                Width = 12,
                Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(12, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Right,
                Tag = index,
            };
            handle.PointerPressed += Handle_PointerPressed;
            handle.PointerMoved += Handle_PointerMoved;
            handle.PointerReleased += Handle_PointerReleased;
            handle.PointerCaptureLost += Handle_PointerCaptureLost;
            grid.Children.Add(handle);
        }

        return grid;
    }

    private FrameworkElement CreateCell(object item, ResizableTableColumn column)
    {
        if (column.CellTemplate?.LoadContent() is FrameworkElement element)
        {
            element.DataContext = item;
            return element;
        }

        var value = item.GetType().GetProperty(column.BindingPath, BindingFlags.Public | BindingFlags.Instance)?.GetValue(item)?.ToString() ?? string.Empty;
        return new TooltipTextBlock
        {
            Text = value,
            FontWeight = column.IsBold ? FontWeights.SemiBold : FontWeights.Normal,
        };
    }

    private void Handle_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: int index } element)
        {
            _activeResizeIndex = index;
            _lastPointerX = e.GetCurrentPoint(HeaderGrid).Position.X;
            _isDragging = true;
            element.CapturePointer(e.Pointer);
            e.Handled = true;
        }
    }

    private void Handle_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging || _activeResizeIndex < 0 || Columns is null)
        {
            return;
        }

        var currentX = e.GetCurrentPoint(HeaderGrid).Position.X;
        var delta = currentX - _lastPointerX;
        if (Math.Abs(delta) < 0.5)
        {
            return;
        }

        var column = Columns[_activeResizeIndex];
        column.Width = Math.Max(column.MinWidth, column.Width + delta);
        _lastPointerX = currentX;
        ApplyColumnWidths();
        e.Handled = true;
    }

    private void Handle_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            element.ReleasePointerCapture(e.Pointer);
        }

        ResetResizeState();
        PersistColumnWidths();
        e.Handled = true;
    }

    private void Handle_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        ResetResizeState();
        PersistColumnWidths();
    }

    private void ResetResizeState()
    {
        _activeResizeIndex = -1;
        _isDragging = false;
    }

    private void TableScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        ApplyColumnWidths();
    }
}
