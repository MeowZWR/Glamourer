using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using Glamourer.Designs;
using Glamourer.Services;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using OtterGui.Widgets;

namespace Glamourer.Gui.Tabs.DesignTab;

public class DesignDetailTab
{
    private readonly SaveService              _saveService;
    private readonly DesignFileSystemSelector _selector;
    private readonly DesignFileSystem         _fileSystem;
    private readonly DesignManager            _manager;
    private readonly DesignColors             _colors;
    private readonly DesignColorCombo         _colorCombo;
    private readonly TagButtons               _tagButtons = new();

    private string? _newPath;
    private string? _newDescription;
    private string? _newName;

    private bool                   _editDescriptionMode;
    private Design?                _changeDesign;
    private DesignFileSystem.Leaf? _changeLeaf;

    public DesignDetailTab(SaveService saveService, DesignFileSystemSelector selector, DesignManager manager, DesignFileSystem fileSystem,
        DesignColors colors)
    {
        _saveService = saveService;
        _selector    = selector;
        _manager     = manager;
        _fileSystem  = fileSystem;
        _colors      = colors;
        _colorCombo  = new DesignColorCombo(_colors, false);
    }

    public void Draw()
    {
        using var h = ImRaii.CollapsingHeader("设计详情");
        if (!h)
            return;

        DrawDesignInfoTable();
        DrawDescription();
        ImGui.NewLine();
    }


