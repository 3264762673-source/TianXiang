using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Scaffolding.Content;
namespace TianXiang;
public abstract class SagePower:ModPowerTemplate
{
    public override PowerType Type=>PowerType.Buff;
    public override PowerStackType StackType=>PowerStackType.Counter;
    public override string CustomIconPath=>"res://TianXiang/images/power.png";
    public override string CustomBigIconPath=>CustomIconPath;
}
public sealed class ObservationPower:SagePower
{
    public override PowerStackType StackType=>PowerStackType.Single;
    public override Task AfterDeath(PlayerChoiceContext ctx,Creature creature,bool prevented,float duration)
    {
        if(!prevented&&Owner.Player is {} player)Sky.For(player).Slots.Remove(creature);return Task.CompletedTask;
    }
    public override Task AfterPlayerTurnStart(PlayerChoiceContext ctx,Player player)=>player==Owner.Player?Sky.For(player).StartTurn(ctx):Task.CompletedTask;

}
public sealed class ColdGuardPower:SagePower;
public sealed class WarmGuardPower:SagePower;
public sealed class ObserveGuardPower:SagePower;
public sealed class WarmBurstPower:SagePower;
public sealed class FirstObserveDrawPower:SagePower;
public sealed class FirstObserveEnergyPower:SagePower;
public sealed class FirstObserveGuardPower:SagePower;
