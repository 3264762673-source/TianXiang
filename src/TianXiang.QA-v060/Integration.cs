using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using TianXiang;
namespace TianXiangQA;
public static partial class Probe
{
    static async Task BakeAttack()
    {
        var vp=new SubViewport{Size=new(384,448),TransparentBg=true,RenderTargetUpdateMode=SubViewport.UpdateMode.Always};
        NRoot().AddChild(vp);
        var rig=CharacterRig.Build(false);
        rig.Position=new(160,400);
        vp.AddChild(rig);
        var anim=rig.GetNode<AnimationPlayer>("AnimationPlayer");
        anim.Play("attack"); anim.SpeedScale=0;
        var atlas=Image.CreateEmpty(384*7,448*4,false,Image.Format.Rgba8);
        for(int i=0;i<27;i++)
        {
            anim.Seek(i/48.0,true);
            await vp.ToSignal(vp.GetTree(),SceneTree.SignalName.ProcessFrame);
            await vp.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            var frame=vp.GetTexture().GetImage();
            frame.Convert(Image.Format.Rgba8);
            Check(frame.GetUsedRect().Size.X>140,"rendered pose "+i);
            atlas.BlitRect(frame,new Rect2I(0,0,384,448),new Vector2I(i%7*384,i/7*448));
        }
        Check(atlas.SavePng(Output+"resources/TianXiang/attack-sheet.png")==Error.Ok,"saved baked attack");
        vp.QueueFree();
    }
    static Node NRoot()=>MegaCrit.Sts2.Core.Nodes.NGame.Instance!;
    static async Task CheckForeign(CombatState state,Player p)
    {
        var ev=ModelDb.Event<ColorfulPhilosophers>().ToMutable();
        await ev.BeginEvent(p,null,false);
        bool seen=false;
        for(int i=0;i<30 && !seen;i++)
        {
            var options=(IReadOnlyList<MegaCrit.Sts2.Core.Events.EventOption>)AccessTools.Method(typeof(ColorfulPhilosophers),"GenerateInitialOptions").Invoke(ev,null)!;
            Check(options.Count<=3,"native option count");
            seen=options.Any(o=>o.TextKey.EndsWith(".TIANXIANG") && o.Title.GetFormattedText().Length>0);
        }
        Check(seen,"weather option for another character");
        await Play(Create(state,p,"Sage002"),state.Enemies[0]);
        Check(p.Creature.GetPower<ObservationPower>()!=null && TianXiang.Sky.For(p).Slot(state.Enemies[0]).Material=="水","borrowed card starts observation");
    }
    static async Task CheckIntegration(CombatState state,Player p)
    {
        var pool=p.Character.PotionPool.GetUnlockedPotions(p.UnlockState).ToList();
        Check(pool.Count==2 && pool.All(x=>x is SkyPotion),"character potion pool only weather potions");
        Check(pool.All(x=>x.Title.GetFormattedText().Length>0 && x.Image!=null),"potion icons and names");
        var philosophers=(IEnumerable<CardPoolModel>)AccessTools.PropertyGetter(typeof(ColorfulPhilosophers),"CardPoolColorOrder").Invoke(null,null)!;
        Check(philosophers.Count(x=>x is WeatherCardPool)==1 && p.UnlockState.CharacterCardPools.Contains(p.Character.CardPool),"philosopher candidate");
        var cookie=ModelDb.Relic<YummyCookie>().ToMutable();p.AddRelicInternal(cookie);
        Check(cookie.Icon.ResourcePath.Contains("cookie.svg"),"owned cookie art");
        foreach(var wins in new[]{0,1,4})
            Check(ModelDb.Event<TheArchitect>().DialogueSet.GetValidDialogues(p.Character.Id,wins,wins,false).Any(),"architect dialogue at wins "+wins);
        var relics=p.Character.RelicPool.GetUnlockedRelics(p.UnlockState).OfType<SkyRelic>().ToArray();
        Check(relics.Length==3,"three class relics");
        foreach(var relic in relics){Check(relic.Icon!=null,"relic icon "+relic.Id);p.AddRelicInternal(relic.ToMutable());}
        var sky=TianXiang.Sky.For(p);var enemy=state.Enemies.First();
        var rain=ModelDb.Potion<RainVial>().ToMutable();p.AddPotionInternal(rain);
        var before=p.Creature.Block;await rain.OnUseWrapper(Ctx,enemy);
        Check(sky.Slot(enemy).Material=="水" && p.Creature.Block-before==6,"rain vial");
        before=p.Creature.Block;await sky.SetTemperature(Ctx,enemy,-1,Create(state,p,"Sage005"));
        Check(p.Creature.Block-before==9,"dew cup");
        var dust=ModelDb.Potion<DustVial>().ToMutable();p.AddPotionInternal(dust);
        var hp=enemy.CurrentHp;await dust.OnUseWrapper(Ctx,enemy);
        Check(hp-enemy.CurrentHp==12,"dust vial and sun needle");
        hp=enemy.CurrentHp;await sky.Observe(Ctx,enemy,"水",-1,null);
        Check(hp-enemy.CurrentHp==5,"third observation bell");
        var rig=NRoot().GetTree().Root.FindChildren("CharacterRig","",true,false).OfType<Node2D>().First();
        var player=rig.GetNode<AnimationPlayer>("AnimationPlayer");var frames=rig.GetNode<Sprite2D>("AttackFrames");
        var cues=new List<string>();player.AnimationStarted+=n=>cues.Add(n.ToString());
        await Play(Create(state,p,"Sage001"),enemy);
        Check(cues.Contains("attack"),"actual strike triggers baked animation");
        player.Play("attack");player.Seek(.15,true);
        Check(frames.Visible && frames.Frame==7 && !rig.GetNode<Polygon2D>("Skin").Visible,"impact frame");
        await Delay(.7);Check(!frames.Visible && player.CurrentAnimation=="idle",$"recovery to idle ({player.CurrentAnimation}, visible={frames.Visible}, playing={player.IsPlaying()}, time={player.CurrentAnimationPosition}, speed={player.SpeedScale}, processing={player.CanProcess()}, paused={player.GetTree().Paused})");
    }
}


