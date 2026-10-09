using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
namespace PrototypeKit {
public sealed class CheckpointStore<T> where T:class {
    [Serializable] sealed class Record {public int schema;public string payload,sha256;}
    readonly string directory,primary,backup,temp;
    public Action BeforeCommit;
    public CheckpointStore(string directory) {this.directory=directory;primary=Path.Combine(directory,"session.json");backup=primary+".bak";temp=primary+".pending";}
    static string Digest(string value) {using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();}
    public bool Save(T snapshot,out string message) {return Save(snapshot,null,out message);}
    T ReadValid(string path,Func<T,bool> validate) {
        var r=JsonUtility.FromJson<Record>(File.ReadAllText(path,Encoding.UTF8));
        if(r==null||r.schema!=1||r.payload==null||r.sha256!=Digest(r.payload))throw new InvalidDataException("版本或校验不匹配");
        T candidate=JsonUtility.FromJson<T>(r.payload);
        if(candidate==null||(validate!=null&&!validate(candidate)))throw new InvalidDataException("状态合同不合法");
        return candidate;
    }
    public bool Save(T snapshot,Func<T,bool> validate,out string message) {
        try {if(snapshot==null||(validate!=null&&!validate(snapshot)))throw new InvalidDataException("状态合同不合法");Directory.CreateDirectory(directory);string body=JsonUtility.ToJson(snapshot);string data=JsonUtility.ToJson(new Record{schema=1,payload=body,sha256=Digest(body)},true);
            byte[] bytes=Encoding.UTF8.GetBytes(data);using(var f=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough)){f.Write(bytes,0,bytes.Length);f.Flush(true);}
            if(BeforeCommit!=null) BeforeCommit();
            if(File.Exists(primary)) {
                bool valid=false;try{ReadValid(primary,validate);valid=true;}catch{}
                // A damaged primary must never replace the only valid backup.
                File.Replace(temp,primary,valid?backup:null);
            }else File.Move(temp,primary);
            message="已保存完整检查点";return true;
        } catch(Exception e) {message="保存未提交："+e.Message;return false;}
    }
    public bool TryLoad(Func<T,bool> validate,out T state,out string message) {
        string error="未找到保存文件";
        foreach(string p in new[]{primary,backup}) {
            try {if(!File.Exists(p)) continue;T candidate=ReadValid(p,validate);
                state=candidate;message=p==backup?"已从有效备份恢复":"已恢复完整检查点";return true;
            }catch(Exception e){error=e.Message;}
        }
        state=null;message="恢复失败，当前局保留："+error;return false;
    }
}
public static class Ui {
    public static readonly Color Cream=new Color(.94f,.91f,.82f),Ink=new Color(.09f,.18f,.18f),Accent=new Color(.93f,.43f,.27f);
    static Texture2D pixel;public static Font Font;public static GUIStyle Title,Body,Small,Button;
    static void Ensure() {if(pixel!=null)return;pixel=new Texture2D(1,1);pixel.SetPixel(0,0,Color.white);pixel.Apply();Font=Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
        Title=Style(27,Cream,FontStyle.Bold);Body=Style(17,Ink);Small=Style(14,Ink);Button=Style(17,Ink,FontStyle.Bold);Button.alignment=TextAnchor.MiddleCenter;
        Button.normal.background=pixel;Button.hover.background=pixel;Button.active.background=pixel;Button.normal.textColor=Ink;Button.hover.textColor=Accent;Button.active.textColor=Ink; }
    static GUIStyle Style(int size,Color color,FontStyle weight=FontStyle.Normal){return new GUIStyle{font=Font,fontSize=size,fontStyle=weight,wordWrap=true,normal={textColor=color},padding=new RectOffset(8,8,4,4)};}
    public static void Begin(){Ensure();GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(Screen.width/1280f,Screen.height/720f,1));if(Event.current.type==EventType.KeyDown||Event.current.type==EventType.KeyUp)Event.current.Use();}
    public static void Box(Rect r,Color color){Ensure();Color old=GUI.color;GUI.color=color;GUI.DrawTexture(r,pixel);GUI.color=old;}
    public static void Card(Rect r,string title){Box(r,Cream);GUI.Label(new Rect(r.x+8,r.y+6,r.width-16,28),title,Body);}
    public static bool Action(Rect r,string title,bool enabled=true){bool old=GUI.enabled;GUI.enabled=old&&enabled;Color c=GUI.color;GUI.color=GUI.enabled?Cream:new Color(.65f,.65f,.62f);bool b=GUI.Button(r,title,Button);GUI.color=c;GUI.enabled=old;return b;}
    public static void Text(Rect r,string text){Ensure();GUI.Label(r,text,Body);}
    public static void Caption(Rect r,string text){Ensure();GUI.Label(r,text,Small);}
}
public sealed class SoundBank:MonoBehaviour {
    AudioSource source,music;public float Volume=.5f;public bool MusicEnabled=true;
    public void Initialize(){source=gameObject.AddComponent<AudioSource>();source.spatialBlend=0;source.playOnAwake=false;music=gameObject.AddComponent<AudioSource>();music.spatialBlend=0;music.volume=.15f;music.loop=true;music.clip=Resources.Load<AudioClip>("Audio/music");if(music.clip!=null)music.Play();}
    public void SetMusic(bool enabled){MusicEnabled=enabled;if(music!=null)music.mute=!enabled;}
    public void Play(string key){var clip=Resources.Load<AudioClip>("Audio/"+key);if(clip!=null)source.PlayOneShot(clip,Volume);}
}
public sealed class DiagnosticCapture:MonoBehaviour {
    Camera camera;bool once;int frames;public string Product;
    public void Initialize(Camera value,string product){camera=value;Product=product;once=Array.IndexOf(Environment.GetCommandLineArgs(),"-captureOnce")>=0;if(once)Application.runInBackground=true;}
    void Update(){if(!once)return;frames++;if(frames==45)Capture();if(frames>=100)Application.Quit();}
    void Capture(){RenderTexture target=null;Texture2D pixels=null;var previous=camera.targetTexture;var active=RenderTexture.active;
        try{target=RenderTexture.GetTemporary(1600,900,24);camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels=new Texture2D(1600,900,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1600,900),0,0);pixels.Apply();
            string[] args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-captureDirectory");string output=i>=0&&i+1<args.Length?args[i+1]:Application.persistentDataPath;Directory.CreateDirectory(output);string path=Path.Combine(output,Product+"-scene.png");File.WriteAllBytes(path,pixels.EncodeToPNG());Debug.Log("SCENE_RENDER cameraOnly=true inputQA=false path="+path);
        }finally{camera.targetTexture=previous;RenderTexture.active=active;if(target!=null)RenderTexture.ReleaseTemporary(target);if(pixels!=null)Destroy(pixels);}
    }
}
}
