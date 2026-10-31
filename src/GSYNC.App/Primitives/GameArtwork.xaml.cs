using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using GSYNC.App.Infrastructure.Wizard;

namespace GSYNC.App.Primitives;

public sealed partial class GameArtwork : UserControl
{
    public static readonly DependencyProperty CoverUriProperty = DependencyProperty.Register(
        nameof(CoverUri), typeof(string), typeof(GameArtwork), new PropertyMetadata(string.Empty, ArtworkChanged));
    public static readonly DependencyProperty LocalArtworkPathProperty = DependencyProperty.Register(
        nameof(LocalArtworkPath), typeof(string), typeof(GameArtwork), new PropertyMetadata(string.Empty, ArtworkChanged));
    public static readonly DependencyProperty ProgramPathProperty = DependencyProperty.Register(
        nameof(ProgramPath), typeof(string), typeof(GameArtwork), new PropertyMetadata(string.Empty, ArtworkChanged));
    public static readonly DependencyProperty FallbackTextProperty = DependencyProperty.Register(
        nameof(FallbackText), typeof(string), typeof(GameArtwork), new PropertyMetadata("G"));

    private int _generation;
    private bool _isLoaded;

    public GameArtwork()
    {
        InitializeComponent();
        Loaded += (_, _) => { _isLoaded = true; _ = RefreshAsync(); };
        Unloaded += (_, _) => { _isLoaded = false; _generation++; };
    }

    public string CoverUri { get => (string)GetValue(CoverUriProperty); set => SetValue(CoverUriProperty, value); }
    public string LocalArtworkPath { get => (string)GetValue(LocalArtworkPathProperty); set => SetValue(LocalArtworkPathProperty, value); }
    public string ProgramPath { get => (string)GetValue(ProgramPathProperty); set => SetValue(ProgramPathProperty, value); }
    public string FallbackText { get => (string)GetValue(FallbackTextProperty); set => SetValue(FallbackTextProperty, value); }

    private static void ArtworkChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var artwork = (GameArtwork)sender;
        if (artwork._isLoaded) _ = artwork.RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var generation = ++_generation;
        ArtworkImage.Source = null;
        if (await TryLocalAsync(LocalArtworkPath, false, generation)) return;
        if (generation != _generation) return;
        if (Uri.TryCreate(CoverUri, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
        {
            ShowRemote(uri, generation, false);
            return;
        }
        await TryLocalAsync(ProgramPath, true, generation);
    }

    private void ShowRemote(Uri uri, int generation, bool isHeader)
    {
        var bitmap = new BitmapImage { DecodePixelWidth = 192 };
        bitmap.ImageFailed += async (_, _) =>
        {
            if (generation != _generation || !ReferenceEquals(ArtworkImage.Source, bitmap)) return;
            if (!isHeader && uri.AbsolutePath.EndsWith("/library_600x900.jpg", StringComparison.Ordinal))
            {
                ShowRemote(new Uri(uri.AbsoluteUri.Replace("library_600x900.jpg", "header.jpg", StringComparison.Ordinal)), generation, true);
                return;
            }
            ArtworkImage.Source = null;
            await TryLocalAsync(ProgramPath, true, generation);
        };
        ArtworkImage.Source = bitmap;
        bitmap.UriSource = uri;
    }

    private async Task<bool> TryLocalAsync(string path, bool thumbnail, int generation)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            var icon = thumbnail ? await ExecutableIconReader.ReadAsync(path) : null;
            if (thumbnail && icon is null) return false;
            if (generation != _generation || !_isLoaded) return false;
            using Stream stream = thumbnail ? new MemoryStream(icon!, writable: false) : File.OpenRead(path);
            using var randomAccessStream = stream.AsRandomAccessStream();
            var bitmap = new BitmapImage { DecodePixelWidth = 192 };
            await bitmap.SetSourceAsync(randomAccessStream);
            if (generation != _generation || !_isLoaded) return false;
            ArtworkImage.Source = bitmap;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
