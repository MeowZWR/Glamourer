using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Glamourer.Automation;
using Glamourer.Events;
using Glamourer.Interop;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using Penumbra.GameData.Actors;
using Penumbra.String;
using ImGuiClip = OtterGui.ImGuiClip;

namespace Glamourer.Gui.Tabs.AutomationTab;

public class SetSelector : IDisposable
{
    private readonly Configuration              _config;
    private readonly AutoDesignManager          _manager;
    private readonly AutomationChanged          _event;
    private readonly ActorManager               _actors;
    private readonly ObjectManager              _objects;
    private readonly List<(AutoDesignSet, int)> _list = [];

    public AutoDesignSet? Selection      { get; private set; }
    public int            SelectionIndex { get; private set; } = -1;

    public bool IncognitoMode
    {
        get => _config.Ephemeral.IncognitoMode;
        set
        {
            _config.Ephemeral.IncognitoMode = value;
            _config.Ephemeral.Save();
        }
    }

    private int     _dragIndex = -1;
    private Action? _endAction;

    internal int _dragDesignIndex = -1;

    public SetSelector(AutoDesignManager manager, AutomationChanged @event, Configuration config, ActorManager actors, ObjectManager objects)
    {
        _manager = manager;
        _event   = @event;
        _config  = config;
        _actors  = actors;
        _objects = objects;
        _event.Subscribe(OnAutomationChange, AutomationChanged.Priority.SetSelector);
    }

    public void Dispose()
    {
        _event.Unsubscribe(OnAutomationChange);
    }

    public string SelectionName
        => GetSetName(Selection, SelectionIndex);

    public string GetSetName(AutoDesignSet? set, int index)
        => set == null ? "未选择" : IncognitoMode ? $"自动化执行集 #{index + 1}" : set.Name;

    private void OnAutomationChange(AutomationChanged.Type type, AutoDesignSet? set, object? data)
    {
        switch (type)
        {
            case AutomationChanged.Type.DeletedSet:
                if (set == Selection)
                {
                    SelectionIndex = _manager.Count == 0 ? -1 : SelectionIndex == 0 ? 0 : SelectionIndex - 1;
                    Selection      = SelectionIndex >= 0 ? _manager[SelectionIndex] : null;
                }

                _dirty = true;
                break;
            case AutomationChanged.Type.AddedSet:
                SelectionIndex = (((int, string))data!).Item1;
                Selection      = set!;
                _dirty         = true;
                break;
            case AutomationChanged.Type.MovedSet:
                _dirty               = true;
                var (oldIdx, newIdx) = ((int, int))data!;
                if (SelectionIndex == oldIdx)
                    SelectionIndex = newIdx;
                break;
            case AutomationChanged.Type.RenamedSet:
            case AutomationChanged.Type.ChangeIdentifier:
            case AutomationChanged.Type.ToggleSet:
                _dirty = true;
                break;
        }
    }

    private LowerString _filter        = LowerString.Empty;
    private uint        _enabledFilter = 0;
    private float       _width;
    private Vector2     _defaultItemSpacing;
    private Vector2     _selectableSize;
    private bool        _dirty = true;

    private bool CheckFilters(AutoDesignSet set, string identifierString)
    {
        if (_enabledFilter switch
            {
                1 => set.Enabled,
                3 => !set.Enabled,
                _ => false,
            })
            return false;

        if (!_filter.IsEmpty && !_filter.IsContained(set.Name) && !_filter.IsContained(identifierString))
            return false;

        return true;
    }

    private void UpdateList()
    {
        if (!_dirty)
            return;

        _list.Clear();
        foreach (var (set, idx) in _manager.WithIndex())
        {
            var id = set.Identifiers[0].ToString();
            if (CheckFilters(set, id))
                _list.Add((set, idx));
        }
    }

    public bool HasSelection
        => Selection != null;

    public void Draw(float width)
    {
        _width = width;
        using var group = ImRaii.Group();
        _defaultItemSpacing = ImGui.GetStyle().ItemSpacing;
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero)
            .Push(ImGuiStyleVar.FrameRounding, 0);
        ImGui.SetNextItemWidth(_width - ImGui.GetFrameHeight());
        if (LowerString.InputWithHint("##filter", "筛选...", ref _filter, 64))
            _dirty = true;
        ImGui.SameLine();
        var f = _enabledFilter;

        if (ImGui.CheckboxFlags("##enabledFilter", ref f, 3))
        {
            _enabledFilter = _enabledFilter switch
            {
                0 => 3,
                3 => 1,
                _ => 0,
            };
            _dirty = true;
        }

