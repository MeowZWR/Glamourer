using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Glamourer.Designs;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Text;

namespace Glamourer.Gui.Tabs.DesignTab;

public class MultiDesignPanel(DesignFileSystemSelector selector, DesignManager editor, DesignColors colors)
{
    private readonly DesignColorCombo _colorCombo = new(colors, true);

    public void Draw()
    {
        if (selector.SelectedPaths.Count == 0)
            return;

        var width = ImGuiHelpers.ScaledVector2(145, 0);
        ImGui.NewLine();
        var treeNodePos = ImGui.GetCursorPos();
        _numDesigns = DrawDesignList();
        DrawCounts(treeNodePos);
        var offset = DrawMultiTagger(width);
        DrawMultiColor(width, offset);
        DrawMultiQuickDesignBar(offset);
    }

    private void DrawCounts(Vector2 treeNodePos)
    {
        var startPos   = ImGui.GetCursorPos();
        var numFolders = selector.SelectedPaths.Count - _numDesigns;
        var text = (_numDesigns, numFolders) switch
        {
            (0, 0)   => string.Empty, // should not happen
            (> 0, 0) => $"{_numDesigns} 个设计",
            (0, > 0) => $"{numFolders} 个折叠组",
            _        => $"{_numDesigns} 个设计, {numFolders} 个折叠组",
        };
        ImGui.SetCursorPos(treeNodePos);
        ImUtf8.TextRightAligned(text);
        ImGui.SetCursorPos(startPos);
    }

    private int DrawDesignList()
    {
        using var tree = ImUtf8.TreeNode("当前选中的对象"u8, ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.NoTreePushOnOpen);
        ImGui.Separator();
        if (!tree)
            return selector.SelectedPaths.Count(l => l is DesignFileSystem.Leaf);

        var sizeType             = new Vector2(ImGui.GetFrameHeight());
        var availableSizePercent = (ImGui.GetContentRegionAvail().X - sizeType.X - 4 * ImGui.GetStyle().CellPadding.X) / 100;
        var sizeMods             = availableSizePercent * 35;
        var sizeFolders          = availableSizePercent * 65;

        _numQuickDesignEnabled = 0;
        var numDesigns = 0;
        using (var table = ImUtf8.Table("mods"u8, 3, ImGuiTableFlags.RowBg))
        {
            if (!table)
                return selector.SelectedPaths.Count(l => l is DesignFileSystem.Leaf);

            ImUtf8.TableSetupColumn("type"u8, ImGuiTableColumnFlags.WidthFixed, sizeType.X);
            ImUtf8.TableSetupColumn("mod"u8,  ImGuiTableColumnFlags.WidthFixed, sizeMods);
            ImUtf8.TableSetupColumn("path"u8, ImGuiTableColumnFlags.WidthFixed, sizeFolders);

            var i = 0;
            foreach (var (fullName, path) in selector.SelectedPaths.Select(p => (p.FullName(), p))
                         .OrderBy(p => p.Item1, StringComparer.OrdinalIgnoreCase))
            {
                using var id = ImRaii.PushId(i++);
                var (icon, text) = path is DesignFileSystem.Leaf l
                    ? (FontAwesomeIcon.FileCircleMinus, l.Value.Name.Text)
                    : (FontAwesomeIcon.FolderMinus, string.Empty);
                ImGui.TableNextColumn();
                if (ImUtf8.IconButton(icon, "从选择中移除。"u8, sizeType))
                    selector.RemovePathFromMultiSelection(path);

                ImUtf8.DrawFrameColumn(text);
                ImUtf8.DrawFrameColumn(fullName);

                if (path is not DesignFileSystem.Leaf l2)
                    continue;

                ++numDesigns;
                if (l2.Value.QuickDesign)
                    ++_numQuickDesignEnabled;
            }
        }

        ImGui.Separator();
        return numDesigns;
    }

    private          string              _tag = string.Empty;
    private          int                 _numQuickDesignEnabled;
    private          int                 _numDesigns;
    private readonly List<Design>        _addDesigns    = [];
    private readonly List<(Design, int)> _removeDesigns = [];

    private float DrawMultiTagger(Vector2 width)
    {
        ImUtf8.TextFrameAligned("批量标签："u8);
        ImGui.SameLine();
        var offset = ImGui.GetItemRectSize().X;
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 2 * (width.X + ImGui.GetStyle().ItemSpacing.X));
        ImUtf8.InputText("##tag"u8, ref _tag, "标签名称..."u8);

        UpdateTagCache();
        var label = _addDesigns.Count > 0
            ? $"添加到{_addDesigns.Count}个设计"
            : "添加";
        var tooltip = _addDesigns.Count == 0
            ? _tag.Length == 0
                ? "未指定标签。"
                : $"所选的所有设计都已包含该标记：\"{_tag}\"."
            : $"添加本地标签“{_tag}”到{_addDesigns.Count}个设计：\n\n\t{string.Join("\n\t", _addDesigns.Select(m => m.Name.Text))}";
        ImGui.SameLine();
        if (ImUtf8.ButtonEx(label, tooltip, width, _addDesigns.Count == 0))
            foreach (var design in _addDesigns)
                editor.AddTag(design, _tag);

