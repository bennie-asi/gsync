using GSYNC.App.Infrastructure.Localization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GSYNC.App.ViewModels;

public partial class AddGameWizardViewModel : ObservableObject
{
    private readonly bool _isChinese;

    [ObservableProperty]
    private int _currentStep = 1;

    public AddGameWizardViewModel(ILocalizationService localizationService)
    {
        _isChinese = localizationService.CurrentLanguageTag == "zh-CN";

        PageTitle = Pick("添加游戏", "Add Game");
        PageSubtitle = Pick("与精炼后的桌面工具流程一致的静态六步向导壳。", "Static six-step wizard shell aligned with the refined desktop utility flow.");
        Step2DetailsTitle = Pick("检测到的游戏详情", "Detected game details");
        Step4ReviewTitle = Pick("已解析路径检查", "Resolved path review");
        Step6ReadyTitle = Pick("准备添加游戏", "Ready to Add Game");
        FinishCalloutTitle = Pick("所有核心检查均已通过", "All core checks passed");
        FinishCalloutMessage = Pick("已验证连通性、路径可访问且权限已授予。", "Connectivity verified, paths are accessible, permissions granted.");
        FinishExcludedText = Pick("未选择图形配置 · 你已排除 'graphics.ini'。", "Graphics Config not selected · You excluded 'graphics.ini'.");
        CancelText = Pick("取消", "Cancel");
        BackText = Pick("返回", "Back");

        Steps =
        [
            new(1, Pick("选择来源", "Select Source"), Pick("选择 GSYNC 应该从哪里查找游戏配置。", "Choose where GSYNC should look for the game profile.")),
            new(2, Pick("选择游戏", "Select Game"), Pick("选择检测到的游戏或检查最佳匹配项。", "Pick a discovered game or review the best match.")),
            new(3, Pick("内容项", "Content Items"), Pick("选择存档、配置文件与可选扩展内容。", "Select saves, config files, and optional extras.")),
            new(4, Pick("检查路径", "Review Paths"), Pick("验证解析路径与可移植性覆盖情况。", "Validate resolved paths and portability coverage.")),
            new(5, Pick("绑定目标", "Bind Target"), Pick("选择配置文件应将同步数据存储到哪里。", "Choose where the profile will store synchronized data.")),
            new(6, Pick("完成", "Finish"), Pick("检查摘要并创建配置文件。", "Review the summary and create the profile.")),
        ];

        SourcesTitle = Pick("可用来源", "Available sources");
        SourcesSubtitle = Pick("自动来源会尝试检测已安装游戏以及云感知默认值。", "Automated sources will try to detect installed games and cloud-aware defaults.");
        GamesTitle = Pick("检测到的游戏", "Detected games");
        GamesSubtitle = Pick("从所选来源中选择检测到的游戏，或手动搜索。", "Choose a discovered game from the selected source or search manually.");
        ContentTitle = Pick("已选内容", "Selected content");
        ContentSubtitle = Pick("精炼后的内容项步骤使用紧凑卡片，显式展示条目名称与摘要。", "The refined content-items step uses dense cards with explicit item labels and summaries.");
        TargetsTitle = Pick("可用目标", "Available targets");
        TargetsSubtitle = Pick("静态目标选项与精炼后的绑定目标步骤卡片保持一致。", "Static target choices mirror the refined bind-target step cards.");

        Sources =
        [
            new("Steam", Pick("桌面库、已拥有游戏与 Steam 云默认值", "Desktop libraries, owned titles, and Steam cloud-aware defaults")),
            new("Epic", Pick("Epic 启动器游戏与本机存档发现", "Epic launcher titles with machine-local save discovery")),
            new(Pick("自定义", "Custom"), Pick("手动跟踪桌面或便携安装", "Manually tracked desktop or portable installations")),
            new(Pick("模拟器", "Emulator"), Pick("多存档模拟器库与复古合集", "Multi-save emulator libraries and retro collections")),
            new(Pick("手动", "Manual"), Pick("从零创建配置文件并完全掌控", "Create a profile from scratch with full control")),
        ];

        Games =
        [
            new("Elden Ring", Pick("Steam App ID 1245620 · 已安装 · 2 小时前游玩", "Steam App ID 1245620 · installed · last played 2h ago")),
            new("Hades", Pick("已安装 · 最近游玩 · 云安全默认值", "Installed · recently played · cloud-safe defaults")),
            new("Stardew Valley", Pick("检测到自定义安装 · 多文件夹存档", "Detected custom install · multi-folder saves")),
        ];

        GameDetails =
        [
            new("Steam App ID", "1245620"),
            new(Pick("状态", "Status"), Pick("已安装 · 检测到存档", "Installed · saves detected")),
            new(Pick("安装路径", "Install Path"), "D:/SteamLibrary/steamapps/common/ELDEN RING"),
            new(Pick("最近游玩", "Last Played"), Pick("2 小时前", "2 hours ago")),
            new(Pick("磁盘大小", "Disk Size"), "60.2 GB"),
        ];

        ContentItems =
        [
            new(Pick("主存档", "Main Save"), Pick("主存档槽位与备份文件", "Primary save slot and backup files")),
            new(Pick("角色槽位", "Character Slots"), Pick("角色资料子文件夹与元数据", "Character profile subfolders and metadata")),
            new(Pick("图形配置", "Graphics Config"), Pick("显示、控制与游戏偏好设置", "Display, controls, and gameplay preferences")),
            new(Pick("模组/附加内容", "Mods/Addons"), Pick("可选辅助内容与外部文件", "Optional auxiliary content and external files")),
        ];

        PathReview =
        [
            new(Pick("模板", "Template"), "%APPDATA%/EldenRing/<SteamId>/ER0000.sl2"),
            new(Pick("解析路径", "Resolved Path"), "C:/Users/Bennie/AppData/Roaming/EldenRing/7656119/ER0000.sl2"),
            new(Pick("变量覆盖", "Variable Coverage"), "%APPDATA%, %STEAM_LIBRARY%"),
            new(Pick("校验结果", "Validation"), Pick("在当前 Windows 目标上可移植", "Portable on current Windows targets")),
        ];

        Targets =
        [
            new("WebDAV Main", Pick("主桌面同步目标，带快照保留", "Primary desktop sync target with snapshot retention")),
            new(Pick("本地备份", "Local Backup"), Pick("用于快速恢复的次级本地镜像", "Secondary local mirror for quick restores")),
            new("OneDrive Preview", Pick("可选云归档目标", "Optional cloud archive target")),
        ];

        FinishSummary =
        [
            new(Pick("已选来源", "Selected Source"), "Steam"),
            new(Pick("游戏", "Game"), "Elden Ring"),
            new(Pick("检测到的配置 ID", "Detected Config ID"), "ER-Primary-Windows"),
            new(Pick("目标", "Target"), "WebDAV Main"),
            new(Pick("核心检查", "Core Checks"), Pick("所有核心检查均已通过", "All core checks passed")),
            new(Pick("排除项", "Excluded"), Pick("未选择图形配置", "Graphics Config not selected")),
        ];

        FinishOptions =
        [
            Pick("为此游戏配置创建桌面快捷方式", "Create desktop shortcut to this game profile"),
            Pick("完成后启动首次同步", "Start first sync after finishing"),
        ];
    }

