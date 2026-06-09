using CommunityToolkit.Mvvm.ComponentModel;

namespace GSYNC.App.ViewModels;

public partial class LibraryPageViewModel : ObservableObject
{
    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isEmptyState;

    [ObservableProperty]
    private bool _isSyncInProgress;

    public IReadOnlyList<LibraryGameRow> Games { get; } = new[]
    {
        new LibraryGameRow("Elden Ring", "Ready", "Steam", true),
        new LibraryGameRow("Cyberpunk 2077", "Pending", "GOG", false),
        new LibraryGameRow("Balatro", "Synced", "Steam", false),
    };
}

public sealed record LibraryGameRow(string Name, string Status, string Source, bool IsSelected);
