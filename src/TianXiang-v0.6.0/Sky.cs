using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
namespace TianXiang;
public sealed class SkySlot{public string Material="";public int Temperature;}
public record Reaction(string Material,int Temperature,int Damage=0,int Block=0,int Weak=0,int Vulnerable=0,int Energy=0,int Draw=0,int Strength=0,int Dexterity=0);
public sealed class Sky(Player player)
{
    public const string TableText="水阴：格挡6，敌人虚弱1。\n水阳：格挡5，力量1。\n尘阴：能量2。\n尘阳：伤害8，敌人易伤1。\n风阴：格挡6，敏捷1。\n风阳：抽3张牌。\n候风仪：每次观天，抽1回1。";
    static readonly ConditionalWeakTable<ICombatState,Sky> States=new();
    public readonly Player Player=player;
    public readonly Dictionary<Creature,SkySlot> Slots=[];
    
    public int Turns,NextObserveBlock,NextObserveDamage;
    public int Observations;
    public int RelicTriggers;
    public readonly HashSet<string> ObservedRecipes=[];
    private bool dewUsed, needleUsed;
    public bool ObserveDrawUsed,ObserveEnergyUsed,ObserveGuardUsed;
    public Reaction? Last;
    public IEnumerable<Creature> Enemies=>Player.Creature.CombatState!.Enemies.Where(e=>e.IsAlive);
    public static Sky For(Player p)=>States.GetValue(p.Creature.CombatState!,_=>new Sky(p));
    public SkySlot Slot(Creature c){if(!Slots.TryGetValue(c,out var s))Slots[c]=s=new();return s;}
    public bool Matches(Creature c,string condition)=>condition=="材料"?Slot(c).Material!="":Slot(c).Temperature==(condition=="阴"?-1:1);
    public static Reaction Recipe(string material,int t)=>(material,t<0) switch
    {
        ("水",true)=>new(material,t,Block:6,Weak:1),
        ("水",false)=>new(material,t,Block:5,Strength:1),
        ("尘",true)=>new(material,t,Energy:2),
        ("尘",false)=>new(material,t,Damage:8,Vulnerable:1),
        ("风",true)=>new(material,t,Block:6,Dexterity:1),
        ("风",false)=>new(material,t,Draw:3),
        _=>throw new ArgumentException("Unknown observation material")
    };
    public static string ReactionText(string material,int temp)
    {
        var r=Recipe(material,temp);var text=new List<string>();
        if(r.Damage>0)text.Add($"对该敌人造成{r.Damage}伤害");
        if(r.Block>0)text.Add($"你获得{r.Block}格挡");
        if(r.Weak>0)text.Add($"对该敌人施加{r.Weak}虚弱");
        if(r.Vulnerable>0)text.Add($"对该敌人施加{r.Vulnerable}易伤");
        if(r.Energy>0)text.Add($"你获得{r.Energy}能量");
        if(r.Draw>0)text.Add($"抽{r.Draw}张牌");
        if(r.Strength>0)text.Add($"你获得{r.Strength}力量（本场）");
        if(r.Dexterity>0)text.Add($"你获得{r.Dexterity}敏捷（本场）");
        return string.Join("，",text);
    }
    public async Task SetTemperature(PlayerChoiceContext ctx,Creature c,int t,CardModel card){if(!c.IsAlive)return;Slot(c).Temperature=t;await Check(ctx,c,card);}
    public async Task SetMaterial(PlayerChoiceContext ctx,Creature c,string m,CardModel? card){if(!c.IsAlive)return;Slot(c).Material=m;await Check(ctx,c,card);}
    async Task Check(PlayerChoiceContext ctx,Creature c,CardModel? card)
    {
        var s=Slot(c);
        if(s.Temperature!=0&&s.Material!=""){var m=s.Material;var t=s.Temperature;s.Material="";s.Temperature=0;await Observe(ctx,c,m,t,card);}
        BattleUi.Refresh(this);
    }
    int Buff<T>()where T:PowerModel=>Player.Creature.GetPower<T>()?.Amount??0;
    public async Task Observe(PlayerChoiceContext ctx,Creature c,string material,int temp,CardModel? card)
    {
        if(!c.IsAlive)return;var r=Recipe(material,temp);Last=r;Observations++;ObservedRecipes.Add(material+temp);
        BattleUi.Pulse(c,temp<0?new Color("86d4ff"):new Color("ffcd75"));
        if(r.Damage>0)await CreatureCmd.Damage(ctx,c,r.Damage,ValueProp.Unpowered,Player.Creature,card,null);
        if(r.Block>0)await CreatureCmd.GainBlock(Player.Creature,r.Block,ValueProp.Unpowered,null);
        if(c.IsAlive&&r.Weak>0)await PowerCmd.Apply<WeakPower>(ctx,c,r.Weak,Player.Creature,card);
        if(c.IsAlive&&r.Vulnerable>0)await PowerCmd.Apply<VulnerablePower>(ctx,c,r.Vulnerable,Player.Creature,card);
        if(r.Energy>0)await PlayerCmd.GainEnergy(r.Energy,Player);
        if(r.Draw>0)await CardPileCmd.Draw(ctx,r.Draw,Player);
        if(r.Strength>0)await PowerCmd.Apply<StrengthPower>(ctx,Player.Creature,r.Strength,Player.Creature,card);
        if(r.Dexterity>0)await PowerCmd.Apply<DexterityPower>(ctx,Player.Creature,r.Dexterity,Player.Creature,card);
        int guard=Buff<ObserveGuardPower>()+(temp<0?Buff<ColdGuardPower>():Buff<WarmGuardPower>())+NextObserveBlock;NextObserveBlock=0;
        if(!ObserveGuardUsed){ObserveGuardUsed=true;guard+=Buff<FirstObserveGuardPower>();}
        int extra=NextObserveDamage;NextObserveDamage=0;
        if(extra>0&&c.IsAlive)await CreatureCmd.Damage(ctx,c,extra,ValueProp.Unpowered,Player.Creature,card,null);
        if(guard>0)await CreatureCmd.GainBlock(Player.Creature,guard,ValueProp.Unpowered,null);
        if(temp>0&&c.IsAlive&&Buff<WarmBurstPower>()>0)await CreatureCmd.Damage(ctx,c,Buff<WarmBurstPower>(),ValueProp.Unpowered,Player.Creature,card,null);
        if(temp<0 && !dewUsed && Player.GetRelic<JadeDewCup>() is {} cup)
        {
            dewUsed=true;
            cup.Flash();
            await CreatureCmd.GainBlock(Player.Creature,3,ValueProp.Unpowered,null);
        }
        if(temp>0 && !needleUsed && Player.GetRelic<SunNeedle>() is {} needle)
        {
            needleUsed=true;
            needle.Flash();
            if(c.IsAlive) await CreatureCmd.Damage(ctx,c,4,ValueProp.Unpowered,Player.Creature,card,null);
        }
        if(Observations % 3 == 0 && Player.GetRelic<ThunderBell>() is {} bell)
        {
            bell.Flash();
            foreach(var enemy in Enemies.ToArray())
                if(enemy.IsAlive) await CreatureCmd.Damage(ctx,enemy,5,ValueProp.Unpowered,Player.Creature,card,null);
        }
        if(Player.GetRelic<CelestialCompass>()!=null)await CreatureCmd.GainBlock(Player.Creature,3,ValueProp.Unpowered,null);
        if(Player.GetRelic<WeatherCompass>()!=null||Player.GetRelic<CelestialCompass>()!=null){RelicTriggers++;await CardPileCmd.Draw(ctx,1,Player);await PlayerCmd.GainEnergy(1,Player);}
        if(!ObserveDrawUsed&&Buff<FirstObserveDrawPower>()>0){ObserveDrawUsed=true;await CardPileCmd.Draw(ctx,Buff<FirstObserveDrawPower>(),Player);}
        if(!ObserveEnergyUsed&&Buff<FirstObserveEnergyPower>()>0){ObserveEnergyUsed=true;await PlayerCmd.GainEnergy(Buff<FirstObserveEnergyPower>(),Player);}
        foreach(var dead in Slots.Keys.Where(x=>!x.IsAlive).ToList())Slots.Remove(dead);
        BattleUi.Refresh(this);
    }
    public Task StartTurn(PlayerChoiceContext ctx)
    {
        Turns=Player.PlayerCombatState!.TurnNumber;RelicTriggers=0;dewUsed=false;needleUsed=false;
        ObserveDrawUsed=false;ObserveEnergyUsed=false;ObserveGuardUsed=false;
        BattleUi.Refresh(this);return Task.CompletedTask;
    }

}
[HarmonyPatch(typeof(Hook),nameof(Hook.BeforeCombatStart))]
public static class BattleStartPatch
{
    static void Postfix(ICombatState? combatState,ref Task __result){var previous=__result;__result=Start(previous,combatState);}
    static async Task Start(Task previous,ICombatState? combat)
    {
        await previous;if(combat==null)return;
        foreach(var p in combat.Players.Where(p=>p.Character is WeatherCharacter))
        {
            var sky=Sky.For(p);
            await PowerCmd.Apply<ObservationPower>(new ThrowingPlayerChoiceContext(),p.Creature,1,p.Creature,null);
            GD.Print("[TianXiang] Observation combat started (no global weather).");
        }
    }
}