        var pos = ImGui.GetItemRectMin();
        pos.X -= ImGuiHelpers.GlobalScale;
        ImGui.GetWindowDrawList().AddLine(pos, pos with { Y = ImGui.GetItemRectMax().Y }, ImGui.GetColorU32(ImGuiCol.Border),
            ImGuiHelpers.GlobalScale);

        ImGuiUtil.HoverTooltip("筛选切换仅显示已启用项目或已禁用项目。");

        DrawSelector();
        DrawSelectionButtons();
    }

    private void DrawSelector()
    {
        using var child = ImRaii.Child("##Selector", new Vector2(_width, -ImGui.GetFrameHeight()), true);
        if (!child)
            return;

        UpdateList();
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, _defaultItemSpacing);
        _selectableSize = new Vector2(0, 2 * ImGui.GetTextLineHeight() + ImGui.GetStyle().ItemSpacing.Y);
        _objects.Update();
        ImGuiClip.ClippedDraw(_list, DrawSetSelectable, _selectableSize.Y + 2 * ImGui.GetStyle().ItemSpacing.Y);
        _endAction?.Invoke();
        _endAction = null;
    }

    private void DrawSetSelectable((AutoDesignSet Set, int Index) pair)
    {
        using var id = ImRaii.PushId(pair.Index);
        using (var color = ImRaii.PushColor(ImGuiCol.Text, pair.Set.Enabled ? ColorId.EnabledAutoSet.Value() : ColorId.DisabledAutoSet.Value()))
        {
            if (ImGui.Selectable(GetSetName(pair.Set, pair.Index), pair.Set == Selection, ImGuiSelectableFlags.None, _selectableSize))
            {
                Selection      = pair.Set;
                SelectionIndex = pair.Index;
            }
        }

        var lineEnd   = ImGui.GetItemRectMax();
        var lineStart = new Vector2(ImGui.GetItemRectMin().X, lineEnd.Y);
        ImGui.GetWindowDrawList().AddLine(lineStart, lineEnd, ImGui.GetColorU32(ImGuiCol.Border), ImGuiHelpers.GlobalScale);

        DrawDragDrop(pair.Set, pair.Index);

        var text = pair.Set.Identifiers[0].ToString();
        if (IncognitoMode)
            text = pair.Set.Identifiers[0].Incognito(text);
        var textSize  = ImGui.CalcTextSize(text);
        var textColor = pair.Set.Identifiers.Any(_objects.ContainsKey) ? ColorId.AutomationActorAvailable : ColorId.AutomationActorUnavailable;
        ImGui.SetCursorPos(new Vector2(ImGui.GetContentRegionAvail().X - textSize.X,
            ImGui.GetCursorPosY() - ImGui.GetTextLineHeightWithSpacing()));
        ImGuiUtil.TextColored(textColor.Value(), text);
    }

    private void DrawSelectionButtons()
    {
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero)
            .Push(ImGuiStyleVar.FrameRounding, 0);
        var buttonWidth = new Vector2(_width / 4, 0);
        NewSetButton(buttonWidth);
        ImGui.SameLine();
        DuplicateSetButton(buttonWidth);
        ImGui.SameLine();
        HelpButton(buttonWidth);
        ImGui.SameLine();
        DeleteSetButton(buttonWidth);
    }

    private static void HelpButton(Vector2 size)
    {
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.QuestionCircle.ToIconString(), size, "自动执行集是如何工作的？", false, true))
            ImGui.OpenPopup("Automation Help");

        static void HalfLine()
            => ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));

        const string longestLine =
            "执行集中的角色设计遵循一个大概的执行规则，如果需要更精确的控制，请使用“角色设计”中的“应用规则”。";

        ImGuiUtil.HelpPopup("Automation Help",
            new Vector2(ImGui.CalcTextSize(longestLine).X + 50 * ImGuiHelpers.GlobalScale, 33 * ImGui.GetTextLineHeightWithSpacing()), () =>
            {
                HalfLine();
                ImGui.TextUnformatted("什么是自动执行？");
                ImGui.BulletText("自动执行可帮助你在特定条件下自动将“角色设计”应用于指定角色。");
                HalfLine();

                ImGui.TextUnformatted("设计自动执行集");
                ImGui.BulletText("首先，你应该创建一个自动执行集。它可以... ");
                using var indent = ImRaii.PushIndent();
                ImGuiUtil.BulletTextColored(ColorId.EnabledAutoSet.Value(),  "... 被启用，或者");
                ImGuiUtil.BulletTextColored(ColorId.DisabledAutoSet.Value(), "... 被禁用。");
                indent.Pop(1);
                ImGui.BulletText("你可以新建一个空白的执行集，或在现有执行集上创建一个副本。");
                ImGui.BulletText("你可以对执行集随意命名。");
                ImGui.BulletText("你可以在左侧选择器中使用鼠标拖拽执行集对它们排序。");
                ImGui.BulletText("每个自动执行集都需要指定一个角色。");
                indent.Push();
                ImGui.BulletText("创建时，会将其指定给当前玩家的角色。");
                ImGui.BulletText("你可以将执行集分配给任何玩家、雇员、服装模特和大多数人类NPC。");
                ImGui.BulletText("每个角色只能同时启用一个自动执行集。");
                indent.Push();
                ImGui.BulletText("启用一个角色的执行集，会自动禁用他的上一个执行集。");
                indent.Pop(2);

                HalfLine();
                ImGui.TextUnformatted("执行集中的角色设计");
                ImGui.BulletText("单个自动执行集可以包含多个角色设计，每个角色设计可以按不同的条件来执行。");
                ImGui.BulletText(
                    "这些角色设计的排序也可以通过鼠标拖拽进行更改，排序也与执行规则有关。");
                ImGui.BulletText("自动执行集将同时使用自己的粗略规则和角色设计中的详细规则。");
                ImGui.BulletText("角色设计可以被分配给指定的职业或职能类型，使其只在对应职业上生效。");
                ImGui.BulletText("还有一个特殊选项“还原”，可用来将指定栏位重置为游戏值。");
                ImGui.BulletText(
                    "执行集也有优先级，从上到下进行应用，无论是在当前Glamourer已生效的状态，还是在游戏角色实际状态上。");
                ImGui.BulletText("要应用某个值（装备、染色、外观的值），它需要：");
                indent.Push();
                ImGui.BulletText("在角色设计中已配置应用规则。");
                ImGui.BulletText("在执行集中已配置执行规则。");
                ImGui.BulletText("满足执行规则的条件。");
                ImGui.BulletText("是角色当前状态（其自己的应用规则）上的有效值。");
                ImGui.BulletText("未在优先级更高的角色设计中应用过相同的值。");
                indent.Pop(1);
            });
    }

    private void NewSetButton(Vector2 size)
    {
        var id = _actors.GetCurrentPlayer();
        if (!id.IsValid)
            id = _actors.CreatePlayer(ByteString.FromSpanUnsafe("New Design"u8, true, false, true), ushort.MaxValue);
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(), size,
                $"新建一个自动执行项目给{id}。关联角色名字可在创建成功后修改。", !id.IsValid, true))
            _manager.AddDesignSet("新建自动执行集", id);
    }

    private void DuplicateSetButton(Vector2 size)
    {
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Clone.ToIconString(), size, "复制当前选中的自动执行集。",
                Selection == null, true))
            _manager.DuplicateDesignSet(Selection!);
    }


    private void DeleteSetButton(Vector2 size)
    {
        var keyValid = _config.DeleteDesignModifier.IsActive();
        var (disabled, tt) = HasSelection
            ? keyValid
                ? (false, "删除选中的项目。")
                : (true, $"删除选中的项目。\n按住 {_config.DeleteDesignModifier} 点击来删除。")
            : (true, "没有选中自动执行项目。");
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Trash.ToIconString(), size, tt, disabled, true))
            _manager.DeleteDesignSet(SelectionIndex);
    }

    private void DrawDragDrop(AutoDesignSet set, int index)
    {
        const string dragDropLabel = "DesignSetDragDrop";
        using (var target = ImRaii.DragDropTarget())
        {
            if (target.Success)
            {
                if (ImGuiUtil.IsDropping(dragDropLabel))
                {
                    if (_dragIndex >= 0)
                    {
                        var idx = _dragIndex;
                        _endAction = () => _manager.MoveSet(idx, index);
                    }

                    _dragIndex = -1;
                }
                else if (ImGuiUtil.IsDropping("DesignDragDrop"))
                {
                    if (_dragDesignIndex >= 0)
                    {
                        var idx     = _dragDesignIndex;
                        var setTo   = set;
                        var setFrom = Selection!;
                        _endAction = () => _manager.MoveDesignToSet(setFrom, idx, setTo);
                    }

                    _dragDesignIndex = -1;
                }
            }
        }

        using (var source = ImRaii.DragDropSource())
        {
            if (source)
            {
                ImGui.TextUnformatted($"移动来自第{index + 1}行的自动执行项目 {GetSetName(set, index)}...");
                if (ImGui.SetDragDropPayload(dragDropLabel, nint.Zero, 0))
                    _dragIndex = index;
            }
        }
    }
}
