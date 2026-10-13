// Technical PNG export only: original target dimensions and functional alpha stencil.
// Creative changes are made by image_gen. Reference stencils/final derivatives remain LOCAL ONLY.
using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Newtonsoft.Json.Linq;

public static class NightAssetExport
{
    static Texture2D Read(string p){var t=new Texture2D(2,2,TextureFormat.RGBA32,false);if(!t.LoadImage(File.ReadAllBytes(p)))throw new Exception("PNG decode failed: "+p);return t;}
    public static void Export()
    {
        ExportSet(null);
    }
    public static void ExportBackgroundFix()
    {
        ExportSet(new[]{"CARD_00_Background","CARD_Selection_Background","CARD_Score","PRJ_Dot","PRJ_Dot 1"});
    }
    public static void ExportReadabilityFix()
    {
        ExportSet(new[]{"CARD_03_DetailsTop","PRJ_CARD_01_Base_Project"});
    }
    public static void ExportPromptFix()
    {
        ExportSet(new[]{"SubTitle"});
    }
    public static void FixUISurfacesAndBuild()
    {
        var spec=JObject.Parse(File.ReadAllText("../../fidelity/data/ui-surfaces.json"));
        ExportSet(System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(((JObject)spec["textures"]).Properties(),p=>p.Name)));
        RecoveryEditor.BuildFinalCandidates();
    }
    public static void ExportNightStyle()
    {
        var spec=JObject.Parse(File.ReadAllText("../../fidelity/data/ui-surfaces.json"));
        ExportSet(System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(((JObject)spec["textures"]).Properties(),p=>p.Name)));
    }
    public static void FixLanguageBadgesAndBuild()
    {
        ExportSet(new[]{"English","Spanish"});
        RecoveryEditor.BuildFinalCandidates();
    }
    public static void FixPromptsAndBuild()
    {
        ExportPromptFix();
        RecoveryEditor.BuildFinalCandidates();
    }
    static void ExportSet(string[] selected)
    {
        string root=Path.GetFullPath("../..");
        var jobs=JObject.Parse(File.ReadAllText(Path.Combine(root,"fidelity/art/jobs.json")));
        var manifest=JObject.Parse(File.ReadAllText("Assets/Editor/RecoverySource/manifest.json"));
        var surfaces=JObject.Parse(File.ReadAllText(Path.Combine(root,"fidelity/data/ui-surfaces.json")))["textures"];
        int count=0;
        foreach(var job in jobs["jobs"])
        {
            if(selected!=null && Array.IndexOf(selected,(string)job["name"])<0)continue;
            var surface=surfaces[(string)job["name"]];
            string raw=Path.Combine(root,surface!=null?(string)surface["source"]:(string)job["raw"]);if(!File.Exists(raw))throw new Exception("Missing production art: "+raw);
            string id=(string)job["id"];JToken original=null;
            foreach(var o in manifest["objects"])if((string)o["id"]==id){original=o;break;}
            if(original==null)throw new Exception("Unknown texture id: "+id);
            var input=Read(raw);int width=(int)job["width"],height=(int)job["height"];
            var output=new Texture2D(width,height,TextureFormat.RGBA32,false);
            var pixels=new Color[width*height];
            string kind=(string)job["kind"];Texture2D stencil=null;
            Rect rawBounds=new Rect(0,0,input.width,input.height),slot=new Rect(0,0,width,height);
            float stampScale=1;
            if(surface!=null && (string)surface["mode"]=="framed" && Mathf.Abs((float)input.width/input.height/((float)width/height)-1)>(float)surface["maxAspectError"])
                throw new Exception("Framed art aspect drift: "+job["name"]);
            if(kind=="stamp")
            {
                rawBounds=AlphaBounds(input);slot=SpriteBounds(manifest,original);
                stampScale=Mathf.Min(slot.width/rawBounds.width,slot.height/rawBounds.height)*.9f;
            }
            if(kind=="interface" || kind=="prompt_backplate" || kind=="functional_ui")
            {
                // Geometry is part of the recovered UI. Read the pristine reference, never the overwritten working asset.
                var e=original["exportFiles"][0];
                string repo=Path.GetFullPath("../../../..");
                stencil=Read(Path.Combine(repo,"prototypes/re-crunch-time",(string)e["path"]));
            }
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                float u=(x+.5f)/width,v=(y+.5f)/height;
                Color color=input.GetPixelBilinear(u,v);
                // SubTitle is Continue.Background, not a logo. Its inherited tight
                // mesh and alpha define the prompt plate; every label is live TextMesh.
                if(surface!=null)
                {
                    if(stencil==null)throw new Exception("UI role has no geometry stencil");
                    color=SurfacePixel(input,stencil,x,y,width,height,surface);
                }
                else if(kind=="prompt_backplate")color=new Color(.02f,.025f,.04f,stencil.GetPixel(x,y).a);
                if(kind=="stamp")
                {
                    // Technical atlas placement: retain the source sprite's pivot and tight mesh.
                    float sx=(x-slot.center.x)/stampScale+rawBounds.center.x;
                    float sy=(y-slot.center.y)/stampScale+rawBounds.center.y;
                    color=sx<0 || sy<0 || sx>=input.width || sy>=input.height?Color.clear:input.GetPixelBilinear(sx/input.width,sy/input.height);
                }
                if(kind=="interface")
                {
                    float alpha=stencil.GetPixel(x,y).a;
                    // Generated ornament supplies color; the original functional silhouette supplies alpha.
                    // Fill transparent source-art gaps with its nearest opaque paper value to avoid new holes.
                    bool mask=((string)job["name"]).EndsWith("_Mask") || ((string)job["name"])=="Solid" || ((string)job["name"])=="PRJ_BUTTON" || ((string)job["name"])=="CARD_04_Turn" || ((string)job["name"])=="PRJ_Dot" || ((string)job["name"])=="PRJ_Dot 1";
                    if(color.a<.1f)color=mask?Color.white:new Color(.09f,.14f,.23f,1);
                    // Neutral source brightness encodes functional panels/turn cuts. Keep that
                    // scalar layout while taking chroma and paper grain from the generated art.
                    var refColor=stencil.GetPixel(x,y);
                    string key=(string)job["name"];
                    bool functionalBands=key.StartsWith("CARD_01_Base") || key.EndsWith("_Mask") || key=="CARD_02_Details" || key=="CARD_03_DetailsTop" || key=="PRJ_CARD_01_Base_Project" || key=="PRJ_CARD_02_Details";
                    if(functionalBands && Mathf.Max(refColor.r,Mathf.Max(refColor.g,refColor.b))-Mathf.Min(refColor.r,Mathf.Min(refColor.g,refColor.b))<.015f)
                    {
                        float level=refColor.r;float peak=Mathf.Max(color.r,Mathf.Max(color.g,color.b));
                        color=peak>.001f?new Color(color.r/peak*level,color.g/peak*level,color.b/peak*level,alpha):new Color(level,level,level,alpha);
                    }
                    color.a=alpha;
                }
                else if(kind=="illustration" || kind=="table")color.a=1;
                pixels[y*width+x]=color;
            }
            output.SetPixels(pixels);output.Apply();
            if(surface!=null && (string)surface["mode"]=="label")
            {
                BakeLabel(output,(string)surfaces[(string)job["name"]]["label"],manifest,true);
                var labeled=output.GetPixels();for(int y=0;y<height;y++)for(int x=0;x<width;x++)labeled[y*width+x].a=stencil.GetPixel(x,y).a;
                output.SetPixels(labeled);output.Apply();
            }
            if((string)job["name"]=="SubTitle" && kind!="prompt_backplate")throw new Exception("SubTitle must remain a text-free Continue background");
            string label=kind=="title"?"妖怪夜市开张":"";
            if(label!="")BakeLabel(output,label,manifest,false);
            string exportRoot=Environment.GetEnvironmentVariable("NIGHT_MARKET_ART_OUTPUT")??Path.Combine(root,"local/NightArt");
            string final=Path.Combine(exportRoot,Path.GetFileName((string)job["final"]));
            Directory.CreateDirectory(Path.GetDirectoryName(final));File.WriteAllBytes(final,output.EncodeToPNG());
            string working=(string)original["assetPath"];File.Copy(final,working,true);
            UnityEngine.Object.DestroyImmediate(input);UnityEngine.Object.DestroyImmediate(output);if(stencil)UnityEngine.Object.DestroyImmediate(stencil);
            count++;
        }
        AssetDatabase.Refresh();File.WriteAllText(selected==null?"../image-export-report.json":"../image-background-fix-report.json",new JObject{{"exported",count},{"technicalResize",true},{"referenceStencilsLocalOnly",true}}.ToString());
        Debug.Log("NIGHT_IMAGE_EXPORT_OK "+count);
    }
    static Color RGB(JToken v) => new Color((float)v[0],(float)v[1],(float)v[2],1);
    static Color SurfacePixel(Texture2D input,Texture2D stencil,int x,int y,int width,int height,JToken spec)
    {
        // One scale for both axes. Center-crop only; never stretch ornaments or grain.
        float scale=Mathf.Max((float)width/input.width,(float)height/input.height);
        float sx=(x+.5f-width*.5f)/scale+input.width*.5f;
        float sy=(y+.5f-height*.5f)/scale+input.height*.5f;
        Color c=input.GetPixelBilinear(sx/input.width,sy/input.height);
        if((string)spec["mode"]=="flat")c=RGB(spec["rgb"]);
        else if(spec["rgb"]!=null){Color tint=RGB(spec["rgb"]);c=new Color(c.r*tint.r,c.g*tint.g,c.b*tint.b,1);}
        var slots=spec["functionalSlots"];
        if(slots!=null && x>=(int)slots["xMin"] && x<(int)slots["xMax"])
        {
            int topY=height-1-y;
            foreach(var band in slots["topRows"])
                if(topY>=(int)band[0] && topY<=(int)band[1])
                {
                    var source=stencil.GetPixel(x,y);
                    if(Mathf.Max(source.r,Mathf.Max(source.g,source.b))<(float)slots["sourceMaxRGB"])c=RGB(slots["rgb"]);
                }
        }
        if((string)spec["mode"]=="outlined_paper")
        {
            int d=(int)spec["borderPixels"];
            // Boundary comes only from the original functional silhouette, including clipped corners.
            bool edge=x<d || y<d || x>=width-d || y>=height-d;
            if(!edge)edge=stencil.GetPixel(x-d,y).a<.1f || stencil.GetPixel(x+d,y).a<.1f || stencil.GetPixel(x,y-d).a<.1f || stencil.GetPixel(x,y+d).a<.1f;
            if(edge)c=RGB(spec["borderRGB"]);
        }
        c.a=stencil.GetPixel(x,y).a;return c;
    }
    static Rect AlphaBounds(Texture2D input)
    {
        int minX=input.width,minY=input.height,maxX=-1,maxY=-1;
        for(int y=0;y<input.height;y++)for(int x=0;x<input.width;x++)if(input.GetPixel(x,y).a>.05f){minX=Mathf.Min(minX,x);minY=Mathf.Min(minY,y);maxX=Mathf.Max(maxX,x);maxY=Mathf.Max(maxY,y);}
        if(maxX<0)throw new Exception("Stamp has no visible alpha");return Rect.MinMaxRect(minX,minY,maxX+1,maxY+1);
    }
    static Rect SpriteBounds(JObject manifest,JToken texture)
    {
        foreach(var row in manifest["objects"])
        {
            if((string)row["type"]!="Sprite" || (string)row["container"]!=(string)texture["container"] || (long)row["tree"]["m_RD"]["texture"]["m_PathID"]!=(long)texture["pathID"])continue;
            var tree=row["tree"];float ppu=(float)tree["m_PixelsToUnits"];
            float px=(float)tree["m_Rect"]["width"]*.5f+(float)tree["m_Offset"]["x"],py=(float)tree["m_Rect"]["height"]*.5f+(float)tree["m_Offset"]["y"];
            float minX=float.MaxValue,minY=float.MaxValue,maxX=float.MinValue,maxY=float.MinValue;
            foreach(var vertex in tree["m_RD"]["vertices"]){float x=(float)vertex["pos"]["x"]*ppu+px,y=(float)vertex["pos"]["y"]*ppu+py;minX=Mathf.Min(minX,x);minY=Mathf.Min(minY,y);maxX=Mathf.Max(maxX,x);maxY=Mathf.Max(maxY,y);}
            return Rect.MinMaxRect(minX,minY,maxX,maxY);
        }
        throw new Exception("Stamp sprite not found: "+texture["id"]);
    }
    static void BakeLabel(Texture2D target,string label,JObject manifest,bool small)
    {
        JToken fontRow=null;foreach(var row in manifest["objects"])if((string)row["type"]=="Font" && (float?)row["tree"]["m_FontSize"]==128){fontRow=row;break;}
        if(fontRow==null)throw new Exception("Native label font missing");
        var font=AssetDatabase.LoadAssetAtPath<Font>((string)fontRow["assetPath"]);font.RequestCharactersInTexture(label,256,FontStyle.Normal);
        var scene=EditorSceneManager.NewPreviewScene();var cameraObject=new GameObject("Export label camera");SceneManager.MoveGameObjectToScene(cameraObject,scene);
        var camera=cameraObject.AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
        camera.scene=scene;
        camera.orthographic=true;camera.orthographicSize=target.height*.5f;camera.nearClipPlane=.1f;camera.farClipPlane=100;camera.transform.position=new Vector3(0,0,-10);camera.cullingMask=1<<31;
        var textObject=new GameObject("Native title");textObject.layer=31;SceneManager.MoveGameObjectToScene(textObject,scene);
        var text=textObject.AddComponent<TextMesh>();text.font=font;text.fontSize=256;text.characterSize=1;text.anchor=TextAnchor.MiddleCenter;text.text=label;
        text.color=small?new Color(.211765f,.176471f,.141176f,1):new Color(.96f,.81f,.48f,1);text.GetComponent<MeshRenderer>().sharedMaterial=font.material;
        var rt=new RenderTexture(target.width,target.height,24);camera.targetTexture=rt;camera.Render();
        var bounds=text.GetComponent<MeshRenderer>().bounds;if(bounds.size.x<=0)throw new Exception("Native title has empty bounds");float scale=target.width*(small?.7f:.61f)/bounds.size.x;
        textObject.transform.localScale=Vector3.one*scale;textObject.transform.position=new Vector3(-bounds.center.x*scale,small?0:-target.height*.12f-bounds.center.y*scale,0);
        for(int i=0;i<(small?0:4);i++)
        {
            var shadow=UnityEngine.Object.Instantiate(textObject);SceneManager.MoveGameObjectToScene(shadow,scene);
            shadow.GetComponent<TextMesh>().color=new Color(.045f,.065f,.10f,1);
            shadow.transform.position+=new Vector3((i%2==0?-1:1)*target.width*.0025f,(i<2?-1:1)*target.width*.0025f,1);
        }
        camera.Render();var active=RenderTexture.active;RenderTexture.active=rt;
        var overlay=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);overlay.ReadPixels(new Rect(0,0,target.width,target.height),0,0);overlay.Apply();
        var back=target.GetPixels();var top=overlay.GetPixels();for(int i=0;i<back.Length;i++){float a=top[i].a;back[i]=new Color(top[i].r*a+back[i].r*(1-a),top[i].g*a+back[i].g*(1-a),top[i].b*a+back[i].b*(1-a),a+back[i].a*(1-a));}
        target.SetPixels(back);target.Apply();RenderTexture.active=active;camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(overlay);EditorSceneManager.ClosePreviewScene(scene);
    }
}