    public string PageTitle { get; }
    public string PageSubtitle { get; }
    public string SourcesTitle { get; }
    public string SourcesSubtitle { get; }
    public string GamesTitle { get; }
    public string GamesSubtitle { get; }
    public string Step2DetailsTitle { get; }
    public string ContentTitle { get; }
    public string ContentSubtitle { get; }
    public string Step4ReviewTitle { get; }
    public string TargetsTitle { get; }
    public string TargetsSubtitle { get; }
    public string Step6ReadyTitle { get; }
    public string FinishCalloutTitle { get; }
    public string FinishCalloutMessage { get; }
    public string FinishExcludedText { get; }
    public string CancelText { get; }
    public string BackText { get; }
    public IReadOnlyList<WizardStepItem> Steps { get; }
    public IReadOnlyList<WizardOption> Sources { get; }
    public IReadOnlyList<WizardOption> Games { get; }
    public IReadOnlyList<UiPropertyRow> GameDetails { get; }
    public IReadOnlyList<WizardOption> ContentItems { get; }
    public IReadOnlyList<UiPropertyRow> PathReview { get; }
    public IReadOnlyList<WizardOption> Targets { get; }
    public IReadOnlyList<UiPropertyRow> FinishSummary { get; }
    public IReadOnlyList<string> FinishOptions { get; }

