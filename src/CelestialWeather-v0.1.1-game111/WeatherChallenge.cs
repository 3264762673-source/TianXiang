using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop;

namespace CelestialWeather;
[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
    public static void Initialize()
    {
        ModTypeDiscoveryHub.RegisterModAssembly("CelestialWeather",typeof(ModEntry).Assembly);
        if(!ProjectSettings.LoadResourcePack(Path.Combine(Path.GetDirectoryName(typeof(ModEntry).Assembly.Location)!,"assets.zip")))throw new IOException("Weather resources missing");
        ModContentRegistry.For("CelestialWeather").RegisterBadModifier<UnpredictableWeather>();
        GD.Print("[CelestialWeather] Loaded v0.1.0; opt-in custom-run modifier only.");
    }
}

// A native run modifier receives hooks only when selected for that run.
// No dependency on TianXiang, no global battle patch and no character checks.
public sealed class UnpredictableWeather:ModifierModel
{
    public override Task BeforeCombatStart()
    {
        foreach(var p in RunState.Players)
            if(p.Creature.CombatState is {} state)Climate.For(state).Initialize(p);
        return Task.CompletedTask;
    }
    protected override string IconPath=>"res://CelestialWeather/images/challenge.png";
    public override Task AfterPlayerTurnStart(PlayerChoiceContext ctx,Player player)=>Climate.For(player.Creature.CombatState!).StartTurn(ctx,player);
    public override Task AfterSideTurnEnd(PlayerChoiceContext ctx,CombatSide side,IEnumerable<Creature> participants)
    {
        if(side==CombatSide.Player)foreach(var p in RunState.Players)
            if(p.Creature.CombatState is {} state)Climate.For(state).RestoreUpgrades();
        return Task.CompletedTask;
    }
    public override Task AfterCombatEnd(CombatRoom room)
    {
        foreach(var p in RunState.Players)if(p.Creature.CombatState is {} state)Climate.For(state).RestoreUpgrades();
        return Task.CompletedTask;
    }
}

