using System.Text.Json;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.HoverTips;
namespace TianXiang;

public record Op(string Kind,int Base,int Upgrade,string Arg);
public record CardSpec(string Id,string Name,int Cost,int UpgradeCost,string Type,string Rarity,string Material,Op[] Ops,bool Exhaust);
public static class Catalogue
{
    public static readonly Dictionary<string,CardSpec> All=Load();
    static Dictionary<string,CardSpec> Load()
    {
        using var stream=typeof(Catalogue).Assembly.GetManifestResourceStream("TianXiang.cards.json")!;
        return JsonSerializer.Deserialize<CardSpec[]>(stream,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!.ToDictionary(x=>x.Id);
    }
}
public abstract class WeatherCard(string key):ModCardTemplate(Catalogue.All[key].Cost,Enum.Parse<CardType>(Catalogue.All[key].Type),Enum.Parse<CardRarity>(Catalogue.All[key].Rarity),TargetFor(Catalogue.All[key]))
{
    public CardSpec Spec=>Catalogue.All[key];
    public bool IsMaterial=>Spec.Material is "水" or "尘" or "风";
    public override string CustomPortraitPath=>$"res://TianXiang/images/{key}.png";
    public override bool GainsBlock=>Spec.Ops.Any(o=>o.Kind=="block");
    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
            if(Spec.Material.Length>0||Spec.Ops.Any(o=>o.Kind is "temp" or "tempAll" or "repeat" or "repeatAll" or "flip"))
            {
                var description="材料与阴阳齐备时触发观天，清空两槽。同类覆盖。先后顺序不限。\n"+Sky.TableText;
                if(Spec.Material.Length>0)description+="\n阴："+Sky.ReactionText(Spec.Material,-1)+"\n阳："+Sky.ReactionText(Spec.Material,1);
                yield return new HoverTip(new LocString("cards","TIAN_XIANG_OBSERVE.title"),description);
            }
            if(Spec.Ops.Any(o=>o.Kind=="weak"))yield return HoverTipFactory.FromPower<WeakPower>();
            if(Spec.Ops.Any(o=>o.Kind=="vulnerable"))yield return HoverTipFactory.FromPower<VulnerablePower>();
            if(IsMaterial||Spec.Ops.Any(o=>o.Kind=="alter"))yield return new HoverTip(new LocString("cards","TIAN_XIANG_MATERIAL.title"),"带有水、尘或风材料的牌。阴阳牌不属于天气牌。");
            if(Spec.Ops.Any(o=>o.Kind=="alter"))yield return new HoverTip(new LocString("cards","TIAN_XIANG_ALTER.title"),"从手牌消耗1张指定类别的牌，触发后续效果。没有符合条件的牌时，仍执行其余效果。非天气牌包括状态和诅咒。");
            if(Spec.Ops.Any(o=>o.Kind is "repeat" or "repeatAll"))yield return new HoverTip(new LocString("cards","TIAN_XIANG_OBSERVE.title"),"重复上次观天的材料与阴阳组合，不改变槽位。本场未观天时无效。");
        }
    }
    public override IEnumerable<CardKeyword> CanonicalKeywords=>Spec.Exhaust?[CardKeyword.Exhaust]:[];
    protected override HashSet<CardTag> CanonicalTags=>key is "Sage001" or "Sage002"?[CardTag.Strike]:key=="Sage004"?[CardTag.Defend]:[];
    protected override IEnumerable<DynamicVar> CanonicalVars=>Catalogue.All[key].Ops.Select((o,i)=>(DynamicVar)(o.Kind=="damage"?new DamageVar(o.Base,ValueProp.Move):o.Kind=="block"?new BlockVar(o.Base,ValueProp.Move):new DynamicVar("N"+i,o.Base)));
    static TargetType TargetFor(CardSpec c)
    {
        if(c.Ops.Any(o=>o.Kind is "all" or "repeatAll"))return TargetType.AllEnemies;
        return c.Material.Length>0||c.Ops.Any(o=>o.Kind is "damage" or "temp" or "weak" or "vulnerable" or "flip" or "repeat" or "conditionalBlock" or "alterObserve" or "alterAttack")?TargetType.AnyEnemy:TargetType.Self;
    }
    int N(Op op)=>IsUpgraded?op.Upgrade:op.Base;
    protected override void OnUpgrade()
    {
        for(int i=0;i<Spec.Ops.Length;i++){var op=Spec.Ops[i];DynamicVars[op.Kind=="damage"?"Damage":op.Kind=="block"?"Block":"N"+i].UpgradeValueBy(op.Upgrade-op.Base);}
        if(Spec.UpgradeCost!=Spec.Cost)EnergyCost.UpgradeBy(Spec.UpgradeCost-Spec.Cost);
    }
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play)
    {
        if(Type!=CardType.Attack)await CreatureCmd.TriggerAnim(Owner.Creature,"Cast",0f);
        if(Owner.Character is not WeatherCharacter && Owner.Creature.GetPower<ObservationPower>()==null)
            await PowerCmd.Apply<ObservationPower>(ctx,Owner.Creature,1,Owner.Creature,this);
        var sky=Sky.For(Owner);var target=play.Target;var all=Spec.Ops.Any(o=>o.Kind=="all");
        var targets=all?sky.Enemies.ToList():target!=null&&target!=Owner.Creature?[target]:new List<Creature>();
        int hits=Spec.Ops.Where(o=>o.Kind=="hits").Select(N).DefaultIfEmpty(1).First();
        hits+=Spec.Ops.Where(o=>o.Kind=="observeHits").Select(o=>Math.Min(sky.Observations,N(o))).Sum();
        hits+=Spec.Ops.Where(o=>o.Kind=="diversityHits").Select(o=>Math.Min(sky.ObservedRecipes.Count,N(o))).Sum();
        bool altered=false;string usedMaterial="";
        foreach(var op in Spec.Ops)
        {
            if(op.Kind.StartsWith("alter")&&op.Kind!="alter"&&!altered)continue;
            int n=N(op);switch(op.Kind)
            {
                case "alter":
                    var candidates=PileType.Hand.GetPile(Owner).Cards.Where(c=>c!=this&&((c is WeatherCard w&&w.IsMaterial)==(op.Arg=="天气"))).ToList();
                    if(candidates.Count>0){var selected=(await CardSelectCmd.FromHand(ctx,Owner,new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt,1),c=>candidates.Contains(c),this)).FirstOrDefault();if(selected!=null){usedMaterial=selected is WeatherCard wc?wc.Spec.Material:"";await CardCmd.Exhaust(ctx,selected);altered=true;}}
                    break;
                case "alterEnergy":await PlayerCmd.GainEnergy(n,Owner);break;
                case "alterDraw":await CardPileCmd.Draw(ctx,n,Owner);break;
                case "alterDex":await PowerCmd.Apply<DexterityPower>(ctx,Owner.Creature,n,Owner.Creature,this);break;
                case "alterAttack":if(target is {IsAlive:true})await DamageCmd.Attack(n).FromCard(this,play).WithHitCount(int.Parse(op.Arg)).Targeting(target).WithNoAttackerAnim().Execute(ctx);break;
                case "alterTempAll":
                    int alteredTemp=await ChooseTemperature(ctx);foreach(var e in sky.Enemies.ToList())await sky.SetTemperature(ctx,e,alteredTemp,this);break;
                case "alterObserve":
                    if(usedMaterial.Length>0){int chosen=await ChooseTemperature(ctx);foreach(var e in targets.Where(e=>e.IsAlive))await sky.Observe(ctx,e,usedMaterial,chosen,this);}break;
                case "alterRetrieve":
                    var materialCards=PileType.Discard.GetPile(Owner).Cards.Where(c=>c is WeatherCard w&&w.IsMaterial).ToList();
                    if(materialCards.Count>0){var selected=await CardSelectCmd.FromSimpleGrid(ctx,materialCards,Owner,new CardSelectorPrefs(new LocString("cards",Id.Entry+".selectionScreenPrompt"),0,Math.Min(n,materialCards.Count)));foreach(var c in selected){await CardPileCmd.Add(c,PileType.Hand);c.EnergyCost.SetThisTurn(0);}}break;
                case "damage":
                    var attack=DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this,play).WithHitCount(hits).WithHitFx("vfx/vfx_attack_blunt");
                    if(all)attack.TargetingAllOpponents(CombatState!);else if(target is {IsAlive:true})attack.Targeting(target);else break;
                    await attack.Execute(ctx);break;
                case "block":await CreatureCmd.GainBlock(Owner.Creature,DynamicVars.Block,play);break;
                case "draw":if(n>0)await CardPileCmd.Draw(ctx,n,Owner);break;
                case "weak":foreach(var t in targets.Where(t=>t.IsAlive))await PowerCmd.Apply<WeakPower>(ctx,t,n,Owner.Creature,this);break;
                case "vulnerable":foreach(var t in targets.Where(t=>t.IsAlive))await PowerCmd.Apply<VulnerablePower>(ctx,t,n,Owner.Creature,this);break;
                case "temp":case "tempAll":
                    int temp=op.Arg=="选择"?await ChooseTemperature(ctx):op.Arg=="阴"?-1:1;
                    foreach(var t in (op.Kind=="tempAll"?sky.Enemies.ToList():targets).Where(t=>t.IsAlive))await sky.SetTemperature(ctx,t,temp,this);break;
                case "flip":foreach(var t in targets.Where(t=>t.IsAlive))await sky.SetTemperature(ctx,t,sky.Slot(t).Temperature==0?-1:-sky.Slot(t).Temperature,this);break;
                
                
                
                case "conditionalDamage":if(target is {IsAlive:true}&&sky.Matches(target,op.Arg))await DamageCmd.Attack(n).FromCard(this,play).Targeting(target).WithNoAttackerAnim().Execute(ctx);break;
                case "conditionalDraw":if(target!=null&&sky.Matches(target,op.Arg))await CardPileCmd.Draw(ctx,n,Owner);break;
                case "conditionalBlock":if(target!=null&&sky.Matches(target,op.Arg))await CreatureCmd.GainBlock(Owner.Creature,n,ValueProp.Move,play);break;
                case "nextObserveBlock":sky.NextObserveBlock+=n;break;
                case "nextObserveDamage":sky.NextObserveDamage+=n;break;
                case "repeat":case "repeatAll":
                    if(sky.Last is {} last)for(int j=0;j<n&&Owner.Creature.IsAlive;j++)
                        foreach(var t in (op.Kind=="repeatAll"?sky.Enemies.ToList():targets).Where(t=>t.IsAlive))await sky.Observe(ctx,t,last.Material,last.Temperature,this);
                    break;
                case "buff":await PowerCmd.Apply(ctx,ModelDb.GetById<PowerModel>(ModelDb.GetId(typeof(WeatherCard).Assembly.GetType("TianXiang."+op.Arg+"Power")!)).ToMutable(),Owner.Creature,n,Owner.Creature,this);break;
                case "discard":await CardCmd.Discard(ctx,await CardSelectCmd.FromHandForDiscard(ctx,Owner,new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt,n),null,this));break;
                case "retrieve":case "retrieveFree":case "retrieveTemp":
                    var list=PileType.Discard.GetPile(Owner).Cards.Where(c=>c is WeatherCard w&&(op.Kind=="retrieveTemp"?w.Spec.Ops.Any(x=>x.Kind is "temp" or "tempAll" or "flip"):w.IsMaterial)).ToList();
                    if(list.Count>0){var selected=await CardSelectCmd.FromSimpleGrid(ctx,list,Owner,new CardSelectorPrefs(new LocString("cards",Id.Entry+".selectionScreenPrompt"),0,Math.Min(n,list.Count)));foreach(var c in selected){await CardPileCmd.Add(c,PileType.Hand);if(op.Kind=="retrieveFree")c.EnergyCost.SetThisTurn(0);}}break;
            }
            if(!Owner.Creature.IsAlive)break;
        }
        if(Spec.Material.Length>0&&Owner.Creature.IsAlive)foreach(var t in targets.Where(t=>t.IsAlive))await sky.SetMaterial(ctx,t,Spec.Material,this);
        BattleUi.Refresh(sky);
    }
    async Task<int> ChooseTemperature(PlayerChoiceContext ctx)
    {
        var choices=new[]{CombatState!.CreateCard(ModelDb.Card<ChooseYin>(),Owner),CombatState.CreateCard(ModelDb.Card<ChooseYang>(),Owner)};
        var selected=await CardSelectCmd.FromChooseACardScreen(ctx,choices,Owner,false);
        return selected is ChooseYang?1:-1;
    }

}
public abstract class ChoiceCard():ModCardTemplate(0,CardType.Skill,CardRarity.Token,TargetType.Self,false)
{
    protected override IEnumerable<IHoverTip> AdditionalHoverTips=>[new HoverTip(new LocString("cards","TIAN_XIANG_OBSERVE.title"),Sky.TableText)];
    // This auxiliary pool is not a character reward pool, so vanilla AllCardPools cannot discover it.
    public override CardPoolModel Pool=>ModelDb.CardPool<ChoiceCardPool>();
    public override string CustomPortraitPath=>$"res://TianXiang/images/{(this is ChooseYin?"Sage017":"Sage018")}.png";
    public override bool CanBeGeneratedInCombat=>false;
    public override bool CanBeGeneratedByModifiers=>false;
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)=>Task.CompletedTask;
}
public sealed class ChooseYin:ChoiceCard;
public sealed class ChooseYang:ChoiceCard;

