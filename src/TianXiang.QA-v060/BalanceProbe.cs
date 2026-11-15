using System.Text.Json;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Helpers;
using TianXiang;
namespace TianXiangQA;
public static partial class Probe
{
    static async Task Paid(CardModel c,MegaCrit.Sts2.Core.Entities.Creatures.Creature enemy)
    {
        Check(c.Pile?.Type==PileType.Hand,"paid play from real hand");
        Check(c.Owner.PlayerCombatState!.Energy>=c.EnergyCost.GetAmountToSpend(),"paid play affordable");
        var (energy,stars)=await c.SpendResources();
        await c.OnPlayWrapper(Ctx,c.TargetType==TargetType.AnyEnemy?enemy:null,false,new ResourceInfo{EnergySpent=energy,EnergyValue=energy,StarsSpent=stars,StarValue=stars},true);
    }
    static async Task Balance(CombatState s,Player p)
    {
        var rows=new List<object>();var enemy=s.Enemies.First();
        using var selector=CardSelectCmd.UseSelector(new Selector());
        foreach(bool up in new[]{false,true})
        {
            await Reset(s);
            foreach(var pile in new[]{PileType.Hand,PileType.Draw,PileType.Discard})foreach(var c in pile.GetPile(p).Cards.ToArray())await CardPileCmd.Add(c,PileType.Exhaust);
            p.PlayerCombatState!.LoseEnergy(p.PlayerCombatState.Energy);await PlayerCmd.GainEnergy(3,p);
            string[] ids=["Sage042","Sage009","Sage044","Sage046"];
            foreach(var id in ids){var c=Create(s,p,id);if(up){c.UpgradeInternal();c.FinalizeUpgradeInternal();}await CardPileCmd.Add(c,PileType.Hand);}
            int plays=0,hp=enemy.CurrentHp;var trace=new List<object>();string stopped="80-play test ceiling";
            for(int i=0;i<80;i++)
            {
                string id=ids[i%4];var c=PileType.Hand.GetPile(p).Cards.FirstOrDefault(c=>c is WeatherCard w&&w.Spec.Id==id);
                if(c==null){stopped="required card not in hand";break;}
                if(p.PlayerCombatState.Energy<c.EnergyCost.GetAmountToSpend()){stopped="insufficient energy";break;}
                await Paid(c,enemy);plays++;
                trace.Add(new{play=plays,id,energy=p.PlayerCombatState.Energy,hand=PileType.Hand.GetPile(p).Cards.Count,observations=TianXiang.Sky.For(p).Observations});
            }
            rows.Add(new{scenario="four-card dust-yin wind-yang paid loop",upgraded=up,plays,stopped,energy=p.PlayerCombatState.Energy,damage=hp-enemy.CurrentHp,trace});
        }
        if(!CommandLineHelper.HasArg("tx-before"))
        {
            await Reset(s);var sky=TianXiang.Sky.For(p);
            foreach(var pile in new[]{PileType.Hand,PileType.Draw,PileType.Discard})foreach(var old in pile.GetPile(p).Cards.ToArray())await CardPileCmd.Add(old,PileType.Exhaust);
            for(int i=0;i<10;i++)await CardPileCmd.Add(Create(s,p,"Sage001"),PileType.Draw);
            int energy=p.PlayerCombatState!.Energy;
            for(int i=0;i<5;i++)await sky.Observe(Ctx,enemy,"水",1,null);
            Check(p.PlayerCombatState.Energy-energy==5,"relic refunds all five observations");
            Check(PileType.Hand.GetPile(p).Cards.Count==5,"relic draws all five cards");
            Check(sky.Observations==5,"observations beyond relic limit still execute");
            Check(p.Creature.GetPower<MegaCrit.Sts2.Core.Models.Powers.StrengthPower>()!.Amount==5,"observation attribute gains uncapped");
            await EndTurn(s,p);Check(sky.RelicTriggers==0,"relic allowance resets on native next turn");
            energy=p.PlayerCombatState.Energy;await sky.Observe(Ctx,enemy,"水",1,null);
            Check(p.PlayerCombatState.Energy==energy+1,"next turn refund restored");
            foreach(var id in new[]{"Sage006","Sage023","Sage031","Sage037","Sage038","Sage039","Sage055","Sage066","Sage073","Sage074","Sage088"})
            foreach(bool up in new[]{false,true})
            {
                await Reset(s);
                foreach(var pile in new[]{PileType.Hand,PileType.Draw,PileType.Discard})foreach(var old in pile.GetPile(p).Cards.ToArray())await CardPileCmd.Add(old,PileType.Exhaust);
                p.PlayerCombatState.LoseEnergy(p.PlayerCombatState.Energy);await PlayerCmd.GainEnergy(3,p);
                var card=(WeatherCard)Create(s,p,id);if(up){card.UpgradeInternal();card.FinalizeUpgradeInternal();}
                await CardPileCmd.Add(card,PileType.Hand);int hp=enemy.CurrentHp;await Paid(card,enemy);
                rows.Add(new{scenario="paid standalone, empty draw pile, no fuel",id,upgraded=up,damage=hp-enemy.CurrentHp,block=p.Creature.Block,energy=p.PlayerCombatState.Energy,observations=sky.Observations});
                var damage=card.Spec.Ops.FirstOrDefault(o=>o.Kind=="damage");
                if(damage!=null){var hits=card.Spec.Ops.FirstOrDefault(o=>o.Kind=="hits");Check(hp-enemy.CurrentHp==(up?damage.Upgrade:damage.Base)*(hits==null?1:up?hits.Upgrade:hits.Base),"rebalanced damage exact "+id+up);}
            }
        }
        if(!CommandLineHelper.HasArg("tx-before"))
        {
            await Reset(s);
            await RelicCmd.Replace(p.GetRelic<WeatherCompass>()!,ModelDb.Relic<CelestialCompass>().ToMutable());
            int energy=p.PlayerCombatState!.Energy;
            for(int i=0;i<5;i++)await TianXiang.Sky.For(p).Observe(Ctx,enemy,"水",1,null);
            Check(p.PlayerCombatState.Energy-energy==5,"upgraded relic refunds all five observations");
            Check(p.Creature.Block==40,"upgraded relic grants block on all five observations");
            await RelicCmd.Replace(p.GetRelic<CelestialCompass>()!,ModelDb.Relic<WeatherCompass>().ToMutable());
        }
        File.WriteAllText(Output+"balance-"+(CommandLineHelper.HasArg("tx-before")?"before":"after")+".json",JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
    }
}