public sealed class Climate
{
    static readonly ConditionalWeakTable<ICombatState,Climate> States=new();
    readonly HashSet<CardModel> upgraded=[];
    bool initialized;
    MegaCrit.Sts2.Core.Random.Rng? rng;
    int lastExecutedTurn;
    public int Weather{get;set;}
    public int Turns{get;private set;}
    public int Executions{get;private set;}
    public static Climate For(ICombatState state)=>States.GetValue(state,_=>new());
    public static readonly string[] Names=["黄昏","疏雨","晴岚","烈日","台风","寒潮","浓雾","雷暴","沙尘","顺风"];
    public static readonly string[] Effects=[
        "所有角色与敌人获得1层易伤（持续至本轮敌人回合结束）",
        "所有玩家当前手牌在本回合升级；回合结束恢复",
        "所有玩家回复2点生命",
        "每个玩家受到1×3伤害；每个敌人受到3×3伤害（可被格挡）",
        "所有玩家获得1层脆弱（持续至本轮敌人回合结束）",
        "所有角色与敌人获得1层虚弱（持续至本轮敌人回合结束）",
        "所有角色与敌人获得6点格挡",
        "每个玩家受到3点伤害；随机一个敌人受到12点伤害（可被格挡）",
        "所有角色与敌人失去最多6点格挡",
        "所有玩家抽2张牌，获得1点能量"
    ];
    public void Initialize(Player p)
    {
        if(initialized)return;initialized=true;
        rng=new MegaCrit.Sts2.Core.Random.Rng(p,ModelDb.GetId(typeof(UnpredictableWeather)),(uint)p.RunState.TotalFloor);
        Weather=rng.NextInt(Names.Length);
    }
    public async Task StartTurn(PlayerChoiceContext ctx,Player p)
    {
        Initialize(p);Turns=p.PlayerCombatState!.TurnNumber;
        // Shared combat effects execute once per round, even in a multi-player run.
        if(Turns>0&&Turns%2==0&&lastExecutedTurn!=Turns)
        {
            lastExecutedTurn=Turns;await Execute(ctx,p);
            Executions++;Weather=rng!.NextInt(Names.Length);
        }
        Refresh(p);
    }
    public void RestoreUpgrades()
    {
        foreach(var c in upgraded)if(c.IsUpgraded)c.DowngradeInternal();
        upgraded.Clear();
    }
    public async Task Execute(PlayerChoiceContext ctx,Player p)
    {
        var combat=p.Creature.CombatState!;
        var players=combat.Players.Where(x=>x.Creature.IsAlive).ToArray();
        var enemies=combat.Enemies.Where(x=>x.IsAlive).ToArray();
        var all=players.Select(x=>x.Creature).Concat(enemies).ToArray();
        Pulse(Weather);
        switch(Weather)
        {
            case 0:foreach(var c in all)if(c.GetPower<VulnerablePower>()==null)await PowerCmd.Apply<VulnerablePower>(ctx,c,1,null,null);break;
            case 1:
                foreach(var player in players)foreach(var c in PileType.Hand.GetPile(player).Cards.ToArray())
                    if(c.IsUpgradable&&!c.IsUpgraded){upgraded.Add(c);CardCmd.Upgrade(c);}
                break;
            case 2:foreach(var player in players)await CreatureCmd.Heal(player.Creature,2);break;
            case 3:
                for(int hit=0;hit<3&&players.Any(x=>x.Creature.IsAlive);hit++)
                {
                    foreach(var player in players.Where(x=>x.Creature.IsAlive))await CreatureCmd.Damage(ctx,player.Creature,1,ValueProp.Unpowered,null,null);
                    if(!players.Any(x=>x.Creature.IsAlive))break;
                    foreach(var enemy in enemies.Where(x=>x.IsAlive))await CreatureCmd.Damage(ctx,enemy,3,ValueProp.Unpowered,null,null);
                }
                break;
            case 4:foreach(var player in players)if(player.Creature.GetPower<FrailPower>()==null)await PowerCmd.Apply<FrailPower>(ctx,player.Creature,1,null,null);break;
            case 5:foreach(var c in all)if(c.GetPower<WeakPower>()==null)await PowerCmd.Apply<WeakPower>(ctx,c,1,null,null);break;
            case 6:foreach(var c in all)await CreatureCmd.GainBlock(c,6,ValueProp.Unpowered,null);break;
            case 7:
                foreach(var player in players)await CreatureCmd.Damage(ctx,player.Creature,3,ValueProp.Unpowered,null,null);
                if(players.Any(x=>x.Creature.IsAlive)&&enemies.Length>0)
                    await CreatureCmd.Damage(ctx,enemies[rng!.NextInt(enemies.Length)],12,ValueProp.Unpowered,null,null);
                break;
            case 8:foreach(var c in all)await CreatureCmd.LoseBlock(ctx,c,Math.Min(c.Block,6),null);break;
            case 9:foreach(var player in players){await CardPileCmd.Draw(ctx,2,player);await PlayerCmd.GainEnergy(1,player);}break;
        }
        GD.Print($"[CelestialWeather] {Names[Weather]} turn={Turns}");
    }
    void Refresh(Player p)
    {
        var room=NCombatRoom.Instance;if(room==null||!GodotObject.IsInstanceValid(room))return;
        var header=room.GetNodeOrNull<Label>("CelestialWeatherForecast");
        if(header==null)
        {
            header=new Label{Name="CelestialWeatherForecast",Position=new(420,110),Size=new(1080,90),HorizontalAlignment=HorizontalAlignment.Center,MouseFilter=Control.MouseFilterEnum.Pass};
            header.AddThemeFontSizeOverride("font_size",22);header.AddThemeColorOverride("font_color",new("f1dfb0"));header.AddThemeColorOverride("font_shadow_color",Colors.Black);header.AddThemeConstantOverride("shadow_offset_x",2);header.AddThemeConstantOverride("shadow_offset_y",2);room.AddChild(header);
        }
        int next=Turns+(Turns%2==0?2:1);
        header.Text=$"天意难测 · {Names[Weather]} | 第{next}回合抽牌后执行\n{Effects[Weather]}";
        header.TooltipText="独立挑战词条：每2回合执行一次；随后从10种天气等概率随机抽取，允许连续相同。\n天气伤害可以致死。\n"+string.Join("\n",Names.Select((n,i)=>n+"："+Effects[i]));
    }
    static void Pulse(int weather)
    {
        var room=NCombatRoom.Instance;if(room==null)return;
        Color[] colors=[new("ba88d0"),new("83cdeb"),new("d7e9bc"),new("ffac57"),new("91bba4"),new("bddfff"),new("c6ccd2"),new("e3d5ff"),new("cba474"),new("abe9d6")];
        var layer=new Control{MouseFilter=Control.MouseFilterEnum.Ignore,ZIndex=10};room.AddChild(layer);
        for(int i=0;i<24;i++)
        {
            var mark=new ColorRect{Color=new Color(colors[weather],.35f),Size=weather==1?new(3,28):new(34,3),Position=new(180+(i*73)%1560,220+(i*47)%410),MouseFilter=Control.MouseFilterEnum.Ignore};layer.AddChild(mark);
            var tween=mark.CreateTween().SetParallel();tween.TweenProperty(mark,"position",mark.Position+new Vector2(weather==1?-45:150,weather==1?170:15),.7);tween.TweenProperty(mark,"modulate:a",0f,.7);
        }
        var cleanup=layer.CreateTween();cleanup.TweenInterval(.85);cleanup.TweenCallback(Callable.From(layer.QueueFree));
    }
}
