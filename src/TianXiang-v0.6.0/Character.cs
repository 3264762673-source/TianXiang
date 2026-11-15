using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.PotionPools;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Scaffolding.Characters;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;
using STS2RitsuLib.Scaffolding.Visuals.Definition;
using STS2RitsuLib.Scaffolding.Characters.Visuals.Definition;

namespace TianXiang;
public sealed class WeatherCardPool:TypeListCardPoolModel, IModColorfulPhilosophersCardPool
{
    public override string Title=>"天相者";
    public override string EnergyColorName=>"tianxiang";
    public override string BigEnergyIconPath=>"res://TianXiang/items/energy.svg";
    public override string TextEnergyIconPath=>BigEnergyIconPath;
    public override string CardFrameMaterialPath=>"card_frame_blue";
    public override Color DeckEntryCardColor=>new("69c8ce");
    public override bool IsColorless=>false;
}
public sealed class ChoiceCardPool:TypeListCardPoolModel
{
    public override string Title=>"阴阳选择";
    public override string EnergyColorName=>"defect";
    public override Color DeckEntryCardColor=>new("69c8ce");
    public override bool IsColorless=>false;
}
public sealed class WeatherRelicPool:TypeListRelicPoolModel{public override string EnergyColorName=>"defect";}
public sealed class WeatherCompass:ModRelicTemplate
{
    public override RelicRarity Rarity=>RelicRarity.Starter;
    public override string CustomIconPath=>"res://TianXiang/images/compass.png";
    public override string CustomBigIconPath=>CustomIconPath;
    public override string CustomIconOutlinePath=>CustomIconPath;
}
public sealed class CelestialCompass:ModRelicTemplate
{
    public override RelicRarity Rarity=>RelicRarity.Ancient;
    public override string CustomIconPath=>"res://TianXiang/images/compass-upgraded.png";
    public override string CustomBigIconPath=>CustomIconPath;
    public override string CustomIconOutlinePath=>CustomIconPath;
}
public sealed class WeatherCharacter:ModCharacterTemplate<WeatherCardPool,WeatherRelicPool,WeatherPotionPool>
{
    public override RelicAssetProfile? TryGetVanillaRelicVisualOverrideForOwnedRelic(RelicModel relic)
    {
        const string cookie = "res://TianXiang/items/cookie.svg";
        return relic is MegaCrit.Sts2.Core.Models.Relics.YummyCookie
            ? new RelicAssetProfile(cookie,cookie,cookie)
            : base.TryGetVanillaRelicVisualOverrideForOwnedRelic(relic);
    }
    public override CharacterGender Gender=>CharacterGender.Feminine;
    public override Color NameColor=>new("74dfdb");
    public override int StartingHp=>72;
    public override int StartingGold=>99;
    public override float AttackAnimDelay=>.15f;
    public override float CastAnimDelay=>.2f;
    public override bool RequiresEpochAndTimeline=>false;
    public override string PlaceholderCharacterId=>"defect";
    public override string CustomIconTexturePath=>"res://TianXiang/images/portrait.png";
    public override string CustomIconPath=>"res://TianXiang/top-panel-icon.tscn";
    public override string CustomIconOutlineTexturePath=>CustomIconTexturePath;
    public override string CustomCharacterSelectIconPath=>CustomIconTexturePath;
    public override string CustomCharacterSelectLockedIconPath=>CustomIconTexturePath;
    public override string CustomMapMarkerPath=>CustomIconTexturePath;
    public override string CustomCharacterSelectBgPath=>"res://TianXiang/select-background.tscn";
    protected override IEnumerable<CardModel> LocalStartingDeck=>[
        ModelDb.Card<Sage001>(),ModelDb.Card<Sage001>(),ModelDb.Card<Sage001>(),ModelDb.Card<Sage001>(),
        ModelDb.Card<Sage004>(),ModelDb.Card<Sage004>(),ModelDb.Card<Sage004>(),ModelDb.Card<Sage004>(),
        ModelDb.Card<Sage002>(),ModelDb.Card<Sage005>()];
    protected override IEnumerable<RelicModel> LocalStartingRelics=>[ModelDb.Relic<WeatherCompass>()];
    public override List<string> GetArchitectAttackVfx()=>["vfx/vfx_attack_blunt"];
    static VisualNodeStyle Pose(float x=0,float y=0,float stretch=1)=>new(){Position=new(x,-190+y),Scale=new(.245f,.245f*stretch)};
    static VisualCueSet WorldCues=>new(TexturePathByCue:new Dictionary<string,string>{
        ["idle"]="res://TianXiang/images/character-idle.png",
        ["overgrowth_loop"]="res://TianXiang/images/character-idle.png",
        ["hive_loop"]="res://TianXiang/images/character-idle.png",
        ["glory_loop"]="res://TianXiang/images/character-idle.png"})
        {TextureStyleByCue=new Dictionary<string,VisualNodeStyle>{["idle"]=Pose(),["overgrowth_loop"]=Pose(),["hive_loop"]=Pose(),["glory_loop"]=Pose()}};
    public override CharacterWorldProceduralVisualSet? WorldProceduralVisuals=>new(new(WorldCues),new(WorldCues));
    protected override NCreatureVisuals? TryCreateCreatureVisuals()
    {
        var node=RitsuGodotNodeFactories.CreateFromResource<NCreatureVisuals>(ModEntry.Texture("character-idle"),new VisualNodeStyle());
        var body=node.GetNode<Sprite2D>("Visuals");body.Texture=null;body.Position=Vector2.Zero;body.Scale=Vector2.One;body.AddChild(CharacterRig.Build());
        var bounds=node.GetNode<Control>("Bounds");bounds.Position=new(-130,-365);bounds.Size=new(260,365);
        node.GetNode<Marker2D>("IntentPos").Position=new(0,-425);
        node.GetNode<Marker2D>("CenterPos").Position=new(0,-175);
        return node;
    }
    public override VisualCueSet? VisualCues=>new();
}
