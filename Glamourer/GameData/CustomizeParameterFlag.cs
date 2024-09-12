namespace Glamourer.GameData;

[Flags]
public enum CustomizeParameterFlag : ushort
{
    SkinDiffuse           = 0x0001,
    MuscleTone            = 0x0002,
    SkinSpecular          = 0x0004,
    LipDiffuse            = 0x0008,
    HairDiffuse           = 0x0010,
    HairSpecular          = 0x0020,
    HairHighlight         = 0x0040,
    LeftEye               = 0x0080,
    RightEye              = 0x0100,
    FeatureColor          = 0x0200,
    FacePaintUvMultiplier = 0x0400,
    FacePaintUvOffset     = 0x0800,
    DecalColor            = 0x1000,
    LeftLimbalIntensity   = 0x2000,
    RightLimbalIntensity  = 0x4000,
}

public static class CustomizeParameterExtensions
{
    // Speculars are not available anymore.
    public const CustomizeParameterFlag All = (CustomizeParameterFlag)0x7FDB;

    public const CustomizeParameterFlag RgbTriples = All
      & ~(RgbaQuadruples | Percentages | Values);

    public const CustomizeParameterFlag RgbaQuadruples = CustomizeParameterFlag.DecalColor | CustomizeParameterFlag.LipDiffuse;

    public const CustomizeParameterFlag Percentages = CustomizeParameterFlag.MuscleTone
      | CustomizeParameterFlag.LeftLimbalIntensity
      | CustomizeParameterFlag.RightLimbalIntensity;

    public const CustomizeParameterFlag Values = CustomizeParameterFlag.FacePaintUvOffset | CustomizeParameterFlag.FacePaintUvMultiplier;

    public static readonly IReadOnlyList<CustomizeParameterFlag> AllFlags        = [.. Enum.GetValues<CustomizeParameterFlag>().Where(f => All.HasFlag(f))];
    public static readonly IReadOnlyList<CustomizeParameterFlag> RgbaFlags       = AllFlags.Where(f => RgbaQuadruples.HasFlag(f)).ToArray();
    public static readonly IReadOnlyList<CustomizeParameterFlag> RgbFlags        = AllFlags.Where(f => RgbTriples.HasFlag(f)).ToArray();
    public static readonly IReadOnlyList<CustomizeParameterFlag> PercentageFlags = AllFlags.Where(f => Percentages.HasFlag(f)).ToArray();
    public static readonly IReadOnlyList<CustomizeParameterFlag> ValueFlags      = AllFlags.Where(f => Values.HasFlag(f)).ToArray();

    public static int Count(this CustomizeParameterFlag flag)
        => RgbaQuadruples.HasFlag(flag) ? 4 : RgbTriples.HasFlag(flag) ? 3 : 1;

    public static IEnumerable<CustomizeParameterFlag> Iterate(this CustomizeParameterFlag flags)
        => AllFlags.Where(f => flags.HasFlag(f));

    public static int ToInternalIndex(this CustomizeParameterFlag flag)
        => BitOperations.TrailingZeroCount((uint)flag);

    public static string ToName(this CustomizeParameterFlag flag)
        => flag switch
        {
            CustomizeParameterFlag.SkinDiffuse           => "皮肤颜色",
            CustomizeParameterFlag.MuscleTone            => "肌肉强度",
            CustomizeParameterFlag.SkinSpecular          => "皮肤光泽",
            CustomizeParameterFlag.LipDiffuse            => "嘴唇颜色",
            CustomizeParameterFlag.HairDiffuse           => "头发颜色",
            CustomizeParameterFlag.HairSpecular          => "头发光泽",
            CustomizeParameterFlag.HairHighlight         => "头发挑染",
            CustomizeParameterFlag.LeftEye               => "左眼瞳色",
            CustomizeParameterFlag.RightEye              => "右眼瞳色",
            CustomizeParameterFlag.FeatureColor          => "纹身颜色",
            CustomizeParameterFlag.FacePaintUvMultiplier => "面妆倍增器",
            CustomizeParameterFlag.FacePaintUvOffset     => "面妆偏移",
            CustomizeParameterFlag.DecalColor            => "面妆颜色",
            CustomizeParameterFlag.LeftLimbalIntensity   => "左瞳轮廓强度",
            CustomizeParameterFlag.RightLimbalIntensity  => "右瞳轮廓强度",
            _                                            => string.Empty,
        };
}
