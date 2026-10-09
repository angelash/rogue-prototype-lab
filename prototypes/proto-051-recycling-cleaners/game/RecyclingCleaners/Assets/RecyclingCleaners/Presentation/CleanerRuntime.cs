using System;
using System.IO;
using System.Linq;
using UnityEngine;
using PrototypeKit;
namespace RecyclingCleaners {
public sealed class CleanerRuntime:MonoBehaviour {
    Session session;CleanerSceneView scene;SoundBank sounds;CheckpointStore<RoundSnapshot> saves;Camera camera;
    int targetX,targetY,lastCommandFrame=-1;bool help,history,historyMoves,quit;string notice="先清洁近处；碎屑装桶，水渍擦洗，顽渍可慢擦或用清洁剂。";
    LayoutKind layout=LayoutKind.Compact;int capacity=Rules.SmallBucketCapacity;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Bootstrap(){if(FindObjectOfType<CleanerRuntime>()==null)new GameObject("Recycling Cleaners Runtime").AddComponent<CleanerRuntime>();}
    void Start(){session=Session.CreatePrototype(layout,capacity,false);saves=new CheckpointStore<RoundSnapshot>(Path.Combine(Application.persistentDataPath,"checkpoints"));
        camera=new GameObject("Room Camera").AddComponent<Camera>();camera.rect=new Rect(.225f,.14f,.775f,.66f);camera.gameObject.AddComponent<AudioListener>();
        scene=gameObject.AddComponent<CleanerSceneView>();scene.Build(camera);sounds=gameObject.AddComponent<SoundBank>();sounds.Initialize();
        var capture=gameObject.AddComponent<DiagnosticCapture>();capture.Initialize(camera,"RecyclingCleaners");Debug.Log("PLAYER_READY project=051 rules="+Rules.Version+" font="+(Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular")!=null));}
    void Update(){
        if(Input.GetKeyDown(KeyCode.F5))ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath,"player-window.png"));
        if(Input.GetKeyDown(KeyCode.Escape)){if(quit)quit=false;else if(help)help=false;else if(history)history=false;else quit=true;return;}
        if(quit)return;
        if(Input.GetKeyDown(KeyCode.Tab)){help=!help;history=false;}
        if(Input.GetKeyDown(KeyCode.H)){history=!history;help=false;}
        if(history&&Input.GetKeyDown(KeyCode.Y))historyMoves=!historyMoves;
        if(help||history)return;
        if(Input.GetKeyDown(KeyCode.F6)){Save();return;}if(Input.GetKeyDown(KeyCode.F9)){Load();return;}if(Input.GetKeyDown(KeyCode.R)){Retry();return;}
        if(Input.GetKeyDown(KeyCode.M))sounds.SetMusic(!sounds.MusicEnabled);
        if(Input.GetKeyDown(KeyCode.W))Move(0,1);else if(Input.GetKeyDown(KeyCode.S))Move(0,-1);else if(Input.GetKeyDown(KeyCode.A))Move(-1,0);else if(Input.GetKeyDown(KeyCode.D))Move(1,0);
        if(Input.GetKeyDown(KeyCode.UpArrow))targetY=Math.Min(Rules.Size-1,targetY+1);if(Input.GetKeyDown(KeyCode.DownArrow))targetY=Math.Max(0,targetY-1);
        if(Input.GetKeyDown(KeyCode.LeftArrow))targetX=Math.Max(0,targetX-1);if(Input.GetKeyDown(KeyCode.RightArrow))targetX=Math.Min(Rules.Size-1,targetX+1);
        if(Input.GetMouseButtonDown(0)&&camera.pixelRect.Contains(Input.mousePosition)&&scene.TryPick(Input.mousePosition,out var x,out var y)){targetX=x;targetY=y;}
        if(Input.GetKeyDown(KeyCode.Space))Execute(()=>session.Vacuum(targetX,targetY));
        if(Input.GetKeyDown(KeyCode.F))Execute(()=>session.Scrub(targetX,targetY));if(Input.GetKeyDown(KeyCode.G))Execute(()=>session.UseDetergent(targetX,targetY));
        if(Input.GetKeyDown(KeyCode.Alpha1))Execute(()=>session.SetBrush(BrushKind.Narrow));if(Input.GetKeyDown(KeyCode.Alpha2))Execute(()=>session.SetBrush(BrushKind.Wide));
        if(Input.GetKeyDown(KeyCode.T))Execute(()=>session.SetFilter(!session.State.FilterEnabled));if(Input.GetKeyDown(KeyCode.V))Execute(()=>session.Convert());
        if(Input.GetKeyDown(KeyCode.E))Execute(()=>session.Sell());if(Input.GetKeyDown(KeyCode.X))Execute(()=>session.Discard(MaterialKind.Ordinary,session.State.Ordinary));
        if(Input.GetKeyDown(KeyCode.Z))Execute(()=>session.Discard(MaterialKind.Convertible,session.State.Convertible));if(Input.GetKeyDown(KeyCode.Q))Execute(()=>session.Restock());
    }
    void LateUpdate(){if(session!=null)scene.Render(session,targetX,targetY);}
    void Move(int dx,int dy){Execute(()=>session.Move(dx,dy));var s=session.State;targetX=s.PlayerX;targetY=s.PlayerY;}
    void Execute(Func<CommandResult> command){if(lastCommandFrame==Time.frameCount)return;lastCommandFrame=Time.frameCount;var r=command();notice=r.Reason;
        if(r.Success&&!r.AlreadyApplied){sounds.Play(r.Events.Any(e=>e.Kind==EventKind.MaterialSold)?"cash":"ui_confirm");bool saved=Save(false);if(saved&&r.Events.Length>0)notice=r.Events[r.Events.Length-1].Reason;}else if(!r.Success)sounds.Play("miss");
        var s=session.State;Debug.Log("GAME_COMMAND project=051 ok="+r.Success+" frame="+Time.frameCount+" actions="+s.ActionsRemaining+" dirt="+s.RemainingDirt+" ordinary="+s.Ordinary+" convertible="+s.Convertible+" detergent="+s.Detergent+" cash="+s.Wallet+" reason="+r.Reason);}
    bool Save(bool show=true){bool ok=saves.Save(session.ExportSnapshot(),s=>Session.ValidateSnapshot(s,out _),out var message);if(show||!ok)notice=message;Debug.Log("SAVE_CHECKPOINT project=051 ok="+ok+" actions="+session.State.ActionsRemaining);return ok;}
    bool BeginControl(){if(lastCommandFrame==Time.frameCount)return false;lastCommandFrame=Time.frameCount;return true;}
    void Load(){if(!BeginControl())return;if(saves.TryLoad(s=>Session.ValidateSnapshot(s,out _),out var snapshot,out var message)&&Session.TryRestore(snapshot,out var restored,out _)){session=restored;layout=snapshot.Layout;capacity=snapshot.BucketCapacity;targetX=snapshot.PlayerX;targetY=snapshot.PlayerY;}notice=message;Debug.Log("LOAD_CHECKPOINT project=051 actions="+session.State.ActionsRemaining+" dirt="+session.State.RemainingDirt+" message="+message);}
    void Reset(LayoutKind choice,int bucket){if(!BeginControl())return;layout=choice;capacity=bucket;session=Session.CreatePrototype(choice,bucket,false);targetX=targetY=0;notice="新配置：滤芯初始关闭。第一次合法动作前可F9回旧局；之后自动保存更新检查点。";}
    void Retry(){if(!BeginControl())return;session=session.Retry();targetX=targetY=0;notice="同一初始配置的新一轮。第一次合法动作前可F9回旧检查点。";}
    void OnGUI(){if(session==null)return;Ui.Begin();var s=session.State;GUI.enabled=!help&&!history&&!quit;
        Ui.Box(new Rect(0,0,1280,84),Ui.Ink);GUI.Label(new Rect(24,12,610,42),"回收保洁队 / 清晨的下一次清洁",Ui.Title);
        GUI.Label(new Rect(650,20,615,36),"行动 "+s.ActionsRemaining+"  ·  耐久 "+s.Durability+"  ·  现金 "+s.Wallet,Ui.Title);
        Ui.Card(new Rect(16,98,256,139),"房间清洁 · "+s.CleanedUnits+" / "+(s.CleanedUnits+s.RemainingDirt));
        Ui.Text(new Rect(24,131,240,78),"桶 "+s.BucketUsed+" / "+s.BucketCapacity+"\n普通料 "+s.Ordinary+"  ·  可转料 "+s.Convertible+"\n清洁剂 "+s.Detergent+" / "+Rules.DetergentCapacity);
        Ui.Card(new Rect(16,251,256,105),"目标 ("+(targetX+1)+","+(targetY+1)+")");var cell=s.Cells[targetY*Rules.Size+targetX];
        Ui.Text(new Rect(24,283,240,63),Rules.DirtName(cell.Remaining==0?DirtKind.None:cell.Dirt)+" · 余量 "+cell.Remaining+"\n刷头 "+(s.Brush==BrushKind.Narrow?"窄刷":"广刷")+"  滤芯 "+(s.FilterEnabled?"开":"关")+" / "+s.FilterChargesRemaining+"次");
        if(Ui.Action(new Rect(24,371,114,34),"吸取 Space",s.Phase==Phase.Active))Execute(()=>session.Vacuum(targetX,targetY));
        if(Ui.Action(new Rect(145,371,114,34),"擦洗 F",s.Phase==Phase.Active))Execute(()=>session.Scrub(targetX,targetY));
        if(Ui.Action(new Rect(24,414,114,34),"用清洁剂 G",s.Phase==Phase.Active))Execute(()=>session.UseDetergent(targetX,targetY));
        if(Ui.Action(new Rect(145,414,114,34),"转换 V",s.Phase==Phase.Active))Execute(()=>session.Convert());
        if(Ui.Action(new Rect(24,457,114,34),"窄刷 1",s.Phase==Phase.Active))Execute(()=>session.SetBrush(BrushKind.Narrow));
        if(Ui.Action(new Rect(145,457,114,34),"广刷 2",s.Phase==Phase.Active))Execute(()=>session.SetBrush(BrushKind.Wide));
        if(Ui.Action(new Rect(24,500,114,34),s.FilterEnabled?"关滤芯 T":"开滤芯 T",s.Phase==Phase.Active))Execute(()=>session.SetFilter(!s.FilterEnabled));
        if(Ui.Action(new Rect(145,500,114,34),"出售 E",s.Phase==Phase.Active))Execute(()=>session.Sell());
        if(Ui.Action(new Rect(24,543,114,34),"弃普通 X",s.Phase==Phase.Active))Execute(()=>session.Discard(MaterialKind.Ordinary,s.Ordinary));
        if(Ui.Action(new Rect(145,543,114,34),"弃可转 Z",s.Phase==Phase.Active))Execute(()=>session.Discard(MaterialKind.Convertible,s.Convertible));
        if(Ui.Action(new Rect(24,586,235,34),"补耐久 Q · 现金 "+Rules.RestockCost,s.Phase==Phase.Active))Execute(()=>session.Restock());
        Ui.Caption(new Rect(24,628,240,58),"换头/出售/丢弃/补给：回左前补给点\nWASD移动 · 方向键选格 · Tab规则");
        if(Ui.Action(new Rect(724,94,130,33),"小桶对照"))Reset(layout,Rules.SmallBucketCapacity);
        if(Ui.Action(new Rect(860,94,130,33),"大桶对照"))Reset(layout,Rules.LargeBucketCapacity);
        if(Ui.Action(new Rect(996,94,130,33),"近区布局"))Reset(LayoutKind.Compact,capacity);
        if(Ui.Action(new Rect(1132,94,130,33),"远区布局"))Reset(LayoutKind.Separated,capacity);
        Ui.Box(new Rect(288,639,976,65),Ui.Cream);Ui.Text(new Rect(300,643,950,31),notice);Ui.Caption(new Rect(300,675,950,25),"F6保存 · F9恢复 · R重试 · H事件复盘 · M音乐 · F5截图 · Esc退出");
        if(s.Phase==Phase.Finished){Ui.Card(new Rect(465,300,650,175),s.Outcome==RoundOutcome.Completed?"房间清洁完成":"本轮结束 · 查看未完成原因");Ui.Text(new Rect(483,345,615,87),"清洁 "+s.CleanedUnits+"单位，剩余 "+s.RemainingDirt+"；普通/可转料出售 "+s.SoldOrdinary+" / "+s.SoldConvertible+"。\n转换 "+s.Conversions+"次，用剂 "+s.DetergentUsed+"，剩余现金 "+s.Wallet+"。H复盘，R重试。");}
        GUI.enabled=true;
        if(history){Ui.Card(new Rect(340,155,875,470),"最近事件 · H或Esc关闭");if(Ui.Action(new Rect(966,163,231,29),historyMoves?"含路线 Y":"仅关键 Y"))historyMoves=!historyMoves;var events=s.Events.Where(e=>historyMoves||e.Kind!=EventKind.Moved).ToArray();int start=Math.Max(0,events.Length-10);for(int i=start;i<events.Length;i++){var e=events[i];Ui.Caption(new Rect(355,199+(i-start)*36,845,34),"行动"+e.ActionIndex+" · ("+(e.X+1)+","+(e.Y+1)+") · "+e.Reason);}if(events.Length==0)Ui.Text(new Rect(355,204,830,45),"尚无清洁/转换/工具事件。");}
        if(help){Ui.Card(new Rect(340,155,875,470),"清晨回收小队 · 规则与操作");Ui.Text(new Rect(358,199,835,393),"WASD移动；点地砖或方向键选目标，Space吸碎屑，F擦洗水渍/顽渍。\n目标必须在身边一格内；窄刷精取，广刷覆盖十字区域，费用更高。\n满桶只装得下的部分，未装污物仍留在地上；各料共享桶。\n滤芯："+Rules.RecipeInput+"可转料 → 1剂，槽容量"+Rules.DetergentCapacity+"，最多"+Rules.FilterCapacity+"次。吸取先用已有料转化，再接新料。\nG用剂加速顽渍；不合适的格拒绝，不吞清洁剂。\n补给点在左前(1,1)：1/2换头，E出售，X/Z丢对应材料，Q付现金补耐久。\n出售普通/可转料单价"+Rules.OrdinarySalePrice+" / "+Rules.ConvertibleSalePrice+"，同一料不能再转换。\n行动与耐久均有限；最后有效清洁先完成，再判断耗尽。\n顶部对照按钮重置独立房间；每个合法动作后自动保存完整安全点。\nF6保存/F9恢复；H看原因，Tab或Esc关帮助。当前为有限可玩候选。");}
        if(quit){Ui.Card(new Rect(435,260,540,218),"保存并退出？");Ui.Text(new Rect(451,305,505,85),"保存失败会保留窗口，可返回重试。Esc取消。\n"+notice);if(Ui.Action(new Rect(455,408,230,42),"保存并退出")){if(Save())Application.Quit();}if(Ui.Action(new Rect(705,408,240,42),"返回"))quit=false;}
    }
}
}
