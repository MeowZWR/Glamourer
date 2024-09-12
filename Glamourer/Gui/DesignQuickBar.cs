using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Glamourer.Automation;
using Glamourer.Designs;
using Glamourer.Interop;
using Glamourer.Interop.Structs;
using Glamourer.State;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using Penumbra.GameData.Actors;

namespace Glamourer.Gui;

[Flags]
public enum QdbButtons
{
    ApplyDesign       = 0x01,
    RevertAll         = 0x02,
    RevertAutomation  = 0x04,
    RevertAdvanced    = 0x08,
    RevertEquip       = 0x10,
    RevertCustomize   = 0x20,
    ReapplyAutomation = 0x40,
}

public sealed class DesignQuickBar : Window, IDisposable
{
    private ImGuiWindowFlags GetFlags
        => _config.Ephemeral.LockDesignQuickBar
            ? ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoMove
            : ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoFocusOnAppearing;

    private readonly Configuration     _config;
    private readonly QuickDesignCombo  _designCombo;
    private readonly StateManager      _stateManager;
    private readonly AutoDesignApplier _autoDesignApplier;
    private readonly ObjectManager     _objects;
    private readonly IKeyState         _keyState;
    private readonly ImRaii.Style      _windowPadding  = new();
    private readonly ImRaii.Color      _windowColor    = new();
    private          DateTime          _keyboardToggle = DateTime.UnixEpoch;
    private          int               _numButtons;

    public DesignQuickBar(Configuration config, QuickDesignCombo designCombo, StateManager stateManager, IKeyState keyState,
        ObjectManager objects, AutoDesignApplier autoDesignApplier)
        : base("Glamourer Quick Bar", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoDocking)
    {
        _config             = config;
        _designCombo        = designCombo;
        _stateManager       = stateManager;
        _keyState           = keyState;
        _objects            = objects;
        _autoDesignApplier  = autoDesignApplier;
        IsOpen              = _config.Ephemeral.ShowDesignQuickBar;
        DisableWindowSounds = true;
        Size                = Vector2.Zero;
    }

    public void Dispose()
        => _windowPadding.Dispose();

    public override void PreOpenCheck()
    {
        CheckHotkeys();
        IsOpen = _config.Ephemeral.ShowDesignQuickBar && _config.QdbButtons != 0;
    }

    public override bool DrawConditions()
        => _objects.Player.Valid;

    public override void PreDraw()
    {
        Flags = GetFlags;
        UpdateWidth();

        _windowPadding.Push(ImGuiStyleVar.WindowPadding, new Vector2(ImGuiHelpers.GlobalScale * 4))
            .Push(ImGuiStyleVar.WindowBorderSize, 0);
        _windowColor.Push(ImGuiCol.WindowBg, ColorId.QuickDesignBg.Value())
            .Push(ImGuiCol.Button,  ColorId.QuickDesignButton.Value())
            .Push(ImGuiCol.FrameBg, ColorId.QuickDesignFrame.Value());
    }

    public override void PostDraw()
    {
        _windowPadding.Dispose();
        _windowColor.Dispose();
    }

    public void DrawAtEnd(float yPos)
    {
        var width = UpdateWidth();
        ImGui.SetCursorPos(new Vector2(ImGui.GetWindowContentRegionMax().X - width, yPos - ImGuiHelpers.GlobalScale));
        Draw();
    }

    public override void Draw()
        => Draw(ImGui.GetContentRegionAvail().X);

    private void Draw(float width)
    {
        using var group      = ImRaii.Group();
        var       spacing    = ImGui.GetStyle().ItemInnerSpacing;
        using var style      = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, spacing);
        var       buttonSize = new Vector2(ImGui.GetFrameHeight());
        PrepareButtons();
        if (_config.QdbButtons.HasFlag(QdbButtons.ApplyDesign))
        {
            var comboSize = width - _numButtons * (buttonSize.X + spacing.X);
            _designCombo.Draw(comboSize);
            ImGui.SameLine();
            DrawApplyButton(buttonSize);
        }