        label = _removeDesigns.Count > 0
            ? $"从{_removeDesigns.Count}个设计移除"
            : "移除";
        tooltip = _removeDesigns.Count == 0
            ? _tag.Length == 0
                ? "未指定标签。"
                : $"选中的设计不包含这个本地标签：“{_tag}”。"
            : $"从{_removeDesigns.Count}个设计移除本地标签“{_tag}”：\n\n\t{string.Join("\n\t", _removeDesigns.Select(m => m.Item1.Name.Text))}";
        ImGui.SameLine();
        if (ImUtf8.ButtonEx(label, tooltip, width, _removeDesigns.Count == 0))
            foreach (var (design, index) in _removeDesigns)
                editor.RemoveTag(design, index);
        ImGui.Separator();
        return offset;
    }

    private void DrawMultiQuickDesignBar(float offset)
    {
        ImUtf8.TextFrameAligned("批量快速设计栏："u8);
        ImGui.SameLine(offset, ImGui.GetStyle().ItemSpacing.X);
        var buttonWidth = new Vector2((ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) / 2, 0);
        var diff        = _numDesigns - _numQuickDesignEnabled;
        var tt = diff == 0
            ? $"All {_numDesigns} selected designs are already displayed in the quick design bar."
            : $"Display all {_numDesigns} selected designs in the quick design bar. Changes {diff} designs.";
        if (ImUtf8.ButtonEx("在快速设计栏中显示选中的设计"u8, tt, buttonWidth, diff == 0))
            foreach (var design in selector.SelectedPaths.OfType<DesignFileSystem.Leaf>())
                editor.SetQuickDesign(design.Value, true);

        ImGui.SameLine();
        tt = _numQuickDesignEnabled == 0
            ? $"All {_numDesigns} selected designs are already hidden in the quick design bar."
            : $"Hide all {_numDesigns} selected designs in the quick design bar. Changes {_numQuickDesignEnabled} designs.";
        if (ImUtf8.ButtonEx("在快速设计栏中隐藏选中的设计"u8, tt, buttonWidth, _numQuickDesignEnabled == 0))
            foreach (var design in selector.SelectedPaths.OfType<DesignFileSystem.Leaf>())
                editor.SetQuickDesign(design.Value, false);
        ImGui.Separator();
    }

    private void DrawMultiColor(Vector2 width, float offset)
    {
        ImUtf8.TextFrameAligned("批量配色：");
        ImGui.SameLine(offset, ImGui.GetStyle().ItemSpacing.X);
        _colorCombo.Draw("##color", _colorCombo.CurrentSelection ?? string.Empty, "选择一个设计颜色。",
            ImGui.GetContentRegionAvail().X - 2 * (width.X + ImGui.GetStyle().ItemSpacing.X), ImGui.GetTextLineHeight());

        UpdateColorCache();
        var label = _addDesigns.Count > 0
            ? $"设置{_addDesigns.Count}个设计"
            : "设置";
        var tooltip = _addDesigns.Count == 0
            ? _colorCombo.CurrentSelection switch
            {
                null                       => "未指定颜色。",
                DesignColors.AutomaticName => "使用另一个按钮设置为自动配色。",
                _                          => $"所选的所有设计都已设置为该颜色“{_colorCombo.CurrentSelection}”。",
            }
            : $"将{_addDesigns.Count}个的颜色设置为“{_colorCombo.CurrentSelection}”\n\n\t{string.Join("\n\t", _addDesigns.Select(m => m.Name.Text))}";
        ImGui.SameLine();
        if (ImUtf8.ButtonEx(label, tooltip, width, _addDesigns.Count == 0))
            foreach (var design in _addDesigns)
                editor.ChangeColor(design, _colorCombo.CurrentSelection!);

        label = _removeDesigns.Count > 0
            ? $"取消设置{_removeDesigns.Count}个设计"
            : "取消设置";
        tooltip = _removeDesigns.Count == 0
            ? "没有选中设计设置为非自动配色。"
            : $"设置{_removeDesigns.Count}个设计为重新使用自动配色：\n\n\t{string.Join("\n\t", _removeDesigns.Select(m => m.Item1.Name.Text))}";
        ImGui.SameLine();
        if (ImUtf8.ButtonEx(label, tooltip, width, _removeDesigns.Count == 0))
            foreach (var (design, _) in _removeDesigns)
                editor.ChangeColor(design, string.Empty);

        ImGui.Separator();
    }

    private void UpdateTagCache()
    {
        _addDesigns.Clear();
        _removeDesigns.Clear();
        if (_tag.Length == 0)
            return;

        foreach (var leaf in selector.SelectedPaths.OfType<DesignFileSystem.Leaf>())
        {
            var index = leaf.Value.Tags.IndexOf(_tag);
            if (index >= 0)
                _removeDesigns.Add((leaf.Value, index));
            else
                _addDesigns.Add(leaf.Value);
        }
    }

    private void UpdateColorCache()
    {
        _addDesigns.Clear();
        _removeDesigns.Clear();
        var selection = _colorCombo.CurrentSelection ?? DesignColors.AutomaticName;
        foreach (var leaf in selector.SelectedPaths.OfType<DesignFileSystem.Leaf>())
        {
            if (leaf.Value.Color.Length > 0)
                _removeDesigns.Add((leaf.Value, 0));
            if (selection != DesignColors.AutomaticName && leaf.Value.Color != selection)
                _addDesigns.Add(leaf.Value);
        }
    }
}
