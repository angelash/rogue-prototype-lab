// Original interoperability tooling. Reads authorized local metadata; contains no source game data.
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using Newtonsoft.Json.Linq;
using U = UnityEngine.Object;

public static class RecoveryEditor
{
    static JObject manifest;
    static Dictionary<string, JObject> source;
    static Dictionary<string, U> restored;
    const string ScenePath = "Assets/Recovery/Scenes/NightMarket.unity";
    static string Id(JToken o) => (string)o["id"];
    static int I(JToken o, string key, int fallback=0) => (int?)o?[key] ?? fallback;
    static float F(JToken o, string key, float fallback=0) => (float?)o?[key] ?? fallback;
    static bool B(JToken o, string key, bool fallback=false) => (bool?)o?[key] ?? fallback;
    static Vector2 V2(JToken t) => t==null ? Vector2.zero : new Vector2(F(t,"x"),F(t,"y"));
    static Vector3 V3(JToken t) => t==null ? Vector3.zero : new Vector3(F(t,"x"),F(t,"y"),F(t,"z"));
    static Color C(JToken t)
    {
        if(t==null) return Color.white;
        if(t["rgba"] != null) { uint c=(uint)t["rgba"];return new Color32((byte)c,(byte)(c>>8),(byte)(c>>16),(byte)(c>>24)); }
        return new Color(F(t,"r"),F(t,"g"),F(t,"b"),F(t,"a",1));
    }
    static string Resolve(string from, JToken p)
    {
        if(p==null || I(p,"m_PathID")==0) return null;
        string container=from.Split(':')[0]; int file=I(p,"m_FileID");
        if(file!=0)
        {
            var entry=manifest["containers"].First(t=>(string)t["name"]==container);
            string path=(string)entry["externals"][file-1]["path"];
            container=path.Split('/').Last();
        }
        return container+":"+I(p,"m_PathID");
    }
    static T Ref<T>(string id, JToken p) where T:U
    {
        string target=Resolve(id,p); return target!=null && restored.ContainsKey(target) ? restored[target] as T : null;
    }
    static U Persist(string id, U obj, string resourceKey=null)
    {
        string file=(resourceKey??id.Replace(':','_')).Replace('/','_')+".asset";
        string folder=resourceKey==null?"Assets/Recovery/Generated":"Assets/Resources";
        Directory.CreateDirectory(folder);
        string path=folder+"/"+file;
        var old=AssetDatabase.LoadAssetAtPath<U>(path);
        if(old) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(obj,path); return restored[id]=obj;
    }
    static void Load()
    {
        manifest=JObject.Parse(File.ReadAllText("Assets/Editor/RecoverySource/manifest.json"));
        source=manifest["objects"].ToDictionary(Id,t=>(JObject)t);
        restored=new Dictionary<string,U>();
    }
    static string ResourceKey(string id)
    {
        foreach(var root in ((JObject)manifest["resourceMap"]).Properties())
            foreach(var pair in root.Value["m_Container"] ?? new JArray())
                if(Resolve(root.Name,pair[1])==id) return (string)pair[0];
        return null;
    }
    static void ImportAssets()
    {
        foreach(var row in source.Values.Where(t=>(string)t["type"]=="MonoScript"))
        {
            string className=(string)row["tree"]["m_ClassName"];
            var script=AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Recovery/Scripts/"+className+".cs");
            if(script)MonoImporter.SetExecutionOrder(script,I(row["tree"],"m_ExecutionOrder"));
        }
        foreach(var row in source.Values.Where(t=>t["assetPath"]!=null))
        {
            string path=(string)row["assetPath"];string type=(string)row["type"];var tree=row["tree"];
            var importer=AssetImporter.GetAtPath(path);
            if(importer is TextureImporter ti)
            {
                ti.textureType=TextureImporterType.Default;ti.isReadable=true;ti.npotScale=TextureImporterNPOTScale.None;
                ti.maxTextureSize=8192;ti.textureCompression=TextureImporterCompression.Uncompressed;
                ti.mipmapEnabled=B(tree,"m_MipMap");ti.filterMode=(FilterMode)I(tree["m_TextureSettings"],"m_FilterMode",1);
                ti.wrapMode=(TextureWrapMode)I(tree["m_TextureSettings"],"m_WrapMode",1);
                ti.alphaIsTransparency=false;ti.SaveAndReimport();
            }
            if(importer is TrueTypeFontImporter fi)
            {fi.fontSize=(int)F(tree,"m_FontSize",16);fi.fontTextureCase=FontTextureCase.Dynamic;fi.SaveAndReimport();}
            if(importer is AudioImporter ai)
            {var settings=ai.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;ai.defaultSampleSettings=settings;ai.SaveAndReimport();}
            U obj=type=="Mesh"?AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().FirstOrDefault():AssetDatabase.LoadAssetAtPath<U>(path);
            if(obj==null) throw new Exception("Asset import missing: "+path);
            restored[Id(row)]=obj;
        }
        foreach(var row in source.Values.Where(t=>(string)t["type"]=="Sprite"))
        {
            string id=Id(row);var tree=row["tree"];var rd=tree["m_RD"];
            var tex=Ref<Texture2D>(id,rd["texture"]);if(!tex)throw new Exception("Sprite texture missing: "+id);
            var r=tree["m_Rect"];float width=F(r,"width"),height=F(r,"height");
            var offset=V2(tree["m_Offset"]);
            var sprite=Sprite.Create(tex,new Rect(F(r,"x"),F(r,"y"),width,height),new Vector2(.5f+offset.x/width,.5f+offset.y/height),F(tree,"m_PixelsToUnits",100),0,SpriteMeshType.FullRect);
            sprite.name=(string)tree["m_Name"];
            var vertices=rd["vertices"].Select(t=>V2(t["pos"])).ToArray();
            var triangles=rd["indices"].Select(t=>(ushort)t).ToArray();
            sprite.OverrideGeometry(vertices,triangles);
            Persist(id,sprite,ResourceKey(id));
            RepairSpriteSerialization(id,sprite,vertices,triangles);
        }
        foreach(var row in source.Values.Where(t=>(string)t["type"]=="Material"))
        {
            string id=Id(row);var tree=row["tree"];var name=(string)tree["m_Name"];
            string shaderId=Resolve(id,tree["m_Shader"]),shaderName=name.Contains("Font")?"GUI/Text Shader":"Sprites/Default";
            if(shaderId!=null && source.ContainsKey(shaderId)){var match=Regex.Match((string)source[shaderId]["tree"]["m_Script"],"Shader\\s+\"([^\"]+)\"");if(match.Success)shaderName=match.Groups[1].Value;}
        var shader=Shader.Find(shaderName);if(!shader)shader=Shader.Find("Legacy Shaders/"+shaderName);
            if(!shader)throw new Exception("Source shader has no Unity 2022 equivalent: "+shaderName);
            var mat=new Material(shader){name=name};
            mat.shaderKeywords=(tree["m_ShaderKeywords"]??new JArray()).Select(t=>(string)t).ToArray();
            var properties=tree["m_SavedProperties"];
            foreach(var pair in properties["m_Colors"]??new JArray()) if(mat.HasProperty((string)pair[0]["name"]))mat.SetColor((string)pair[0]["name"],C(pair[1]));
            foreach(var pair in properties["m_TexEnvs"]??new JArray())
            {
                string key=(string)pair[0]["name"];if(!mat.HasProperty(key))continue;
                mat.SetTexture(key,Ref<Texture2D>(id,pair[1]["m_Texture"]));
                mat.SetTextureScale(key,V2(pair[1]["m_Scale"]));mat.SetTextureOffset(key,V2(pair[1]["m_Offset"]));
            }
            foreach(var pair in properties["m_Floats"]??new JArray())if(mat.HasProperty((string)pair[0]["name"]))mat.SetFloat((string)pair[0]["name"],(float)pair[1]);
            Persist(id,mat,ResourceKey(id));
        }
        // XML is loaded by its original logical Resources key; file contents remain private here.
        foreach(var row in source.Values.Where(t=>(string)t["type"]=="TextAsset" && ResourceKey(Id(t))!=null))
        {
            string target="Assets/Resources/"+ResourceKey(Id(row))+".xml";
            File.Copy((string)row["assetPath"],target,true);AssetDatabase.ImportAsset(target);
            restored[Id(row)]=AssetDatabase.LoadAssetAtPath<TextAsset>(target);
        }
    }
    static void RepairSpriteSerialization(string id,Sprite sprite,Vector2[] vertices,ushort[] triangles)
    {
        // Unity's dynamically-created Sprite asset writer omits UVs and resets tight meshes.
        // Encode the authored geometry in the generated asset, then reimport it normally.
        string path=AssetDatabase.GetAssetPath(sprite);AssetDatabase.SaveAssets();string yaml=File.ReadAllText(path);
        var bytes=new List<byte>();
        foreach(var v in vertices){bytes.AddRange(BitConverter.GetBytes(v.x));bytes.AddRange(BitConverter.GetBytes(v.y));bytes.AddRange(BitConverter.GetBytes(0f));}
        foreach(var v in vertices){bytes.AddRange(BitConverter.GetBytes((v.x*sprite.pixelsPerUnit+sprite.pivot.x+sprite.rect.x)/sprite.texture.width));bytes.AddRange(BitConverter.GetBytes((v.y*sprite.pixelsPerUnit+sprite.pivot.y+sprite.rect.y)/sprite.texture.height));}
        string hex=BitConverter.ToString(bytes.ToArray()).Replace("-","").ToLowerInvariant();
        string indices=BitConverter.ToString(triangles.SelectMany(BitConverter.GetBytes).ToArray()).Replace("-","").ToLowerInvariant();
        yaml=Regex.Replace(yaml,@"(?m)^(\s*indexCount:) \d+","$1 "+triangles.Length);
        yaml=Regex.Replace(yaml,@"(?m)^(\s*vertexCount:) \d+","$1 "+vertices.Length);
        yaml=Regex.Replace(yaml,@"(?m)^(\s*m_VertexCount:) \d+","$1 "+vertices.Length);
        yaml=Regex.Replace(yaml,@"(?m)^(\s*m_DataSize:) \d+","$1 "+bytes.Count);
        yaml=Regex.Replace(yaml,@"(?m)^(\s*m_IndexBuffer:) [0-9a-fA-F]+","$1 "+indices);
        yaml=Regex.Replace(yaml,@"(?m)^(\s*_typelessdata:) [0-9a-fA-F]+","$1 "+hex);
        File.WriteAllText(path,yaml);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
        restored[id]=AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    static void CreateSceneObjects()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        foreach(var row in source.Values.Where(t=>(string)t["type"]=="GameObject"))
        {
            var go=new GameObject((string)row["tree"]["m_Name"]);go.layer=I(row["tree"],"m_Layer");
            restored[Id(row)]=go;
        }
        foreach(var row in source.Values.Where(t=>(string)t["type"]=="Transform"))
            restored[Id(row)]=Ref<GameObject>(Id(row),row["tree"]["m_GameObject"]).transform;
        foreach(var row in source.Values.Where(t=>(string)t["type"]=="Transform"))
        {
            string id=Id(row);var tree=row["tree"];var tr=(Transform)restored[id];
            tr.SetParent(Ref<Transform>(id,tree["m_Father"]),false);
            tr.localPosition=V3(tree["m_LocalPosition"]);tr.localScale=V3(tree["m_LocalScale"]);
            var q=tree["m_LocalRotation"];tr.localRotation=new Quaternion(F(q,"x"),F(q,"y"),F(q,"z"),F(q,"w",1));
        }
        // Match serialized sibling order, which controls GetComponentsInChildren and card setup order.
        foreach(var row in source.Values.Where(t=>(string)t["type"]=="Transform"))
        {
            int index=0;foreach(var pointer in row["tree"]["m_Children"]??new JArray())Ref<Transform>(Id(row),pointer)?.SetSiblingIndex(index++);
        }
        var addOrder=new[]{"Camera","AudioListener","AudioSource","MeshFilter","TextMesh","MeshRenderer","SpriteRenderer","BoxCollider2D","MeshCollider","ParticleSystem","ParticleSystemRenderer","MonoBehaviour"};
        foreach(string kind in addOrder)
            foreach(var row in source.Values.Where(t=>(string)t["type"]==kind && ((string)t["container"])=="mainData"))
            {
                string id=Id(row);var go=Ref<GameObject>(id,row["tree"]["m_GameObject"]);if(!go)continue;
                Type type=kind=="MonoBehaviour"?typeof(Loader).Assembly.GetType((string)row["scriptClass"]):typeof(GameObject).Assembly.GetType("UnityEngine."+kind);
                if(kind!="MonoBehaviour" && type==null)type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEngine."+kind)).FirstOrDefault(t=>t!=null);
                if(type==null)throw new Exception("Component type missing: "+kind+" "+id);
                if(typeof(ScriptableObject).IsAssignableFrom(type))
                {var slot=go.AddComponent<LegacyObjectSlot>();slot.Value=ScriptableObject.CreateInstance(type);Persist(id,slot.Value);restored[id]=slot;}
                else { var existing=go.GetComponent(type); restored[id]=existing ? existing : go.AddComponent(type); }
            }
    }
    static void Components()
    {
        foreach(var row in source.Values.Where(t=>(string)t["container"]=="mainData" && restored.ContainsKey(Id(t))))
        {
            string id=Id(row);var tree=row["tree"];U obj=restored[id];
            if(obj is Behaviour behaviour) behaviour.enabled=B(tree,"m_Enabled",true);
            if(obj is Renderer renderer)
            {
                renderer.enabled=B(tree,"m_Enabled",true);renderer.sortingOrder=I(tree,"m_SortingOrder");
                renderer.sortingLayerID=I(tree,"m_SortingLayerID");renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows=B(tree,"m_ReceiveShadows");
                if(tree["m_Materials"]!=null)renderer.sharedMaterials=tree["m_Materials"].Select(t=>Ref<Material>(id,t)).Where(t=>t).ToArray();
            }
            if(obj is SpriteRenderer sr){sr.sprite=Ref<Sprite>(id,tree["m_Sprite"]);sr.color=C(tree["m_Color"]);}
            if(obj is Camera camera)
            {
                camera.gameObject.tag="MainCamera";camera.clearFlags=(CameraClearFlags)I(tree,"m_ClearFlags",2);
                camera.backgroundColor=C(tree["m_BackGroundColor"]);camera.orthographic=B(tree,"orthographic");
                camera.orthographicSize=F(tree,"orthographic size");camera.nearClipPlane=F(tree,"near clip plane");camera.farClipPlane=F(tree,"far clip plane")+.001f;
                camera.fieldOfView=F(tree,"field of view");camera.depth=F(tree,"m_Depth");camera.allowHDR=B(tree,"m_HDR");camera.allowMSAA=false;
                var rect=tree["m_NormalizedViewPortRect"];camera.rect=new Rect(F(rect,"x"),F(rect,"y"),F(rect,"width"),F(rect,"height"));
            }
            if(obj is TextMesh text)
            {
                text.text=(string)tree["m_Text"];text.characterSize=F(tree,"m_CharacterSize");text.lineSpacing=F(tree,"m_LineSpacing",1);
                text.anchor=(TextAnchor)I(tree,"m_Anchor");text.alignment=(TextAlignment)I(tree,"m_Alignment");text.tabSize=F(tree,"m_TabSize",4);
                text.fontSize=I(tree,"m_FontSize");text.fontStyle=(FontStyle)I(tree,"m_FontStyle");text.richText=B(tree,"m_RichText");text.offsetZ=F(tree,"m_OffsetZ");
                text.font=Ref<Font>(id,tree["m_Font"]);text.color=C(tree["m_Color"]);
                if(text.font)text.GetComponent<MeshRenderer>().sharedMaterial=text.font.material;
            }
            if(obj is MeshFilter mf)mf.sharedMesh=Ref<Mesh>(id,tree["m_Mesh"]);
            if(obj is MeshCollider mc){mc.sharedMesh=Ref<Mesh>(id,tree["m_Mesh"]);mc.convex=B(tree,"m_Convex");mc.isTrigger=B(tree,"m_IsTrigger");mc.enabled=B(tree,"m_Enabled",true);}
            if(obj is BoxCollider2D bc){bc.size=V2(tree["m_Size"]);bc.offset=V2(tree["m_Center"]);bc.isTrigger=B(tree,"m_IsTrigger");}
            if(obj is AudioSource audio)
            {
                audio.clip=Ref<AudioClip>(id,tree["m_audioClip"]);audio.playOnAwake=B(tree,"m_PlayOnAwake");audio.volume=F(tree,"m_Volume",1);audio.pitch=F(tree,"m_Pitch",1);
                audio.loop=B(tree,"Loop");audio.mute=B(tree,"Mute");audio.priority=I(tree,"Priority",128);audio.spatialBlend=0;
            }
            if((string)row["type"]=="MonoBehaviour") RestoreFields(obj,tree,id);
            if(obj is ParticleSystem ps) RestoreParticles(ps,tree);
        }
        // Restore TextMesh material after MeshRenderer property pass to bind dynamic glyph atlas.
        foreach(var tm in restored.Values.OfType<TextMesh>())if(tm.font)tm.GetComponent<MeshRenderer>().sharedMaterial=tm.font.material;
        foreach(var row in source.Values.Where(t=>(string)t["type"]=="GameObject"))((GameObject)restored[Id(row)]).SetActive(B(row["tree"],"m_IsActive",true));
    }
    static void RestoreFields(U obj,JToken fields,string id)
    {
        if(obj is LegacyObjectSlot slot)obj=slot.Value;
        var so=new SerializedObject(obj);
        foreach(var pair in ((JObject)fields).Properties())
        {
            if(pair.Name.StartsWith("m_GameObject") || pair.Name=="m_Script" || pair.Name=="m_Enabled" || pair.Name=="m_Name")continue;
            var prop=so.FindProperty(pair.Name);if(prop!=null)Assign(prop,pair.Value,id);
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    static void Assign(SerializedProperty p,JToken v,string id)
    {
        if(v is JArray a && p.isArray){p.arraySize=a.Count;for(int i=0;i<a.Count;i++)Assign(p.GetArrayElementAtIndex(i),a[i],id);return;}
        switch(p.propertyType)
        {
            case SerializedPropertyType.ObjectReference:p.objectReferenceValue=Ref<U>(id,v);break;
            case SerializedPropertyType.Integer:p.intValue=(int)v;break;
            case SerializedPropertyType.Boolean:p.boolValue=v.Type==JTokenType.Boolean?(bool)v:(int)v!=0;break;
            case SerializedPropertyType.Float:p.floatValue=(float)v;break;
            case SerializedPropertyType.String:p.stringValue=(string)v;break;
            case SerializedPropertyType.Color:p.colorValue=C(v);break;
            case SerializedPropertyType.Vector2:p.vector2Value=V2(v);break;
            case SerializedPropertyType.Vector3:p.vector3Value=V3(v);break;
        }
    }
    static void RestoreParticles(ParticleSystem ps,JToken t)
    {
        var main=ps.main;main.duration=F(t,"lengthInSec",.4f);main.loop=B(t,"looping");main.playOnAwake=B(t,"playOnAwake");main.startDelay=F(t,"startDelay");
        var init=t["InitialModule"];main.startLifetime=F(init["startLifetime"],"scalar",.6f);main.startSpeed=F(init["startSpeed"],"scalar");
        main.startSize=F(init["startSize"],"scalar",.5f);main.startColor=C(init["startColor"]?["maxColor"]);main.maxParticles=I(init,"maxNumParticles",1000);main.simulationSpace=B(t,"moveWithTransform")?ParticleSystemSimulationSpace.Local:ParticleSystemSimulationSpace.World;
        var emission=ps.emission;var e=t["EmissionModule"];emission.enabled=B(e,"enabled");emission.rateOverTime=F(e?["rate"],"scalar");
        var shape=ps.shape;var s=t["ShapeModule"];shape.enabled=B(s,"enabled");shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=F(s,"radius",3);shape.randomDirectionAmount=B(s,"randomDirection")?1:0;
        var size=ps.sizeOverLifetime;size.enabled=B(t["SizeModule"],"enabled");size.size=Curve(t["SizeModule"]?["curve"]);
        var rotation=ps.rotationOverLifetime;rotation.enabled=B(t["RotationModule"],"enabled");rotation.z=Curve(t["RotationModule"]?["curve"]);
        var limit=ps.limitVelocityOverLifetime;var lm=t["ClampVelocityModule"];limit.enabled=B(lm,"enabled");limit.separateAxes=B(lm,"separateAxis");limit.limit=Curve(lm?["magnitude"]);limit.dampen=F(lm,"dampen",.2f);
        ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
    }
    static ParticleSystem.MinMaxCurve Curve(JToken value)
    {
        if(value==null)return new ParticleSystem.MinMaxCurve(0);
        float scalar=F(value,"scalar",1);int mode=I(value,"minMaxState");
        if(mode==0)return new ParticleSystem.MinMaxCurve(scalar);
        Func<JToken,AnimationCurve> curve=(v)=>new AnimationCurve((v?["m_Curve"]??new JArray()).Select(k=>new Keyframe(F(k,"time"),F(k,"value"),F(k,"inSlope"),F(k,"outSlope"))).ToArray());
        if(mode==1)return new ParticleSystem.MinMaxCurve(scalar,curve(value["maxCurve"]));
        if(mode==2)return new ParticleSystem.MinMaxCurve(0,scalar);
        return new ParticleSystem.MinMaxCurve(scalar,curve(value["minCurve"]),curve(value["maxCurve"]));
    }
    static void Audit()
    {
        var failures=new List<string>();int transforms=0,parents=0,siblings=0,colliders=0,sprites=0,spriteVertices=0,components=0;
        foreach(var row in source.Values.Where(t=>(string)t["type"]=="GameObject"))
        {
            var go=(GameObject)restored[Id(row)];int index=0;
            foreach(var pair in row["tree"]["m_Component"])
            {
                var expected=Ref<Component>(Id(row),pair[1]);if(!expected)throw new Exception("Component reference missing: "+Id(row));
                while(Array.IndexOf(go.GetComponents<Component>(),expected)>index)
                    if(!UnityEditorInternal.ComponentUtility.MoveComponentUp(expected))throw new Exception("Cannot restore component order: "+Id(row));
                if(go.GetComponents<Component>()[index++]!=expected)failures.Add(Id(row)+" component order");components++;
            }
            if(go.GetComponents<Component>().Length!=index)failures.Add(Id(row)+" extra component");
        }
        foreach(var row in source.Values.Where(t=>(string)t["type"]=="Transform"))
        {
            string id=Id(row);var tree=row["tree"];var tr=(Transform)restored[id];transforms++;
            if(tr.localPosition!=V3(tree["m_LocalPosition"]) || tr.localScale!=V3(tree["m_LocalScale"]))failures.Add(id+" transform");
            var q=tree["m_LocalRotation"];if(Quaternion.Angle(tr.localRotation,new Quaternion(F(q,"x"),F(q,"y"),F(q,"z"),F(q,"w",1)))>.001f)failures.Add(id+" rotation");
            parents++;if(tr.parent!=Ref<Transform>(id,tree["m_Father"]))failures.Add(id+" parent");
            int i=0;foreach(var p in tree["m_Children"]??new JArray()){siblings++;if(Ref<Transform>(id,p).GetSiblingIndex()!=i++)failures.Add(id+" sibling");}
        }
        foreach(var row in source.Values.Where(t=>(string)t["type"]=="BoxCollider2D"))
        {var b=(BoxCollider2D)restored[Id(row)];colliders++;if(b.size!=V2(row["tree"]["m_Size"]) || b.offset!=V2(row["tree"]["m_Center"]))failures.Add(Id(row)+" collider");}
        foreach(var row in source.Values.Where(t=>(string)t["type"]=="Sprite"))
        {
            var s=(Sprite)restored[Id(row)];var expected=row["tree"]["m_RD"]["vertices"].Select(t=>V2(t["pos"])).ToArray();sprites++;spriteVertices+=expected.Length;
            if(!s.vertices.SequenceEqual(expected) || !s.triangles.SequenceEqual(row["tree"]["m_RD"]["indices"].Select(t=>(ushort)t)))failures.Add(Id(row)+" sprite mesh");
            for(int i=0;i<expected.Length;i++){var v=expected[i];var uv=new Vector2((v.x*s.pixelsPerUnit+s.pivot.x+s.rect.x)/s.texture.width,(v.y*s.pixelsPerUnit+s.pivot.y+s.rect.y)/s.texture.height);if(Vector2.Distance(s.uv[i],uv)>.00001f)failures.Add(Id(row)+" UV "+i);}
        }
        File.WriteAllText("../structure-audit.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{transforms,parents,siblings,components,boxColliders=colliders,sprites,spriteVertices,farClipCompatibilityEpsilon=.001f,failures,passed=failures.Count==0,legacyScriptableSlots=restored.Values.OfType<LegacyObjectSlot>().Count()},Newtonsoft.Json.Formatting.Indented));
        if(failures.Count>0)throw new Exception("Structure mismatch: "+string.Join(",",failures));
    }
    static void Settings()
    {
        PlayerSettings.companyName="RoguePrototypeLab";PlayerSettings.productName="妖怪夜市开张";PlayerSettings.bundleVersion="0.2.0";
        PlayerSettings.defaultScreenWidth=1920;PlayerSettings.defaultScreenHeight=1080;PlayerSettings.defaultIsNativeResolution=true;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=false;PlayerSettings.runInBackground=false;
        PlayerSettings.colorSpace=ColorSpace.Gamma;PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone,ScriptingImplementation.Mono2x);
        PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Standalone,ApiCompatibilityLevel.NET_Standard_2_0);
        if(restored!=null && restored.ContainsKey("sharedassets0.assets:52"))PlayerSettings.defaultCursor=restored["sharedassets0.assets:52"] as Texture2D;
        PlayerSettings.cursorHotspot=Vector2.zero;
        QualitySettings.SetQualityLevel(0,false);QualitySettings.antiAliasing=2;QualitySettings.vSyncCount=0;
        RenderSettings.fog=false;RenderSettings.skybox=null;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.2f,.2f,.2f,1);
        // Unity 4 synchronized moved raycast targets immediately; preserve that input behavior.
        Physics.autoSyncTransforms=true;Physics2D.autoSyncTransforms=true;
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
    }
    [MenuItem("NightMarket/Restore Local Reference")]
    public static void Restore()
    {
        Load();AssetDatabase.Refresh();ImportAssets();CreateSceneObjects();Components();Settings();Audit();
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),ScenePath);
        Directory.CreateDirectory("Assets/Recovery/Prefabs");
        var roots=restored.Values.OfType<GameObject>().Where(t=>t.transform.parent==null).ToArray();
        foreach(var go in roots)PrefabUtility.SaveAsPrefabAsset(go,"Assets/Recovery/Prefabs/"+go.name+".prefab");
        File.WriteAllText("../prefab-report.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{scope="All serialized scene roots; editor-only original prefab links are absent from a Player build",rootPrefabs=roots.Length,nodesCovered=roots.Sum(g=>g.GetComponentsInChildren<Transform>(true).Length),roots=roots.Select(g=>new{name=g.name,nodes=g.GetComponentsInChildren<Transform>(true).Length})},Newtonsoft.Json.Formatting.Indented));
        AssetDatabase.SaveAssets();
        var report=new {sceneObjects=restored.Values.OfType<GameObject>().Count(),transforms=restored.Values.OfType<Transform>().Count(),sprites=restored.Values.OfType<Sprite>().Count(),textMeshes=restored.Values.OfType<TextMesh>().Count(),monoSlots=source.Values.Count(t=>(string)t["container"]=="mainData" && (string)t["type"]=="MonoBehaviour"),scene=ScenePath,success=true};
        File.WriteAllText("../restoration-report.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
        Debug.Log("NIGHT_MARKET_RESTORE_OK "+report.sceneObjects);
    }
    public static void Build()
    {
        EditorSceneManager.OpenScene(ScenePath);Settings();
        string path="../Builds/Windows/YokaiNightMarket.exe";Directory.CreateDirectory(Path.GetDirectoryName(path));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName=path,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
        File.WriteAllText("../build-report.json",Newtonsoft.Json.JsonConvert.SerializeObject(new {result=report.summary.result.ToString(),errors=report.summary.totalErrors,warnings=report.summary.totalWarnings,bytes=report.summary.totalSize,seconds=report.summary.totalTime.TotalSeconds},Newtonsoft.Json.Formatting.Indented));
        if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Build failed");
    }
    public static void BuildQA()
    {
        BuildValidation("QA","妖怪夜市开张 QA","../qa-build-report.json");
    }
    public static void BuildQAFinal()
    {
        BuildValidation("QA-Final","妖怪夜市开张 QA Final","../qa-final-build-report.json");
    }
    public static void BuildQAVisual()
    {
        BuildValidation("QA-Visual","妖怪夜市开张 QA Visual","../qa-visual-build-report.json");
    }
    static void BuildValidation(string bucket,string product,string output)
    {
        EditorSceneManager.OpenScene(ScenePath);Settings();PlayerSettings.productName=product;
        string path=Path.Combine(Environment.GetEnvironmentVariable("NIGHT_MARKET_BUILD_OUTPUT")??"../Builds",bucket,"NightMarketQA.exe");Directory.CreateDirectory(Path.GetDirectoryName(path));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName=path,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None|BuildOptions.CleanBuildCache,extraScriptingDefines=new[]{"NIGHT_MARKET_VALIDATION"}});
        PlayerSettings.productName="妖怪夜市开张";
        File.WriteAllText(output,Newtonsoft.Json.JsonConvert.SerializeObject(new{result=report.summary.result.ToString(),errors=report.summary.totalErrors,warnings=report.summary.totalWarnings,bytes=report.summary.totalSize},Newtonsoft.Json.Formatting.Indented));
        if(report.summary.result!=BuildResult.Succeeded)throw new Exception("QA build failed");
    }
    public static void BuildRelease()
    {
        EditorSceneManager.OpenScene(ScenePath);Settings();
        string path=Path.Combine(Environment.GetEnvironmentVariable("NIGHT_MARKET_BUILD_OUTPUT")??"../Builds","Release","YokaiNightMarket.exe");Directory.CreateDirectory(Path.GetDirectoryName(path));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName=path,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None|BuildOptions.CleanBuildCache});
        File.WriteAllText("../release-build-report.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{result=report.summary.result.ToString(),development=false,errors=report.summary.totalErrors,warnings=report.summary.totalWarnings,bytes=report.summary.totalSize,seconds=report.summary.totalTime.TotalSeconds},Newtonsoft.Json.Formatting.Indented));
        if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Release build failed");
    }
    public static void BuildFinalCandidates()
    {
        BuildQAVisual();BuildRelease();
    }
}
