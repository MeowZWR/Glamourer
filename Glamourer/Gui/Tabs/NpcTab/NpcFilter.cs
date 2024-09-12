using Glamourer.Designs;
using Glamourer.GameData;
using OtterGui.Classes;

namespace Glamourer.Gui.Tabs.NpcTab;

public sealed class NpcFilter(LocalNpcAppearanceData _favorites) : FilterUtility<NpcData>
{
    protected override string Tooltip
        => "按指定字符串来筛选NPC。\n"
          + "输入 i:[数字] 按NPC的确切ID来筛选。\n"
          + "输入 c:[字符串] 按NPC外观详情里的指定配色来筛选。";

    protected override (LowerString, long, int) FilterChange(string input)
        => input.Length switch
        {
            0 => (LowerString.Empty, 0, -1),
            > 1 when input[1] == ':' =>
                input[0] switch
                {
                    'i' or 'I' => input.Length == 2     ? (LowerString.Empty, 0, -1) :
                        long.TryParse(input.AsSpan(2), out var r) ? (LowerString.Empty, r, 1) : (LowerString.Empty, 0, -1),
                    'c' or 'C' => input.Length == 2 ? (LowerString.Empty, 0, -1) : (new LowerString(input[2..]), 0, 2),
                    _          => (new LowerString(input), 0, 0),
                },
            _ => (new LowerString(input), 0, 0),
        };

    public override bool ApplyFilter(in NpcData value)
        => FilterMode switch
        {
            -1 => false,
            0  => Filter.IsContained(value.Name),
            1  => value.Id.Id == NumericalFilter,
            2  => Filter.IsContained(GetColor(value)),
            _  => false, // Should never happen
        };

    private string GetColor(in NpcData value)
    {
        var color = _favorites.GetColor(value);
        return color.Length == 0 ? DesignColors.AutomaticName : color;
    }
}
