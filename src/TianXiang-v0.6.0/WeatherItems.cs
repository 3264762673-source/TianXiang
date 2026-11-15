using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Scaffolding.Content;

namespace TianXiang;

public sealed class WeatherPotionPool : TypeListPotionPoolModel
{
    public override string EnergyColorName => "defect";
}

public abstract class SkyPotion : ModPotionTemplate
{
    protected abstract string IconName { get; }
    public override string CustomImagePath => $"res://TianXiang/items/{IconName}.svg";
    public override string CustomOutlinePath => CustomImagePath;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.AnyEnemy;
}

public sealed class RainVial : SkyPotion
{
    protected override string IconName => "rain";
    public override PotionRarity Rarity => PotionRarity.Common;
    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        AssertValidForTargetedPotion(target);
        await CreatureCmd.GainBlock(Owner.Creature, 6, ValueProp.Unpowered, null);
        await Sky.For(Owner).SetMaterial(ctx, target!, "水", null);
    }
}

public sealed class DustVial : SkyPotion
{
    protected override string IconName => "dust";
    public override PotionRarity Rarity => PotionRarity.Uncommon;
    protected override async Task OnUse(PlayerChoiceContext ctx, Creature? target)
    {
        AssertValidForTargetedPotion(target);
        await Sky.For(Owner).Observe(ctx, target!, "尘", 1, null);
    }
}

public abstract class SkyRelic : ModRelicTemplate
{
    protected abstract string IconName { get; }
    public override string CustomIconPath => $"res://TianXiang/items/{IconName}.svg";
    public override string CustomBigIconPath => CustomIconPath;
    public override string CustomIconOutlinePath => CustomIconPath;
}
public sealed class JadeDewCup : SkyRelic
{
    protected override string IconName => "cup";
    public override RelicRarity Rarity => RelicRarity.Common;
}
public sealed class SunNeedle : SkyRelic
{
    protected override string IconName => "needle";
    public override RelicRarity Rarity => RelicRarity.Uncommon;
}
public sealed class ThunderBell : SkyRelic
{
    protected override string IconName => "bell";
    public override RelicRarity Rarity => RelicRarity.Rare;
}
