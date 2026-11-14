using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;

namespace KartLab.Editor
{
    public static class BuildDemo
    {
        const string Root="Assets/KartLab/";
        static Transform environment;
        static Material mint,amber,ink,green,cream,cyan,glow;
        static GameObject bolt,trap,ice;
        static Texture2D[] icons;
        static readonly Dictionary<string,Material> palettes=new();
        [MenuItem("Kart Lab/Build Test Scene")]
        public static void Build()
        {
            foreach(var dir in new[]{"Scenes","Prefabs","Materials","Icons"})Directory.CreateDirectory(Root+dir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);palettes.Clear();
            PlayerSettings.companyName="A4 VR";PlayerSettings.productName="APEX Driver Lab";
            PlayerSettings.defaultScreenWidth=1440;PlayerSettings.defaultScreenHeight=900;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            PlayerSettings.SetArchitecture(UnityEditor.Build.NamedBuildTarget.Standalone,1);PlayerSettings.colorSpace=ColorSpace.Linear;PlayerSettings.runInBackground=true;
            var ps=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var inputSetting=ps.FindProperty("activeInputHandler");if(inputSetting!=null){inputSetting.intValue=1;ps.ApplyModifiedPropertiesWithoutUndo();}
            var tags=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);tags.FindProperty("layers").GetArrayElementAtIndex(8).stringValue="Kart";tags.ApplyModifiedPropertiesWithoutUndo();
            Time.fixedDeltaTime=1f/72;QualitySettings.antiAliasing=4;QualitySettings.shadowDistance=100;
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.45f,.58f,.8f);
            RenderSettings.ambientEquatorColor=new Color(.3f,.36f,.42f);RenderSettings.ambientGroundColor=new Color(.16f,.23f,.19f);
            RenderSettings.fog=true;RenderSettings.fogColor=new Color(.64f,.77f,.87f);RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.0025f;
            var sun=new GameObject("Golden hour sunlight").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.1f;sun.color=new Color(1,.92f,.8f);sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(32,-36,0);RenderSettings.sun=sun;
            var sky=LoadMaterial("Sky","Skybox/Procedural");sky.SetColor("_SkyTint",new Color(.5f,.5f,.5f));sky.SetFloat("_AtmosphereThickness",.85f);sky.SetColor("_GroundColor",new Color(.27f,.28f,.3f));sky.SetFloat("_Exposure",1.05f);RenderSettings.skybox=sky;
            mint=Material("Mint",new Color(.12f,.95f,.68f));amber=Material("Amber",new Color(1,.62f,.06f));ink=Material("Ink",new Color(.025f,.055f,.09f));
            green=Material("Shell green",new Color(.12f,.65f,.18f));cream=Material("Shell rim",new Color(1,.94f,.72f));cyan=Material("Ice crystal",new Color(.16f,.85f,1));
            glow=LoadMaterial("SoftGlow","KartLab/SoftGlow");
            MigratePrefabs();BuildItemPrefabs();RenderItemIcons();
            environment=new GameObject("SUNSET CIRCUIT - replace with teammate track").transform;BuildCourse();
            var player=Vehicle("Player Kart",new Vector3(24,.27f,-24),true);player.gameObject.AddComponent<KartRecovery>();SetupView(player);
            PrefabUtility.SaveAsPrefabAsset(player.gameObject,Root+"Prefabs/PlayerKart.prefab");
            var target=Vehicle("Moving target - no race AI",new Vector3(24,.27f,2),false);target.topSpeed=9;
            var driver=target.gameObject.AddComponent<TargetDriver>();driver.motor=target;driver.travel=23;
            var stationary=Vehicle("Ice range target",new Vector3(-26.8f,.27f,8),false);stationary.transform.rotation=Quaternion.Euler(0,180,0);
            for(int n=0;n<5;n++)
            {
                var kind=(ItemKind)(n+1);Vector3 p=n<4?new Vector3(24+(n%2==0?-2:2),.25f,-11+n*10):new Vector3(3,.25f,36);
                Supply(kind,p,true);
            }
            for(int i=0;i<3;i++)Supply(ItemKind.None,new Vector3(-24+(i-1)*2.6f,.25f,-4),false);
            Supply(ItemKind.IceFlower,new Vector3(-24,.25f,25),false);
            var boostZone=Zone("Boost strip",ZoneKind.Boost,new Vector3(-24,.17f,-18),new Vector3(7,.6f,3));
            boostZone.visual=Model("racing-kit","roadStraightArrow",boostZone.transform,new Vector3(0,.01f,0),6).transform;
            for(int n=0;n<3;n++){var arrow=Model("platformer-kit","arrows",boostZone.transform,new Vector3((n-1)*1.6f,.15f,0),1.2f);Tint(arrow,mint);arrow.transform.localRotation=Quaternion.Euler(0,180,0);}
            PrefabUtility.SaveAsPrefabAsset(boostZone.gameObject,Root+"Prefabs/BoostStrip.prefab");
            Zone("Off road test",ZoneKind.Slow,new Vector3(0,.15f,-36),new Vector3(6,.6f,7));
            // Asphalt is 0.01 local high. Match the main road at 0.15m, with 2mm overlap.
            // Use the full 12m tile width so the ramp curbs do not become obstacles inside the lane.
            var bump=Model("racing-kit","roadBump",environment,new Vector3(-24,.098f,17),1);bump.transform.localScale=new Vector3(12,5,5);Collide(bump);
            Sign("JUMP",new Vector3(-31,0,17),90);
            var turret= new GameObject("Ice test emitter").AddComponent<TestTurret>();turret.transform.SetParent(environment);turret.transform.position=new Vector3(-30,.8f,0);turret.target=player;turret.projectilePrefab=ice;turret.range=13;turret.interval=6;
            Model("racing-kit","lightPostModern",environment,new Vector3(-32,0,0),5);Sign("ICE ZONE",new Vector3(-33,0,0),90);
            Checkpoint(new Vector3(24,.27f,-24),0);Checkpoint(new Vector3(24,.27f,18),0);Checkpoint(new Vector3(-24,.27f,24),180);Checkpoint(new Vector3(-24,.27f,-24),180);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Root+"Scenes/DriverLab.unity");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Root+"Scenes/DriverLab.unity",true)};
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();Selection.activeGameObject=player.gameObject;Debug.Log("KARTLAB: polished scene and prefabs built");
        }
        static void MigratePrefabs()
        {
            foreach(var pair in new[]{("TurboBox","MushroomBox"),("ShieldBox","StarBox"),("BoltBox","GreenShellBox"),("TrapBox","BananaBox"),("EnergyBolt","GreenShell"),("GlueTrap","BananaTrap")})
            {string a=Root+"Prefabs/"+pair.Item1+".prefab",b=Root+"Prefabs/"+pair.Item2+".prefab";if(File.Exists(a)&&!File.Exists(b))AssetDatabase.MoveAsset(a,b);}
        }
        static void BuildCourse()
        {
            var route=environment.gameObject.AddComponent<TrackRoute>();
            var points=new List<Vector3>();
            // Four six-metre arcs joined by straight centre lines, in driving order.
            foreach(var corner in new[]{new Vector3(18,0,30),new Vector3(-18,90,30),new Vector3(-18,180,-30),new Vector3(18,270,-30)})
                for(int n=0;n<=18;n++)
                {float angle=(corner.y+n*5)*Mathf.Deg2Rad;points.Add(new Vector3(corner.x+6*Mathf.Cos(angle),.18f,corner.z+6*Mathf.Sin(angle)));}
            route.points=points.ToArray();route.roadHalfWidth=4.5f;
            var turf=Material("Turf",new Color(.12f,.43f,.27f));
            for(int x=-6;x<=6;x++)for(int z=-6;z<=6;z++)
            {var g=Model("racing-kit","grass",environment,new Vector3(x*12,0,z*12),12);Tint(g,turf);Collide(g);foreach(var c in g.GetComponentsInChildren<Collider>())c.gameObject.AddComponent<GroundSurface>();}
            foreach(int x in new[]{-24,24})for(int z=-24;z<=24;z+=12)Road("roadStraight",new Vector3(x,.03f,z),0);
            foreach(int z in new[]{-36,36})for(int x=-12;x<=12;x+=12)Road("roadStraight",new Vector3(x,.03f,z),90);
            Road("roadCornerSmall",new Vector3(24,.03f,36),90);Road("roadCornerSmall",new Vector3(-24,.03f,36),0);Road("roadCornerSmall",new Vector3(-24,.03f,-36),270);Road("roadCornerSmall",new Vector3(24,.03f,-36),180);
            for(int n=0;n<22;n++)
            {
                float z=-31.5f+n*3;
                foreach(float x in new[]{-30.5f,30.5f})
                {var b=Model("racing-kit",n%2==0?"barrierRed":"barrierWhite",environment,new Vector3(x,.16f,z),7);b.transform.localRotation=Quaternion.Euler(0,90,0);Collide(b);}
            }
            // Roadside objects, trees and rocks provide near/far motion cues.
            for(int n=0;n<14;n++)
            {
                float z=-48+n*7.5f;
                foreach(float x in new[]{-42f,42f})
                {var tree=Model("platformer-kit",n%3==0?"tree-pine":"tree",environment,new Vector3(x+(n%2)*4,0,z),4.5f+n%3);tree.transform.localRotation=Quaternion.Euler(0,n*37,0);}
            }
            for(int n=0;n<7;n++)
            {
                Model("platformer-kit","rocks",environment,new Vector3(n%2==0?-63:63,0,-45+n*15),14+n%3*4);
                var flowers=Model("platformer-kit","flowers-tall",environment,new Vector3(n%2==0?14:-14,0,-25+n*8),3);
                Model("platformer-kit","plant",environment,new Vector3(n%2==0?10:-10,0,-25+n*8),4);
            }
            for(int n=0;n<5;n++)Model("platformer-kit","tree",environment,new Vector3(-9+n*4,0,10+n%2*5),4);
            Model("racing-kit","grandStand",environment,new Vector3(0,0,52),9);
            Model("racing-kit","grandStand",environment,new Vector3(-14,0,52),7);
            Model("racing-kit","pitsGarage",environment,new Vector3(0,0,-51),9);
            Model("racing-kit","tentRoofDouble",environment,new Vector3(15,0,-51),8);
            var gantry=Model("racing-kit","overheadRoundColored",environment,new Vector3(24,.15f,-26),11);
            Label(environment,"APEX  KART CLUB",new Vector3(24,5,-26.15f),.055f,Color.white);
            Model("racing-kit","flagCheckers",environment,new Vector3(31,.15f,-27),7);
            Sign("DRIFT  <<",new Vector3(30,0,29),0);Sign("<<  DRIFT",new Vector3(-30,0,-29),180);
            Sign("SUNSET CIRCUIT",new Vector3(0,0,-6),0);
            Zone("Out of bounds catcher",ZoneKind.OutOfBounds,new Vector3(0,-4,0),new Vector3(400,1,400));
        }
        static void Sign(string text,Vector3 p,float yaw)
        {
            var sign=Model("racing-kit","billboardLow",environment,p,6);sign.transform.localRotation=Quaternion.Euler(0,yaw,0);
            Label(sign.transform,text,new Vector3(0,.31f,-.035f),.007f,Color.white);
        }
        static void Road(string name,Vector3 p,float angle){var road=Model("racing-kit",name,environment,p,12);road.transform.localRotation=Quaternion.Euler(0,angle,0);Collide(road);}
        static void Supply(ItemKind kind,Vector3 p,bool save)
        {
            var zone=Zone(KartInventory.Name(kind)+" supply",ZoneKind.ItemBox,p,new Vector3(2,2.3f,2));zone.fixedItem=kind;
            var visual=new GameObject("Rotating item box").transform;visual.SetParent(zone.transform,false);visual.localPosition=Vector3.up*.7f;zone.visual=visual;
            var box=Model("platformer-kit","crate-item",visual,Vector3.zero,1.7f);
            var boxMaterial=Material("Item box",new Color(.17f,.65f,1));boxMaterial.SetFloat("_Glossiness",.7f);boxMaterial.EnableKeyword("_EMISSION");boxMaterial.SetColor("_EmissionColor",new Color(.015f,.1f,.3f));Tint(box,boxMaterial);
            for(int n=0;n<4;n++)
            {var face=new GameObject("Question face").transform;face.SetParent(visual,false);face.localRotation=Quaternion.Euler(0,n*90,0);Label(face,"?",new Vector3(0,.45f,-.438f),.1f,Color.white);}
            if(kind!=ItemKind.None)
            {
                var art=ItemArt(kind,zone.transform);art.transform.localPosition=new Vector3(0,2.05f,0);art.transform.localScale*=.55f;
                Label(zone.transform,KartInventory.Name(kind),new Vector3(0,2.85f,0),.026f,Color.white);
            }
            if(save)PrefabUtility.SaveAsPrefabAsset(zone.gameObject,Root+"Prefabs/"+kind+"Box.prefab");
        }
        static GameObject ItemArt(ItemKind kind,Transform parent)
        {
            var root=new GameObject(kind+" art");root.transform.SetParent(parent,false);
            switch(kind)
            {
                case ItemKind.Mushroom:
                    var mushroom=Model("food-kit","mushroom",root.transform,Vector3.zero,6);
                    Tint(mushroom,LoadMaterial("Dash mushroom","KartLab/Mushroom"));break;
                case ItemKind.Star:
                    var star=Model("platformer-kit","star",root.transform,Vector3.zero,2.3f);Tint(star,amber);break;
                case ItemKind.GreenShell:
                    var dome=Model("platformer-kit","button-round",root.transform,new Vector3(0,.08f,0),1.5f);dome.transform.localScale=new Vector3(1.5f,2,1.5f);Tint(dome,green);
                    var rim=Model("car-kit","debris-tire",root.transform,Vector3.zero,1.5f);rim.transform.localRotation=Quaternion.Euler(0,0,90);rim.transform.localScale=new Vector3(.45f,1.6f,1.6f);rim.transform.localPosition=new Vector3(.48f,.08f,0);Tint(rim,cream);break;
                case ItemKind.Banana:
                    for(int n=0;n<3;n++){var peel=Model("food-kit","banana",root.transform,Vector3.zero,1.1f);peel.transform.localRotation=Quaternion.Euler(0,n*120,15);}
                    break;
                case ItemKind.IceFlower:
                    var flower=Model("platformer-kit","flowers-tall",root.transform,Vector3.zero,1.2f);Tint(flower,cyan);
                    var gem=Model("platformer-kit","jewel",root.transform,new Vector3(0,.3f,0),1.2f);Tint(gem,cream);break;
            }
            return root;
        }
        static void BuildItemPrefabs()
        {
            foreach(bool frozen in new[]{false,true})
            {
                var go=new GameObject(frozen?"Ice shot":"Green shell");var art=ItemArt(frozen?ItemKind.IceFlower:ItemKind.GreenShell,go.transform);art.transform.localScale=Vector3.one*(frozen?.5f:.7f);
                var projectile=go.AddComponent<KartProjectile>();projectile.ice=frozen;projectile.spinningArt=art.transform;projectile.maxBounces=frozen?0:3;
                var trail=go.AddComponent<TrailRenderer>();trail.time=.16f;trail.startWidth=.18f;trail.endWidth=0;trail.sharedMaterial=glow;trail.startColor=frozen?Color.cyan:new Color(.3f,1,.2f);trail.endColor=Color.clear;
                var prefab=PrefabUtility.SaveAsPrefabAsset(go,Root+"Prefabs/"+(frozen?"IceShot":"GreenShell")+".prefab");if(frozen)ice=prefab;else bolt=prefab;UnityEngine.Object.DestroyImmediate(go);
            }
            var trapObject=new GameObject("Banana peel");ItemArt(ItemKind.Banana,trapObject.transform);
            var c=trapObject.AddComponent<BoxCollider>();c.isTrigger=true;c.size=new Vector3(1.4f,.8f,1.4f);c.center=Vector3.up*.3f;
            trapObject.AddComponent<KartTrap>();trap=PrefabUtility.SaveAsPrefabAsset(trapObject,Root+"Prefabs/BananaTrap.prefab");UnityEngine.Object.DestroyImmediate(trapObject);
        }
        static void RenderItemIcons()
        {
            icons=new Texture2D[6];
            for(int n=1;n<=5;n++)
            {
                var art=ItemArt((ItemKind)n,null);art.transform.position=new Vector3(1000,0,1000);
                foreach(var t in art.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
                var renderers=art.GetComponentsInChildren<Renderer>();var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);
                var camera=new GameObject("Icon camera").AddComponent<Camera>();camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.orthographic=true;camera.orthographicSize=Mathf.Max(b.size.x,b.size.y,b.size.z)*.8f;
                camera.transform.position=b.center+new Vector3(1,.75f,-1.8f)*2;camera.transform.LookAt(b.center);
                var rt=new RenderTexture(256,256,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                var texture=new Texture2D(256,256,TextureFormat.RGBA32,false);texture.ReadPixels(new Rect(0,0,256,256),0,0);texture.Apply();string path=Root+"Icons/"+(ItemKind)n+".png";File.WriteAllBytes(path,texture.EncodeToPNG());
                RenderTexture.active=null;camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(art);
                AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.SaveAndReimport();icons[n]=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
        }
        static KartMotor Vehicle(string name,Vector3 p,bool player)
        {
            var go=new GameObject(name);go.layer=8;go.transform.position=p;go.AddComponent<Rigidbody>();
            var collider=go.AddComponent<BoxCollider>();collider.center=new Vector3(0,.35f,0);collider.size=new Vector3(1.15f,.65f,1.65f);
            var i=go.AddComponent<KartInput>();i.useExternalInput=!player;var motor=go.AddComponent<KartMotor>();motor.input=i;
            var visual=new GameObject("Vehicle art").transform;visual.SetParent(go.transform,false);motor.visual=visual;
            if(player)CompleteKart(visual);else Model("car-kit","kart-ooli",visual,Vector3.zero,1.4f);
            if(!player)Label(visual,"12",new Vector3(0,.6f,-.95f),.035f,Color.white);
            var inventory=go.AddComponent<KartInventory>();inventory.boltPrefab=bolt;inventory.trapPrefab=trap;inventory.icePrefab=ice;inventory.icons=icons;
            var skills=go.AddComponent<KartRaceSkills>();skills.motor=motor;
            const string hornPath="Assets/ThirdParty/horn-audio/horn-loop.wav";
            var hornImporter=AssetImporter.GetAtPath(hornPath) as AudioImporter;
            if(hornImporter)
            {
                var settings=hornImporter.defaultSampleSettings;
                if(!settings.preloadAudioData||settings.compressionFormat!=AudioCompressionFormat.PCM)
                {settings.preloadAudioData=true;settings.compressionFormat=AudioCompressionFormat.PCM;hornImporter.defaultSampleSettings=settings;hornImporter.SaveAndReimport();}
            }
            go.AddComponent<KartHorn>().clip=AssetDatabase.LoadAssetAtPath<AudioClip>(hornPath);
            var effects=go.AddComponent<KartEffects>();effects.motor=motor;effects.glowMaterial=glow;effects.orbitStars=new Transform[4];
            effects.idleLoop=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ThirdParty/racing-audio/idle.wav");effects.loadLoop=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ThirdParty/racing-audio/load.wav");effects.highLoop=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ThirdParty/racing-audio/high.wav");effects.skidLoop=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ThirdParty/racing-audio/skid.wav");
            for(int n=0;n<4;n++){var star=ItemArt(ItemKind.Star,go.transform);star.transform.localScale=Vector3.one*.28f;star.SetActive(false);effects.orbitStars[n]=star.transform;}
            return motor;
        }
        static void SetupView(KartMotor player)
        {
            var camera=new GameObject("Driver Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.transform.SetParent(player.transform,false);camera.transform.localPosition=new Vector3(0,1.35f,-.12f);camera.transform.localRotation=Quaternion.identity;camera.clearFlags=CameraClearFlags.Skybox;camera.fieldOfView=65;camera.farClipPlane=300;camera.gameObject.AddComponent<AudioListener>();
            var view=player.gameObject.AddComponent<KartView>();view.motor=player;view.view=camera;view.thirdPerson=false;view.fadeShader=Shader.Find("Unlit/Color");view.vignetteShader=Shader.Find("Unlit/Transparent");view.exterior=player.visual.gameObject;view.seat=new Vector3(0,1.18f,-.40f);
            view.trackingSpace=new GameObject("XR tracking space").transform;view.trackingSpace.SetParent(player.transform,false);
            var cockpit=new GameObject("Cockpit dashboard");cockpit.transform.SetParent(player.transform,false);view.cockpit=cockpit;
            var presentation=player.gameObject.AddComponent<KartCockpit>();presentation.motor=player;presentation.view=view;
            var body=CompleteKart(cockpit.transform);
            var dash=new GameObject("Cockpit structure").transform;dash.SetParent(cockpit.transform,false);presentation.dashboard=dash;
            var wheel=new GameObject("Virtual wheel").transform;wheel.SetParent(cockpit.transform,false);wheel.localPosition=presentation.wheelPosition;view.wheel=wheel;presentation.wheel=wheel;
            var wheelArt=Imported("sports-steering-wheel",wheel,Vector3.zero,1.05f,true);wheelArt.transform.localRotation=Quaternion.Euler(0,180,0);
            var cluster=Imported("instrument-cluster",dash,new Vector3(0,.85f,.60f),.78f,true);cluster.transform.localRotation=Quaternion.Euler(45,0,0)*Quaternion.Euler(0,180,0);
            Label(wheel,"APEX",new Vector3(0,-.045f,-.072f),.0025f,new Color(.95f,.95f,.9f));
            // Screen mounted in the instrument binnacle, below the line of sight.
            var canvas=new GameObject("Instrument display").AddComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.transform.SetParent(dash,false);canvas.transform.localPosition=new Vector3(0,.89f,.56f);canvas.transform.localRotation=Quaternion.Euler(45,0,0);canvas.transform.localScale=Vector3.one*.00052f;
            var rect=canvas.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(640,160);var uiMaterial=LoadMaterial("DashboardUI","UI/Default");
            var panel=new GameObject("Screen glass").AddComponent<Image>();panel.transform.SetParent(canvas.transform,false);panel.color=new Color(.008f,.018f,.027f,1);panel.rectTransform.sizeDelta=rect.sizeDelta;panel.material=uiMaterial;
            Text Readout(string name,Vector2 pos,Vector2 size,int fontSize)
            {var t=new GameObject(name).AddComponent<Text>();t.transform.SetParent(canvas.transform,false);t.rectTransform.anchoredPosition=pos;t.rectTransform.sizeDelta=size;t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=fontSize;t.alignment=TextAnchor.MiddleCenter;t.color=new Color(.75f,1,.9f);t.material=uiMaterial;return t;}
            presentation.speedText=Readout("Digital speed",new Vector2(-130,3),new Vector2(260,90),30);
            presentation.statusText=Readout("Drive mode",new Vector2(145,20),new Vector2(280,38),21);
            presentation.itemText=Readout("Item ready",new Vector2(145,-20),new Vector2(280,38),19);
            presentation.revLights=new Image[10];for(int n=0;n<10;n++){var led=new GameObject("RPM light "+n).AddComponent<Image>();led.transform.SetParent(canvas.transform,false);led.rectTransform.anchoredPosition=new Vector2(-243+n*54,60);led.rectTransform.sizeDelta=new Vector2(40,9);led.material=uiMaterial;presentation.revLights[n]=led;}
            var text=Readout("XR status",new Vector2(0,-65),new Vector2(620,24),15);
            var mount=new GameObject("Held item mount").transform;mount.SetParent(cockpit.transform,false);mount.localPosition=new Vector3(.31f,.58f,.68f);presentation.heldMount=mount;presentation.itemModels=new GameObject[6];
            for(int n=1;n<6;n++){var art=ItemArt((ItemKind)n,mount);art.transform.localScale=Vector3.one*.18f;art.SetActive(false);presentation.itemModels[n]=art;}
            // Both views use the complete downloaded chassis. No virtual hands.
            Transform MountColumn(GameObject vehicleArt,Transform parent)
            {
                var mesh=vehicleArt.GetComponentsInChildren<Transform>().First(t=>t.name=="steering-column");
                var pivot=new GameObject("Adjustable steering column").transform;pivot.SetParent(parent,false);
                pivot.localPosition=new Vector3(0,.554f,.531f);
                pivot.localRotation=Quaternion.LookRotation(presentation.wheelPosition-pivot.localPosition);
                mesh.SetParent(pivot,true);return pivot;
            }
            presentation.column=MountColumn(body,cockpit.transform);
            presentation.exteriorColumn=MountColumn(player.visual.gameObject,player.visual);
            presentation.columnLength=Vector3.Distance(presentation.wheelPosition,presentation.column.localPosition);
            // The external model uses the same wheel at the same mounting point.
            var outerWheel=UnityEngine.Object.Instantiate(wheel.gameObject,player.visual).transform;
            outerWheel.name="Exterior sports wheel";presentation.exteriorWheel=outerWheel;
            UnityEngine.Object.Instantiate(cluster,player.visual);
            var pivots=new List<Transform>();var meshes=new List<Transform>();
            foreach(var art in new[]{body,player.visual.gameObject})
                foreach(var t in art.GetComponentsInChildren<Transform>().Where(x=>x.name.StartsWith("wheel-")).OrderBy(x=>x.name.Contains("front")?0:1).ToArray())
                {var renderer=t.GetComponent<Renderer>();if(!renderer)continue;var pivot=new GameObject("Tyre steering pivot").transform;pivot.SetParent(t.parent,false);pivot.position=renderer.bounds.center;t.SetParent(pivot,true);var rolling=new GameObject("Tyre rolling pivot").transform;rolling.SetParent(pivot,false);t.SetParent(rolling,true);pivots.Add(pivot);meshes.Add(rolling);}
            presentation.wheelPivots=pivots.ToArray();presentation.wheelMeshes=meshes.ToArray();

            var hud=player.gameObject.AddComponent<KartHUD>();hud.motor=player;hud.kartView=view;hud.dashText=text;hud.audioSource=player.gameObject.AddComponent<AudioSource>();
            hud.pickupSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ThirdParty/interface-sounds/Audio/select_001.ogg");hud.hitSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ThirdParty/racing-audio/crash.ogg");hud.boostSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ThirdParty/interface-sounds/Audio/maximize_001.ogg");
        }
        static GameObject CompleteKart(Transform parent)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/keith-racing-kart/complete-kart.obj");
            if(!asset)throw new Exception("Missing Keith CC0 kart: run Tools/prepare_complete_kart.py");
            var art=(GameObject)PrefabUtility.InstantiatePrefab(asset);art.name="Complete racing kart - Keith CC0";
            PrefabUtility.UnpackPrefabInstance(art,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            art.transform.SetParent(parent,false);
            foreach(var renderer in art.GetComponentsInChildren<Renderer>())
            {
                var materials=renderer.sharedMaterials;
                for(int n=0;n<materials.Length;n++)
                {
                    string source=materials[n]?materials[n].name:"Grey_-_40";
                    bool paint=source.Contains("Blue"),metal=source.Contains("80")||source.Contains("60");
                    Color color=paint?new Color(.045f,.32f,.62f):metal?new Color(.42f,.48f,.53f):new Color(.045f,.055f,.065f);
                    var material=Material("Keith kart "+source,color);
                    material.SetFloat("_Metallic",paint?.35f:metal?.75f:0);
                    material.SetFloat("_Glossiness",paint?.65f:metal?.42f:.18f);materials[n]=material;
                }
                renderer.sharedMaterials=materials;
            }
            return art;
        }
        static GameObject Imported(string name,Transform parent,Vector3 pos,float scale,bool centered)
        {
            string folder="Assets/ThirdParty/grab3d/"+name+"/";
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(folder+name+".obj");if(!asset)throw new Exception("Missing model "+name);
            var root=new GameObject(name+" mount");root.transform.SetParent(parent,false);root.transform.localPosition=pos;root.transform.localScale=Vector3.one*scale;
            var art=(GameObject)PrefabUtility.InstantiatePrefab(asset);art.transform.SetParent(root.transform,false);
            var rs=art.GetComponentsInChildren<Renderer>();var bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);
            if(centered)art.transform.localPosition=-root.transform.InverseTransformPoint(bounds.center);
            foreach(var r in rs)
            {
                string key=name=="sports-steering-wheel"?name+"_steering_rim":name+"_body";
                var mat=LoadMaterial(key,"Standard");mat.color=Color.white;mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(folder+key+"_basecolor.jpg");mat.SetFloat("_Glossiness",.35f);mat.SetFloat("_Metallic",.15f);
                string normalPath=folder+key+"_normal.jpg";
                var importer=AssetImporter.GetAtPath(normalPath) as TextureImporter;
                if(importer){if(importer.textureType!=TextureImporterType.NormalMap){importer.textureType=TextureImporterType.NormalMap;importer.SaveAndReimport();}mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));mat.SetFloat("_BumpScale",.45f);mat.EnableKeyword("_NORMALMAP");}
                var metallic=AssetDatabase.LoadAssetAtPath<Texture2D>(folder+key+"_metallic-"+key+"_roughness_metallic.jpg");
                if(metallic){mat.SetTexture("_MetallicGlossMap",metallic);mat.SetFloat("_GlossMapScale",.4f);mat.EnableKeyword("_METALLICGLOSSMAP");}
                if(r.name.Contains("glow")){mat=LoadMaterial(r.name,"Unlit/Color");mat.color=new Color(.15f,.8f,.7f);}
                r.sharedMaterials=Enumerable.Repeat(mat,r.sharedMaterials.Length).ToArray();
            }
            return root;
        }
        static TrackZone Zone(string name,ZoneKind kind,Vector3 p,Vector3 size)
        {var go=new GameObject(name);go.transform.SetParent(environment);go.transform.position=p;var c=go.AddComponent<BoxCollider>();c.isTrigger=true;c.size=size;c.center=Vector3.up*size.y*.5f;var zone=go.AddComponent<TrackZone>();zone.kind=kind;return zone;}
        static void Checkpoint(Vector3 p,float yaw)
        {var zone=Zone("Safe checkpoint",ZoneKind.Checkpoint,p,new Vector3(8,2,1));var spawn=new GameObject("Safe pose").transform;spawn.SetParent(zone.transform,false);spawn.rotation=Quaternion.Euler(0,yaw,0);zone.spawn=spawn;PrefabUtility.SaveAsPrefabAsset(zone.gameObject,Root+"Prefabs/Checkpoint.prefab");}
        static GameObject Model(string pack,string name,Transform parent,Vector3 p,float scale)
        {
            string path=$"Assets/ThirdParty/{pack}/Models/OBJ format/{name}.obj";var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(!asset)throw new Exception("Missing downloaded model: "+path);
            var root=new GameObject(name);var model=(GameObject)PrefabUtility.InstantiatePrefab(asset);model.transform.SetParent(root.transform,false);
            var renderers=model.GetComponentsInChildren<Renderer>();var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);model.transform.localPosition=new Vector3(-b.center.x,-b.min.y,-b.center.z);
            root.transform.SetParent(parent,false);root.transform.localPosition=p;root.transform.localScale=Vector3.one*scale;
            if(pack!="racing-kit")
            {
                if(!palettes.TryGetValue(pack,out var material))
                {
                    material=LoadMaterial(pack+" palette","Standard");material.color=Color.white;material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/ThirdParty/{pack}/Models/OBJ format/Textures/colormap.png");material.SetFloat("_Glossiness",pack=="car-kit"?.4f:.2f);material.SetFloat("_Metallic",pack=="car-kit"?.12f:0);material.EnableKeyword("_EMISSION");palettes[pack]=material;
                }
                Tint(root,material);
            }
            return root;
        }
        static void Collide(GameObject go){foreach(var m in go.GetComponentsInChildren<MeshFilter>()){var c=m.gameObject.AddComponent<MeshCollider>();c.sharedMesh=m.sharedMesh;}}
        static void Tint(GameObject go,Material material){foreach(var r in go.GetComponentsInChildren<Renderer>())r.sharedMaterials=Enumerable.Repeat(material,r.sharedMaterials.Length).ToArray();}
        static Material LoadMaterial(string name,string shader)
        {string path=Root+"Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){var s=Shader.Find(shader);if(!s)throw new Exception("Missing shader "+shader);m=new Material(s);AssetDatabase.CreateAsset(m,path);}return m;}
        static Material Material(string name,Color color){var m=LoadMaterial(name,"Standard");m.color=color;m.SetFloat("_Glossiness",.35f);return m;}
        static void Label(Transform parent,string text,Vector3 p,float size,Color color,float yaw=0)
        {var label=new GameObject(text).AddComponent<TextMesh>();label.transform.SetParent(parent,false);label.transform.localPosition=p;label.transform.localRotation=Quaternion.Euler(0,yaw,0);label.text=text;label.characterSize=size;label.fontSize=64;label.anchor=TextAnchor.MiddleCenter;label.color=color;}
        public static void BuildMac(){SyncCredits();var report=BuildPipeline.BuildPlayer(EditorBuildSettings.scenes,"Builds/APEX Driver Lab.app",BuildTarget.StandaloneOSX,BuildOptions.None);if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Mac build failed");}
        public static void Deliver(){Build();ExportModule();BuildMac();}
        static void SyncCredits()
        {Directory.CreateDirectory("Assets/StreamingAssets");File.Copy("THIRD-PARTY-NOTICES.txt","Assets/StreamingAssets/KartLab-Credits.txt",true);AssetDatabase.Refresh();}
        public static void ExportModule()
        {
            SyncCredits();Directory.CreateDirectory("Deliverables");Directory.CreateDirectory(Root+"Documentation");
            foreach(var doc in new[]{"README.md","DESIGN.md","INTEGRATION.md","DESIGN-REFERENCES.md"})if(File.Exists(doc))File.Copy(doc,Root+"Documentation/"+doc,true);
            File.Copy("Evidence/asset-sources.json",Root+"Documentation/asset-sources.json",true);AssetDatabase.Refresh();
            var paths=new List<string>{Root+"Runtime",Root+"Prefabs",Root+"Materials",Root+"Icons",Root+"Shaders",Root+"Documentation","Assets/StreamingAssets/KartLab-Credits.txt"};
            paths.AddRange(Directory.GetFiles("Assets/ThirdParty","*",SearchOption.AllDirectories).Where(p=>Path.GetFileName(p).Equals("License.txt",StringComparison.OrdinalIgnoreCase)));
            AssetDatabase.ExportPackage(paths.ToArray(),"Deliverables/KartLab-Module.unitypackage",ExportPackageOptions.Recurse|ExportPackageOptions.IncludeDependencies);
        }
    }
}
