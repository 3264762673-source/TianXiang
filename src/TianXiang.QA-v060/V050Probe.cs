using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using TianXiang;
using Sky=TianXiang.Sky;
namespace TianXiangQA;
public static partial class Probe
{
    static async Task NativeAlterSelection(CombatState s,Player p)
    {
        foreach(var id in new[]{"Sage021","Sage022"})
        {
            await Reset(s);var enemy=s.Enemies.First();bool weather=id=="Sage021";
            CardModel fuel=weather?Create(s,p,"Sage002"):s.CreateCard(ModelDb.Card<Burn>(),p);
            CardModel other=Create(s,p,weather?"Sage003":"Sage001");
            CardModel excluded=weather?s.CreateCard(ModelDb.Card<Burn>(),p):Create(s,p,"Sage002");
            foreach(var c in new[]{fuel,other,excluded})await CardPileCmd.Add(c,PileType.Hand);
            var play=Create(s,p,id);await CardPileCmd.Add(play,PileType.Hand);var ctx=new UiChoiceContext();
            var task=play.OnPlayWrapper(ctx,play.TargetType==TargetType.AnyEnemy?enemy:null,true,new ResourceInfo{EnergyValue=1,EnergySpent=0,StarsSpent=0,StarValue=0},false);
            await Delay(.4);
            var hand=MegaCrit.Sts2.Core.Nodes.Combat.NPlayerHand.Instance!;
            var deadline=DateTime.UtcNow.AddSeconds(10);while(!hand.IsInCardSelection&&DateTime.UtcNow<deadline){if(task.IsFaulted)await task;await Delay(.1);}
            Check(hand.IsInCardSelection,"native hand select active "+id);
            var holder=hand.FindChildren("*","",true,false).OfType<MegaCrit.Sts2.Core.Nodes.Cards.Holders.NHandCardHolder>().Single(h=>h.CardNode?.Model==fuel);
            Check(holder.Visible,"matching fuel is visible "+id);
            typeof(MegaCrit.Sts2.Core.Nodes.Combat.NPlayerHand).GetMethod("SelectCardInSimpleMode",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(hand,[holder]);
            typeof(MegaCrit.Sts2.Core.Nodes.Combat.NPlayerHand).GetMethod("OnSelectModeConfirmButtonPressed",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(hand,[null]);
            for(int i=0;i<100&&!task.IsCompleted;i++)await Delay();
            if(task.IsFaulted)await task;
            Check(task.IsCompletedSuccessfully,"native exhaust selection returns "+id);await task;
            Check(fuel.Pile?.Type==PileType.Exhaust&&excluded.Pile?.Type==PileType.Hand,"only selected category fuel exhausted "+id);
            Check(ctx.Begun==ctx.Ended,"alter selection signals balanced "+id);
            await Play(Create(s,p,"Sage001"),enemy);
        }
    }
    static async Task V050Numbers(CombatState s,Player p)
    {
        var enemy=s.Enemies.First();var sky=Sky.For(p);
        using var selector=CardSelectCmd.UseSelector(new Selector());
        var alters=Catalogue.All.Values.Where(c=>c.Ops.Any(o=>o.Kind=="alter")).ToArray();
        Check(alters.Length==12,"exactly 12 alter cards");
        Check(alters.Count(c=>c.Rarity=="Common")==5&&alters.Count(c=>c.Rarity=="Uncommon")==4&&alters.Count(c=>c.Rarity=="Rare")==3,"alter rarity 5/4/3");
        foreach(var spec in alters)foreach(bool up in new[]{false,true})foreach(bool match in new[]{false,true})
        {
            await Reset(s);bool needsWeather=spec.Ops.Single(o=>o.Kind=="alter").Arg=="天气";
            CardModel fuel=needsWeather==match?Create(s,p,"Sage002"):s.CreateCard(ModelDb.Card<Burn>(),p);
            await CardPileCmd.Add(fuel,PileType.Hand);
            // Empty draw pile ensures a card's initial draw cannot accidentally create matching fuel.
            foreach(var c in PileType.Draw.GetPile(p).Cards.Concat(PileType.Discard.GetPile(p).Cards).ToArray())await CardPileCmd.Add(c,PileType.Exhaust);
            var cplay=Create(s,p,spec.Id);if(up){cplay.UpgradeInternal();cplay.FinalizeUpgradeInternal();}
            int energy=p.PlayerCombatState!.Energy,hp=enemy.CurrentHp;
            await Play(cplay,enemy);
            Check((fuel.Pile?.Type==PileType.Exhaust)==match,"alter consumes only matching category "+spec.Id+up+match);
            var energyOp=spec.Ops.FirstOrDefault(o=>o.Kind=="alterEnergy");
            if(energyOp!=null)Check(p.PlayerCombatState.Energy-energy==(match?(up?energyOp.Upgrade:energyOp.Base):0),"alter energy gated "+spec.Id+up+match);
            if(spec.Id=="Sage066")Check((p.Creature.GetPower<DexterityPower>()?.Amount??0)==(match?1:0),"alter dex gated "+up+match);
            if(spec.Id is "Sage064" or "Sage086")Check(sky.Observations==(match?(spec.Id=="Sage086"?s.Enemies.Count():1):0),"direct alter observation count "+spec.Id+up+match);
            if(spec.Id=="Sage021")Check(hp-enemy.CurrentHp==(up?9:6)+(match?2*(up?6:4):0),"alter attack exact two extra hits "+up+match);
            if(spec.Id=="Sage057")Check(hp-enemy.CurrentHp==2*(up?5:4)+(match?2*(up?5:3):0),"nonweather extra hits exact "+up+match);
        }
        foreach(var id in new[]{"Sage083","Sage090"})foreach(bool up in new[]{false,true})foreach(int observations in new[]{0,1,8})
        {
            await Reset(s);sky.Observations=observations;await PowerCmd.Apply<StrengthPower>(Ctx,p.Creature,2,p.Creature,null);
            var c=Create(s,p,id);if(up){c.UpgradeInternal();c.FinalizeUpgradeInternal();}int hp=enemy.CurrentHp;await Play(c,enemy);
            int perHit=id=="Sage083"?(up?5:4):(up?6:5);int hits=2+Math.Min(observations,up?4:2);
            Check(hp-enemy.CurrentHp==(perHit+2)*hits,"observation finisher and strength "+id+up+observations);
        }
        foreach(int diversity in new[]{0,3,6})
        {
            await Reset(s);foreach(var m in new[]{"水","尘","风"})foreach(int t in new[]{-1,1})if(sky.ObservedRecipes.Count<diversity)sky.ObservedRecipes.Add(m+t);
            int hp=enemy.CurrentHp;await Play(Create(s,p,"Sage088"),enemy);Check(hp-enemy.CurrentHp==4*(2+diversity),"diversity extra hit count "+diversity);
        }
        foreach(var id in new[]{"Sage052","Sage053","Sage054","Sage055","Sage077","Sage078","Sage079","Sage080","Sage081","Sage082"})
        {
            var c=(WeatherCard)Create(s,p,id);Check(c.Spec.Ops.Where(o=>o.Kind=="buff").All(o=>o.Upgrade>o.Base),"power smithing increases scaling "+id);
        }
        await Reset(s);await Play(Create(s,p,"Sage068"),enemy);Check(sky.NextObserveDamage==8,"precision prepares damage before polarity");
        await sky.SetMaterial(Ctx,enemy,"尘",Create(s,p,"Sage002"));Check(sky.NextObserveDamage==0,"prepared damage consumed by next observation");
        Check(Sky.Recipe("水",1).Strength==1&&Sky.Recipe("风",-1).Dexterity==1,"core attribute gains nerfed to one");
        Check(BattleUi.Compact("水",1).Contains("力量1")&&BattleUi.Compact("风",-1).Contains("敏捷1"),"UI preview matches core gains");
        var room=MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom.Instance!;BattleUi.Refresh(sky);
        Check(room.GetNodeOrNull<Button>("TianXiangReferenceToggle")!=null,"combat reference toggle exists");
        var label=room.GetNode<PanelContainer>("TianXiangReference").GetNode<Label>("Table");
        Check(label.Text.Contains("水阴")&&label.Text.Contains("风阳")&&!label.Text.Contains("暖")&&!label.Text.Contains("冷"),"all six combos and no cold-warm wording");
    }
}
