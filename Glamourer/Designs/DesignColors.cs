using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility.Raii;
using Glamourer.Gui;
using Glamourer.Services;
using ImGuiNET;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OtterGui;
using OtterGui.Classes;

namespace Glamourer.Designs;

public class DesignColorUi(DesignColors colors, Configuration config)
{
    private string _newName = string.Empty;

    public void Draw()
    {
        using var table = ImRaii.Table("designColors", 3, ImGuiTableFlags.RowBg);
        if (!table)
            return;

        var   changeString = string.Empty;
        uint? changeValue  = null;

        var buttonSize = new Vector2(ImGui.GetFrameHeight());
        ImGui.TableSetupColumn("##Delete",   ImGuiTableColumnFlags.WidthFixed, buttonSize.X);
        ImGui.TableSetupColumn("##Select",   ImGuiTableColumnFlags.WidthFixed, buttonSize.X);
        ImGui.TableSetupColumn("颜色名称", ImGuiTableColumnFlags.WidthStretch);

        ImGui.TableHeadersRow();

        ImGui.TableNextColumn();
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Recycle.ToIconString(), buttonSize,
                "还原被用于已不存在的设计的颜色到默认状态。", colors.MissingColor == DesignColors.MissingColorDefault,
                true))
        {
            changeString = DesignColors.MissingColorName;
            changeValue  = DesignColors.MissingColorDefault;
        }

        ImGui.TableNextColumn();
        if (DrawColorButton(DesignColors.MissingColorName, colors.MissingColor, out var newColor))
        {
            changeString = DesignColors.MissingColorName;
            changeValue  = newColor;
        }

        ImGui.TableNextColumn();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
        ImGui.TextUnformatted(DesignColors.MissingColorName);
        ImGuiUtil.HoverTooltip("当设计中指定的颜色不可用时，将使用此颜色。");


        var disabled = !config.DeleteDesignModifier.IsActive();
        var tt       = "删除此颜色。但并不会将其从正在使用它的设计中删除。";
        if (disabled)
            tt += $"\n按住{config.DeleteDesignModifier}来删除。";

        foreach (var ((name, color), idx) in colors.WithIndex())
        {
            using var id = ImRaii.PushId(idx);
            ImGui.TableNextColumn();

            if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Trash.ToIconString(), buttonSize, tt, disabled, true))
            {
                changeString = name;
                changeValue  = null;
            }

            ImGui.TableNextColumn();
            if (DrawColorButton(name, color, out newColor))
            {
                changeString = name;
                changeValue  = newColor;
            }

            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X);
            ImGui.TextUnformatted(name);
        }

        ImGui.TableNextColumn();
        (tt, disabled) = _newName.Length == 0
            ? ("请先指定新颜色的名称。", true)
            : _newName is DesignColors.MissingColorName or DesignColors.AutomaticName
                ? ($"你不能使用这个名称 {DesignColors.MissingColorName} 或 {DesignColors.AutomaticName}，请选择不同的名称。", true)
                : colors.ContainsKey(_newName)
                    ? ($"此颜色名称：{_newName}已经存在，请选择不同的名称。", true)
                    : ($"添加新颜色：{_newName}到你的列表。", false);
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(), buttonSize, tt, disabled, true))
        {
            changeString = _newName;
            changeValue  = 0xFFFFFFFF;
        }

        ImGui.TableNextColumn();
        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (ImGui.InputTextWithHint("##newDesignColor", "新颜色名称...", ref _newName, 64, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            changeString = _newName;
            changeValue  = 0xFFFFFFFF;
        }


        if (changeString.Length > 0)
        {
            if (!changeValue.HasValue)
                colors.DeleteColor(changeString);
            else
                colors.SetColor(changeString, changeValue.Value);
        }
    }

    public static bool DrawColorButton(string tooltip, uint color, out uint newColor)
    {
        var vec = ImGui.ColorConvertU32ToFloat4(color);
        if (!ImGui.ColorEdit4(tooltip, ref vec, ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.NoInputs))
        {
            ImGuiUtil.HoverTooltip(tooltip);
            newColor = color;
            return false;
        }

        ImGuiUtil.HoverTooltip(tooltip);

        newColor = ImGui.ColorConvertFloat4ToU32(vec);
        return newColor != color;
    }
}

