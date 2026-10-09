using System;
using System.IO;
using System.Linq;
using UnityEngine;
using PrototypeKit;
namespace SushiWorkshop {
public sealed class SushiRuntime:MonoBehaviour {
    Session session;SushiSceneView scene;SoundBank sounds;CheckpointStore<RoundSnapshot> saves;
    int selected,moveSource=-1,lastCommandFrame=-1;bool continuous,help,quit;float clock;string notice="先读订单，选择工位。整份：把5号切分搬到1号；小份：把4号加热搬到1号。";
    OrderKind order=OrderKind.WholeHeated;bool history;
    static string OrderStatusName(OrderStatus status){switch(status){case OrderStatus.Waiting:return "等待中";case OrderStatus.Fulfilled:return "已完成";case OrderStatus.Expired:return "已离席";default:return "已弃单";}}
    static string OutcomeName(RoundOutcome value){switch(value){case RoundOutcome.OrderCompleted:return "订单完成";case RoundOutcome.BaseTradeCompleted:return "基础营业达标";default:return "未达成目标";}}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Bootstrap(){if(FindObjectOfType<SushiRuntime>()==null)new GameObject("Sushi Workshop Runtime").AddComponent<SushiRuntime>();}
    void Start(){session=Session.CreatePrototype(order);saves=new CheckpointStore<RoundSnapshot>(Path.Combine(Application.persistentDataPath,"checkpoints"));
        var camera=new GameObject("Workshop Camera").AddComponent<Camera>();camera.rect=new Rect(.225f,.14f,.775f,.78f);camera.gameObject.AddComponent<AudioListener>();
        scene=gameObject.AddComponent<SushiSceneView>();scene.Build(camera);sounds=gameObject.AddComponent<SoundBank>();sounds.Initialize();
        var diagnostic=gameObject.AddComponent<DiagnosticCapture>();diagnostic.Initialize(camera,"SushiWorkshop");Debug.Log("PLAYER_READY project=032 rules="+Rules.Version+" font="+(Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular")!=null));}
    void Update(){
        if(quit){if(Input.GetKeyDown(KeyCode.Escape))quit=false;return;}
        if(Input.GetKeyDown(KeyCode.F5))ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath,"player-window.png"));
        if(Input.GetKeyDown(KeyCode.Escape)){if(help)help=false;else if(history)history=false;else quit=true;continuous=false;return;}
        if(Input.GetKeyDown(KeyCode.Tab)){help=!help;continuous=false;}
        if(help)return;
        if(Input.GetKeyDown(KeyCode.H)){history=!history;continuous=false;}
        if(history)return;
        if(Input.GetKeyDown(KeyCode.S))Save();
        if(Input.GetKeyDown(KeyCode.L))Load();
        if(Input.GetKeyDown(KeyCode.R))Reset(order);
        if(Input.GetKeyDown(KeyCode.P)){continuous=!continuous;clock=0;}
        if(Input.GetKeyDown(KeyCode.M))sounds.SetMusic(!sounds.MusicEnabled);
        for(int i=0;i<6;i++)if(Input.GetKeyDown(KeyCode.Alpha1+i))Select(i);
        var pointer=new Vector2(Input.mousePosition.x*1280f/Screen.width,(Screen.height-Input.mousePosition.y)*720f/Screen.height);
        if(Input.GetMouseButtonDown(0)&&new Rect(288,138,976,491).Contains(pointer)){int hit=scene.RaycastSlot(Input.mousePosition);if(hit>=0)Select(hit);}
        if(Input.GetKeyDown(KeyCode.Return))Execute(()=>session.StartOrResume());
        if(Input.GetKeyDown(KeyCode.Space))Execute(()=>session.Step());
        if(Input.GetKeyDown(KeyCode.I))Execute(()=>session.LoadIngredient());
        if(Input.GetKeyDown(KeyCode.C))Execute(()=>session.WashOne());
        if(continuous){clock+=Time.deltaTime;if(clock>=.8f){clock=0;if(session.State.Phase==Phase.Running)Execute(()=>session.Step());else continuous=false;}}
    }
    void LateUpdate(){scene.Render(session,selected);}
    void Select(int slot){if(moveSource>=0){int source=moveSource;moveSource=-1;Execute(()=>session.MoveStation(source,slot));}else selected=slot;}
    void Execute(Func<CommandResult> command){if(lastCommandFrame==Time.frameCount)return;lastCommandFrame=Time.frameCount;var r=command();notice=r.Reason;
        if(r.Success){sounds.Play(r.Events.Any(x=>x.Kind==EventKind.OrderDelivered||x.Kind==EventKind.BaseSold)?"cash":"ui_confirm");bool saved=Save(false);if(saved&&r.Events.Length>0)notice=r.Events[r.Events.Length-1].Reason;}
        else sounds.Play("miss");var s=session.State;Debug.Log("GAME_COMMAND project=032 ok="+r.Success+" frame="+Time.frameCount+" step="+s.Step+" cash="+s.Wallet+" order="+s.Order.DeliveredQuantity+" phase="+s.Phase+" reason="+r.Reason);}
    void Reset(OrderKind choice){session=Session.CreatePrototype(choice);order=choice;selected=0;moveSource=-1;continuous=false;notice="新一轮："+Rules.OrderName(choice)+"。保存旧局前可使用S；新局第一次合法操作将更新安全点。";}
    bool Save(bool show=true){bool ok=saves.Save(session.ExportSnapshot(),x=>Session.ValidateSnapshot(x,out _),out var result);if(show||!ok)notice=result;Debug.Log("SAVE_CHECKPOINT project=032 ok="+ok+" step="+session.State.Step);return ok;}
    void Load(){if(saves.TryLoad(x=>Session.ValidateSnapshot(x,out _),out var snapshot,out var message)&&Session.TryRestore(snapshot,out var restored,out _)){session=restored;order=snapshot.Order.Kind;continuous=false;moveSource=-1;}notice=message;Debug.Log("LOAD_CHECKPOINT project=032 message="+message+" step="+session.State.Step+" cash="+session.State.Wallet);}
    void OnGUI(){if(session==null)return;Ui.Begin();var s=session.State;
        GUI.enabled=!help&&!quit&&!history;
        Ui.Box(new Rect(0,0,1280,84),Ui.Ink);GUI.Label(new Rect(24,12,660,40),"回转寿司工坊  /  一盘的下一站",Ui.Title);
        GUI.Label(new Rect(710,20,550,34),"资金 "+s.Wallet+"  ·  步数 "+s.Step+" / "+s.StepLimit+"  ·  改站 "+s.AdjustmentsLeft,Ui.Title);
        Ui.Card(new Rect(16,98,256,151),"当前订单 · "+Rules.OrderName(order));
        Ui.Text(new Rect(24,130,240,63),Rules.FoodName(s.Order.RequiredFood)+"  "+s.Order.DeliveredQuantity+" / "+s.Order.RequiredQuantity+"份\n剩余等待 "+s.Order.RemainingWait+"步  ·  单价 "+s.Order.UnitPrice);
        Ui.Caption(new Rect(24,196,240,44),"订单："+OrderStatusName(s.Order.Status)+"  基础营业 "+s.BaseReceipts+" / "+Rules.BaseTradeTarget+"\n净订单回款 "+s.OrderReceipts);
        Ui.Card(new Rect(16,263,256,289),"工位 "+(selected+1)+" · "+Rules.StationName(s.Stations[selected]));
        Ui.Caption(new Rect(24,295,240,42),"干净盘 "+s.CleanPlateCount+"  ·  脏盘 "+s.DirtyPlateCount+"\n环上 "+s.RingPlateCount+"  ·  暂存 "+s.StagedPlateCount);
        if(Ui.Action(new Rect(24,343,114,35),"投米料 -"+Rules.IngredientCost,s.Phase!=Phase.Finished))Execute(()=>session.LoadIngredient());
        if(Ui.Action(new Rect(145,343,114,35),"清洗 -"+Rules.WashCost,s.Phase!=Phase.Finished))Execute(()=>session.WashOne());
        var plate=s.Plates.FirstOrDefault(x=>x.Location==PlateLocation.Ring&&x.Slot==selected);
        if(Ui.Action(new Rect(24,387,114,35),plate!=null&&plate.Deliverable?"标记保留":"标记交付",session.CanPrepare&&plate!=null))Execute(()=>session.SetDeliverable(plate.Id,!plate.Deliverable));
        if(Ui.Action(new Rect(145,387,114,35),"基础出售",session.CanPrepare&&plate!=null))Execute(()=>session.QueueBaseSale(plate.Id,!plate.BaseSaleRequested));
        if(Ui.Action(new Rect(24,431,114,35),moveSource>=0?"取消搬站":"搬站 -"+Rules.MoveStationCost,session.CanPrepare)){moveSource=moveSource>=0?-1:selected;notice=moveSource>=0?"请选择目标槽：数字1–6或点击台面。成功才扣费。":"已取消";}
        if(Ui.Action(new Rect(145,431,114,35),"转卖 +"+Rules.SellStationPrice,session.CanPrepare))Execute(()=>session.SellStation(selected));
        for(int i=1;i<=4;i++){int kind=i;if(Ui.Action(new Rect(24+(i-1)%2*121,475+(i-1)/2*33,114,28),Rules.StationName((StationKind)i)+" -"+Rules.BuyStationCost,session.CanPrepare&&s.Stations[selected]==StationKind.None))Execute(()=>session.BuyStation(selected,(StationKind)kind));}
        Ui.Card(new Rect(16,564,256,135),"节拍与复盘");
        if(Ui.Action(new Rect(24,600,114,34),s.Phase==Phase.Running?"单步 Space":"开轮 Enter"))Execute(()=>s.Phase==Phase.Running?session.Step():session.StartOrResume());
        if(Ui.Action(new Rect(145,600,114,34),continuous?"暂停 P":"连续 P")){continuous=!continuous;clock=0;}
        if(Ui.Action(new Rect(24,642,114,34),"弃单 -"+Rules.AbandonOrderCost,session.CanPrepare))Execute(()=>session.AbandonOrder());
        if(Ui.Action(new Rect(145,642,114,34),"重试 R"))Reset(order);
        Ui.Box(new Rect(288,639,976,65),Ui.Cream);Ui.Text(new Rect(300,643,950,31),moveSource>=0?"正在搬站：源 "+(moveSource+1)+" → 请选择目标。":notice);
        Ui.Caption(new Rect(300,675,950,26),"1–6 / 点击选槽 · S保存 · L恢复 · H事件复盘 · Tab帮助 · M音乐 · F5截图 · Esc退出");
        if(Ui.Action(new Rect(1020,94,120,33),"整份对照"))Reset(OrderKind.WholeHeated);
        if(Ui.Action(new Rect(1144,94,120,33),"小份对照"))Reset(OrderKind.QuickSmall);
        if(s.Phase==Phase.Finished){Ui.Card(new Rect(486,290,570,165),"本轮结束 · "+OutcomeName(s.Outcome));Ui.Text(new Rect(504,337,530,76),"订单回款 "+s.OrderReceipts+"，基础营业 "+s.BaseReceipts+"，资金 "+s.Wallet+"。\n对照两类订单；观察同样工位为什么需要搬动。H查看事件原因，可重试或读取安全点。");}
        GUI.enabled=true;
        if(history){Ui.Card(new Rect(340,155,880,465),"最近关键事件 · H关闭（运行已暂停）");var events=s.Events.Where(x=>x.Kind==EventKind.FoodProcessed||x.Kind==EventKind.FoodSplit||x.Kind==EventKind.SplitBlocked||x.Kind==EventKind.OrderRejected||x.Kind==EventKind.OrderDeparted||x.Kind==EventKind.OrderDelivered).ToArray();int start=Math.Max(0,events.Length-10);for(int i=start;i<events.Length;i++){var e=events[i];Ui.Caption(new Rect(358,197+(i-start)*36,845,34),"第"+e.Step+"步 · 盘"+(e.PlateId>=0?(e.PlateId+1).ToString():"—")+" / 槽"+(e.Slot>=0?(e.Slot+1).ToString():"—")+" · "+e.Reason);}if(events.Length==0)Ui.Text(new Rect(358,210,830,44),"尚无加工、切分、拒收或交付事件。");}
        if(help){Ui.Card(new Rect(346,166,860,410),"料理、盘子与订单");Ui.Text(new Rect(366,211,820,330),"每次传送先移动，再按工位加工，再交付，最后扣等待。\n米基底 → 加料 → 卷制 → 成卷；加热得到熟卷，切分得到两小份。\n交付整份熟卷：先把5号切分搬到1号，投料，连续或单步。\n快速小份卷：先把4号加热搬到1号，投料，连续或单步。\n每绕一圈停下，可改变盘标记/改站；单步之间也可投料/清洗。\n保留盘不会被顾客取走；基础出售收益低，可解决缺站坏局面。\n切分要有额外干净盘，新盘进入暂存，下一步才入环。\n安全点保留全部状态和已提交动作；恢复不重新发奖。\nTab关闭帮助。当前为首轮纵切，不包含Steam平台接入。");}
        if(quit){Ui.Card(new Rect(440,260,530,218),"退出工坊？");Ui.Text(new Rect(455,305,500,90),"合法操作已有安全点；保存失败将保留当前窗口。Esc取消。\n"+notice);if(Ui.Action(new Rect(465,410,230,40),"保存并退出")){if(Save())Application.Quit();}if(Ui.Action(new Rect(710,410,230,40),"返回"))quit=false;}
    }
}
}
