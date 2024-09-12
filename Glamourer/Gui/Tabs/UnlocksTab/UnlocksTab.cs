using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using OtterGui.Raii;
using OtterGui;
using OtterGui.Widgets;

namespace Glamourer.Gui.Tabs.UnlocksTab;

public class UnlocksTab : Window, ITab
{
    private readonly EphemeralConfig  _config;
    private readonly UnlockOverview _overview;
    private readonly UnlockTable    _table;

    public UnlocksTab(EphemeralConfig config, UnlockOverview overview, UnlockTable table)
        : base("已解锁装备/物品")
    {
        _config   = config;
        _overview = overview;
        _table    = table;

        IsOpen = false;
        SizeConstraints = new WindowSizeConstraints()
        {
            MinimumSize = new Vector2(700, 675),
            MaximumSize = ImGui.GetIO().DisplaySize,
        };
    }

    private bool DetailMode
    {
        get => _config.UnlockDetailMode;
        set
        {
            _config.UnlockDetailMode = value;
            _config.Save();
        }
    }

    public ReadOnlySpan<byte> Label
        => "解锁物品"u8;

    public void DrawContent()
    {
        DrawTypeSelection();
        if (DetailMode)
            _table.Draw(ImGui.GetFrameHeightWithSpacing());
        else
            _overview.Draw();
        _table.Flags |= ImGuiTableFlags.Resizable;
    }

    public override void Draw()
    {
        DrawContent();
    }

    private void DrawTypeSelection()
    {
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero)
            .Push(ImGuiStyleVar.FrameRounding, 0);
        var buttonSize = new Vector2(ImGui.GetContentRegionAvail().X / 2, ImGui.GetFrameHeight());
        if (!IsOpen)
            buttonSize.X -= ImGui.GetFrameHeight() / 2;
        if (DetailMode)
            buttonSize.X -= ImGui.GetFrameHeight() / 2;

        if (ImGuiUtil.DrawDisabledButton("总览模式", buttonSize, "显示已解锁物品的图标。", !DetailMode))
            DetailMode = false;

        ImGui.SameLine();
        if (ImGuiUtil.DrawDisabledButton("详情模式", buttonSize, "显示所有解锁数据为可筛选和排序的组合表格。",
                DetailMode))
            DetailMode = true;

        if (DetailMode)
        {
            ImGui.SameLine();
            if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Expand.ToIconString(), new Vector2(ImGui.GetFrameHeight()),
                    "将所有列恢复到其原始大小。", false, true))
                _table.Flags &= ~ImGuiTableFlags.Resizable;
        }

        if (!IsOpen)
        {
            ImGui.SameLine();
            if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.SquareArrowUpRight.ToIconString(), new Vector2(ImGui.GetFrameHeight()),
                    "打开“解锁物品”独立窗口。", false, true))
                IsOpen = true;
        }
    }
}
