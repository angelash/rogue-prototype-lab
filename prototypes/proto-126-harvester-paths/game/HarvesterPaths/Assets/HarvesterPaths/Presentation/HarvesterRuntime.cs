using System;
using System.IO;
using System.Linq;
using UnityEngine;
using PrototypeKit;
namespace HarvesterPaths {
public sealed class HarvesterRuntime:MonoBehaviour {
    Session session;HarvesterSceneView scene;Camera camera;SoundBank sounds;CheckpointStore<RoundSnapshot> saves;
    int selectedX=3,selectedY=1,amount=1,lastFrame=-1;ModuleKind draftModule;bool help,history,showMoves,quit;
    string actionHint;
    string notice="清晨试验田：先交粮，再决定秸秆怎样帮下一段路。";
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Bootstrap(){if(FindObjectOfType<HarvesterRuntime>()==null)new GameObject("Harvester Paths Runtime").AddComponent<HarvesterRuntime>();}
    void Start(){session=Session.CreatePrototype();saves=new CheckpointStore<RoundSnapshot>(Path.Combine(Application.persistentDataPath,"checkpoints"));camera=new GameObject("Field Camera").AddComponent<Camera>();camera.rect=new Rect(.225f,.14f,.775f,.66f);camera.gameObject.AddComponent<AudioListener>();scene=gameObject.AddComponent<HarvesterSceneView>();scene.Build(camera);sounds=gameObject.AddComponent<SoundBank>();sounds.Initialize();gameObject.AddComponent<DiagnosticCapture>().Initialize(camera,"HarvesterPaths");Debug.Log("PLAYER_READY project=126 rules="+Rules.Version+" font="+(Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular")!=null));}
    bool Claim(){if(lastFrame==Time.frameCount)return false;lastFrame=Time.frameCount;return true;}
    void Update(){
        if(Input.GetKeyDown(KeyCode.F5))ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath,"player-window.png"));
        if(Input.GetKeyDown(KeyCode.Escape)){if(quit)quit=false;else if(help)help=false;else if(history)history=false;else quit=true;return;}
        if(quit)return;if(Input.GetKeyDown(KeyCode.Tab)){help=!help;history=false;}if(Input.GetKeyDown(KeyCode.H)){history=!history;help=false;}if(history&&Input.GetKeyDown(KeyCode.Y))showMoves=!showMoves;if(help||history)return;
        if(Input.GetKeyDown(KeyCode.F6)){Save();return;}if(Input.GetKeyDown(KeyCode.F9)){Load();return;}if(Input.GetKeyDown(KeyCode.R)){Retry();return;}if(Input.GetKeyDown(KeyCode.M))sounds.SetMusic(!sounds.MusicEnabled);
        for(int i=0;i<4;i++)if(Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1+i)))draftModule=(ModuleKind)i;
        if(Input.GetKeyDown(KeyCode.I))selectedY=Math.Min(Rules.Height-1,selectedY+1);if(Input.GetKeyDown(KeyCode.K))selectedY=Math.Max(0,selectedY-1);if(Input.GetKeyDown(KeyCode.J))selectedX=Math.Max(0,selectedX-1);if(Input.GetKeyDown(KeyCode.L))selectedX=Math.Min(Rules.Width-1,selectedX+1);
        if(Input.GetKeyDown(KeyCode.Z))amount=Math.Max(1,amount-1);if(Input.GetKeyDown(KeyCode.X))amount=Math.Min(Rules.BoxCapacity,amount+1);
        if(Input.GetKeyDown(KeyCode.W)||Input.GetKeyDown(KeyCode.UpArrow))Execute(()=>session.MoveForward());if(Input.GetKeyDown(KeyCode.Q))Execute(()=>session.Turn(-1));if(Input.GetKeyDown(KeyCode.E))Execute(()=>session.Turn(1));
        if(Input.GetKeyDown(KeyCode.Space))Execute(()=>session.Harvest());if(Input.GetKeyDown(KeyCode.C))Execute(()=>session.ConvertStraw());if(Input.GetKeyDown(KeyCode.P))Execute(()=>session.Pave(selectedX,selectedY));if(Input.GetKeyDown(KeyCode.Return))Execute(()=>session.Deliver(amount));if(Input.GetKeyDown(KeyCode.U))Execute(()=>session.UnloadStraw(amount));if(Input.GetKeyDown(KeyCode.F))Execute(()=>session.Refuel());if(Input.GetKeyDown(KeyCode.N))Continue();
        if(Input.GetMouseButtonDown(0)&&camera.pixelRect.Contains(Input.mousePosition)&&scene.TryPick(Input.mousePosition,out var x,out var y)){selectedX=x;selectedY=y;}
    }
    void LateUpdate(){if(session!=null)scene.Render(session,selectedX,selectedY);}
    void Execute(Func<CommandResult> command){if(!Claim())return;int oldWallet=session.State.Wallet;var r=command();notice=r.Reason;if(r.Success&&!r.AlreadyApplied){sounds.Play(session.State.Outcome==RoundOutcome.Failed?"miss":session.State.Wallet>oldWallet||session.State.Outcome==RoundOutcome.Completed?"cash":"ui_confirm");Save(false);}else if(!r.Success)sounds.Play("miss");var s=session.State;Debug.Log("GAME_COMMAND project=126 ok="+r.Success+" round="+s.RoundIndex+" actions="+s.ActionsRemaining+" fuel="+s.Vehicle.Fuel+" grain="+s.Vehicle.Grain+" straw="+s.Vehicle.Straw+" delivered="+s.DeliveredTotal+" reason="+r.Reason);}
    void Continue(){Execute(()=>session.ContinueRound(draftModule));}
    bool Save(bool show=true){bool ok=saves.Save(session.ExportSnapshot(),s=>Session.ValidateSnapshot(s,out _),out var message);if(show||!ok)notice=message;Debug.Log("SAVE_CHECKPOINT project=126 ok="+ok+" round="+session.State.RoundIndex);return ok;}
    void Load(){if(!Claim())return;if(saves.TryLoad(s=>Session.ValidateSnapshot(s,out _),out var snapshot,out var message)&&Session.TryRestore(snapshot,out var restored,out _))session=restored;notice=message;Debug.Log("LOAD_CHECKPOINT project=126 round="+session.State.RoundIndex+" message="+message);}
    void Retry(){if(!Claim())return;session=session.Retry();ResetSelection();notice="按原始首轮模块重试三轮。首个合法动作前可F9回旧安全点，之后自动保存更新。";}
    void NewCampaign(){if(!Claim())return;session=Session.CreatePrototype(draftModule);ResetSelection();notice="新试验田已选首轮模块，余下两轮仍在轮界选择。首个合法动作前可F9恢复旧安全点。";}
    void ResetSelection(){selectedX=3;selectedY=1;amount=1;}
    static string Cost(ActionPreview p){return p.ActionCost+"行动 / "+p.FuelCost+"油";}
    void Button(Rect rect,string title,ActionPreview p,Func<CommandResult> command){if(rect.Contains(Event.current.mousePosition))actionHint=title+" · "+Cost(p)+" · "+p.Reason;if(Ui.Action(rect,title+" · "+p.ActionCost,p.Legal))Execute(command);}
    void OnGUI(){if(session==null)return;Ui.Begin();actionHint=null;var s=session.State;var v=s.Vehicle;GUI.enabled=!(help||history||quit);
        Ui.Box(new Rect(0,0,1280,84),Ui.Ink);GUI.Label(new Rect(24,12,620,42),"收割机路径规划 / 清晨试验田",Ui.Title);GUI.Label(new Rect(654,18,610,38),"第"+s.RoundIndex+"轮 · 行动 "+s.ActionsRemaining+" · 油 "+v.Fuel,Ui.Title);
        Ui.Card(new Rect(16,98,256,153),"订单与共享货箱");Ui.Text(new Rect(24,132,240,114),"本轮粮 "+s.DeliveredThisRound+" / "+s.Goal+" · 总交 "+s.DeliveredTotal+"\n粮 "+v.Grain+" + 秸秆 "+v.Straw+" / "+v.Capacity+"\n仓存秸秆 "+s.StoredStraw+" · 现金 "+s.Wallet+"\n"+Rules.ModuleName(v.Module)+" · 朝"+Rules.DirectionName(v.Facing));
        var harvest=session.PreviewHarvest();Ui.Card(new Rect(16,265,256,119),"割台预告");Ui.Caption(new Rect(24,300,240,80),"Space收割：产粮 "+harvest.GrainProduced+" / 秸秆 "+harvest.StrawProduced+"\n需箱格 "+harvest.SpaceRequired+" · "+Cost(harvest)+"\n"+harvest.Reason);
        Button(new Rect(24,397,114,34),"前进 W",session.PreviewMoveForward(),()=>session.MoveForward());Button(new Rect(145,397,114,34),"收割",harvest,()=>session.Harvest());
        Button(new Rect(24,438,114,34),"左转 Q",session.PreviewTurn(-1),()=>session.Turn(-1));Button(new Rect(145,438,114,34),"右转 E",session.PreviewTurn(1),()=>session.Turn(1));
        Button(new Rect(24,479,114,34),"转油 C",session.PreviewConvertStraw(),()=>session.ConvertStraw());Button(new Rect(145,479,114,34),"铺路 P",session.PreviewPave(selectedX,selectedY),()=>session.Pave(selectedX,selectedY));
        Button(new Rect(24,520,114,34),"交粮",session.PreviewDeliver(amount),()=>session.Deliver(amount));Button(new Rect(145,520,114,34),"卸秆 U",session.PreviewUnloadStraw(amount),()=>session.UnloadStraw(amount));
        Button(new Rect(24,561,114,34),"补油 F",session.PreviewRefuel(),()=>session.Refuel());if(Ui.Action(new Rect(145,561,114,34),"规则 Tab"))help=true;
        Ui.Caption(new Rect(24,607,240,83),"交粮/卸秆数量 "+amount+" · Z/X调整\n鼠标或IJKL选铺路格\n选中 ("+(selectedX+1)+","+(selectedY+1)+") · "+session.PreviewPave(selectedX,selectedY).Reason);
        Ui.Caption(new Rect(300,94,592,44),"1–4预选模块："+Rules.ModuleName(draftModule)+" · 开始/换轮生效\n前进 "+Cost(session.PreviewMoveForward())+" · "+session.PreviewMoveForward().Reason);if(Ui.Action(new Rect(924,94,161,33),"新三轮 · 当前模块"))NewCampaign();if(Ui.Action(new Rect(1094,94,167,33),"下一轮 N",session.PreviewContinueRound(draftModule).Legal))Continue();
        Ui.Box(new Rect(288,639,976,65),Ui.Cream);Ui.Text(new Rect(300,643,950,31),actionHint??notice);Ui.Caption(new Rect(300,675,950,25),"F6保存 · F9恢复 · R原配置重试 · H事件 · Tab规则 · M音乐 · F5截图 · Esc退出");
        if(s.Phase==Phase.RoundComplete){Ui.Card(new Rect(506,292,659,194),"本轮交粮完成 · 先维护再换轮");Ui.Text(new Rect(522,337,625,133),"剩余旧预算 "+s.ActionsRemaining+"，只可在仓库卸秸秆/补油。\n1–4选择下一轮模块，N启用下一轮固定预算；\n车辆位置、燃油、货物和已收割/铺过的田块全保留。\n"+session.PreviewContinueRound(draftModule).Reason+"");}
        if(s.Phase==Phase.Finished){Ui.Card(new Rect(506,292,659,184),s.Outcome==RoundOutcome.Completed?"三轮订单完成":"行动截止 · 本次结束");Ui.Text(new Rect(522,337,625,116),"总交粮 "+s.DeliveredTotal+"，现金 "+s.Wallet+"，余油 "+v.Fuel+"。\n剩余作物 "+s.RemainingCrops+"，转油耗秆 "+s.ConvertedStraw+"，铺路耗秆 "+s.PavedStraw+"。\nH查看三轮真实变化，R原模块重试。");}
        GUI.enabled=true;
        if(history){Ui.Card(new Rect(340,155,875,470),"最近事件 · H关闭");if(Ui.Action(new Rect(966,163,231,29),showMoves?"含移动 Y":"仅关键 Y"))showMoves=!showMoves;var es=s.Events.Where(e=>showMoves||(e.Kind!=EventKind.Moved&&e.Kind!=EventKind.Turned)).ToArray();int start=Math.Max(0,es.Length-10);for(int i=start;i<es.Length;i++)Ui.Caption(new Rect(355,199+(i-start)*36,845,34),"轮"+es[i].RoundIndex+" 动作"+es[i].ActionIndex+" · 量"+es[i].Amount+" · 行动−"+es[i].ActionCost+" 油−"+es[i].FuelCost+" · "+es[i].Reason);}
        if(help){Ui.Card(new Rect(340,155,875,470),"清晨作业规则");Ui.Text(new Rect(358,199,835,397),"W/上箭头前进，Q/E转向；先Space收割前格，收过的地永久变普通地。\n普通地"+Rules.MoveActionCost+"行动/"+Rules.MoveFuelCost+"油，泥地"+Rules.MudActionCost+"行动/"+Rules.MudFuelCost+"油；大箱每步另加1油。\n作物每格一次产1粮+1秸秆，共用货箱。整次收割装不下则拒绝，不丢产物。\n"+Rules.ModuleName(ModuleKind.None)+"箱"+Rules.BaseCapacity+"；滚轮箱"+Rules.RollerCapacity+"降低泥耗；大箱"+Rules.BoxCapacity+"，每步多1油。\n宽切头覆盖前方三格，收割"+Rules.WideHarvestActionCost+"行动/"+Rules.WideHarvestFuelCost+"油，转向"+Rules.WideTurnActionCost+"行动/"+Rules.WideTurnFuelCost+"油。\nC用"+Rules.ConvertStrawAmount+"车内秸秆转至多"+Rules.ConvertFuelAmount+"油；P用"+Rules.PaveStrawAmount+"车内秸秆铺相邻泥。\n鼠标/IJKL选格，Z/X改交粮/卸秆数量；Enter交粮每份"+Rules.GrainPrice+"元，U卸秆入仓。\n仓存秸秆在本切片不取回，不能当车上燃料；F在仓库用"+Rules.RefuelPrice+"元补满油。\n上述操作各按预告扣行动；补油/转油绝不加行动预算，油为0也不直接判输。\n三轮交粮目标3/3/4；达标后先用旧余量维护，再N换轮，1–4只在轮界选模块。\n最后一笔交粮先判成功再判行动截止；非法命令不扣资源。\nF6/F9保存恢复完整三轮；Tab/H只读，R原配置重试，Esc保存退出。");}
        if(quit){Ui.Card(new Rect(435,260,540,218),"保存并退出？");Ui.Text(new Rect(451,305,505,85),"保存失败保留窗口，Esc取消。\n"+notice);if(Ui.Action(new Rect(455,408,230,42),"保存并退出")){if(Save())Application.Quit();}if(Ui.Action(new Rect(705,408,240,42),"返回"))quit=false;}
    }
}}
