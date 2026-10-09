using System;
using System.IO;
using System.Linq;
using UnityEngine;
using PrototypeKit;
namespace SnakeHatchery {
public sealed class SnakeRuntime:MonoBehaviour {
    Session session;SnakeSceneView scene;Camera camera;SoundBank sounds;CheckpointStore<RoundSnapshot> saves;
    int cut=Rules.MinMainSegments,cloneId,lastFrame=-1;bool help,history,showMoves,quit,splitPrompt,reclaimPrompt;CloneBehavior behavior;GridPoint[] draft=new GridPoint[0];
    string notice="先看身体与地面模块；移动会成长，撞墙或身体会结束本轮。";
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Bootstrap(){if(FindObjectOfType<SnakeRuntime>()==null)new GameObject("Snake Hatchery Runtime").AddComponent<SnakeRuntime>();}
    void Start(){session=Session.CreatePrototype();saves=new CheckpointStore<RoundSnapshot>(Path.Combine(Application.persistentDataPath,"checkpoints"));
        camera=new GameObject("Hatchery Camera").AddComponent<Camera>();camera.rect=new Rect(.225f,.14f,.775f,.66f);camera.gameObject.AddComponent<AudioListener>();
        scene=gameObject.AddComponent<SnakeSceneView>();scene.Build(camera);sounds=gameObject.AddComponent<SoundBank>();sounds.Initialize();
        gameObject.AddComponent<DiagnosticCapture>().Initialize(camera,"SnakeHatchery");Debug.Log("PLAYER_READY project=027 rules="+Rules.Version+" font="+(Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular")!=null));}
    bool Claim(){if(lastFrame==Time.frameCount)return false;lastFrame=Time.frameCount;return true;}
    void Update(){
        if(Input.GetKeyDown(KeyCode.F5))ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath,"player-window.png"));
        if(Input.GetKeyDown(KeyCode.Escape)){if(splitPrompt)CancelSplit();else if(reclaimPrompt)reclaimPrompt=false;else if(quit)quit=false;else if(help)help=false;else if(history)history=false;else quit=true;return;}
        if(quit)return;if(splitPrompt){if(Input.GetKeyDown(KeyCode.Return))ConfirmSplit();return;}if(reclaimPrompt){if(Input.GetKeyDown(KeyCode.Return))ConfirmReclaim();return;}
        if(Input.GetKeyDown(KeyCode.Tab)){help=!help;history=false;}if(Input.GetKeyDown(KeyCode.H)){history=!history;help=false;}
        if(history&&Input.GetKeyDown(KeyCode.Y))showMoves=!showMoves;if(help||history)return;
        if(Input.GetKeyDown(KeyCode.F6)){Save();return;}if(Input.GetKeyDown(KeyCode.F9)){Load();return;}if(Input.GetKeyDown(KeyCode.R)){Reset(session.State.Preset);return;}
        if(Input.GetKeyDown(KeyCode.M))sounds.SetMusic(!sounds.MusicEnabled);
        if(Input.GetKeyDown(KeyCode.Z))cut=Math.Max(Rules.MinMainSegments,cut-1);if(Input.GetKeyDown(KeyCode.X))cut=Math.Min(Math.Max(Rules.MinMainSegments,session.State.Main.Length-1),cut+1);
        if(Input.GetKeyDown(KeyCode.Alpha1))SelectClone(0);if(Input.GetKeyDown(KeyCode.Alpha2))SelectClone(1);
        if(Input.GetKeyDown(KeyCode.C)){PrepareSplit(CloneBehavior.PatrolCollect);return;}if(Input.GetKeyDown(KeyCode.V)){PrepareSplit(CloneBehavior.Shuttle);return;}
        if(Input.GetKeyDown(KeyCode.W)||Input.GetKeyDown(KeyCode.UpArrow))Execute(()=>session.Move(Direction.Up));else if(Input.GetKeyDown(KeyCode.D)||Input.GetKeyDown(KeyCode.RightArrow))Execute(()=>session.Move(Direction.Right));else if(Input.GetKeyDown(KeyCode.S)||Input.GetKeyDown(KeyCode.DownArrow))Execute(()=>session.Move(Direction.Down));else if(Input.GetKeyDown(KeyCode.A)||Input.GetKeyDown(KeyCode.LeftArrow))Execute(()=>session.Move(Direction.Left));
        if(Input.GetKeyDown(KeyCode.Space))Execute(()=>session.Wait());if(Input.GetKeyDown(KeyCode.E))Execute(()=>session.Collect());if(Input.GetKeyDown(KeyCode.Return))Execute(()=>session.Deliver());
        if(Input.GetKeyDown(KeyCode.Q)){reclaimPrompt=true;return;}if(Input.GetKeyDown(KeyCode.G))PickModule();if(Input.GetKeyDown(KeyCode.B))DropModule();
        if(Input.GetMouseButtonDown(0)&&camera.pixelRect.Contains(Input.mousePosition)&&scene.TryPick(Input.mousePosition,out var x,out var y)){
            var s=session.State;var c=s.Clones.FirstOrDefault(v=>v.HeadX==x&&v.HeadY==y);if(c!=null)cloneId=c.Id;
            else {int i=Array.FindIndex(s.Main.Segments,v=>v.X==x&&v.Y==y);if(i>=Rules.MinMainSegments)cut=i;}
        }
    }
    void LateUpdate(){if(session==null)return;cut=Math.Min(Math.Max(Rules.MinMainSegments,session.State.Main.Length-1),cut);scene.SetDraftRoute(splitPrompt?draft:null);scene.Render(session,cut,cloneId);}
    static string Kind(SegmentKind k){return k==SegmentKind.Basic?"基":k==SegmentKind.Collector?"采":"仓";}
    static string Behavior(CloneBehavior b){return Rules.BehaviorName(b);}
    void SelectClone(int index){var c=session.State.Clones.OrderBy(v=>v.Id).ToArray();cloneId=index<c.Length?c[index].Id:0;}
    void PrepareSplit(CloneBehavior choice){var s=session.State;behavior=choice;draft=cut<s.Main.Length?Rules.SuggestedRoute(s.Main.Segments[cut].X,s.Main.Segments[cut].Y,choice):new GridPoint[0];splitPrompt=true;}
    void CancelSplit(){splitPrompt=false;draft=new GridPoint[0];}
    void ConfirmSplit(){if(!session.PreviewSplit(cut,behavior,draft).Legal){notice=session.PreviewSplit(cut,behavior,draft).Reason;return;}Execute(()=>session.Split(cut,behavior,draft));CancelSplit();}
    void ConfirmReclaim(){var preview=session.PreviewReclaim(cloneId);if(!preview.Legal){notice=preview.Reason;return;}Execute(()=>session.Reclaim(cloneId));reclaimPrompt=false;}
    void PickModule(){var s=session.State;var m=s.Modules.Where(v=>v.Location==ModuleLocation.Ground&&Math.Abs(v.X-s.Main.HeadX)+Math.Abs(v.Y-s.Main.HeadY)<=1).OrderBy(v=>Math.Abs(v.X-s.Main.HeadX)+Math.Abs(v.Y-s.Main.HeadY)).ThenBy(v=>v.Id).FirstOrDefault();if(m==null){notice="身边没有地面模块；G只收进库存，不免费装身。";return;}Execute(()=>session.CollectModule(m.Id));}
    void DropModule(){var ids=session.State.InventoryModuleIds;if(ids.Length==0){notice="没有库存模块。";return;}Execute(()=>session.DropModule(ids.Min()));}
    void Execute(Func<CommandResult> command){if(!Claim())return;var r=command();notice=r.Reason;if(r.Success&&!r.AlreadyApplied){var outcome=session.State.Outcome;sounds.Play(outcome==RoundOutcome.Completed?"cash":outcome==RoundOutcome.Failed?"miss":"ui_confirm");Save(false);}else if(!r.Success)sounds.Play("miss");var s=session.State;Debug.Log("GAME_COMMAND project=027 ok="+r.Success+" actions="+s.ActionsRemaining+" length="+s.Main.Length+" clones="+s.Clones.Length+" cargo="+s.Main.Cargo+" delivered="+s.Delivered+" reason="+r.Reason);}
    bool Save(bool show=true){bool ok=saves.Save(session.ExportSnapshot(),s=>Session.ValidateSnapshot(s,out _),out var message);if(show||!ok)notice=message;Debug.Log("SAVE_CHECKPOINT project=027 ok="+ok+" actions="+session.State.ActionsRemaining);return ok;}
    void Load(){if(!Claim())return;if(saves.TryLoad(s=>Session.ValidateSnapshot(s,out _),out var snapshot,out var message)&&Session.TryRestore(snapshot,out var restored,out _)){session=restored;cut=Rules.MinMainSegments;cloneId=0;}notice=message;Debug.Log("LOAD_CHECKPOINT project=027 actions="+session.State.ActionsRemaining+" message="+message);}
    void Reset(PresetKind preset){if(!Claim())return;session=Session.CreatePrototype(preset);cut=Rules.MinMainSegments;cloneId=0;notice="同图新一轮。第一次合法动作前可F9返回旧安全点；之后自动保存更新。";}
    void OnGUI(){if(session==null)return;Ui.Begin();var s=session.State;bool modal=help||history||quit||splitPrompt||reclaimPrompt;GUI.enabled=!modal;
        Ui.Box(new Rect(0,0,1280,84),Ui.Ink);GUI.Label(new Rect(24,12,635,42),"贪吃蛇孵化场 / 玻璃棚的接力",Ui.Title);GUI.Label(new Rect(672,20,590,36),"行动 "+s.ActionsRemaining+" · 交付 "+s.Delivered+" / "+s.DeliveryGoal,Ui.Title);
        Ui.Card(new Rect(16,98,256,174),"主蛇 · "+s.Main.Length+"节");Ui.Text(new Rect(24,132,240,127),"货物 "+s.Main.Cargo+" / "+s.Main.Capacity+"\n每次采集 "+s.Main.CollectionRate+" · 分身 "+s.Clones.Length+" / "+Rules.MaxClones+"\n身体："+string.Join(" ",s.Main.Segments.Select(v=>Kind(v.Kind)+v.ModuleId))+"\n库存："+(s.InventoryModuleIds.Length==0?"空":string.Join(",",s.InventoryModuleIds)));
        Ui.Card(new Rect(16,287,256,100),"断尾 · 保留前 "+cut+"节");Ui.Caption(new Rect(24,319,240,58),"Z/X选切点，C/V查看路线与能力损失\n确认才花行动；新分身下步出发");
        if(Ui.Action(new Rect(24,402,114,34),"巡线 C",s.Phase==Phase.Active))PrepareSplit(CloneBehavior.PatrolCollect);if(Ui.Action(new Rect(145,402,114,34),"往返 V",s.Phase==Phase.Active))PrepareSplit(CloneBehavior.Shuttle);
        if(Ui.Action(new Rect(24,445,114,34),"采货 E",s.Phase==Phase.Active))Execute(()=>session.Collect());if(Ui.Action(new Rect(145,445,114,34),"交付 Enter",s.Phase==Phase.Active))Execute(()=>session.Deliver());
        if(Ui.Action(new Rect(24,488,114,34),"等待 Space",s.Phase==Phase.Active))Execute(()=>session.Wait());if(Ui.Action(new Rect(145,488,114,34),"回收预告 Q",s.Phase==Phase.Active))reclaimPrompt=true;
        if(Ui.Action(new Rect(24,531,114,34),"拾模块 G",s.Phase==Phase.Active))PickModule();if(Ui.Action(new Rect(145,531,114,34),"放模块 B",s.Phase==Phase.Active))DropModule();
        var selected=s.Clones.FirstOrDefault(v=>v.Id==cloneId);Ui.Caption(new Rect(24,578,240,105),selected==null?"1/2或点分身头选择回收对象\nWASD/箭头移动；撞击会失败\n基/采/仓：基础/采集/货仓节":"小蛇ID"+selected.Id+" · "+Behavior(selected.Behavior)+"\n货物 "+selected.Cargo+" / "+selected.Capacity+"\n"+(selected.WaitingReason??""));
        if(Ui.Action(new Rect(994,94,130,33),"短蛇对照"))Reset(PresetKind.Short);if(Ui.Action(new Rect(1132,94,130,33),"长蛇对照"))Reset(PresetKind.Long);
        var directions=new[]{Direction.Up,Direction.Right,Direction.Down,Direction.Left};for(int i=0;i<directions.Length;i++){var p=session.PreviewMove(directions[i]);Ui.Caption(new Rect(300+i*168,95,165,33),Rules.DirectionName(directions[i])+"："+(s.Phase==Phase.Finished?"本轮结束":p.Legal?(p.WillGrow?"成长 / 尾留":"可走"):"碰撞会失败"));}
        Ui.Box(new Rect(288,639,976,65),Ui.Cream);Ui.Text(new Rect(300,643,950,31),notice);Ui.Caption(new Rect(300,675,950,25),"F6保存 · F9恢复 · R重试 · H事件 · Tab规则 · M音乐 · F5截图 · Esc退出");
        if(s.Phase==Phase.Finished){Ui.Card(new Rect(465,300,650,175),s.Outcome==RoundOutcome.Completed?"接力交付完成":"本轮结束 · H看原因");Ui.Text(new Rect(483,345,615,87),"交付 "+s.Delivered+" / "+s.DeliveryGoal+"，余行动 "+s.ActionsRemaining+"。\n地面、库存与每条蛇的实际模块均保留在安全点。R重试不同路线。");}
        GUI.enabled=true;
        if(splitPrompt){var p=session.PreviewSplit(cut,behavior,draft);Ui.Card(new Rect(345,180,850,406),"断尾预览 · "+Behavior(behavior));Ui.Text(new Rect(364,225,809,237),p.Legal?"保留主蛇 "+p.MainLengthAfter+"节 / 分身 "+p.CloneLength+"节；成本 "+p.Cost+"行动。\n主蛇容量变为 "+p.MainCapacityAfter+"，失去采集 "+p.LostCollectionRate+" / 容量 "+p.LostCapacity+"。\n货物转分身 "+p.TransferredCargo+"，留地面 "+p.DroppedCargo+"；模块全部转移原有ID。\n路线："+string.Join(" → ",draft.Select(v=>"("+(v.X+1)+","+(v.Y+1)+")"))+"\n"+p.Reason:p.Reason+"\n取消后调整切点或先回收分身。");if(Ui.Action(new Rect(364,510,390,44),"确认断尾 · Enter",p.Legal))ConfirmSplit();if(Ui.Action(new Rect(777,510,395,44),"取消 · Esc"))CancelSplit();}
        if(reclaimPrompt){var p=session.PreviewReclaim(cloneId);var c=s.Clones.FirstOrDefault(v=>v.Id==cloneId);Ui.Card(new Rect(345,180,850,406),"回收预告 · 小蛇ID"+cloneId);Ui.Text(new Rect(364,225,809,237),p.Legal?"成本 "+p.Cost+"全局行动；终止当前分身任务，其他分身继续公开一步。\n从头到尾先入库存 "+p.ModulesToInventory+"个模块 / 其余原节格留下 "+p.DroppedModules+"；主蛇不会免费接长。\n货物给主蛇 "+p.TransferredCargo+" / 留在头格("+(p.DropX+1)+","+(p.DropY+1)+") "+p.DroppedCargo+"。\n实际模块原位置："+(c==null?"无":string.Join("，",c.Segments.Select(v=>v.ModuleId+"@("+(v.X+1)+","+(v.Y+1)+")")))+"\n"+p.Reason:p.Reason+"\n取消后靠近并选择现有分身。");if(Ui.Action(new Rect(364,510,390,44),"确认回收 · Enter",p.Legal))ConfirmReclaim();if(Ui.Action(new Rect(777,510,395,44),"取消 · Esc"))reclaimPrompt=false;}
        if(history){Ui.Card(new Rect(340,155,875,470),"最近事件 · H关闭");if(Ui.Action(new Rect(966,163,231,29),showMoves?"含路线 Y":"仅关键 Y"))showMoves=!showMoves;var es=s.Events.Where(e=>showMoves||(e.Kind!=EventKind.Moved&&e.Kind!=EventKind.CloneMoved&&e.Kind!=EventKind.Waited)).ToArray();int start=Math.Max(0,es.Length-10);for(int i=start;i<es.Length;i++)Ui.Caption(new Rect(355,199+(i-start)*36,845,34),"行动"+es[i].ActionIndex+" · 实体"+es[i].EntityId+" · ("+(es[i].X+1)+","+(es[i].Y+1)+") · 量"+es[i].Amount+" · "+es[i].Reason);}
        if(help){Ui.Card(new Rect(340,155,875,470),"玻璃棚的接力 · 操作与规则");Ui.Text(new Rect(358,199,835,393),"WASD/箭头单步移动。身体按顺序跟随；确认撞墙/身体会失败。\n移动吃入地面模块会增长，基础/采集/货仓节同时占格并提供对应能力。\nE在货物点采货，Enter在交付点交货；没有分身也能完成基础目标。\nZ/X选择保留节数，C巡线/V往返打开预告，再Enter确认，Esc取消。\n分身使用可见短路径，主蛇优先，冲突者等待；新分身下步才行动。\n1/2或点分身头选择，Q相邻回收；尾段实际模块和溢出货物有公开去向。\nG拾身边地面模块到有限库存，B放库存最低ID到主蛇格；不会免费装身。\n每个合法命令都推进一全局行动，其它分身同时尝试公开任务；无效选择不耗步。\nF6/F9保存恢复；H只读事件，Y含路线；R同配置重试，顶部可切短长对照。\n所有ID唯一归属，恢复不重采集/孵化/交付；当前为有限单关候选。");}
        if(quit){Ui.Card(new Rect(435,260,540,218),"保存并退出？");Ui.Text(new Rect(451,305,505,85),"保存失败会保留窗口，可返回重试。Esc取消。\n"+notice);if(Ui.Action(new Rect(455,408,230,42),"保存并退出")){if(Save())Application.Quit();}if(Ui.Action(new Rect(705,408,240,42),"返回"))quit=false;}
    }
}}
