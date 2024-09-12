using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Glamourer.Automation;
using Glamourer.Designs;
using Glamourer.Designs.Links;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Services;

namespace Glamourer.Gui.Tabs.DesignTab;

public class DesignLinkDrawer(DesignLinkManager _linkManager, DesignFileSystemSelector _selector, LinkDesignCombo _combo, DesignColors _colorManager) : IUiService
{
    private int       _dragDropIndex       = -1;
    private LinkOrder _dragDropOrder       = LinkOrder.None;
    private int       _dragDropTargetIndex = -1;
    private LinkOrder _dragDropTargetOrder = LinkOrder.None;

    public void Draw()
    {
        using var header = ImRaii.CollapsingHeader("设计链接");
        ImGuiUtil.HoverTooltip(
            "设计链接是指向其他设计的链接，这些设计将根据规则直接或通过自动执行应用于角色。\n"
          + "它们从上到下生效，就像自动执行里的一样，所以前面的设计设置的任何内容都不会被后面的设计再次设置，顺序很重要。\n"
          + "如果被链接设计链接到其他设计，它们也将被应用，因此禁止循环链接。 ");
        if (!header)
            return;

        DrawList();
    }

    private void MoveLink()
    {
        if (_dragDropTargetIndex < 0 || _dragDropIndex < 0)
            return;

        if (_dragDropOrder is LinkOrder.Self)
            switch (_dragDropTargetOrder)
            {
                case LinkOrder.Before:
                    for (var i = _selector.Selected!.Links.Before.Count - 1; i >= _dragDropTargetIndex; --i)
                        _linkManager.MoveDesignLink(_selector.Selected!, i, LinkOrder.Before, 0, LinkOrder.After);
                    break;
                case LinkOrder.After:
                    for (var i = 0; i <= _dragDropTargetIndex; ++i)
                    {
                        _linkManager.MoveDesignLink(_selector.Selected!, 0, LinkOrder.After, _selector.Selected!.Links.Before.Count,
                            LinkOrder.Before);
                    }

                    break;
            }
        else if (_dragDropTargetOrder is LinkOrder.Self)
            _linkManager.MoveDesignLink(_selector.Selected!, _dragDropIndex, _dragDropOrder, _selector.Selected!.Links.Before.Count,
                LinkOrder.Before);
        else
            _linkManager.MoveDesignLink(_selector.Selected!, _dragDropIndex, _dragDropOrder, _dragDropTargetIndex, _dragDropTargetOrder);

        _dragDropIndex       = -1;
        _dragDropTargetIndex = -1;
        _dragDropOrder       = LinkOrder.None;
        _dragDropTargetOrder = LinkOrder.None;
    }

