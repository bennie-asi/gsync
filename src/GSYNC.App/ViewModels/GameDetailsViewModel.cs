using CommunityToolkit.Mvvm.ComponentModel;

namespace GSYNC.App.ViewModels;

public partial class GameDetailsViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Elden Ring";

    [ObservableProperty]
    private string _subtitle = "内容项、路径与历史预览将按 Stitch Game Details refined 设计稿收敛。";

    public IReadOnlyList<string> ContentItems { get; } = new[]
    {
        "Main Save",
        "Config",
        "Extra Files",
    };
}