public class DesignColors : ISavable, IReadOnlyDictionary<string, uint>
{
    public const string AutomaticName       = "自动配色";
    public const string MissingColorName    = "缺失颜色";
    public const uint   MissingColorDefault = 0xFF0000D0;

    private readonly SaveService              _saveService;
    private readonly Dictionary<string, uint> _colors = [];
    public           uint                     MissingColor { get; private set; } = MissingColorDefault;

    public event Action? ColorChanged;

    public DesignColors(SaveService saveService)
    {
        _saveService = saveService;
        Load();
    }

    public uint GetColor(Design? design)
    {
        if (design == null)
            return ColorId.NormalDesign.Value();

        if (design.Color.Length == 0)
            return AutoColor(design);

        return TryGetValue(design.Color, out var color) ? color : MissingColor;
    }

    public void SetColor(string key, uint newColor)
    {
        if (key.Length == 0)
            return;

        if (key is MissingColorName && MissingColor != newColor)
        {
            MissingColor = newColor;
            SaveAndInvoke();
            return;
        }

        if (_colors.TryAdd(key, newColor))
        {
            SaveAndInvoke();
            return;
        }

        _colors.TryGetValue(key, out var color);
        _colors[key] = newColor;

        if (color != newColor)
            SaveAndInvoke();
    }

    private void SaveAndInvoke()
    {
        ColorChanged?.Invoke();
        _saveService.DelaySave(this, TimeSpan.FromSeconds(2));
    }

    public void DeleteColor(string key)
    {
        if (_colors.Remove(key))
            SaveAndInvoke();
    }

    public string ToFilename(FilenameService fileNames)
        => fileNames.DesignColorFile;

    public void Save(StreamWriter writer)
    {
        var jObj = new JObject
        {
            ["Version"]      = 1,
            ["MissingColor"] = MissingColor,
            ["Definitions"]  = JToken.FromObject(_colors),
        };
        writer.Write(jObj.ToString(Formatting.Indented));
    }

    private void Load()
    {
        _colors.Clear();
        var file = _saveService.FileNames.DesignColorFile;
        if (!File.Exists(file))
            return;

        try
        {
            var text    = File.ReadAllText(file);
            var jObj    = JObject.Parse(text);
            var version = jObj["Version"]?.ToObject<int>() ?? 0;
            switch (version)
            {
                case 1:
                {
                    var dict = jObj["Definitions"]?.ToObject<Dictionary<string, uint>>() ?? new Dictionary<string, uint>();
                    _colors.EnsureCapacity(dict.Count);
                    foreach (var kvp in dict)
                        _colors.Add(kvp.Key, kvp.Value);
                    MissingColor = jObj["MissingColor"]?.ToObject<uint>() ?? MissingColorDefault;
                    break;
                }
                default: throw new Exception($"Unknown Version {version}");
            }
        }
        catch (Exception ex)
        {
            Glamourer.Messager.NotificationMessage(ex, "Could not read design color file.", NotificationType.Error);
        }
    }

    public IEnumerator<KeyValuePair<string, uint>> GetEnumerator()
        => _colors.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public int Count
        => _colors.Count;

    public bool ContainsKey(string key)
        => _colors.ContainsKey(key);

    public bool TryGetValue(string key, out uint value)
    {
        if (_colors.TryGetValue(key, out value))
        {
            if (value == 0)
                value = ImGui.GetColorU32(ImGuiCol.Text);
            return true;
        }

        return false;
    }

    public static uint AutoColor(DesignBase design)
    {
        var customize = design.ApplyCustomizeExcludingBodyType == 0;
        var equip     = design.Application.Equip == 0;
        return (customize, equip) switch
        {
            (true, true)   => ColorId.StateDesign.Value(),
            (true, false)  => ColorId.EquipmentDesign.Value(),
            (false, true)  => ColorId.CustomizationDesign.Value(),
            (false, false) => ColorId.NormalDesign.Value(),
        };
    }

    public uint this[string key]
        => _colors[key];

    public IEnumerable<string> Keys
        => _colors.Keys;

    public IEnumerable<uint> Values
        => _colors.Values;
}
