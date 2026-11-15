using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Rooms;
namespace TianXiang;
public static class BattleUi
{
    static Label MakeLabel(string name,int size)
    {
        var label=new Label{Name=name,MouseFilter=Control.MouseFilterEnum.Ignore,HorizontalAlignment=HorizontalAlignment.Center};
        label.AddThemeFontSizeOverride("font_size",size);label.AddThemeColorOverride("font_color",new Color("d7f4ee"));label.AddThemeColorOverride("font_shadow_color",Colors.Black);label.AddThemeConstantOverride("shadow_offset_x",2);label.AddThemeConstantOverride("shadow_offset_y",2);return label;
    }
    public static void Refresh(Sky sky)
    {
        var room=NCombatRoom.Instance;if(room==null||!GodotObject.IsInstanceValid(room))return;
        var toggle=room.GetNodeOrNull<Button>("TianXiangReferenceToggle");
        if(toggle==null)
        {
            var panel=new PanelContainer{Name="TianXiangReference",Position=new(24,282),Size=new(340,300),Visible=false};
            var table=MakeLabel("Table",18);table.HorizontalAlignment=HorizontalAlignment.Left;table.Text="  观天对应表\n  "+Sky.TableText.Replace("\n","\n  ")+"\n  力量、敏捷持续本场战斗。";panel.AddChild(table);room.AddChild(panel);
            toggle=new Button{Name="TianXiangReferenceToggle",Text="观天对应表",Position=new(24,232),Size=new(250,42)};
            toggle.Pressed+=()=>panel.Visible=!panel.Visible;room.AddChild(toggle);
        }
        toggle.Text="观天对应表";
        toggle.TooltipText="查看六种观天效果。候风仪每次观天抽1张牌、获得1能量。";
        foreach(var c in sky.Enemies)
        {
            var node=room.GetCreatureNode(c);if(node==null)continue;
            var label=node.GetNodeOrNull<Label>("TianXiangSlots");
            if(label==null){label=MakeLabel("TianXiangSlots",18);label.Size=new Vector2(340,110);node.AddChild(label);}
            var slot=sky.Slot(c);var anchor=node.Visuals.IntentPosition.GlobalPosition-node.GlobalPosition;
            label.Position=anchor+new Vector2(-170,-132);
            label.Text=$"材料 [ {(slot.Material==""?"空":slot.Material)} ]  阴阳 [ {(slot.Temperature==0?"空":slot.Temperature<0?"阴":"阳")} ]";
            if(slot.Material!="")label.Text+="\n阴 → "+Compact(slot.Material,-1)+"\n阳 → "+Compact(slot.Material,1);
            else if(slot.Temperature!=0)label.Text+="\n水 → "+Compact("水",slot.Temperature)+"\n尘 → "+Compact("尘",slot.Temperature)+"  风 → "+Compact("风",slot.Temperature);
        }
    }
    public static string Compact(string material,int polarity)=>(material,polarity<0) switch
    {("水",true)=>"格挡6 / 虚弱1",("水",false)=>"格挡5 / 力量1",("尘",true)=>"能量2",("尘",false)=>"伤害8 / 易伤1",("风",true)=>"格挡6 / 敏捷1",_=>"抽3"};
    public static void Pulse(Creature c,Color color)
    {
        var node=NCombatRoom.Instance?.GetCreatureNode(c);if(node==null)return;
        var overlay=new ColorRect{Color=new Color(color,.35f),Position=new Vector2(-80,-210),Size=new Vector2(160,200),MouseFilter=Control.MouseFilterEnum.Ignore};
        node.AddChild(overlay);var tween=overlay.CreateTween();tween.TweenProperty(overlay,"modulate:a",0f,.35);tween.TweenCallback(Callable.From(overlay.QueueFree));
    }
}
