using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Glamourer.Designs;
using ImGuiNET;
using OtterGui;
using OtterGui.Filesystem;
using OtterGui.Raii;

namespace Glamourer.Gui.Tabs.DesignTab;

public class MultiDesignPanel(DesignFileSystemSelector _selector, DesignManager _editor, DesignColors _colors)
{
    private readonly DesignColorCombo _colorCombo = new(_colors, true);

    public void Draw()
    {
        if (_selector.SelectedPaths.Count == 0)
            return;

        var width = ImGuiHelpers.ScaledVector2(145, 0);
        ImGui.NewLine();
        DrawDesignList();
        var offset = DrawMultiTagger(width);
        DrawMultiColor(width, offset);
        DrawMultiQuickDesignBar(offset);
    }

    private void DrawDesignList()
    {
        using var tree = ImRaii.TreeNode("当前选中的对象", ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.NoTreePushOnOpen);
        ImGui.Separator();
        if (!tree)
            return;

        var sizeType             = ImGui.GetFrameHeight();
        var availableSizePercent = (ImGui.GetContentRegionAvail().X - sizeType - 4 * ImGui.GetStyle().CellPadding.X) / 100;
        var sizeMods             = availableSizePercent * 35;
        var sizeFolders          = availableSizePercent * 65;

        _numQuickDesignEnabled = 0;
        _numDesigns = 0;
        using (var table = ImRaii.Table("mods", 3, ImGuiTableFlags.RowBg))
        {
            if (!table)
                return;

            ImGui.TableSetupColumn("type", ImGuiTableColumnFlags.WidthFixed, sizeType);
            ImGui.TableSetupColumn("mod",  ImGuiTableColumnFlags.WidthFixed, sizeMods);
            ImGui.TableSetupColumn("path", ImGuiTableColumnFlags.WidthFixed, sizeFolders);

            var i = 0;
            foreach (var (fullName, path) in _selector.SelectedPaths.Select(p => (p.FullName(), p))
                         .OrderBy(p => p.Item1, StringComparer.OrdinalIgnoreCase))
            {
                using var id = ImRaii.PushId(i++);
                ImGui.TableNextColumn();
                var icon = (path is DesignFileSystem.Leaf ? FontAwesomeIcon.FileCircleMinus : FontAwesomeIcon.FolderMinus).ToIconString();
                if (ImGuiUtil.DrawDisabledButton(icon, new Vector2(sizeType), "Remove from selection.", false, true))
                    _selector.RemovePathFromMultiSelection(path);

                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(path is DesignFileSystem.Leaf l ? l.Value.Name : string.Empty);

                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(fullName);

                if (path is not DesignFileSystem.Leaf l2)
                    continue;

                ++_numDesigns;
                if (l2.Value.QuickDesign)
                    ++_numQuickDesignEnabled;
            }
        }

        ImGui.Separator();
    }

    private          string              _tag = string.Empty;
    private          int                 _numQuickDesignEnabled;
    private          int                 _numDesigns;
    private readonly List<Design>        _addDesigns    = [];
    private readonly List<(Design, int)> _removeDesigns = [];

