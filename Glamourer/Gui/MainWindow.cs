using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Glamourer.Designs;
using Glamourer.Events;
using Glamourer.Gui.Tabs;
using Glamourer.Gui.Tabs.ActorTab;
using Glamourer.Gui.Tabs.AutomationTab;
using Glamourer.Gui.Tabs.DebugTab;
using Glamourer.Gui.Tabs.DesignTab;
using Glamourer.Gui.Tabs.NpcTab;
using Glamourer.Gui.Tabs.SettingsTab;
using Glamourer.Gui.Tabs.UnlocksTab;
using Glamourer.Interop.Penumbra;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Custom;
using OtterGui.Raii;
using OtterGui.Services;
using OtterGui.Text;
using OtterGui.Widgets;

namespace Glamourer.Gui;

public class MainWindowPosition : IService
{
    public bool    IsOpen   { get; set; }
    public Vector2 Position { get; set; }
    public Vector2 Size     { get; set; }
}

public class MainWindow : Window, IDisposable
{
    public enum TabType
    {
        None       = -1,
        Settings   = 0,
        Debug      = 1,
        Actors     = 2,
        Designs    = 3,
        Automation = 4,
        Unlocks    = 5,
        Messages   = 6,
        Npcs       = 7,
    }

    private readonly Configuration      _config;
    private readonly PenumbraService    _penumbra;
    private readonly DesignQuickBar     _quickBar;
    private readonly TabSelected        _event;
    private readonly MainWindowPosition _position;
    private readonly ITab[]             _tabs;
    private          bool               _ignorePenumbra = false;

    public readonly SettingsTab   Settings;
    public readonly ActorTab      Actors;
    public readonly DebugTab      Debug;
    public readonly DesignTab     Designs;
    public readonly AutomationTab Automation;
    public readonly UnlocksTab    Unlocks;
    public readonly NpcTab        Npcs;
    public readonly MessagesTab   Messages;

    public TabType SelectTab;

    public MainWindow(IDalamudPluginInterface pi, Configuration config, SettingsTab settings, ActorTab actors, DesignTab designs,
        DebugTab debugTab, AutomationTab automation, UnlocksTab unlocks, TabSelected @event, MessagesTab messages, DesignQuickBar quickBar,
        NpcTab npcs, MainWindowPosition position, PenumbraService penumbra)
        : base("GlamourerMainWindow")
    {
        pi.UiBuilder.DisableGposeUiHide = true;
        SizeConstraints = new WindowSizeConstraints()
        {
            MinimumSize = new Vector2(700, 675),
            MaximumSize = ImGui.GetIO().DisplaySize,
        };
        Settings   = settings;
        Actors     = actors;
        Designs    = designs;
        Automation = automation;
        Debug      = debugTab;
        Unlocks    = unlocks;
        _event     = @event;
        Messages   = messages;
        _quickBar  = quickBar;
        Npcs       = npcs;
        _position  = position;
        _config    = config;
        _penumbra  = penumbra;
        _tabs =
        [
            settings,
            actors,
            designs,
            automation,
            unlocks,
            npcs,
            messages,
            debugTab,
        ];
        SelectTab = _config.Ephemeral.SelectedTab;
        _event.Subscribe(OnTabSelected, TabSelected.Priority.MainWindow);
        IsOpen = _config.OpenWindowAtStart;
    }

    public void OpenSettings()
    {
        IsOpen    = true;
        SelectTab = TabType.Settings;
    }

    public override void PreDraw()
    {
        Flags = _config.Ephemeral.LockMainWindow
            ? Flags | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize
            : Flags & ~(ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize);
        _position.IsOpen = IsOpen;
        WindowName       = GetLabel();
    }

    public void Dispose()
        => _event.Unsubscribe(OnTabSelected);

    public override void Draw()
    {
        var yPos = ImGui.GetCursorPosY();
        _position.Size     = ImGui.GetWindowSize();
        _position.Position = ImGui.GetWindowPos();

        if (!_penumbra.Available && !_ignorePenumbra)
        {
            if (_penumbra.CurrentMajor == 0)
                DrawProblemWindow(
                    "无法附加到 Penumbra。请确保 Penumbra 已安装并正在运行。\n\nGlamourer 需要 Penumbra 才能正常工作。");
            else if (_penumbra is
                     {

                         CurrentMajor: PenumbraService.RequiredPenumbraBreakingVersion,
                         CurrentMinor: >= PenumbraService.RequiredPenumbraFeatureVersion,
                     })
                DrawProblemWindow(
                    $"您当前未连接到 Penumbra，似乎是通过手动断开连接的。\n\nPenumbra 的最后 API 版本是 {_penumbra.CurrentMajor}.{_penumbra.CurrentMinor}。\n\nGlamourer 需要 Penumbra 才能正常工作。");
            else
                DrawProblemWindow(
                    $"连接到 Penumbra 失败。\n\nPenumbra 的 API 版本是 {_penumbra.CurrentMajor}.{_penumbra.CurrentMinor}，但 Glamourer 需要的版本是 {PenumbraService.RequiredPenumbraBreakingVersion}.{PenumbraService.RequiredPenumbraFeatureVersion}，其中主版本号必须完全匹配，次版本号必须大于或等于。\n您可能需要更新 Penumbra 或为这个版本的 Glamourer 启用测试构建。\n\nGlamourer 需要 Penumbra 才能正常工作。");
        }
        else
        {
            if (TabBar.Draw("##tabs", ImGuiTabBarFlags.None, ToLabel(SelectTab), out var currentTab, () => { }, _tabs))
                SelectTab = TabType.None;
            var tab = FromLabel(currentTab);

            if (tab != _config.Ephemeral.SelectedTab)
            {
                _config.Ephemeral.SelectedTab = FromLabel(currentTab);
                _config.Ephemeral.Save();
            }

            if (_config.ShowQuickBarInTabs)
                _quickBar.DrawAtEnd(yPos);
        }
    }