    private void DrawList()
    {
        using var table = ImRaii.Table("table", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersOuter);
        if (!table)
            return;

        ImGui.TableSetupColumn("Del",  ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight());
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Detail", ImGuiTableColumnFlags.WidthFixed,
            6 * ImGui.GetFrameHeight() + 5 * ImGui.GetStyle().ItemInnerSpacing.X);

        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, ImGui.GetStyle().ItemInnerSpacing);
        DrawSubList(_selector.Selected!.Links.Before, LinkOrder.Before);
        DrawSelf();
        DrawSubList(_selector.Selected!.Links.After, LinkOrder.After);
        DrawNew();
        MoveLink();
    }

    private void DrawSelf()
    {
        using var id = ImRaii.PushId((int)LinkOrder.Self);
        ImGui.TableNextColumn();
        var color = _colorManager.GetColor(_selector.Selected!);
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            using var c = ImRaii.PushColor(ImGuiCol.Text, color);
            ImGui.AlignTextToFramePadding();
            ImGuiUtil.RightAlign(FontAwesomeIcon.ArrowRightLong.ToIconString());
        }

        ImGui.TableNextColumn();
        using (ImRaii.PushColor(ImGuiCol.Text, color))
        {
            ImGui.AlignTextToFramePadding();
            ImGui.Selectable(_selector.IncognitoMode ? _selector.Selected!.Incognito : _selector.Selected!.Name.Text);
        }

        ImGuiUtil.HoverTooltip("当前设计");
        DrawDragDrop(_selector.Selected!, LinkOrder.Self, 0);
        ImGui.TableNextColumn();
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            using var c = ImRaii.PushColor(ImGuiCol.Text, color);
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(FontAwesomeIcon.ArrowLeftLong.ToIconString());
        }
    }

    private void DrawSubList(IReadOnlyList<DesignLink> list, LinkOrder order)
    {
        using var id = ImRaii.PushId((int)order);

        var buttonSize = new Vector2(ImGui.GetFrameHeight());
        for (var i = 0; i < list.Count; ++i)
        {
            id.Push(i);

            ImGui.TableNextColumn();
            var delete = ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Trash.ToIconString(), buttonSize, "Delete this link.", false, true);
            var (design, flags) = list[i];
            ImGui.TableNextColumn();

            using (ImRaii.PushColor(ImGuiCol.Text, _colorManager.GetColor(design)))
            {
                ImGui.AlignTextToFramePadding();
                ImGui.Selectable(_selector.IncognitoMode ? design.Incognito : design.Name.Text);
            }

            DrawDragDrop(design, order, i);

            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            DrawApplicationBoxes(i, order, flags);

            if (delete)
                _linkManager.RemoveDesignLink(_selector.Selected!, i--, order);
        }
    }

    private void DrawNew()
    {
        var buttonSize = new Vector2(ImGui.GetFrameHeight());
        ImGui.TableNextColumn();
        ImGui.TableNextColumn();
        _combo.Draw(ImGui.GetContentRegionAvail().X);
        ImGui.TableNextColumn();
        string ttBefore,     ttAfter;
        bool   canAddBefore, canAddAfter;
        var    design = _combo.Design as Design;
        if (design == null)
        {
            ttAfter      = ttBefore    = "请先选中一个设计";
            canAddBefore = canAddAfter = false;
        }
        else
        {
            canAddBefore = LinkContainer.CanAddLink(_selector.Selected!, design, LinkOrder.Before, out var error);
            ttBefore = canAddBefore
                ? $"将{design.Name}链接到上方。"
                : $"不能为{design.Name}添加链接：\n{error}";
            canAddAfter = LinkContainer.CanAddLink(_selector.Selected!, design, LinkOrder.After, out error);
            ttAfter = canAddAfter
                ? $"将{design.Name}链接到下方。"
                : $"不能为{design.Name}添加链接：\n{error}";
        }

        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.ArrowCircleUp.ToIconString(), buttonSize, ttBefore, !canAddBefore, true))
        {
            _linkManager.AddDesignLink(_selector.Selected!, design!, LinkOrder.Before);
            _linkManager.MoveDesignLink(_selector.Selected!, _selector.Selected!.Links.Before.Count - 1, LinkOrder.Before, 0, LinkOrder.Before);
        }

        ImGui.SameLine();
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.ArrowCircleDown.ToIconString(), buttonSize, ttAfter, !canAddAfter, true))
            _linkManager.AddDesignLink(_selector.Selected!, design!, LinkOrder.After);
    }

    private void DrawDragDrop(Design design, LinkOrder order, int index)
    {
        using (var source = ImRaii.DragDropSource())
        {
            if (source)
            {
                ImGui.SetDragDropPayload("DraggingLink", IntPtr.Zero, 0);
                ImGui.TextUnformatted($"Reordering {design.Name}...");
                _dragDropIndex = index;
                _dragDropOrder = order;
            }
        }

        using var target = ImRaii.DragDropTarget();
        if (!target)
            return;

        if (!ImGuiUtil.IsDropping("DraggingLink"))
            return;

        _dragDropTargetIndex = index;
        _dragDropTargetOrder = order;
    }

    private void DrawApplicationBoxes(int idx, LinkOrder order, ApplicationType current)
    {
        var newType    = current;
        var newTypeInt = (uint)newType;
        using (ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, ImGuiHelpers.GlobalScale))
        {
            using var _ = ImRaii.PushColor(ImGuiCol.Border, ColorId.FolderLine.Value());
            if (ImGui.CheckboxFlags("##all", ref newTypeInt, (uint)ApplicationType.All))
                newType = (ApplicationType)newTypeInt;
        }

        ImGuiUtil.HoverTooltip("应用规则总开关");

        ImGui.SameLine();
        Box(0);
        ImGui.SameLine();
        Box(1);
        ImGui.SameLine();

        Box(2);
        ImGui.SameLine();
        Box(3);
        ImGui.SameLine();
        Box(4);
        if (newType != current)
            _linkManager.ChangeApplicationType(_selector.Selected!, idx, order, newType);
        return;

        void Box(int i)
        {
            var (applicationType, description) = ApplicationTypeExtensions.Types[i];
            var value = current.HasFlag(applicationType);
            if (ImGui.Checkbox($"##{(byte)applicationType}", ref value))
                newType = value ? newType | applicationType : newType & ~applicationType;
            ImGuiUtil.HoverTooltip(description);
        }
    }
}