    private float DrawMultiTagger(Vector2 width)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("批量标签：");
        ImGui.SameLine();
        var offset = ImGui.GetItemRectSize().X;
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 2 * (width.X + ImGui.GetStyle().ItemSpacing.X));
        ImGui.InputTextWithHint("##tag", "标签名称...", ref _tag, 128);

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
        if (ImGuiUtil.DrawDisabledButton(label, width, tooltip, _addDesigns.Count == 0))
            foreach (var design in _addDesigns)
                _editor.AddTag(design, _tag);

        label = _removeDesigns.Count > 0
            ? $"从{_removeDesigns.Count}个设计移除"
            : "移除";
        tooltip = _removeDesigns.Count == 0
            ? _tag.Length == 0
                ? "未指定标签。"
                : $"选中的设计不包含这个本地标签：“{_tag}”。"
            : $"从{_removeDesigns.Count}个设计移除本地标签“{_tag}”：\n\n\t{string.Join("\n\t", _removeDesigns.Select(m => m.Item1.Name.Text))}";
        ImGui.SameLine();
        if (ImGuiUtil.DrawDisabledButton(label, width, tooltip, _removeDesigns.Count == 0))
            foreach (var (design, index) in _removeDesigns)
                _editor.RemoveTag(design, index);
        ImGui.Separator();
        return offset;
    }

    private void DrawMultiQuickDesignBar(float offset)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("批量快速设计栏");
        ImGui.SameLine(offset, ImGui.GetStyle().ItemSpacing.X);
        var buttonWidth = new Vector2((ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) / 2, 0);
        var diff        = _numDesigns - _numQuickDesignEnabled;
        var tt = diff == 0
            ? $"选中的{_numDesigns}个设计已经在快速设计栏中显示。"
            : $"选中的{_numDesigns}个设计将在快速设计栏中显示，{diff}个设计被修改。";
        if (ImGuiUtil.DrawDisabledButton("在快速设计栏中显示选中的设计", buttonWidth, tt, diff == 0))
            foreach(var design in _selector.SelectedPaths.OfType<DesignFileSystem.Leaf>())
                _editor.SetQuickDesign(design.Value, true);

        ImGui.SameLine();
        tt = _numQuickDesignEnabled == 0
            ? $"选中的{_numDesigns}个设计已经在快速设计栏中隐藏。"
            : $"选中的{_numDesigns}个设计将在快速设计栏中隐藏，{_numQuickDesignEnabled}个设计被修改。";
        if (ImGuiUtil.DrawDisabledButton("在快速设计栏中隐藏选中的设计", buttonWidth, tt, _numQuickDesignEnabled == 0))
            foreach (var design in _selector.SelectedPaths.OfType<DesignFileSystem.Leaf>())
                _editor.SetQuickDesign(design.Value, false);
        ImGui.Separator();
    }

    private void DrawMultiColor(Vector2 width, float offset)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("批量配色：");
        ImGui.SameLine(offset, ImGui.GetStyle().ItemSpacing.X);
        _colorCombo.Draw("##color", _colorCombo.CurrentSelection ?? string.Empty, "Select a design color.",
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
        if (ImGuiUtil.DrawDisabledButton(label, width, tooltip, _addDesigns.Count == 0))
            foreach (var design in _addDesigns)
                _editor.ChangeColor(design, _colorCombo.CurrentSelection!);

        label = _removeDesigns.Count > 0
            ? $"取消设置{_removeDesigns.Count}个设计"
            : "取消设置";
        tooltip = _removeDesigns.Count == 0
            ? "没有选中设计设置为非自动配色。"
            : $"设置{_removeDesigns.Count}个设计为重新使用自动配色：\n\n\t{string.Join("\n\t", _removeDesigns.Select(m => m.Item1.Name.Text))}";
        ImGui.SameLine();
        if (ImGuiUtil.DrawDisabledButton(label, width, tooltip, _removeDesigns.Count == 0))
            foreach (var (design, _) in _removeDesigns)
                _editor.ChangeColor(design, string.Empty);

        ImGui.Separator();
    }

    private void UpdateTagCache()
    {
        _addDesigns.Clear();
        _removeDesigns.Clear();
        if (_tag.Length == 0)
            return;

        foreach (var leaf in _selector.SelectedPaths.OfType<DesignFileSystem.Leaf>())
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
        foreach (var leaf in _selector.SelectedPaths.OfType<DesignFileSystem.Leaf>())
        {
            if (leaf.Value.Color.Length > 0)
                _removeDesigns.Add((leaf.Value, 0));
            if (selection != DesignColors.AutomaticName && leaf.Value.Color != selection)
                _addDesigns.Add(leaf.Value);
        }
    }
}