    public string CurrentStepTitle => CurrentStep switch
    {
        1 => Pick("第 1 步 · 选择来源", "Step 1 · Select Source"),
        2 => Pick("第 2 步 · 选择游戏", "Step 2 · Select Game"),
        3 => Pick("第 3 步 · 内容项", "Step 3 · Content Items"),
        4 => Pick("第 4 步 · 检查路径", "Step 4 · Review Paths"),
        5 => Pick("第 5 步 · 绑定目标", "Step 5 · Bind Target"),
        6 => Pick("第 6 步 · 完成", "Step 6 · Finish"),
        _ => Pick("第 1 步 · 选择来源", "Step 1 · Select Source"),
    };

    public string CurrentStepDescription => CurrentStep switch
    {
        1 => Pick("选择最适合要添加游戏的来源提供程序。自动来源可以展示检测到的安装与元数据。", "Choose the source provider that best matches the game you want to add. Automated sources can surface discovered installations and metadata."),
        2 => Pick("从所选来源中选择一个检测到的游戏，或在继续前检查高亮匹配项。", "Choose a discovered game from the selected source or review the highlighted match before moving on."),
        3 => Pick("选择此配置文件应同步哪些存档、配置与可选内容。", "Choose which save, configuration, and optional items should be synchronized for this profile."),
        4 => Pick("在绑定目标前验证路径模板、解析结果与可移植性覆盖情况。", "Validate path templates, resolved results, and portability coverage before binding a target."),
        5 => Pick("选择此游戏配置应同步到哪个目标，以及可选备份副本。", "Choose the sync target that should receive the game profile and any optional backup copy."),
        6 => Pick("在创建跟踪游戏条目并启用同步动作前检查最终摘要。", "Review the final summary before creating the tracked game entry and enabling synchronization actions."),
        _ => string.Empty,
    };

    public string CurrentStepFooter => CurrentStep switch
    {
        1 => Pick("共 6 步中的第 1 步 · 选择来源后继续。", "Step 1 of 6 · Continue after choosing a source."),
        2 => Pick("共 6 步中的第 2 步 · 继续前确认检测到的游戏。", "Step 2 of 6 · Confirm the detected game before continuing."),
        3 => Pick("共 6 步中的第 3 步 · 保持可选内容显式可见。", "Step 3 of 6 · Keep optional content explicit."),
        4 => Pick("共 6 步中的第 4 步 · 绑定前验证所有解析路径。", "Step 4 of 6 · Verify all resolved paths before binding."),
        5 => Pick("共 6 步中的第 5 步 · 选择主要目标。", "Step 5 of 6 · Pick the primary destination."),
        6 => Pick("共 6 步中的第 6 步 · 已准备创建配置。", "Step 6 of 6 · Ready to create the profile."),
        _ => string.Empty,
    };

    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool IsStep3 => CurrentStep == 3;
    public bool IsStep4 => CurrentStep == 4;
    public bool IsStep5 => CurrentStep == 5;
    public bool IsStep6 => CurrentStep == 6;
    public bool CanGoBack => CurrentStep > 1;
    public string NextButtonText => CurrentStep == 6 ? Pick("创建配置", "Create Profile") : Pick("下一步", "Next Step");

    partial void OnCurrentStepChanged(int value)
    {
        OnPropertyChanged(nameof(CurrentStepTitle));
        OnPropertyChanged(nameof(CurrentStepDescription));
        OnPropertyChanged(nameof(CurrentStepFooter));
        OnPropertyChanged(nameof(IsStep1));
        OnPropertyChanged(nameof(IsStep2));
        OnPropertyChanged(nameof(IsStep3));
        OnPropertyChanged(nameof(IsStep4));
        OnPropertyChanged(nameof(IsStep5));
        OnPropertyChanged(nameof(IsStep6));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(NextButtonText));
    }

    public void GoNext()
    {
        if (CurrentStep < 6)
        {
            CurrentStep++;
        }
    }

    public void GoBack()
    {
        if (CurrentStep > 1)
        {
            CurrentStep--;
        }
    }

    private string Pick(string zh, string en) => _isChinese ? zh : en;
}

public sealed record WizardStepItem(int Index, string Title, string Summary);
public sealed record WizardOption(string Title, string Summary);
