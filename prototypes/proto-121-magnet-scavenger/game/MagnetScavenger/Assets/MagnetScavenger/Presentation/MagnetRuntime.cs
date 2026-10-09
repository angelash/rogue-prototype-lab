using System;
using System.IO;
using System.Linq;
using UnityEngine;
using PrototypeKit;
namespace MagnetScavenger {
public sealed class MagnetRuntime:MonoBehaviour {
    Session session;MagnetSceneView scene;Camera camera;SoundBank sounds;CheckpointStore<RoundSnapshot> saves;
    int selected=2,lastFrame=-1;bool help,history,showMoves,quit,detachPrompt;
    string notice="夜班废料箱：先看唯一末端与首件，再决定货物还是工具。";
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Bootstrap(){if(FindObjectOfType<MagnetRuntime>()==null)new GameObject("Magnet Scavenger Runtime").AddComponent<MagnetRuntime>();}
    void Start(){session=Session.CreatePrototype();saves=new CheckpointStore<RoundSnapshot>(Path.Combine(Application.persistentDataPath,"checkpoints"));camera=new GameObject("Scrap Camera").AddComponent<Camera>();camera.rect=new Rect(.225f,.14f,.775f,.66f);camera.gameObject.AddComponent<AudioListener>();scene=gameObject.AddComponent<MagnetSceneView>();scene.Build(camera);sounds=gameObject.AddComponent<SoundBank>();sounds.Initialize();gameObject.AddComponent<DiagnosticCapture>().Initialize(camera,"MagnetScavenger");Debug.Log("PLAYER_READY project=121 rules="+Rules.Version+" font="+(Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular")!=null));}
    bool Claim(){if(lastFrame==Time.frameCount)return false;lastFrame=Time.frameCount;return true;}
    void Update(){
        if(Input.GetKeyDown(KeyCode.F5))ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath,"player-window.png"));
        if(Input.GetKeyDown(KeyCode.Escape)){if(detachPrompt)detachPrompt=false;else if(quit)quit=false;else if(help)help=false;else if(history)history=false;else quit=true;return;}
        if(quit)return;if(detachPrompt){if(Input.GetKeyDown(KeyCode.Return))ConfirmDetach();return;}
        if(Input.GetKeyDown(KeyCode.Tab)){help=!help;history=false;}if(Input.GetKeyDown(KeyCode.H)){history=!history;help=false;}if(history&&Input.GetKeyDown(KeyCode.Y))showMoves=!showMoves;if(help||history)return;
        if(Input.GetKeyDown(KeyCode.F6)){Save();return;}if(Input.GetKeyDown(KeyCode.F9)){Load();return;}if(Input.GetKeyDown(KeyCode.R)){Reset(session.State.OrderKind);return;}if(Input.GetKeyDown(KeyCode.M))sounds.SetMusic(!sounds.MusicEnabled);
        for(int i=0;i<5;i++)if(Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1+i)))selected=i+1;
        if(Input.GetKeyDown(KeyCode.X)){detachPrompt=true;return;}
        if(Input.GetKeyDown(KeyCode.W)||Input.GetKeyDown(KeyCode.UpArrow))Execute(()=>session.Move(Direction.Up));else if(Input.GetKeyDown(KeyCode.D)||Input.GetKeyDown(KeyCode.RightArrow))Execute(()=>session.Move(Direction.Right));else if(Input.GetKeyDown(KeyCode.S)||Input.GetKeyDown(KeyCode.DownArrow))Execute(()=>session.Move(Direction.Down));else if(Input.GetKeyDown(KeyCode.A)||Input.GetKeyDown(KeyCode.LeftArrow))Execute(()=>session.Move(Direction.Left));
        if(Input.GetKeyDown(KeyCode.Q))Execute(()=>session.Rotate(-1));if(Input.GetKeyDown(KeyCode.E))Execute(()=>session.Rotate(1));if(Input.GetKeyDown(KeyCode.Space))Execute(()=>session.Attract());if(Input.GetKeyDown(KeyCode.K))Execute(()=>session.Retain());if(Input.GetKeyDown(KeyCode.Return))SellSelected();if(Input.GetKeyDown(KeyCode.T))Execute(()=>session.Attach(selected));
        if(Input.GetMouseButtonDown(0)&&camera.pixelRect.Contains(Input.mousePosition)&&scene.TryPick(Input.mousePosition,out var x,out var y)){var item=session.State.Items.FirstOrDefault(v=>v.Location!=ItemLocation.Sold&&v.Location!=ItemLocation.Storage&&v.Cells.Any(c=>c.X==x&&c.Y==y));if(item!=null)selected=item.Id;}
    }
    void LateUpdate(){if(session!=null)scene.Render(session,selected);}
    int SaleId(){return session.State.PendingItemId>0?session.State.PendingItemId:selected;}
    int DetachIndex(){return Array.IndexOf(session.State.ChainIds,selected);}
    void SellSelected(){Execute(()=>session.Sell(SaleId()));}
    void ConfirmDetach(){var p=session.PreviewDetach(DetachIndex());if(!p.Legal){notice=p.Reason;return;}Execute(()=>session.Detach(DetachIndex()));detachPrompt=false;}
    void Execute(Func<CommandResult> command){if(!Claim())return;var r=command();notice=r.Reason;if(r.Success&&!r.AlreadyApplied){sounds.Play(session.State.Outcome==RoundOutcome.Completed?"cash":session.State.Outcome==RoundOutcome.Failed?"miss":"ui_confirm");Save(false);}else if(!r.Success)sounds.Play("miss");var s=session.State;Debug.Log("GAME_COMMAND project=121 ok="+r.Success+" energy="+s.Energy+" wallet="+s.Wallet+" chain="+string.Join(",",s.ChainIds)+" pending="+s.PendingItemId+" reason="+r.Reason);}
    bool Save(bool show=true){bool ok=saves.Save(session.ExportSnapshot(),s=>Session.ValidateSnapshot(s,out _),out var message);if(show||!ok)notice=message;Debug.Log("SAVE_CHECKPOINT project=121 ok="+ok+" energy="+session.State.Energy);return ok;}
    void Load(){if(!Claim())return;if(saves.TryLoad(s=>Session.ValidateSnapshot(s,out _),out var snapshot,out var message)&&Session.TryRestore(snapshot,out var restored,out _))session=restored;notice=message;Debug.Log("LOAD_CHECKPOINT project=121 energy="+session.State.Energy+" message="+message);}
    void Reset(OrderKind order){if(!Claim())return;session=Session.CreatePrototype(order);selected=2;notice="同箱新订单。首次合法动作前可F9回旧安全点，之后自动保存更新。";}
    static string Location(ItemLocation value){switch(value){case ItemLocation.Crate:return "箱内";case ItemLocation.Pending:return "待处置";case ItemLocation.Chain:return "已装链";case ItemLocation.Storage:return "暂存";default:return "已售";}}
    string ItemText(int id){var item=session.State.Items.FirstOrDefault(v=>v.Id==id);return item==null?"无":id+" "+Rules.ItemName(item.Kind)+" / "+Location(item.Location);}
    void OnGUI(){if(session==null)return;Ui.Begin();var s=session.State;bool modal=help||history||quit||detachPrompt;GUI.enabled=!modal;
        Ui.Box(new Rect(0,0,1280,84),Ui.Ink);GUI.Label(new Rect(24,12,620,42),"磁铁拾荒者 / 夜班废料箱",Ui.Title);GUI.Label(new Rect(670,20,590,36),"电量 "+s.Energy+" · 现金 "+s.Wallet,Ui.Title);
        Ui.Card(new Rect(16,98,256,154),Rules.OrderName(s.OrderKind));Ui.Text(new Rect(24,132,240,113),string.Join("\n",s.Order.Requirements.Select(v=>Rules.ItemName(v.Kind)+" "+v.Delivered+" / "+v.Required))+"\n总负载 "+s.TotalWeight+" / "+Rules.MaxLoad+"\n根朝向 "+Rules.DirectionName(s.Facing));
        Ui.Card(new Rect(16,267,256,138),"选中 · "+selected);Ui.Caption(new Rect(24,301,240,96),ItemText(selected)+"\n链："+(s.ChainIds.Length==0?"空":string.Join(",",s.ChainIds))+"\n暂存："+(s.StorageIds.Length==0?"空":string.Join(",",s.StorageIds))+"\n待处置："+(s.PendingItemId>0?s.PendingItemId.ToString():"无"));
        if(Ui.Action(new Rect(24,419,114,34),"吸取 Space",session.PreviewAttract().Legal))Execute(()=>session.Attract());if(Ui.Action(new Rect(145,419,114,34),"保留 K",session.PreviewRetain().Legal))Execute(()=>session.Retain());
        if(Ui.Action(new Rect(24,462,114,34),"出售 Enter",session.PreviewSell(SaleId()).Legal))SellSelected();if(Ui.Action(new Rect(145,462,114,34),"拆尾段 X"))detachPrompt=true;
        if(Ui.Action(new Rect(24,505,114,34),"重装 T",session.PreviewAttach(selected).Legal))Execute(()=>session.Attach(selected));if(Ui.Action(new Rect(145,505,114,34),"规则 Tab"))help=true;
        Ui.Caption(new Rect(24,554,240,133),"1–5或点物件选ID\nWASD/箭头平移 · Q/E整链旋转\n售/拆/装要根在回收区\nPending占格承重，不延伸也不续吸\n非法动作不扣电");
        var attract=session.PreviewAttract();Ui.Caption(new Rect(300,94,680,42),"末端 ("+(s.EndX+1)+","+(s.EndY+1)+") "+Rules.DirectionName(s.EndDirection)+" · 首件 "+(attract.ItemId>0?attract.ItemId.ToString():"无")+" · "+attract.Reason);
        if(Ui.Action(new Rect(994,94,130,33),"零件订单"))Reset(OrderKind.Parts);if(Ui.Action(new Rect(1132,94,130,33),"重件订单"))Reset(OrderKind.Heavy);
        Ui.Box(new Rect(288,639,976,65),Ui.Cream);Ui.Text(new Rect(300,643,950,31),notice);Ui.Caption(new Rect(300,675,950,25),"F6保存 · F9恢复 · R重试 · H事件 · Tab规则 · M音乐 · F5截图 · Esc退出");
        if(s.Phase==Phase.Finished){Ui.Card(new Rect(465,300,650,174),s.Outcome==RoundOutcome.Completed?"夜班订单完成":"电量截止 · 本轮结束");Ui.Text(new Rect(483,345,615,92),"现金 "+s.Wallet+"，余电 "+s.Energy+"。售出的ID永久离开工具链。\nH查看每次首件、几何变化与去向，R同箱重试。");}
        GUI.enabled=true;
        if(detachPrompt){var p=session.PreviewDetach(DetachIndex());Ui.Card(new Rect(345,210,850,330),"拆段预告 · 选中ID"+selected);Ui.Text(new Rect(364,254,810,175),p.Legal?"从选中段及其全部后段进入公开暂存，末端立即缩短。\n受影响ID："+string.Join(",",p.AffectedItemIds)+"\n成本 "+p.Cost+"电 · 新负载 "+p.AfterWeight+"。不留下悬空免费延伸。\n"+p.Reason:p.Reason+"\n取消后选择链内ID，回到回收区再拆。");if(Ui.Action(new Rect(364,469,390,44),"确认拆段 · Enter",p.Legal))ConfirmDetach();if(Ui.Action(new Rect(777,469,395,44),"取消 · Esc"))detachPrompt=false;}
        if(history){Ui.Card(new Rect(340,155,875,470),"最近事件 · H关闭");if(Ui.Action(new Rect(966,163,231,29),showMoves?"含移动 Y":"仅关键 Y"))showMoves=!showMoves;var es=s.Events.Where(e=>showMoves||e.Kind.ToString()!="Moved").ToArray();int start=Math.Max(0,es.Length-10);for(int i=start;i<es.Length;i++)Ui.Caption(new Rect(355,199+(i-start)*36,845,34),"动作"+es[i].ActionIndex+" · ID"+es[i].ItemId+" · ("+(es[i].X+1)+","+(es[i].Y+1)+") · 量"+es[i].Amount+" · "+es[i].Reason);}
        if(help){Ui.Card(new Rect(340,155,875,470),"夜班作业规则");Ui.Text(new Rect(358,199,835,393),"磁头沿唯一末端朝向查射程"+Rules.BaseRange+"格首件；墙或首件过重不能跳过。\nWASD/箭头平移整个根与结构，Q/E旋转整条已装链，全部占格一起检验。\nSpace吸取"+Rules.AttractCost+"电；拾起的Pending占空间与重量，但暂不改变末端。\nK保留"+Rules.RetainCost+"电，才把实际物件加入工具链、改变下一吸起点。\nEnter出售"+Rules.SellCost+"电，在回收区兑现Pending、链尾或选中暂存ID；售出不再提供能力。\n1–5/点物件选ID，X预告并确认拆选中段及后段"+Rules.DetachCost+"电；T重装选中暂存"+Rules.AttachCost+"电。\n移动"+Rules.MoveCost+"电、旋转"+Rules.RotateCost+"电；非法选择整体拒绝，不扣电/删目标。\n同箱两订单可对照；最后合法出售先判目标再判电量。\nF6/F9完整保存恢复，H/Y只读事件，R同箱重试；M音乐，Esc安全退出。\n当前有限候选，所有成本/形状和订单为助手版本化初值。");}
        if(quit){Ui.Card(new Rect(435,260,540,218),"保存并退出？");Ui.Text(new Rect(451,305,505,85),"保存失败保留窗口，Esc取消。\n"+notice);if(Ui.Action(new Rect(455,408,230,42),"保存并退出")){if(Save())Application.Quit();}if(Ui.Action(new Rect(705,408,240,42),"返回"))quit=false;}
    }
}}
