using System.Text.Json;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using TianXiang;
namespace TianXiangQA;
public static partial class Probe
{
    static async Task CombatPlaytest(CombatState s,Player p)
    {
        using var selector=CardSelectCmd.UseSelector(new Selector());
        bool built=CommandLineHelper.HasArg("tx-built");
        int initialHp=p.Creature.CurrentHp;int maxHp=p.Creature.MaxHp;
        Check(p.RunState.AscensionLevel==(CommandLineHelper.HasArg("tx-a0")?0:10),"actual native ascension level");
        var nativeExtra=p.Deck.Cards.Where(c=>c is not WeatherCard).Select(c=>c.Id.ToString()).ToArray();
        if(built)
        {
            foreach(var pile in new[]{PileType.Hand,PileType.Draw,PileType.Discard})foreach(var c in pile.GetPile(p).Cards.ToArray())await CardPileCmd.Add(c,c is WeatherCard?PileType.Exhaust:PileType.Discard);
            foreach(string id in new[]{"Sage001","Sage001","Sage004","Sage004","Sage002","Sage005","Sage011","Sage009","Sage044","Sage046","Sage037","Sage022","Sage083","Sage055"})await CardPileCmd.Add(Create(s,p,id),PileType.Discard);
            await CardPileCmd.Shuffle(Ctx,p);await CardPileCmd.Draw(Ctx,5,p);
        }
        int plays=0;var rounds=new List<object>();
        for(int turn=0;turn<40&&!CombatManager.Instance.IsOverOrEnding&&p.Creature.IsAlive;turn++)
        {
            Check(MegaCrit.Sts2.Core.Runs.RunManager.Instance.ActionQueueSynchronizer.CombatState.ToString()=="PlayPhase","bot waits for native PlayPhase");
            for(int step=0;step<100&&!CombatManager.Instance.IsOverOrEnding&&p.Creature.IsAlive;step++)
            {
                var enemy=s.Enemies.Where(e=>e.IsAlive).OrderBy(e=>e.CurrentHp).FirstOrDefault();if(enemy==null)break;
                var candidates=PileType.Hand.GetPile(p).Cards.Where(c=>c.CanPlay()&&(c.TargetType!=TargetType.AnyEnemy||c.CanPlayTargeting(enemy))).OfType<WeatherCard>();
                var sky=TianXiang.Sky.For(p);var slot=sky.Slot(enemy);
                int Score(WeatherCard c)
                {
                    bool temp=c.Spec.Ops.Any(o=>o.Kind is "temp" or "tempAll" or "flip");
                    if(c.IsMaterial&&slot.Temperature!=0||temp&&slot.Material!="")return 100;
                    if(c.Type==CardType.Power)return 70;
                    if(c.Type==CardType.Attack)return 60;
                    if(c.Spec.Ops.Any(o=>o.Kind=="block")&&p.Creature.Block<10)return 50;
                    return 20;
                }
                var card=candidates.OrderByDescending(Score).FirstOrDefault();if(card==null)break;
                await Paid(card,enemy);plays++;Check(PileType.Hand.GetPile(p).Cards.Count<=10,"native hand model respects ten-card limit");
            }
            rounds.Add(new{turn=p.PlayerCombatState!.TurnNumber,hp=p.Creature.CurrentHp,block=p.Creature.Block,energy=p.PlayerCombatState.Energy,enemies=s.Enemies.Where(e=>e.IsAlive).Select(e=>new{hp=e.CurrentHp}).ToArray()});
            if(CombatManager.Instance.IsOverOrEnding||p.Creature.IsDead)break;
            int n=p.PlayerCombatState.TurnNumber;CombatManager.Instance.SetReadyToEndTurn(p,false);
            for(int i=0;i<600&&!CombatManager.Instance.IsOverOrEnding&&p.Creature.IsAlive&&(p.PlayerCombatState.TurnNumber==n||s.CurrentSide!=CombatSide.Player||MegaCrit.Sts2.Core.Runs.RunManager.Instance.ActionQueueSynchronizer.CombatState.ToString()!="PlayPhase");i++)await Delay(.05);
            if(!CombatManager.Instance.IsOverOrEnding&&p.Creature.IsAlive)Check(p.PlayerCombatState.TurnNumber>n,"playtest native enemy turn completed");
        }
        bool won=p.Creature.IsAlive&&!s.Enemies.Any(e=>e.IsAlive);
        var suffix=(CommandLineHelper.HasArg("tx-elite")?"elite":built?"built":"starter")+"-A"+p.RunState.AscensionLevel+"-"+Settings.TestSeed;
        File.WriteAllText(Output+"combat-playtest-"+suffix+".json",JsonSerializer.Serialize(new{fixture=suffix,seed=Settings.TestSeed,ascension=p.RunState.AscensionLevel,initialHp,maxHp,nativeExtra,won,hp=p.Creature.CurrentHp,plays,rounds},new JsonSerializerOptions{WriteIndented=true}));
        Check(CombatManager.Instance.IsOverOrEnding||p.Creature.IsDead,"playtest reaches natural battle outcome");
    }
}
