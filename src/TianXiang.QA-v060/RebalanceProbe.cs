using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using TianXiang;
using Sky=TianXiang.Sky;
namespace TianXiangQA;
public static partial class Probe
{
    static void ExportBaselines(CombatState s,Player p)
    {
        var names=new[]{"StrikeIronclad","DefendIronclad","PommelStrike","ShrugItOff","Backflip","Acrobatics","BattleTrance","Coolheaded","BallLightning","Footwork","Inflame","Skim","Offering","Prepared","DaggerSpray","SweepingBeam","Dash","LegSweep","Glacier","Hologram","AllForOne","FlameBarrier","Predator","Uppercut","Sunder"};
        var rows=new List<object>();
        foreach(var name in names)
        {
            var t=typeof(CardModel).Assembly.GetType("MegaCrit.Sts2.Core.Models.Cards."+name)!;
            var c=s.CreateCard(ModelDb.GetById<CardModel>(ModelDb.GetId(t)),p);
            foreach(bool up in new[]{false,true})
            {
                if(up){c.UpgradeInternal();c.FinalizeUpgradeInternal();}
                rows.Add(new {id=name,title=c.Title,upgraded=up,cost=c.EnergyCost.GetWithModifiers(CostModifiers.All),rarity=c.Rarity.ToString(),type=c.Type.ToString(),vars=c.DynamicVars.ToDictionary(x=>x.Key,x=>x.Value.BaseValue),keywords=c.Keywords.Select(k=>k.ToString()).ToArray()});
                Check(c.EnergyCost.GetWithModifiers(CostModifiers.All)>=0,"vanilla runtime baseline "+name+(up?"+":""));
            }
        }
        File.WriteAllText(Output+"vanilla-runtime-baselines.json",System.Text.Json.JsonSerializer.Serialize(rows,new System.Text.Json.JsonSerializerOptions{WriteIndented=true,Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping}));
    }
    static async Task RebalanceNumbers(CombatState s,Player p)
    {
        var enemy=s.Enemies.First();var sky=Sky.For(p);
        using var selector=CardSelectCmd.UseSelector(new Selector());
        foreach(bool up in new[]{false,true})
        {
            foreach(var id in new[]{"Sage006","Sage029","Sage047","Sage051","Sage060","Sage062","Sage079","Sage082"})
            {
                await Reset(s);var card=(WeatherCard)Create(s,p,id);if(up){card.UpgradeInternal();card.FinalizeUpgradeInternal();}
                var hp=enemy.CurrentHp;await Play(card,enemy);
                var block=card.Spec.Ops.FirstOrDefault(o=>o.Kind=="block");var damage=card.Spec.Ops.FirstOrDefault(o=>o.Kind=="damage");
                if(block!=null)Check(p.Creature.Block==(up?block.Upgrade:block.Base),"exact standalone block "+id+up);
                if(damage!=null)Check(hp-enemy.CurrentHp==(up?damage.Upgrade:damage.Base),"exact standalone damage "+id+up);
            }
            foreach(bool cold in new[]{false,true})
            {
                await Reset(s);sky.Slot(enemy).Temperature=cold?-1:0;var hp=enemy.CurrentHp;var c=Create(s,p,"Sage026");if(up){c.UpgradeInternal();c.FinalizeUpgradeInternal();}await Play(c,enemy);
                Check(hp-enemy.CurrentHp==(up?16:12)+(cold?(up?4:3):0),"cold conditional multihit "+up+cold);
            }
            foreach(bool warm in new[]{false,true})
            {
                await Reset(s);sky.Slot(enemy).Temperature=warm?1:0;
                for(int i=0;i<5;i++)await CardPileCmd.Add(Create(s,p,"Sage001"),PileType.Draw);
                var c=Create(s,p,"Sage027");if(up){c.UpgradeInternal();c.FinalizeUpgradeInternal();}await Play(c,enemy);
                Check(PileType.Hand.GetPile(p).Cards.Count==(warm?1:0),"warm conditional draw "+up+warm);
            }
            await Reset(s);
            foreach(var old in PileType.Discard.GetPile(p).Cards.ToArray())await CardPileCmd.Add(old,PileType.Exhaust);
            var a=Create(s,p,"Sage009");var b=Create(s,p,"Sage010");var other=Create(s,p,"Sage002");
            foreach(var c in new[]{a,b,other})await CardPileCmd.Add(c,PileType.Discard);
            var retrieve=Create(s,p,"Sage086");if(up){retrieve.UpgradeInternal();retrieve.FinalizeUpgradeInternal();}await Play(retrieve,enemy);
            Check(new[]{a,b}.Count(c=>c.Pile?.Type==PileType.Hand)==(up?2:1),"temperature retrieval count "+up);
            Check(other.Pile?.Type==PileType.Discard,"temperature retrieval excludes material-only card "+up);
            Check(retrieve.Pile?.Type==PileType.Exhaust,"temperature retrieval exhausts "+up);
            await Reset(s);await Play(Create(s,p,"Sage078"),enemy);
            await sky.Observe(Ctx,enemy,"尘",-1,null);Check(p.Creature.Block==8,"hybrid guard first observation 3+5");
            await sky.Observe(Ctx,enemy,"尘",-1,null);Check(p.Creature.Block==11,"hybrid guard subsequent observation only 3");
            // A replay card must not be a dead draw when no observation happened yet.
            foreach(var id in new[]{"Sage048","Sage065","Sage071","Sage085","Sage088"})
            {
                await Reset(s);for(int i=0;i<5;i++)await CardPileCmd.Add(Create(s,p,"Sage001"),PileType.Draw);
                var hp=enemy.CurrentHp;await Play(Create(s,p,id),enemy);
                Check(p.Creature.Block>0||enemy.CurrentHp<hp||PileType.Hand.GetPile(p).Cards.Count>0,"replay fallback without history "+id);
            }
        }
    }
}
