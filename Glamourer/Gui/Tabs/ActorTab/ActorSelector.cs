using System.Security.AccessControl;
using Dalamud.Interface;
using Glamourer.Interop;
using Glamourer.Interop.Structs;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using OtterGui.Text;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;

namespace Glamourer.Gui.Tabs.ActorTab;

public class ActorSelector(ObjectManager objects, ActorManager actors, EphemeralConfig config)
{
    private ActorIdentifier _identifier = ActorIdentifier.Invalid;

    public bool IncognitoMode
    {
        get => config.IncognitoMode;
        set
        {
            config.IncognitoMode = value;
            config.Save();
        }
    }

    private LowerString _actorFilter = LowerString.Empty;
    private Vector2     _defaultItemSpacing;
    private WorldId     _world;
    private float       _width;

    public (ActorIdentifier Identifier, ActorData Data) Selection
        => objects.TryGetValue(_identifier, out var data) ? (_identifier, data) : (_identifier, ActorData.Invalid);

    public bool HasSelection
        => _identifier.IsValid;

    public void Draw(float width)
    {
        _width = width;
        using var group = ImUtf8.Group();
        _defaultItemSpacing = ImGui.GetStyle().ItemSpacing;
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero)
            .Push(ImGuiStyleVar.FrameRounding, 0);
        ImGui.SetNextItemWidth(_width);
        LowerString.InputWithHint("##actorFilter", "筛选...", ref _actorFilter, 64);
        if (ImGui.IsItemHovered())
        {
            using var tt = ImUtf8.Tooltip();
            ImUtf8.Text("筛选包含输入内容的名字。"u8);
            ImGui.Dummy(new Vector2(0, ImGui.GetTextLineHeight() / 2));
            ImUtf8.Text("指定筛选规则为:"u8);
            var color = ColorId.HeaderButtons.Value();
            ImUtf8.Text("<p>"u8, color);
            ImGui.SameLine(0, 0);
            ImUtf8.Text(": 仅显示玩家角色。"u8);

            ImUtf8.Text("<o>"u8, color);
            ImGui.SameLine(0, 0);
            ImUtf8.Text(": 仅显示已拥有的游戏对象。"u8);

            ImUtf8.Text("<n>"u8, color);
            ImGui.SameLine(0, 0);
            ImUtf8.Text(": 仅显示NPC。"u8);

            ImUtf8.Text("<r>"u8, color);
            ImGui.SameLine(0, 0);
            ImUtf8.Text(": 仅显示雇员。"u8);

            ImUtf8.Text("<s>"u8, color);
            ImGui.SameLine(0, 0);
            ImUtf8.Text(": 仅显示特殊屏幕角色。"u8);

            ImUtf8.Text("<w>"u8, color);
            ImGui.SameLine(0, 0);
            ImUtf8.Text(": 仅显示来自你所在世界的玩家。"u8);
        }

        DrawSelector();
        DrawSelectionButtons();
    }

    private void DrawSelector()
    {
        using var child = ImUtf8.Child("##Selector"u8, new Vector2(_width, -ImGui.GetFrameHeight()), true);
        if (!child)
            return;

        objects.Update();
        _world = new WorldId(objects.Player.Valid ? objects.Player.HomeWorld : (ushort)0);
        using var style     = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, _defaultItemSpacing);
        var       skips     = ImGuiClip.GetNecessarySkips(ImGui.GetTextLineHeight());
        var       remainder = ImGuiClip.FilteredClippedDraw(objects.Identifiers, skips, CheckFilter, DrawSelectable);
        ImGuiClip.DrawEndDummy(remainder, ImGui.GetTextLineHeight());
    }

    private bool CheckFilter(KeyValuePair<ActorIdentifier, ActorData> pair)
        => _actorFilter.Lower switch
        {
            ""    => true,
            "<p>" => pair.Key.Type is IdentifierType.Player,
            "<o>" => pair.Key.Type is IdentifierType.Owned,
            "<n>" => pair.Key.Type is IdentifierType.Npc,
            "<r>" => pair.Key.Type is IdentifierType.Retainer,
            "<s>" => pair.Key.Type is IdentifierType.Special,
            "<w>" => pair.Key.Type is IdentifierType.Player && pair.Key.HomeWorld == _world,
            _     => _actorFilter.IsContained(pair.Value.Label),
        };

    private void DrawSelectable(KeyValuePair<ActorIdentifier, ActorData> pair)
    {
        var equals = pair.Key.Equals(_identifier);
        if (ImUtf8.Selectable(IncognitoMode ? pair.Key.Incognito(pair.Value.Label) : pair.Value.Label, equals) && !equals)
            _identifier = pair.Key.CreatePermanent();
    }

    private void DrawSelectionButtons()
    {
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero)
            .Push(ImGuiStyleVar.FrameRounding, 0);
        var buttonWidth = new Vector2(_width / 2, 0);

        if (ImUtf8.IconButton(FontAwesomeIcon.UserCircle, "选择本地玩家角色。"u8, buttonWidth, !objects.Player))
            _identifier = objects.Player.GetIdentifier(actors);

        ImGui.SameLine();
        var (id, data) = objects.TargetData;
        var tt = data.Valid ? $"选择列表中的当前目标 {id}。" :
            id.IsValid      ? $"目标 {id} 不在列表中。" : "未选择目标。";
        if (ImUtf8.IconButton(FontAwesomeIcon.HandPointer, tt, buttonWidth, objects.IsInGPose || !data.Valid))
            _identifier = id;
    }
}