        DrawRevertButton(buttonSize);
        DrawRevertEquipButton(buttonSize);
        DrawRevertCustomizeButton(buttonSize);
        DrawRevertAdvancedCustomization(buttonSize);
        DrawRevertAutomationButton(buttonSize);
        DrawReapplyAutomationButton(buttonSize);
    }

    private ActorIdentifier _playerIdentifier;
    private ActorData       _playerData;
    private ActorState?     _playerState;

    private ActorData       _targetData;
    private ActorIdentifier _targetIdentifier;
    private ActorState?     _targetState;

    private void PrepareButtons()
    {
        (_playerIdentifier, _playerData) = _objects.PlayerData;
        (_targetIdentifier, _targetData) = _objects.TargetData;
        _playerState                     = _stateManager.GetValueOrDefault(_playerIdentifier);
        _targetState                     = _stateManager.GetValueOrDefault(_targetIdentifier);
    }

    private void DrawApplyButton(Vector2 size)
    {
        var design    = _designCombo.Design as Design;
        var available = 0;
        var tooltip   = string.Empty;
        if (design == null)
        {
            tooltip = "未选择任何设计。";
        }
        else
        {
            if (_playerIdentifier.IsValid && _playerData.Valid)
            {
                available |= 1;
                tooltip   =  $"左键单击：应用{design.ResolveName(_config.Ephemeral.IncognitoMode)}到你自己。";
            }

            if (_targetIdentifier.IsValid && _targetData.Valid)
            {
                if (available != 0)
                    tooltip += '\n';
                available |= 2;
                tooltip   += $"右键单击：应用{design.ResolveName(_config.Ephemeral.IncognitoMode)}到{_targetIdentifier}。";
            }

            if (available == 0)
                tooltip = "玩家和目标都不可用。";
        }


        var (clicked, id, data, state) = ResolveTarget(FontAwesomeIcon.PlayCircle, size, tooltip, available);
        ImGui.SameLine();
        if (!clicked)
            return;

        if (state == null && !_stateManager.GetOrCreate(id, data.Objects[0], out state))
        {
            Glamourer.Messager.NotificationMessage(
                $"C无法应用{design!.ResolveName(true)}到{id.Incognito(null)}：无法创建状态。");
            return;
        }

        using var _ = design!.TemporarilyRestrictApplication(ApplicationCollection.FromKeys());
        _stateManager.ApplyDesign(state, design, ApplySettings.ManualWithLinks);
    }

    private void DrawRevertButton(Vector2 buttonSize)
    {
        if (!_config.QdbButtons.HasFlag(QdbButtons.RevertAll))
            return;

        var available = 0;
        var tooltip   = string.Empty;
        if (_playerIdentifier.IsValid && _playerState is { IsLocked: false })
        {
            available |= 1;
            tooltip   = "左键单击：将玩家角色恢复到游戏状态。";
        }

        if (_targetIdentifier.IsValid && _targetState is { IsLocked: false })
        {
            if (available != 0)
                tooltip += '\n';
            available |= 2;
            tooltip   += $"右键单击：将{_targetIdentifier}恢复到游戏状态。";
        }

        if (available == 0)
            tooltip = "玩家角色和目标都不可用，被Glamourer修改了状态，或者他们的状态被锁定。";

        var (clicked, _, _, state) = ResolveTarget(FontAwesomeIcon.UndoAlt, buttonSize, tooltip, available);
        ImGui.SameLine();
        if (clicked)
            _stateManager.ResetState(state!, StateSource.Manual);
    }

    private void DrawRevertAutomationButton(Vector2 buttonSize)
    {
        if (!_config.EnableAutoDesigns)
            return;

        if (!_config.QdbButtons.HasFlag(QdbButtons.RevertAutomation))
            return;

        var available = 0;
        var tooltip   = string.Empty;

        if (_playerIdentifier.IsValid && _playerState is { IsLocked: false } && _playerData.Valid)
        {
            available |= 1;
            tooltip   = "左键单击：将玩家角色恢复到自动执行状态。";
        }

        if (_targetIdentifier.IsValid && _targetState is { IsLocked: false } && _targetData.Valid)
        {
            if (available != 0)
                tooltip += '\n';
            available |= 2;
            tooltip   += $"右键单击：将{_targetIdentifier}恢复到自动执行状态。";
        }

        if (available == 0)
            tooltip = "玩家角色和目标都不可用，被Glamourer修改了状态，或者他们的状态被锁定。";

        var (clicked, id, data, state) = ResolveTarget(FontAwesomeIcon.SyncAlt, buttonSize, tooltip, available);
        ImGui.SameLine();
        if (!clicked)
            return;

        foreach (var actor in data.Objects)
        {
            _autoDesignApplier.ReapplyAutomation(actor, id, state!, true, out var forcedRedraw);
            _stateManager.ReapplyState(actor, forcedRedraw, StateSource.Manual);
        }
    }

    private void DrawReapplyAutomationButton(Vector2 buttonSize)
    {
        if (!_config.EnableAutoDesigns)
            return;

        if (!_config.QdbButtons.HasFlag(QdbButtons.ReapplyAutomation))
            return;

        var available = 0;
        var tooltip   = string.Empty;

        if (_playerIdentifier.IsValid && _playerState is { IsLocked: false } && _playerData.Valid)
        {
            available |= 1;
            tooltip   =  "左键单击：在玩家角色当前状态的基础上重新应用其当前的自动执行。";
        }

        if (_targetIdentifier.IsValid && _targetState is { IsLocked: false } && _targetData.Valid)
        {
            if (available != 0)
                tooltip += '\n';
            available |= 2;
            tooltip   += $"右键单击：在{_targetIdentifier}当前状态的基础上重新应用其当前的自动执行。";
        }

        if (available == 0)
            tooltip = "玩家角色和目标均不可用，由 Glamourer 修改了状态，或者它们的状态已被锁定。";

        var (clicked, id, data, state) = ResolveTarget(FontAwesomeIcon.Repeat, buttonSize, tooltip, available);
        ImGui.SameLine();
        if (!clicked)
            return;

        foreach (var actor in data.Objects)
        {
            _autoDesignApplier.ReapplyAutomation(actor, id, state!, false, out var forcedRedraw);
            _stateManager.ReapplyState(actor, forcedRedraw, StateSource.Manual);
        }
    }

    private void DrawRevertAdvancedCustomization(Vector2 buttonSize)
    {
        if (!_config.UseAdvancedParameters)
            return;

        if (!_config.QdbButtons.HasFlag(QdbButtons.RevertAdvanced))
            return;

        var available = 0;
        var tooltip   = string.Empty;

        if (_playerIdentifier.IsValid && _playerState is { IsLocked: false } && _playerData.Valid)
        {
            available |= 1;
            tooltip   = "左键单击：将玩家角色的高级外貌、高级染料设置还原为游戏状态。";
        }

        if (_targetIdentifier.IsValid && _targetState is { IsLocked: false } && _targetData.Valid)
        {
            if (available != 0)
                tooltip += '\n';
            available |= 2;
            tooltip   += $"右键单击：将{_targetIdentifier}高级外貌、高级染料设置还原为游戏状态。";
        }

        if (available == 0)
            tooltip = "玩家角色和目标都不可用，或者他们的状态被锁定。";

        var (clicked, _, _, state) = ResolveTarget(FontAwesomeIcon.Palette, buttonSize, tooltip, available);
        ImGui.SameLine();
        if (clicked)
            _stateManager.ResetAdvancedState(state!, StateSource.Manual);
    }

    private void DrawRevertCustomizeButton(Vector2 buttonSize)
    {
        if (!_config.QdbButtons.HasFlag(QdbButtons.RevertCustomize))
            return;

        var available = 0;
        var tooltip   = string.Empty;

        if (_playerIdentifier.IsValid && _playerState is { IsLocked: false } && _playerData.Valid)
        {
            available |= 1;
            tooltip   = "左键单击：将玩家角色的外貌设置恢复到游戏状态。";
        }

        if (_targetIdentifier.IsValid && _targetState is { IsLocked: false } && _targetData.Valid)
        {
            if (available != 0)
                tooltip += '\n';
            available |= 2;
            tooltip   += $"右键单击：恢复{_targetIdentifier}的外貌设置恢复到游戏状态。";
        }

        if (available == 0)
            tooltip = "玩家角色和目标都不可用，或者他们的状态被锁定。";

        var (clicked, _, _, state) = ResolveTarget(FontAwesomeIcon.User, buttonSize, tooltip, available);
        ImGui.SameLine();
        if (clicked)
            _stateManager.ResetCustomize(state!, StateSource.Manual);
    }

    private void DrawRevertEquipButton(Vector2 buttonSize)
    {
        if (!_config.QdbButtons.HasFlag(QdbButtons.RevertEquip))
            return;

        var available = 0;
        var tooltip   = string.Empty;

        if (_playerIdentifier.IsValid && _playerState is { IsLocked: false } && _playerData.Valid)
        {
            available |= 1;
            tooltip   = "左键单击：将玩家的装备恢复到游戏状态。";
        }

        if (_targetIdentifier.IsValid && _targetState is { IsLocked: false } && _targetData.Valid)
        {
            if (available != 0)
                tooltip += '\n';
            available |= 2;
            tooltip   += $"右键单击：将{_targetIdentifier}的装备恢复到游戏状态。";
        }

        if (available == 0)
            tooltip = "玩家角色和目标都不可用，或者他们的状态被锁定。";

        var (clicked, _, _, state) = ResolveTarget(FontAwesomeIcon.Vest, buttonSize, tooltip, available);
        ImGui.SameLine();
        if (clicked)
            _stateManager.ResetEquip(state!, StateSource.Manual);
    }

    private (bool, ActorIdentifier, ActorData, ActorState?) ResolveTarget(FontAwesomeIcon icon, Vector2 buttonSize, string tooltip,
        int available)
    {
        ImGuiUtil.DrawDisabledButton(icon.ToIconString(), buttonSize, tooltip, available == 0, true);
        if ((available & 1) == 1 && ImGui.IsItemClicked(ImGuiMouseButton.Left))
            return (true, _playerIdentifier, _playerData, _playerState);
        if ((available & 2) == 2 && ImGui.IsItemClicked(ImGuiMouseButton.Right))
            return (true, _targetIdentifier, _targetData, _targetState);

        return (false, ActorIdentifier.Invalid, ActorData.Invalid, null);
    }

    private void CheckHotkeys()
    {
        if (_keyboardToggle > DateTime.UtcNow || !CheckKeyState(_config.ToggleQuickDesignBar, false))
            return;

        _keyboardToggle                      = DateTime.UtcNow.AddMilliseconds(500);
        _config.Ephemeral.ShowDesignQuickBar = !_config.Ephemeral.ShowDesignQuickBar;
        _config.Ephemeral.Save();
    }

    private bool CheckKeyState(ModifiableHotkey key, bool noKey)
    {
        if (key.Hotkey == VirtualKey.NO_KEY)
            return noKey;

        return _keyState[key.Hotkey] && key.Modifier1.IsActive() && key.Modifier2.IsActive();
    }

    private float UpdateWidth()
    {
        _numButtons = 0;
        if (_config.QdbButtons.HasFlag(QdbButtons.RevertAll))
            ++_numButtons;
        if (_config.EnableAutoDesigns)
        {
            if (_config.QdbButtons.HasFlag(QdbButtons.RevertAutomation))
                ++_numButtons;
            if (_config.QdbButtons.HasFlag(QdbButtons.ReapplyAutomation))
                ++_numButtons;
        }

        if ((_config.UseAdvancedParameters || _config.UseAdvancedDyes) && _config.QdbButtons.HasFlag(QdbButtons.RevertAdvanced))
            ++_numButtons;
        if (_config.QdbButtons.HasFlag(QdbButtons.RevertCustomize))
            ++_numButtons;
        if (_config.QdbButtons.HasFlag(QdbButtons.RevertEquip))
            ++_numButtons;
        if (_config.QdbButtons.HasFlag(QdbButtons.ApplyDesign))
        {
            ++_numButtons;
            Size = new Vector2((7 + _numButtons) * ImGui.GetFrameHeight() + _numButtons * ImGui.GetStyle().ItemInnerSpacing.X,
                ImGui.GetFrameHeight());
        }
        else
        {
            Size = new Vector2(
                _numButtons * ImGui.GetFrameHeight()
              + (_numButtons - 1) * ImGui.GetStyle().ItemInnerSpacing.X
              + ImGui.GetStyle().WindowPadding.X * 2,
                ImGui.GetFrameHeight());
        }

        return Size.Value.X;
    }
}
