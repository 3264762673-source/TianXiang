using Godot;
namespace TianXiang;

// Continuous weighted 2D skinning. The texture is bound once; animations never swap it.
public static class CharacterRig
{
    public static Node2D Build(bool useBakedAttack=true)
    {
        var root=new Node2D{Name="CharacterRig"};
        var skeleton=new Skeleton2D{Name="Skeleton"};root.AddChild(skeleton);
        Bone2D Bone(Node parent,string name,Vector2 position)
        {var b=new Bone2D{Name=name,Position=position};b.Call("set_autocalculate_length_and_angle",false);b.Call("set_length",30f);b.Rest=b.Transform;parent.AddChild(b);return b;}
        var torso=Bone(skeleton,"Torso",new(0,-180));
        Bone(torso,"Head",new(18,-100));
        Bone(torso,"Arm",new(35,-70));
        Bone(torso,"Instrument",new(-12,-58));
        Bone(torso,"Cape",new(-35,-25));
        Bone(skeleton,"Feet",Vector2.Zero);
        const int cols=32,rows=48;
        var points=new Vector2[(cols+1)*(rows+1)];var uv=new Vector2[points.Length];
        var weights=Enumerable.Range(0,6).Select(_=>new float[points.Length]).ToArray();
        float Smooth(float a,float b,float v){var t=Math.Clamp((v-a)/(b-a),0,1);return t*t*(3-2*t);}
        for(int y=0;y<=rows;y++)for(int x=0;x<=cols;x++)
        {
            int i=y*(cols+1)+x;float px=x*1024f/cols,py=y*1536f/rows;
            uv[i]=new(px,py);points[i]=new((px-512)*.245f,(py-1510)*.245f);
            float head=1-Smooth(300,390,py);
            float arm=Smooth(700,820,px)*Smooth(370,620,py)*(1-Smooth(940,1100,py));
            float instrument=Smooth(400,490,px)*(1-Smooth(660,740,px))*Smooth(410,500,py)*(1-Smooth(650,750,py));
            float cape=(1-Smooth(360,520,px))*Smooth(650,850,py)*(1-Smooth(1250,1430,py));
            float feet=Smooth(1180,1420,py);
            float[] w=[Math.Max(.01f,1-head-arm-instrument-cape-feet),head,arm,instrument,cape,feet];float sum=w.Sum();
            for(int k=0;k<6;k++)weights[k][i]=w[k]/sum;
        }
        var polys=new Godot.Collections.Array<int[]>();
        for(int y=0;y<rows;y++)for(int x=0;x<cols;x++)
        {int i=y*(cols+1)+x;polys.Add([i,i+1,i+cols+2]);polys.Add([i,i+cols+2,i+cols+1]);}
        var skin=new Polygon2D{Name="Skin",Polygon=points,UV=uv,Polygons=(Godot.Collections.Array)polys,Texture=ModEntry.Texture("character-idle"),Skeleton=new NodePath("../Skeleton")};
        root.AddChild(skin);
        string[] paths=["Torso","Torso/Head","Torso/Arm","Torso/Instrument","Torso/Cape","Feet"];
        for(int k=0;k<6;k++)skin.AddBone(new NodePath(paths[k]),weights[k]);
        var player=new AnimationPlayer{Name="AnimationPlayer",RootNode=new NodePath("..")};root.AddChild(player);
        var lib=new AnimationLibrary();player.AddAnimationLibrary("",lib);
        void Clip(string name,double length,double[] times,float[] body,float[] head,float[] arm,float[] instrument,float[] cape,float[] shift,float[] rise,bool loop=false)
        {
            var a=new Animation{Length=(float)length,LoopMode=loop?Animation.LoopModeEnum.Linear:Animation.LoopModeEnum.None};
            void Track(string path,Variant[] values)
            {int t=a.AddTrack(Animation.TrackType.Value);a.TrackSetPath(t,new NodePath(path));a.TrackSetInterpolationType(t,Animation.InterpolationType.Cubic);for(int i=0;i<times.Length;i++)a.TrackInsertKey(t,times[i],values[i]);}
            float[][] angles=[body,head,arm,instrument,cape,new float[times.Length]];
            for(int k=0;k<6;k++)Track("Skeleton/"+paths[k]+":rotation",angles[k].Select(v=>(Variant)v).ToArray());
            Track("Skeleton/Torso:position",shift.Select((v,i)=>(Variant)new Vector2(v,-180+rise[i])).ToArray());
            Track(".:rotation",times.Select((_,i)=>(Variant)(name=="die"?new float[]{0,.12f,-.55f,-1.48f,-1.48f}[i]:0f)).ToArray());
            lib.AddAnimation(name,a);
        }
        Clip("idle",2.4,[0,.6,1.2,1.8,2.4],[0,.008f,0,-.008f,0],[0,-.012f,0,.012f,0],[0,.025f,0,-.025f,0],[0,-.02f,0,.02f,0],[0,.05f,0,-.05f,0],[0,0,0,0,0],[0,-2,-3,-2,0],true);
        Clip("attack",.55,[0,.065,.108,.15,.205,.32,.44,.55],
            [0,-.045f,-.065f,.105f,.12f,.075f,.025f,0],
            [0,.025f,.03f,-.05f,-.055f,-.035f,-.015f,0],
            [0,.18f,.24f,-.83f,-.92f,-.55f,-.16f,0],
            [0,.05f,.075f,.12f,.08f,.03f,-.02f,0],
            [0,.045f,.10f,.02f,-.20f,-.14f,.045f,0],
            [0,-6,-10,29,32,18,5,0],[0,2,4,-3,-2,0,1,0]);
        Clip("cast",.8,[0,.12,.2,.48,.8],[0,-.02f,-.04f,-.04f,0],[0,-.025f,-.06f,-.03f,0],[0,-.14f,-.4f,-.35f,0],[0,-.08f,-.25f,-.2f,0],[0,.04f,.12f,.08f,0],[0,-2,-3,-2,0],[0,1,-5,-3,0]);
        Clip("hurt",.3,[0,.045,.1,.19,.3],[0,-.09f,-.075f,-.025f,0],[0,.06f,.04f,.01f,0],[0,.12f,.08f,.02f,0],[0,.05f,.03f,0,0],[0,.1f,.07f,.02f,0],[0,-13,-10,-3,0],[0,2,1,0,0]);
        Clip("die",1.1,[0,.15,.4,.8,1.1],[0,.13f,.25f,.25f,.25f],[0,.09f,.18f,.18f,.18f],[0,.18f,.4f,.4f,.4f],[0,.1f,.25f,.25f,.25f],[0,.1f,.2f,.2f,.2f],[0,-4,-12,-12,-12],[0,14,30,30,30]);
        if(useBakedAttack && ResourceLoader.Exists("res://TianXiang/attack-sheet.png"))
        {
            var sprite=new Sprite2D{Name="AttackFrames",Texture=ResourceLoader.Load<Texture2D>("res://TianXiang/attack-sheet.png"),
                Hframes=7,Vframes=4,Centered=false,Position=new(-160,-400),Visible=false};
            root.AddChild(sprite);
            foreach(var clipName in lib.GetAnimationList())
            {
                var clip=lib.GetAnimation(clipName);
                void Key(string path,Variant value)
                {
                    int track=clip.AddTrack(Animation.TrackType.Value);
                    clip.TrackSetPath(track,new NodePath(path));
                    clip.ValueTrackSetUpdateMode(track,Animation.UpdateMode.Discrete);
                    clip.TrackInsertKey(track,0,value);
                }
                var attacking=clipName.ToString()=="attack";
                Key("Skin:visible",!attacking);
                Key("AttackFrames:visible",attacking);
                if(!attacking)continue;
                int frames=clip.AddTrack(Animation.TrackType.Value);
                clip.TrackSetPath(frames,new NodePath("AttackFrames:frame"));
                clip.ValueTrackSetUpdateMode(frames,Animation.UpdateMode.Discrete);
                for(int i=0;i<27;i++)clip.TrackInsertKey(frames,i/48.0,i);
            }
        }
        lib.AddAnimation("hit",lib.GetAnimation("hurt"));lib.AddAnimation("relaxed",lib.GetAnimation("idle"));lib.AddAnimation("revive",lib.GetAnimation("idle"));
        player.Autoplay="idle";
        // Method tracks resolve on the live creature even when the game duplicates its visuals.
        foreach(var cue in new[]{"attack","cast","hurt"})
        {
            var clip=lib.GetAnimation(cue);
            var track=clip.AddTrack(Animation.TrackType.Method);
            clip.TrackSetPath(track,new NodePath("AnimationPlayer"));
            clip.TrackInsertKey(track,clip.Length-.001,new Godot.Collections.Dictionary
            {
                ["method"]="play",
                ["args"]=new Godot.Collections.Array { "idle" }
            });
        }
        return root;
    }
}