    private ReadOnlySpan<byte> ToLabel(TabType type)
        => type switch
        {
            TabType.Settings   => Settings.Label,
            TabType.Debug      => Debug.Label,
            TabType.Actors     => Actors.Label,
            TabType.Designs    => Designs.Label,
            TabType.Automation => Automation.Label,
            TabType.Unlocks    => Unlocks.Label,
            TabType.Messages   => Messages.Label,
            TabType.Npcs       => Npcs.Label,
            _                  => ReadOnlySpan<byte>.Empty,
        };

    private TabType FromLabel(ReadOnlySpan<byte> label)
    {
        // @formatter:off
        if (label == Actors.Label)     return TabType.Actors;
        if (label == Designs.Label)    return TabType.Designs;
        if (label == Settings.Label)   return TabType.Settings;
        if (label == Automation.Label) return TabType.Automation;
        if (label == Unlocks.Label)    return TabType.Unlocks;
        if (label == Npcs.Label)       return TabType.Npcs;
        if (label == Messages.Label)   return TabType.Messages;
        if (label == Debug.Label)      return TabType.Debug;
        // @formatter:on
        return TabType.None;
    }

    /// <summary> The longest support button text. </summary>
    public static ReadOnlySpan<byte> SupportInfoButtonText
        => "复制支持信息到剪贴板"u8;

    /// <summary> Draw the support button group on the right-hand side of the window. </summary>
    public static void DrawSupportButtons(Glamourer glamourer, Changelog changelog)
    {
        var width = ImUtf8.CalcTextSize(SupportInfoButtonText).X + ImGui.GetStyle().FramePadding.X * 2;
        var xPos  = ImGui.GetWindowWidth() - width;
        ImGui.SetCursorPos(new Vector2(xPos, 0));
        CustomGui.DrawDiscordButton(Glamourer.Messager, width);

        ImGui.SetCursorPos(new Vector2(xPos, ImGui.GetFrameHeightWithSpacing()));
        CustomGui.DrawCNDiscordButton(Glamourer.Messager, width);

        ImGui.SetCursorPos(new Vector2(xPos, 2 * ImGui.GetFrameHeightWithSpacing()));
        DrawSupportButton(glamourer); 

        ImGui.SetCursorPos(new Vector2(xPos, 3 * ImGui.GetFrameHeightWithSpacing()));
        CustomGui.DrawGuideButton(Glamourer.Messager, width);

        ImGui.SetCursorPos(new Vector2(xPos, 4 * ImGui.GetFrameHeightWithSpacing()));
        if (ImGui.Button("显示更新日志", new Vector2(width, 0)))
            changelog.ForceOpen = true;
    }

    /// <summary>
    /// Draw a button that copies the support info to clipboards.
    /// </summary>
    private static void DrawSupportButton(Glamourer glamourer)
    {
        if (!ImUtf8.Button(SupportInfoButtonText))
            return;

        var text = glamourer.GatherSupportInformation();
        ImGui.SetClipboardText(text);
        Glamourer.Messager.NotificationMessage("Copied Support Info to Clipboard.", NotificationType.Success, false);
    }

    private void OnTabSelected(TabType type, Design? _)
    {
        SelectTab = type;
        IsOpen    = true;
    }

    private string GetLabel()
        => (Glamourer.Version.Length == 0, _config.Ephemeral.IncognitoMode) switch
        {
            (true, true)   => "Glamourer（匿名模式）###GlamourerMainWindow",
            (true, false)  => "Glamourer###GlamourerMainWindow",
            (false, false) => $"Glamourer v{Glamourer.Version}###GlamourerMainWindow",
            (false, true)  => $"Glamourer v{Glamourer.Version}（匿名模式）###GlamourerMainWindow",
        };

    private void DrawProblemWindow(string text)
    {
        using var color = ImRaii.PushColor(ImGuiCol.Text, Colors.SelectedRed);
        ImGui.NewLine();
        ImGui.NewLine();
        ImGuiUtil.TextWrapped(text);
        color.Pop();

        ImGui.NewLine();
        if (ImUtf8.Button("尝试重新连接"u8))
            _penumbra.Reattach();

        var ignoreAllowed = _config.DeleteDesignModifier.IsActive();
        ImGui.SameLine();
        if (ImUtf8.ButtonEx("这次忽略 Penumbra"u8,
                $"某些功能，如自动执行或保持状态，没有 Penumbra 将无法正常工作。\n\n忽略此操作风险自负！{(ignoreAllowed ? string.Empty : $"\n\n按住 {_config.DeleteDesignModifier} 键并单击以启用此按钮。)")}",
                default, !ignoreAllowed))
            _ignorePenumbra = true;

        ImGui.NewLine();
        ImGui.NewLine();
        CustomGui.DrawDiscordButton(Glamourer.Messager, 0);
        ImGui.SameLine();
        ImGui.NewLine();
        ImGui.NewLine();
    }
}
