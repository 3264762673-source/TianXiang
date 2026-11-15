using MegaCrit.Sts2.Core.Nodes.Combat;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Debug;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;
using MegaCrit.Sts2.Core.TestSupport;
using TianXiang;
using CelestialWeather;
using Sky=TianXiang.Sky;
using MegaCrit.Sts2.Core.Runs;
namespace TianXiangQA;
[ModInitializer(nameof(Initialize))]
public static partial class Probe
{
    static bool started;static int assertions;
    static readonly PlayerChoiceContext Ctx=new ThrowingPlayerChoiceContext();
    const string Output="D:/mod/TianXiang-v0.6.0/";
    public static void Initialize()
    {
        new Harmony("TianXiang.QA").PatchAll();
        CombatManager.Instance.TurnStarted+=state=>{if(started||state.CurrentSide!=CombatSide.Player)return;started=true;TaskHelper.RunSafely(Run(state));};
    }
    static void Check(bool value,string name){if(!value)throw new Exception("FAIL "+name);assertions++;GD.Print("[QA PASS] "+name);}
    static async Task Delay(double n=.1)=>await NGame.Instance!.ToSignal(NGame.Instance.GetTree().CreateTimer(n),SceneTreeTimer.SignalName.Timeout);
    static CardModel Create(CombatState state,Player p,string id)=>state.CreateCard(ModelDb.GetById<CardModel>(ModelDb.GetId(typeof(WeatherCard).Assembly.GetType("TianXiang."+id)!)),p);
    static async Task ClearHand(Player p){foreach(var c in PileType.Hand.GetPile(p).Cards.ToArray())await CardPileCmd.Add(c,PileType.Discard);}
    static async Task Reset(CombatState s)
    {
        var p=s.Players[0];var sky=Sky.For(p);Climate.For(s).RestoreUpgrades();sky.Slots.Clear();sky.Last=null;
        sky.NextObserveBlock=sky.NextObserveDamage=0;sky.ObserveDrawUsed=sky.ObserveEnergyUsed=sky.ObserveGuardUsed=false;sky.Observations=0;sky.RelicTriggers=0;sky.ObservedRecipes.Clear();
        foreach(var c in s.Creatures){foreach(var power in c.Powers.Where(x=>x is not ObservationPower).ToArray())await PowerCmd.Remove(power);c.LoseBlockInternal(c.Block);c.SetCurrentHpInternal(c.MaxHp);}
        await ClearHand(p);
    }
    static async Task Play(CardModel c,MegaCrit.Sts2.Core.Entities.Creatures.Creature enemy)
    {
        await CardPileCmd.Add(c,PileType.Hand);
        await c.OnPlayWrapper(Ctx,c.TargetType==TargetType.AnyEnemy?enemy:null,true,new ResourceInfo{EnergySpent=0,EnergyValue=c.EnergyCost.Canonical,StarsSpent=0,StarValue=0},false);
        await Delay(.04);
    }
    static async Task EndTurn(CombatState s,Player p)
    {
        int n=p.PlayerCombatState!.TurnNumber;CombatManager.Instance.SetReadyToEndTurn(p,false);
        for(int i=0;i<300&&(p.PlayerCombatState.TurnNumber==n||s.CurrentSide!=CombatSide.Player);i++)await Delay();
        await Delay(.35);Check(p.PlayerCombatState.TurnNumber==n+1,"native next turn");
    }
    static async Task Cards(CombatState state,Player p)
    {
        using var selector=CardSelectCmd.UseSelector(new Selector());var enemy=state.Enemies.First();
        foreach(var spec in Catalogue.All.Values)
        {
            var c=Create(state,p,spec.Id);Check(c.Title==spec.Name,"localization "+spec.Id);
            Check(!c.Description.GetRawText().Contains("\n消耗。")&&!c.Description.GetRawText().Contains("合成"),"no manual self-exhaust keyword "+spec.Id);
            Check(!c.Description.GetRawText().Contains("冷")&&!c.Description.GetRawText().Contains("暖"),"yin-yang terminology "+spec.Id);
            Check(c.Keywords.Contains(CardKeyword.Exhaust)==spec.Exhaust,"native Exhaust "+spec.Id);
            Check(c.Portrait!=null,"real portrait "+spec.Id);
            c.UpgradeInternal();c.FinalizeUpgradeInternal();var loaded=CardModel.FromSerializable(c.ToSerializable());Check(loaded.IsUpgraded&&loaded.Id==c.Id,"save restore "+spec.Id);
            foreach(bool up in new[]{false,true})
            {
                await Reset(state);for(int j=0;j<3;j++)await CardPileCmd.Add(Create(state,p,"Sage002"),PileType.Hand);
                var card=Create(state,p,spec.Id);if(up){card.UpgradeInternal();card.FinalizeUpgradeInternal();}
                await Play(card,enemy);Check(card.Pile?.Type!=PileType.Play,"play "+spec.Id+(up?"+":""));
                if(spec.Exhaust)Check(card.Pile?.Type==PileType.Exhaust,"exhaust destination "+spec.Id);
            }
        }
        foreach(var room in new[]{RoomType.Monster,RoomType.Elite,RoomType.Boss})for(int i=0;i<5;i++)
        {var reward=new CardReward(CardCreationOptions.ForRoom(p,room),3,p);reward.Populate();Check(reward.Cards.Count()==3&&reward.Cards.All(c=>c is WeatherCard&&c.Rarity!=CardRarity.Basic),"reward pool "+room);}
        await Reset(state);foreach(var c in state.Enemies)c.SetCurrentHpInternal(1);await Play(Create(state,p,"Sage037"),enemy);await Delay(2);Check(CombatManager.Instance.IsOverOrEnding,"battle victory");
    }
    static async Task Numbers(CombatState state,Player p)
    {
        var enemy=state.Enemies.First();var sky=Sky.For(p);var source=Create(state,p,"Sage005");
        ExportBaselines(state,p);
        await V050Numbers(state,p);
        foreach(var material in new[]{"水","尘","风"})foreach(int temp in new[]{-1,1})
        {
            await Reset(state);int energy=p.PlayerCombatState!.Energy;int hp=enemy.CurrentHp;
            await sky.SetMaterial(Ctx,enemy,material,source);await sky.SetTemperature(Ctx,enemy,temp,source);
            var r=Sky.Recipe(material,temp);
            Check(sky.Slot(enemy).Material==""&&sky.Slot(enemy).Temperature==0,"slots clear "+material+temp);
            Check(enemy.CurrentHp==hp-r.Damage,"reaction damage "+material+temp);
            Check(p.Creature.Block==r.Block,"reaction block "+material+temp);
            Check(p.PlayerCombatState.Energy==energy+r.Energy+1,"reaction and relic energy "+material+temp);
            Check((p.Creature.GetPower<StrengthPower>()?.Amount??0)==r.Strength,"strength "+material+temp);
            Check((p.Creature.GetPower<DexterityPower>()?.Amount??0)==r.Dexterity,"dexterity "+material+temp);
        }
        Check(ModelDb.Relic<TouchOfOrobas>().GetUpgradedStarterRelic(ModelDb.Relic<WeatherCompass>()) is CelestialCompass,"Orobas custom starter upgrade");
        Check(ModelDb.Relic<TouchOfOrobas>().GetUpgradedStarterRelic(ModelDb.Relic<BurningBlood>()) is BlackBlood,"Orobas vanilla mapping preserved");
        await Reset(state);
        using(var selector=CardSelectCmd.UseSelector(new Selector()))
        {
            foreach(var c in PileType.Discard.GetPile(p).Cards.ToArray())await CardPileCmd.Add(c,PileType.Exhaust);
            var water=Create(state,p,"Sage058");var dust=Create(state,p,"Sage059");await CardPileCmd.Add(water,PileType.Discard);await CardPileCmd.Add(dust,PileType.Discard);
            await CardPileCmd.Add(Create(state,p,"Sage001"),PileType.Hand);await Play(Create(state,p,"Sage089"),enemy);
            Check(water.Pile?.Type==PileType.Hand&&dust.Pile?.Type==PileType.Hand,"archive retrieves two material cards");
            Check(water.EnergyCost.GetWithModifiers(CostModifiers.All)==0&&dust.EnergyCost.GetWithModifiers(CostModifiers.All)==0,"archive retrieved cards zero this turn");
            await EndTurn(state,p);
            Check(water.EnergyCost.GetWithModifiers(CostModifiers.All)==2,"archive discount expires");
        }
        for(int i=0;i<3;i++)await EndTurn(state,p);
        Check(Climate.For(state).Executions==0,"standalone character has no automatic weather");
    }
    static async Task Weather(CombatState state,Player p)
    {
        var sky=Climate.For(state);var enemy=state.Enemies.First();
        Check(Climate.Names.Length==10,"ten weather types");
        Check(ModelDb.BadModifiers.Any(x=>x is UnpredictableWeather),"native custom-run modifier registered");
        for(int i=0;i<10;i++)
        {
            await Reset(state);sky.Weather=i;sky.Initialize(p);sky.Weather=i;
            foreach(var c in state.Creatures)c.SetCurrentHpInternal(c.MaxHp-100);
            var card=Create(state,p,"Sage001");await CardPileCmd.Add(card,PileType.Hand);
            int hp=p.Creature.CurrentHp,enemyHp=state.Enemies.Sum(e=>e.CurrentHp),energy=p.PlayerCombatState!.Energy;
            if(i==8)foreach(var c in state.Creatures)await CreatureCmd.GainBlock(c,9,MegaCrit.Sts2.Core.ValueProps.ValueProp.Unpowered,null);
            await sky.Execute(Ctx,p);
            switch(i)
            {
                case 0:Check(state.Creatures.All(c=>c.GetPower<VulnerablePower>()?.Amount==1),"dusk both sides vulnerable");break;
                case 1:Check(card.IsUpgraded,"rain upgrades current hand");sky.RestoreUpgrades();Check(!card.IsUpgraded,"rain restores current hand");break;
                case 2:Check(p.Creature.CurrentHp==hp+2,"fair heals two");break;
                case 3:Check(p.Creature.CurrentHp==hp-3&&state.Enemies.Sum(e=>e.CurrentHp)==enemyHp-state.Enemies.Count()*9,"sun three hits each side");break;
                case 4:Check(p.Creature.GetPower<FrailPower>()?.Amount==1,"typhoon frail");break;
                case 5:Check(state.Creatures.All(c=>c.GetPower<WeakPower>()?.Amount==1),"cold both sides weak");break;
                case 6:Check(state.Creatures.All(c=>c.Block==6),"fog both sides block");break;
                case 7:Check(p.Creature.CurrentHp==hp-3&&state.Enemies.Sum(e=>e.CurrentHp)==enemyHp-12,"lightning random enemy");break;
                case 8:Check(state.Creatures.All(c=>c.Block==3),"dust removes six block");break;
                case 9:Check(p.PlayerCombatState.Energy==energy+1,"tailwind energy");break;
            }
        }
        await Reset(state);int previous=sky.Executions;
        for(int i=0;i<6;i++){int next=p.PlayerCombatState!.TurnNumber+1;await EndTurn(state,p);if(next%2==0)previous++;Check(sky.Executions==previous,"weather executes exactly on even round "+next);Check(sky.Weather>=0&&sky.Weather<10,"random weather within ten");}
    }
    static async Task Choice(CombatState s,Player p)
    {
        var enemy=s.Enemies.First();
        foreach(var id in new[]{"Sage005","Sage016","Sage032","Sage064","Sage067","Sage068","Sage072","Sage086"})for(int n=0;n<2;n++)
        {
            await Reset(s);
            foreach(var old in PileType.Discard.GetPile(p).Cards.ToArray())await CardPileCmd.Add(old,PileType.Exhaust);
            if(id is "Sage032" or "Sage064" or "Sage086")await CardPileCmd.Add(Create(s,p,"Sage002"),PileType.Hand);
            var card=Create(s,p,id);await CardPileCmd.Add(card,PileType.Hand);var ctx=new UiChoiceContext();
            var task=card.OnPlayWrapper(ctx,card.TargetType==TargetType.AnyEnemy?enemy:null,true,new ResourceInfo{EnergyValue=card.EnergyCost.Canonical,EnergySpent=0,StarsSpent=0,StarValue=0},false);
            await Delay(.5);
            MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NChooseACardSelectionScreen? screen=null;
            var deadline=DateTime.UtcNow.AddSeconds(10);
            while(screen==null&&DateTime.UtcNow<deadline){if(task.IsFaulted)await task;screen=NGame.Instance!.GetTree().Root.FindChildren("NChooseACardSelectionScreen","",true,false).OfType<MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NChooseACardSelectionScreen>().SingleOrDefault();if(screen==null)await Delay(.1);}
            Check(screen!=null,"native choose screen "+id);
            var holders=screen!.GetNode<Control>("CardRow").GetChildren().OfType<MegaCrit.Sts2.Core.Nodes.Cards.Holders.NCardHolder>().ToArray();
            Check(holders.Length==2,"two visible temperature choices");
            // Native reward screens reject input for 350 wall-clock milliseconds.
            // FastMode accelerates Godot timers, so wait against the same native clock.
            while(Time.GetTicksMsec()-screen.Get("_openedTicks").AsUInt64()<=400)await Delay(.05);
            screen.Call("SelectHolder",holders[n]);
            for(int i=0;i<100&&!task.IsCompleted;i++)await Delay();
            if(task.IsFaulted)await task;
            Check(task.IsCompletedSuccessfully,"choice returns without deadlock");await task;
            Check(ctx.Begun==ctx.Ended,"choice signals paired");
            Check((id is "Sage064" or "Sage086"?Sky.For(p).Last?.Temperature:Sky.For(p).Slot(enemy).Temperature)==(n==0?-1:1),"selected polarity applied");
            await Play(Create(s,p,"Sage001"),enemy);
        }
        await NativeAlterSelection(s,p);
    }
    static async Task AnimationProof(CombatState state,Player p)
    {
        MegaCrit.Sts2.Core.Nodes.Screens.Capstones.NCapstoneContainer.Instance?.Close();await Delay(.3);await ClearHand(p);
        foreach(var id in new[]{"Sage001","Sage004","Sage005","Sage021","Sage083"})await CardPileCmd.Add(Create(state,p,id),PileType.Hand);
        var visual=NGame.Instance!.GetTree().Root.FindChildren("*","",true,false).OfType<NCreatureVisuals>().Single(v=>v.GetNodeOrNull("Visuals/CharacterRig")!=null);
        var rig=visual.GetNode<Node2D>("Visuals/CharacterRig");var anim=rig.GetNode<AnimationPlayer>("AnimationPlayer");var skin=rig.GetNode<Polygon2D>("Skin");
        Check(skin.GetBoneCount()==6,"six weighted bones");Check(skin.Polygon.Length==1617,"dense skin mesh");Check(((Sprite2D)visual.GetNode("Visuals")).Texture==null,"static sprite removed");
        Directory.CreateDirectory(Output+"animation-frames30");int frame=0; bool capture=!CommandLineHelper.HasArg("tx-no-capture");
        foreach(var cue in new[]{"idle","attack","cast","hurt","die"})
        {
            Check(STS2RitsuLib.Scaffolding.Characters.Visuals.ModCreatureVisualPlayback.TryPlayCue(visual,p.Character,cue),"native cue accepted "+cue);
            Check(anim.CurrentAnimation==cue,"native cue selects continuous clip "+cue);
            anim.SpeedScale=0;double length=anim.GetAnimation(cue).Length;
            var before=rig.GetNode<Bone2D>("Skeleton/Torso/Arm").Rotation;
            int samples=(int)Math.Ceiling(length*30);
            for(int i=0;i<samples;i++)
            {
                anim.Seek(length*i/samples,true);await NGame.Instance.ToSignal(NGame.Instance.GetTree(),SceneTree.SignalName.ProcessFrame);await NGame.Instance.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                if(capture)NGame.Instance.GetViewport().GetTexture().GetImage().GetRegion(new Rect2I(300,250,850,850)).SavePng(Output+$"animation-frames30/{frame++:D4}.png");
                if(i==10)Check(Math.Abs(rig.GetNode<Bone2D>("Skeleton/Torso").Rotation)>0.00001f||cue=="idle","interpolated torso "+cue);
            }
        }
        anim.SpeedScale=1;anim.Play("idle");await Delay(.2);
        Check(skin.Texture.ResourcePath.EndsWith("character-idle.png"),"one bound skin, no texture swaps");
        NGame.Instance.GetViewport().GetTexture().GetImage().SavePng(Output+"native-animation.png");
        var seen=new List<string>();anim.AnimationStarted+=n=>seen.Add(n.ToString());
        await Play(Create(state,p,"Sage001"),state.Enemies.First());await Delay(.3);
        Check(seen.Contains("attack"),"actual attack card triggers rig");
        seen.Clear();await Play(Create(state,p,"Sage004"),p.Creature);await Delay(.3);
        Check(seen.Contains("cast"),"actual skill card triggers rig");
        await Delay(.7);Check(anim.CurrentAnimation=="idle","action returns to idle");
        var expanded=MegaCrit.Sts2.Core.Nodes.Cards.NCard.Create(Create(state,p,"Sage083"))!;NGame.Instance.AddChild(expanded);expanded.UpdateVisuals(PileType.Hand,CardPreviewMode.Normal);expanded.Position=new(1250,650);expanded.Scale=new(2,2);await Delay(.3);
        NGame.Instance.GetViewport().GetTexture().GetImage().SavePng(Output+"native-wide-card.png");expanded.QueueFree();

    }
    static async Task Run(CombatState state)
    {
        string mode=CommandLineHelper.HasArg("tx-playtest")?"playtest":CommandLineHelper.HasArg("tx-balance")?"balance":CommandLineHelper.HasArg("tx-animation")?"animation":CommandLineHelper.HasArg("tx-avatar")?"avatar":CommandLineHelper.HasArg("tx-weather")?"weather":CommandLineHelper.HasArg("tx-numbers")?"numbers":CommandLineHelper.HasArg("tx-preview")?"preview":CommandLineHelper.HasArg("tx-choice")?"choice":"cards";
        try
        {
            await Delay(1);SaveManager.Instance.PrefsSave.FastMode=FastModeType.Instant;var p=state.Players[0];
            if(mode!="preview"&&mode!="animation"&&mode!="playtest")foreach(var c in state.Creatures){c.SetMaxHpInternal(100000);c.SetCurrentHpInternal(100000);}
            if(CommandLineHelper.HasArg("tx-foreign"))
            {
                await CheckForeign(state,p);
                GD.Print("[QA COMPLETE] foreign event");
                NGame.Instance!.GetTree().Quit();
                return;
            }
            if(mode!="weather")Check(Catalogue.All.Count==90&&p.Deck.Cards.Count(c=>c is WeatherCard)==10,"90 cards / 10 starter cards");
            if(CommandLineHelper.HasArg("tx-bake"))await BakeAttack();
            else if(CommandLineHelper.HasArg("tx-integration"))await CheckIntegration(state,p);
            else if(mode=="cards")await Cards(state,p);
            else if(mode=="playtest")await CombatPlaytest(state,p);
            else if(mode=="balance")await Balance(state,p);
            else if(mode=="numbers")await Numbers(state,p);
            else if(mode=="weather")await Weather(state,p);
            else if(mode=="choice")await Choice(state,p);
            else if(mode=="animation")await AnimationProof(state,p);
            else if(mode=="avatar") {var icon=p.Character.Icon;Check(icon.Name=="TianXiangPortrait","top-panel actual character Icon uses own scene");Check(icon.GetNode<TextureRect>("Portrait").Texture.ResourcePath.Contains("TianXiang/images/portrait"),"top-panel own portrait texture");icon.QueueFree();}
            else
            {
                MegaCrit.Sts2.Core.Nodes.Screens.Capstones.NCapstoneContainer.Instance?.Close();await Delay(.5);await ClearHand(p);
                foreach(var id in new[]{"Sage001","Sage004","Sage005","Sage021","Sage083"})await CardPileCmd.Add(Create(state,p,id),PileType.Hand);
                await Delay(1);MegaCrit.Sts2.Core.Nodes.HoverTips.NHoverTipSet.Clear();await Delay(.1);
                NGame.Instance!.GetViewport().GetTexture().GetImage().SavePng(Output+"native-battle.png");
                var top=NGame.Instance.GetTree().Root.FindChildren("*","",true,false).OfType<MegaCrit.sts2.Core.Nodes.TopBar.NTopBarPortrait>().Single();
                Check(top.GetNodeOrNull<Control>("TianXiangPortrait")!=null,"real top bar contains custom portrait scene");
                var scene=MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom.Instance!;Sky.For(p).Slot(state.Enemies.First()).Temperature=1;BattleUi.Refresh(Sky.For(p));
                scene.GetNode<PanelContainer>("TianXiangReference").Visible=true;await Delay(.5);
                NGame.Instance.GetViewport().GetTexture().GetImage().SavePng(Output+"native-reference-table.png");
                scene.GetNode<PanelContainer>("TianXiangReference").Visible=false;
                var selection=MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect.NCharacterSelectScreen.Create()!;
                NGame.Instance.AddChild(selection);selection.InitializeSingleplayer();
                var button=selection.GetNode<MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect.NCharacterSelectButton>("CharSelectButtons/ButtonContainer/"+p.Character.Id.Entry+"_button");
                Check(!button.IsLocked,"native independent character selectable");selection.SelectCharacter(button,p.Character);
                await Delay(1);
                NGame.Instance.GetViewport().GetTexture().GetImage().SavePng(Output+"native-character-select.png");selection.QueueFree();await Delay(.2);
                var backdrop=new ColorRect{Color=new Color("101a24"),Size=new Vector2(3840,2160),MouseFilter=Control.MouseFilterEnum.Ignore};NGame.Instance.AddChild(backdrop);
                var custom=MegaCrit.Sts2.Core.Nodes.Screens.CustomRun.NCustomRunScreen.Create()!;NGame.Instance.AddChild(custom);custom.InitializeSingleplayer();
                var list=custom.GetNode<MegaCrit.Sts2.Core.Nodes.Screens.MainMenu.NCustomRunModifiersList>("%ModifiersList");
                var tickbox=list.FindChildren("*","",true,false).OfType<MegaCrit.Sts2.Core.Nodes.Screens.CustomRun.NRunModifierTickbox>().Single(t=>t.Modifier is UnpredictableWeather);
                tickbox.IsTicked=true;Check(list.GetModifiersTickedOn().Any(m=>m is UnpredictableWeather),"native weather challenge checkbox");
                Check(ModifierModel.FromSerializable(tickbox.Modifier!.ToSerializable()) is UnpredictableWeather,"weather modifier save restore");
                await Delay(.4);list.GetNode("ScrollContainer").Set("_targetDragPosY",list.GetNode("ScrollContainer").Get("ScrollLimitBottom"));await Delay(.8);
                await Delay(1);NGame.Instance.GetViewport().GetTexture().GetImage().SavePng(Output+"native-custom-run.png");custom.QueueFree();
            }
            var result=$"PASS {mode}: {assertions} assertions";GD.Print("[QA COMPLETE] "+result);File.WriteAllText(Output+"qa-"+mode+"-result.txt",result);NGame.Instance!.GetTree().Quit();
        }
        catch(Exception e){GD.PrintErr("[QA FAILURE] "+e);File.WriteAllText(Output+"qa-"+mode+"-result.txt",e.ToString());NGame.Instance!.GetTree().Quit(1);}
    }
}
public class UiChoiceContext:PlayerChoiceContext
{
    public int Begun,Ended;
    public override ulong? OwnerId=>null;
    public override Task SignalPlayerChoiceBegun(Player player,MegaCrit.Sts2.Core.Entities.Multiplayer.PlayerChoiceOptions options){Begun++;return Task.CompletedTask;}
    public override Task SignalPlayerChoiceEnded(){Ended++;return Task.CompletedTask;}
}
public class Selector:ICardSelector
{
    public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options,int min,int max)=>Task.FromResult(options.Take(Math.Max(min,Math.Min(2,max))).AsEnumerable());
    public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options,IReadOnlyList<CardRewardAlternative> alternatives)=>default;
}
[HarmonyPatch(typeof(BootstrapSettingsUtil),nameof(BootstrapSettingsUtil.Get))]
public static class BootstrapPatch{static bool Prefix(ref Type? __result){__result=typeof(Settings);return false;}}
public class Settings:IBootstrapSettings
{
    public CharacterModel Character=>CommandLineHelper.HasArg("tx-vanilla")?ModelDb.Character<MegaCrit.Sts2.Core.Models.Characters.Ironclad>():ModelDb.Character<WeatherCharacter>();
    public RoomType RoomType=>CommandLineHelper.HasArg("tx-elite")?RoomType.Elite:RoomType.Monster;public EncounterModel Encounter=>CommandLineHelper.HasArg("tx-elite")?ModelDb.Encounter<BygoneEffigyElite>():CommandLineHelper.HasArg("tx-built")?ModelDb.Encounter<CultistsNormal>():ModelDb.Encounter<SlimesWeak>();public EventModel Event=>null!;public ActModel Act=>ModelDb.Act<Overgrowth>();
    public int Ascension=>CommandLineHelper.HasArg("tx-a0")?0:10;public bool SaveRunHistory=>false;public static string TestSeed=>System.Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("--tx-seed="))?.Split("=",2)[1]??"TIANXIANGQA";public string Seed=>TestSeed;public bool DoPreloading=>true;public bool BootstrapInMultiplayer=>false;
    public List<ModifierModel> Modifiers=>CommandLineHelper.HasArg("tx-weather")||CommandLineHelper.HasArg("tx-preview")?[ModelDb.Modifier<UnpredictableWeather>().ToMutable()]:[];
    public string Language=>"zhs";public Task Setup(Player p){SaveManager.Instance.SetFtuesEnabled(false);return Task.CompletedTask;}
}





