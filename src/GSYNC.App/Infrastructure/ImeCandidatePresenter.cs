using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace GSYNC.App.Infrastructure;

/// <summary>
/// WinUI requests UI-less TSF candidates. WeType provides the candidates but does not
/// display them in this mode, so render only that input method's hidden candidate list.
/// Candidate contents remain in memory and are never logged or persisted.
/// </summary>
public sealed class ImeCandidatePresenter : IDisposable
{
    private static readonly Guid WeTypeCandidateId = new("ed70ecde-c8aa-4170-96cc-0090dea8aec2");
    private readonly FrameworkElement _root;
    private readonly IntPtr _windowHandle;
    private readonly DispatcherQueue _dispatcher;
    private readonly Popup _popup;
    private readonly Border _border;
    private readonly StackPanel _items;
    private readonly CandidateSink _sink;
    private object? _threadManager;
    private ITfSource? _source;
    private ITfUIElementMgr? _elements;
    private uint _cookie;
    private uint? _activeId;
    private uint? _pendingId;
    private TextBox? _activeTextBox;
    private bool _disposed;

    public ImeCandidatePresenter(FrameworkElement root, IntPtr windowHandle)
    {
        _root = root;
        _windowHandle = windowHandle;
        _dispatcher = root.DispatcherQueue;
        _items = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        var scrolling = new ScrollViewer
        {
            Content = _items,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Enabled,
            VerticalScrollMode = ScrollMode.Disabled,
        };
        _border = new Border
        {
            Background = (Brush)Application.Current.Resources["AppSurfaceRaisedBrush"],
            BorderBrush = (Brush)Application.Current.Resources["AppDividerBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(5),
            Child = scrolling,
        };
        _popup = new Popup { Child = _border, IsLightDismissEnabled = false };
        _sink = new CandidateSink(this);
        _root.Loaded += RootLoaded;
        _root.GotFocus += RootGotFocus;
        _root.LostFocus += RootLostFocus;
        _root.SizeChanged += RootSizeChanged;
    }

    private void RootLoaded(object sender, RoutedEventArgs e) => QueueInstall();
    private void RootGotFocus(object sender, RoutedEventArgs e) => QueueInstall();

    private void RootLostFocus(object sender, RoutedEventArgs e)
    {
        _dispatcher.TryEnqueue(() =>
        {
            if (_activeTextBox is not null && !ReferenceEquals(FocusedTextBox(), _activeTextBox)) Hide();
        });
    }

    private void RootSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_popup.IsOpen) PositionPopup();
    }

    private void QueueInstall()
    {
        if (!_disposed && _source is null) _dispatcher.TryEnqueue(Install);
    }

    private void Install()
    {
        if (_disposed || _source is not null) return;
        IntPtr pointer = IntPtr.Zero;
        try
        {
            if (TF_GetThreadMgr(out pointer) != 0 || pointer == IntPtr.Zero) return;
            var manager = Marshal.GetObjectForIUnknown(pointer);
            var source = (ITfSource)manager;
            var elements = (ITfUIElementMgr)manager;
            var id = typeof(ITfUIElementSink).GUID;
            if (source.AdviseSink(ref id, _sink, out var cookie) != 0)
            {
                Marshal.ReleaseComObject(manager);
                return;
            }
            _threadManager = manager;
            _source = source;
            _elements = elements;
            _cookie = cookie;
        }
        catch (Exception exception) when (exception is COMException or InvalidCastException or DllNotFoundException or EntryPointNotFoundException)
        {
            // Text input stays usable through the system if TSF is unavailable.
        }
        finally
        {
            if (pointer != IntPtr.Zero) Marshal.Release(pointer);
        }
    }

    private TextBox? FocusedTextBox() =>
        _root.XamlRoot is null ? null : FocusManager.GetFocusedElement(_root.XamlRoot) as TextBox;

    private void Begin(uint id, bool systemShowsUi)
    {
        if (!systemShowsUi && !_disposed) _dispatcher.TryEnqueue(() => { _pendingId = id; Render(id, true); });
    }

    private void Update(uint id)
    {
        if (!_disposed) _dispatcher.TryEnqueue(() =>
        {
            if (_activeId == id || _pendingId == id) Render(id, _activeId != id);
        });
    }

    private void End(uint id)
    {
        if (!_disposed) _dispatcher.TryEnqueue(() => { if (_activeId == id || _pendingId == id) Hide(); });
    }

    private void Render(uint id, bool isBeginning)
    {
        if (_disposed || _elements is null || (!isBeginning && _activeId != id)) return;
        object? element = null;
        try
        {
            if (_elements.GetUIElement(id, out element) != 0 || element is not ITfCandidateList candidate) return;
            if (isBeginning)
            {
                if (!IsWeType(candidate) || FocusedTextBox() is not { } textBox) return;
                _activeId = id;
                _activeTextBox = textBox;
            }
            if (_activeId != id || !ReferenceEquals(FocusedTextBox(), _activeTextBox)) { Hide(); return; }
            if (candidate.GetCount(out var count) != 0 || count == 0) { HidePopup(); return; }
            count = Math.Min(count, 256);
            var selected = candidate.GetSelection(out var index) == 0 && index < count ? index : 0;
            var (start, end) = GetVisiblePage(candidate, count, selected);
            _items.Children.Clear();
            for (var itemIndex = start; itemIndex < end; itemIndex++)
            {
                if (candidate.GetString(itemIndex, out var value) != 0 || string.IsNullOrEmpty(value)) continue;
                var button = new Button
                {
                    Content = $"{(itemIndex - start + 1) % 10}. {value}",
                    Padding = new Thickness(7, 4, 7, 4),
                    MinWidth = 0,
                    MinHeight = 30,
                    IsTabStop = false,
                    AllowFocusOnInteraction = false,
                    Background = itemIndex == selected
                        ? (Brush)Application.Current.Resources["AppPrimaryBrush"]
                        : (Brush)Application.Current.Resources["AppSurfaceRaisedBrush"],
                    Foreground = itemIndex == selected
                        ? new SolidColorBrush(Microsoft.UI.Colors.Black)
                        : (Brush)Application.Current.Resources["AppTextPrimaryBrush"],
                };
                var chosen = itemIndex - start;
                button.Click += (_, _) => Select(id, chosen);
                _items.Children.Add(button);
            }
            if (_items.Children.Count == 0) { HidePopup(); return; }
            _popup.XamlRoot = _root.XamlRoot;
            PositionPopup();
            _popup.IsOpen = true;
        }
        catch (Exception exception) when (exception is COMException or ArgumentException or InvalidOperationException)
        {
            Hide();
        }
        finally
        {
            if (element is not null && Marshal.IsComObject(element)) Marshal.ReleaseComObject(element);
        }
    }

    private static bool IsWeType(ITfCandidateList candidate)
    {
        if (candidate.GetGUID(out var id) == 0 && id == WeTypeCandidateId) return true;
        return candidate.GetDescription(out var description) == 0 &&
            description?.Contains("微信输入法", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static (uint Start, uint End) GetVisiblePage(ITfCandidateList candidate, uint count, uint selected)
    {
        var slots = checked((int)count + 1);
        var buffer = Marshal.AllocCoTaskMem(slots * sizeof(int));
        try
        {
            if (candidate.GetPageIndex(buffer, (uint)slots, out var pages) == 0 && pages > 0 && pages <= slots &&
                candidate.GetCurrentPage(out var current) == 0 && current < pages)
            {
                var offsets = new int[pages];
                Marshal.Copy(buffer, offsets, 0, offsets.Length);
                var start = offsets[current];
                var end = current + 1 < pages ? offsets[current + 1] : (int)count;
                if (start >= 0 && start < end && end <= count) return ((uint)start, Math.Min((uint)end, (uint)start + 10));
            }
        }
        finally { Marshal.FreeCoTaskMem(buffer); }
        var fallbackStart = selected / 9 * 9;
        return (fallbackStart, Math.Min(count, fallbackStart + 9));
    }

    private void Select(uint id, uint indexOnPage)
    {
        if (_disposed || _activeId != id || indexOnPage > 9 || !ReferenceEquals(FocusedTextBox(), _activeTextBox) ||
            GetForegroundWindow() != _windowHandle || IsModifierDown()) return;

        // WeType can report a successful TSF Finalize while leaving its composition active.
        // Route mouse selection through the same numbered key that WeType handles itself.
        var key = (ushort)(indexOnPage == 9 ? '0' : '1' + indexOnPage);
        var inputs = new[]
        {
            new NativeInput { Type = 1, Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = key } } },
            new NativeInput { Type = 1, Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = key, Flags = 2 } } },
        };
        _ = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeInput>());
    }

    private static bool IsModifierDown() =>
        (GetAsyncKeyState(0x10) & 0x8000) != 0 || // Shift
        (GetAsyncKeyState(0x11) & 0x8000) != 0 || // Control
        (GetAsyncKeyState(0x12) & 0x8000) != 0 || // Alt
        (GetAsyncKeyState(0x5b) & 0x8000) != 0 || // Left Windows key
        (GetAsyncKeyState(0x5c) & 0x8000) != 0;   // Right Windows key

    private void PositionPopup()
    {
        if (_activeTextBox is null || _root.XamlRoot is null) return;
        try
        {
            var location = _activeTextBox.TransformToVisual(_root).TransformPoint(new Point(0, _activeTextBox.ActualHeight));
            var width = Math.Max(1, _root.ActualWidth - 16);
            _border.MaxWidth = width;
            _border.Measure(new Size(width, double.PositiveInfinity));
            var popupWidth = Math.Min(width, _border.DesiredSize.Width);
            var popupHeight = _border.DesiredSize.Height;
            _popup.HorizontalOffset = Math.Clamp(location.X, 0, Math.Max(0, _root.ActualWidth - popupWidth));
            var below = location.Y + popupHeight <= _root.ActualHeight;
            _popup.VerticalOffset = Math.Clamp(below ? location.Y : location.Y - _activeTextBox.ActualHeight - popupHeight,
                0, Math.Max(0, _root.ActualHeight - popupHeight));
        }
        catch (ArgumentException) { Hide(); }
    }

    private void Hide()
    {
        HidePopup();
        _activeId = null;
        _pendingId = null;
        _activeTextBox = null;
    }

    private void HidePopup()
    {
        _popup.IsOpen = false;
        _items.Children.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _root.Loaded -= RootLoaded;
        _root.GotFocus -= RootGotFocus;
        _root.LostFocus -= RootLostFocus;
        _root.SizeChanged -= RootSizeChanged;
        Hide();
        try { _source?.UnadviseSink(_cookie); }
        catch (COMException) { }
        if (_threadManager is not null && Marshal.IsComObject(_threadManager)) Marshal.ReleaseComObject(_threadManager);
        _elements = null;
        _source = null;
        _threadManager = null;
    }

    [DllImport("msctf.dll", PreserveSig = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int TF_GetThreadMgr(out IntPtr manager);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint SendInput(uint count, NativeInput[] inputs, int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeInput { public uint Type; public InputUnion Data; }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KeyboardInput Keyboard;
        [FieldOffset(0)] public MouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint Data;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [ComImport, Guid("4ea48a35-60ae-446f-8fd6-e6a8d82459f7"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITfSource
    {
        [PreserveSig] int AdviseSink(ref Guid id, [MarshalAs(UnmanagedType.IUnknown)] object sink, out uint cookie);
        [PreserveSig] int UnadviseSink(uint cookie);
    }

    [ComImport, Guid("ea1ea135-19df-11d7-a6d2-00065b84435c"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITfUIElementMgr
    {
        [PreserveSig] int BeginUIElement(IntPtr element, ref int show, out uint id);
        [PreserveSig] int UpdateUIElement(uint id);
        [PreserveSig] int EndUIElement(uint id);
        [PreserveSig] int GetUIElement(uint id, [MarshalAs(UnmanagedType.IUnknown)] out object? element);
        [PreserveSig] int EnumUIElements(out IntPtr enumerator);
    }

    [ComImport, Guid("ea1ea138-19df-11d7-a6d2-00065b84435c"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITfCandidateList
    {
        [PreserveSig] int GetDescription([MarshalAs(UnmanagedType.BStr)] out string description);
        [PreserveSig] int GetGUID(out Guid guid);
        [PreserveSig] int Show([MarshalAs(UnmanagedType.Bool)] bool show);
        [PreserveSig] int IsShown([MarshalAs(UnmanagedType.Bool)] out bool shown);
        [PreserveSig] int GetUpdatedFlags(out uint flags);
        [PreserveSig] int GetDocumentMgr(out IntPtr document);
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetSelection(out uint index);
        [PreserveSig] int GetString(uint index, [MarshalAs(UnmanagedType.BStr)] out string value);
        [PreserveSig] int GetPageIndex(IntPtr indices, uint size, out uint count);
        [PreserveSig] int SetPageIndex(IntPtr indices, uint count);
        [PreserveSig] int GetCurrentPage(out uint page);
    }

    [ComVisible(true), Guid("ea1ea136-19df-11d7-a6d2-00065b84435c"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ITfUIElementSink
    {
        [PreserveSig] int BeginUIElement(uint id, [MarshalAs(UnmanagedType.Bool)] ref bool show);
        [PreserveSig] int UpdateUIElement(uint id);
        [PreserveSig] int EndUIElement(uint id);
    }

    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    private sealed class CandidateSink(ImeCandidatePresenter owner) : ITfUIElementSink
    {
        public int BeginUIElement(uint id, ref bool show) { owner.Begin(id, show); return 0; }
        public int UpdateUIElement(uint id) { owner.Update(id); return 0; }
        public int EndUIElement(uint id) { owner.End(id); return 0; }
    }
}