    private void DrawDesignInfoTable()
    {
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ButtonTextAlign, new Vector2(0, 0.5f));
        using var table = ImRaii.Table("Details", 2);
        if (!table)
            return;

        ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("Last Update Datem").X);
        ImGui.TableSetupColumn("Data", ImGuiTableColumnFlags.WidthStretch);

        ImGuiUtil.DrawFrameColumn("设计名称");
        ImGui.TableNextColumn();
        var width = new Vector2(ImGui.GetContentRegionAvail().X, 0);
        var name  = _newName ?? _selector.Selected!.Name;
        ImGui.SetNextItemWidth(width.X);
        if (ImGui.InputText("##Name", ref name, 128))
        {
            _newName      = name;
            _changeDesign = _selector.Selected;
        }

        if (ImGui.IsItemDeactivatedAfterEdit() && _changeDesign != null)
        {
            _manager.Rename(_changeDesign, name);
            _newName      = null;
            _changeDesign = null;
        }

        var identifier = _selector.Selected!.Identifier.ToString();
        ImGuiUtil.DrawFrameColumn("唯一标识符");
        ImGui.TableNextColumn();
        var fileName = _saveService.FileNames.DesignFile(_selector.Selected!);
        using (var mono = ImRaii.PushFont(UiBuilder.MonoFont))
        {
            if (ImGui.Button(identifier, width))
                try
                {
                    Process.Start(new ProcessStartInfo(fileName) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    Glamourer.Messager.NotificationMessage(ex, $"Could not open file {fileName}.", $"Could not open file {fileName}",
                        NotificationType.Warning);
                }

            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                ImGui.SetClipboardText(identifier);
        }

        ImGuiUtil.HoverTooltip(
            $"打开此文件：\n\t{fileName}\n在您选择的.json编辑器中包控制此设计。\n\n右键单击可将标识符复制到剪贴板。");

        ImGuiUtil.DrawFrameColumn("完整选择器路径");
        ImGui.TableNextColumn();
        var path = _newPath ?? _selector.SelectedLeaf!.FullName();
        ImGui.SetNextItemWidth(width.X);
        if (ImGui.InputText("##Path", ref path, 1024))
        {
            _newPath    = path;
            _changeLeaf = _selector.SelectedLeaf!;
        }

        if (ImGui.IsItemDeactivatedAfterEdit() && _changeLeaf != null)
            try
            {
                _fileSystem.RenameAndMove(_changeLeaf, path);
                _newPath    = null;
                _changeLeaf = null;
            }
            catch (Exception ex)
            {
                Glamourer.Messager.NotificationMessage(ex, ex.Message, "无法重命名或移动设计", NotificationType.Error);
            }

        ImGuiUtil.DrawFrameColumn("快速设计栏");
        ImGui.TableNextColumn();
        if (ImGui.RadioButton("显示##qdb", _selector.Selected.QuickDesign))
            _manager.SetQuickDesign(_selector.Selected!, true);
        var hovered = ImGui.IsItemHovered();
        ImGui.SameLine();
        if (ImGui.RadioButton("隐藏##qdb", !_selector.Selected.QuickDesign))
            _manager.SetQuickDesign(_selector.Selected!, false);
        if (hovered || ImGui.IsItemHovered())
            ImGui.SetTooltip("在快速设计栏中显示或隐藏此设计。");

        var forceRedraw = _selector.Selected!.ForcedRedraw;
        ImGuiUtil.DrawFrameColumn("强制重绘");
        ImGui.TableNextColumn();
        if (ImGui.Checkbox("##ForceRedraw", ref forceRedraw))
            _manager.ChangeForcedRedraw(_selector.Selected!, forceRedraw);
        ImGuiUtil.HoverTooltip("设置使此设计在任何方式应用时始终强制重新绘制。");

        var resetMaterials = _selector.Selected!.ResetAdvancedDyes;
        ImGuiUtil.DrawFrameColumn("重置高级染色");
        ImGui.TableNextColumn();
        if (ImGui.Checkbox("##ResetAdvancedDyes", ref resetMaterials))
            _manager.ChangeResetAdvancedDyes(_selector.Selected!, resetMaterials);
        ImGuiUtil.HoverTooltip("将此设计设置为在通过任何方式应用时，重置先前应用的任何高级染色。");

        ImGuiUtil.DrawFrameColumn("配色");
        var colorName = _selector.Selected!.Color.Length == 0 ? DesignColors.AutomaticName : _selector.Selected!.Color;
        ImGui.TableNextColumn();
        if (_colorCombo.Draw("##colorCombo", colorName, "将颜色与此设计相关联。\n"
              + "右键单击可恢复为自动配色。\n"
              + "按住Ctrl并滚动鼠标滚轮进行滚动选择。",
                width.X - ImGui.GetStyle().ItemSpacing.X - ImGui.GetFrameHeight(), ImGui.GetTextLineHeight())
         && _colorCombo.CurrentSelection != null)
        {
            colorName = _colorCombo.CurrentSelection is DesignColors.AutomaticName ? string.Empty : _colorCombo.CurrentSelection;
            _manager.ChangeColor(_selector.Selected!, colorName);
        }

        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            _manager.ChangeColor(_selector.Selected!, string.Empty);

        if (_colors.TryGetValue(_selector.Selected!.Color, out var currentColor))
        {
            ImGui.SameLine();
            if (DesignColorUi.DrawColorButton($"与 {_selector.Selected!.Color} 关联的颜色", currentColor, out var newColor))
                _colors.SetColor(_selector.Selected!.Color, newColor);
        }
        else if (_selector.Selected!.Color.Length != 0)
        {
            ImGui.SameLine();
            var       size = new Vector2(ImGui.GetFrameHeight());
            using var font = ImRaii.PushFont(UiBuilder.IconFont);
            ImGuiUtil.DrawTextButton(FontAwesomeIcon.ExclamationCircle.ToIconString(), size, 0, _colors.MissingColor);
            ImGuiUtil.HoverTooltip("与此设计相关联的颜色不存在。");
        }

        ImGuiUtil.DrawFrameColumn("创建日期");
        ImGui.TableNextColumn();
        ImGuiUtil.DrawTextButton(_selector.Selected!.CreationDate.LocalDateTime.ToString("F"), width, 0);

        ImGuiUtil.DrawFrameColumn("最后更新日期");
        ImGui.TableNextColumn();
        ImGuiUtil.DrawTextButton(_selector.Selected!.LastEdit.LocalDateTime.ToString("F"), width, 0);

        ImGuiUtil.DrawFrameColumn("标签");
        ImGui.TableNextColumn();
        DrawTags();
    }

    private void DrawTags()
    {
        var idx = _tagButtons.Draw(string.Empty, string.Empty, _selector.Selected!.Tags, out var editedTag);
        if (idx < 0)
            return;

        if (idx < _selector.Selected!.Tags.Length)
        {
            if (editedTag.Length == 0)
                _manager.RemoveTag(_selector.Selected!, idx);
            else
                _manager.RenameTag(_selector.Selected!, idx, editedTag);
        }
        else
        {
            _manager.AddTag(_selector.Selected!, editedTag);
        }
    }

    private void DrawDescription()
    {
        var desc = _selector.Selected!.Description;
        var size = new Vector2(ImGui.GetContentRegionAvail().X, 12 * ImGui.GetTextLineHeightWithSpacing());
        if (!_editDescriptionMode)
        {
            using (var textBox = ImRaii.ListBox("##desc", size))
            {
                ImGuiUtil.TextWrapped(desc);
            }

            if (ImGui.Button("编辑描述"))
                _editDescriptionMode = true;
        }
        else
        {
            var edit = _newDescription ?? desc;
            if (ImGui.InputTextMultiline("##desc", ref edit, (uint)Math.Max(2000, 4 * edit.Length), size))
                _newDescription = edit;

            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                _manager.ChangeDescription(_selector.Selected!, edit);
                _newDescription = null;
            }

            if (ImGui.Button("停止编辑"))
                _editDescriptionMode = false;
        }
    }
}
