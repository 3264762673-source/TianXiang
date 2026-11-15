using System.Reflection;
using System.IO.Compression;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop;

namespace TianXiang;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
    public const string Id="TianXiang";
    internal static readonly Dictionary<string,Texture2D> Textures=[];
    public static string DirectoryPath=>Path.GetDirectoryName(typeof(ModEntry).Assembly.Location)!;
    public static void Initialize()
    {
        var asm=typeof(ModEntry).Assembly;
        ModTypeDiscoveryHub.RegisterModAssembly(Id,asm);
        // Godot accepts ZIP resource packs. All paths are private to this mod.
        var zip=Path.Combine(DirectoryPath,"assets.zip");
        if(!ProjectSettings.LoadResourcePack(zip)) throw new IOException("Could not mount TianXiang/assets.zip");
        using(var archive=ZipFile.OpenRead(zip))
            foreach(var e in archive.Entries.Where(e=>e.FullName.EndsWith(".png")))
            {
                var path="res://"+e.FullName;
                Textures[path]=ResourceLoader.Load<Texture2D>(path)??throw new IOException("Missing texture: "+path);
            }
        var registry=ModContentRegistry.For(Id);
        registry.RegisterCharacter<WeatherCharacter>();
        registry.RegisterRelic<WeatherRelicPool,WeatherCompass>();
        registry.RegisterRelic<WeatherRelicPool,CelestialCompass>();
        registry.RegisterRelic<WeatherRelicPool,JadeDewCup>();
        registry.RegisterRelic<WeatherRelicPool,SunNeedle>();
        registry.RegisterRelic<WeatherRelicPool,ThunderBell>();
        registry.RegisterPotion<WeatherPotionPool,RainVial>();
        registry.RegisterPotion<WeatherPotionPool,DustVial>();
        foreach(var t in asm.GetTypes().Where(t=>!t.IsAbstract&&t.IsSubclassOf(typeof(WeatherCard))))
            registry.RegisterCard(typeof(WeatherCardPool),t);
        foreach(var t in asm.GetTypes().Where(t=>!t.IsAbstract&&t.IsSubclassOf(typeof(ChoiceCard))))
            registry.RegisterCard(typeof(ChoiceCardPool),t);
        foreach(var t in asm.GetTypes().Where(t=>!t.IsAbstract&&t.IsSubclassOf(typeof(SagePower))))registry.RegisterPower(t);
        new Harmony("TianXiang.core").PatchAll(asm);
        GD.Print("[TianXiang] Loaded: v0.6.0: 90 cards, no synthesis. Target 0.111.0.");
    }
    public static Texture2D Texture(string name)=>Textures["res://TianXiang/images/"+name+".png"];
}
