using System.Collections.Generic;
using System.Linq;
using MagnetShift;
using UnityEngine;

namespace MagnetScavenger
{
    public sealed partial class MagnetRuntime
    {
        static readonly Color Ink = new Color32(12, 27, 37, 255);
        static readonly Color Panel = new Color32(28, 49, 58, 255);
        static readonly Color Edge = new Color32(57, 81, 87, 255);
        static readonly Color Paper = new Color32(239, 229, 200, 255);
        static readonly Color Quiet = new Color32(175, 192, 188, 255);
        static readonly Color Amber = new Color32(255, 196, 98, 255);
        static readonly Color Mint = new Color32(126, 223, 198, 255);
        static readonly Color Brick = new Color32(153, 66, 44, 255);
        readonly Dictionary<int, GUIStyle> styles = new Dictionary<int, GUIStyle>();

        void OnGUI()
        {
            if (game == null) return;
            if (Event.current.type == EventType.KeyDown || Event.current.type == EventType.KeyUp) Event.current.Use();
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            float s = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width-1280*s)/2,(Screen.height-720*s)/2,0), Quaternion.identity, new Vector3(s,s,1));
            GUI.color = Color.white;
            if (page == Page.Ledger || page == Page.End) DrawLedger();
            else
            {
                bool active = page == Page.Work;
                GUI.enabled = active;
                DrawWork();
                GUI.enabled = true;
                if (!active) DrawReset();
            }
            if (storageError != null)
            {
                Fill(15,674,1250,40,Brick);
                Label(30,678,1220,32,storageError,16,Color.white);
            }
            GUI.color = oldColor;
            GUI.matrix = oldMatrix;
        }

        static void Fill(float x,float y,float w,float h,Color color)
        {
            var was = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(x,y,w,h),Texture2D.whiteTexture);
            GUI.color = was;
        }

        void Label(float x,float y,float w,float h,string value,int size,Color color,
                   bool bold=false, TextAnchor align=TextAnchor.MiddleLeft)
        {
            int key = size * 32 + (bold ? 16 : 0) + (int)align;
            if (!styles.TryGetValue(key, out var style))
            {
                style = new GUIStyle {font=font, fontSize=size, alignment=align,
                    fontStyle=bold && size>=28 ? FontStyle.Bold : FontStyle.Normal, wordWrap=true, clipping=TextClipping.Clip};
                styles.Add(key,style);
            }
            style.normal.textColor = color;
            GUI.Label(new Rect(x,y,w,h),value,style);
        }

        bool Button(float x,float y,float w,float h,string title,bool primary=false,bool available=true)
        {
            var rect = new Rect(x,y,w,h);
            bool over = GUI.enabled && rect.Contains(Event.current.mousePosition);
            Color bg = primary ? Amber : Panel;
            if (over) bg = Color.Lerp(bg,Color.white,.14f);
            if (!available) bg = Ink;
            Fill(x,y,w,h,primary ? Amber : Edge);
            Fill(x+1,y+1,w-2,h-2,bg);
            Label(x+3,y,w-6,h,title,17,!available ? Quiet : primary ? Ink : Paper,true,TextAnchor.MiddleCenter);
            return available && GUI.Button(rect,GUIContent.none,GUIStyle.none);
        }

        void DrawWork()
        {
            Fill(0,0,1280,720,Ink);
            if (yard != null) GUI.DrawTexture(new Rect(0,0,1280,105),yard,ScaleMode.ScaleAndCrop);
            Fill(0,0,520,105,new Color(.025f,.10f,.13f,.89f));
            Fill(26,20,5,65,Amber);
            Label(43,13,440,23,"NIGHT SHIFT   /   磁选回收站",14,Mint,true);
            Label(40,33,470,62,"磁铁拾荒者",43,Paper,true);
            if (Button(1120,33,135,40,"工单簿   Esc")) Ledger();
            Fill(0,105,1280,3,Amber);
            board.Draw(new Rect(24,127,795,462),game,font);
            DrawTicket();
            ActionBar();
        }

        string TargetNames()
        {
            var targets = game.Level.Pieces.Where(p => p.Target).GroupBy(p => p.Name);
            return string.Join("、",targets.Select(g => g.Key + (g.Count() == 1 ? "" : " ×" + g.Count())));
        }

        void DrawTicket()
        {
            const int x = 839, w = 417;
            Fill(x,127,w,210,Paper);
            Fill(x,127,7,210,Amber);
            Label(x+22,138,229,26,"夜班工单   /   " + (progress.current+1).ToString("00"),16,Brick,true);
            Label(x+242,138,152,26,"已交  " + game.Delivered + " / " + game.Level.Targets,17,Ink,true,TextAnchor.MiddleRight);
            Fill(x+22,171,w-44,1,new Color32(172,167,144,255));
            Label(x+21,174,w-42,50,game.Level.Title,32,Ink,true);
            Label(x+22,225,w-40,32,"交货    " + TargetNames(),19,Brick,true);
            string brief = game.Level.Brief;
            if (progress.current == 0 && game.Chain.Count > 0)
            {
                var tail = game.Chain[game.Chain.Count-1];
                brief = tail.Target ? "重芯已经挂在链尾。按回车，把它卸进吊车的回收斗。" :
                    "长杆已展开两格。把杆尖对准金标重芯，再吸一次。";
            }
            else if (progress.current == 0 && !game.Won && game.Sold.Count > 0)
                brief = "长杆已经卸走，空磁头够不到井底。撤销卸货，把杆接回来。";
            if (game.Won) brief = "本单的货已入斗。可以接着开工，也可以重接这单，试试更短的路线。";
            Label(x+22,261,w-45,65,brief,17,Ink);
            for (int i=0;i<24;i++) Fill(x+i*18,334,8,3,Ink);

            Fill(x,349,w,77,Panel);
            DrawGauge(x+16,355,180,"剩余电量",game.Remaining,game.Level.Budget,Amber);
            DrawGauge(x+216,355,182,"吊车载重",game.Load,game.Level.Capacity,Mint);

            Fill(x,438,w,151,Panel);
            string lead, detail;
            if (game.Won)
            {
                lead = "工单已交齐";
                detail = "本次 " + game.History.Length + " 步   /   最短路线 " + ShiftLevels.Par(progress.current) + " 步";
            }
            else if (game.Lost)
            {
                lead = "电量耗尽"; detail = "撤回一步，再换条路线。也可以重新开工。";
            }
            else if (game.Chain.Count>0 && game.Bin.All(p => !p.Target))
            {
                var tail = game.Chain[game.Chain.Count-1];
                lead = tail.Target ? "待交货  " + tail.Name : "先卸下" + tail.Name;
                detail = "剩下的订单件都已上钩，从链尾逐件卸货。";
            }
            else
            {
                var pick = game.Preview(ShiftAction.Pulse);
                lead = pick.Piece == null ? "磁头前方" : "瞄准  " + pick.Piece.Name + (pick.Piece.Target ? "  ·  订单件" : "");
                if (pick.Piece != null && pick.Legal)
                {
                    var exit = pick.ExitHeading;
                    string dir = exit.X > 0 ? "右" : exit.X < 0 ? "左" : exit.Y < 0 ? "上" : "下";
                    detail = "接上占 " + pick.Piece.Length + " 格   /   重 +" + pick.Piece.Weight + "   /   磁口向" + dir;
                }
                else detail = pick.Message;
            }
            Label(x+17,444,w-35,37,lead,23,game.Won ? Amber : Mint,true);
            Label(x+17,482,w-32,37,detail,17,Paper);
            Fill(x+17,525,w-34,1,Edge);
            string status = note;
            if (string.IsNullOrEmpty(status)) status = game.Chain.Count > 0
                ? "链尾的" + game.Chain[game.Chain.Count-1].Name + "可以卸进回收斗。"
                : "金色菱标是本单要收的货。";
            Label(x+17,531,w-32,49,status,16,recoveryWarning ? Amber : Quiet);
        }

        void DrawGauge(int x,int y,int w,string name,int amount,int max,Color accent)
        {
            Label(x,y,w,26,name,15,Quiet);
            Label(x,y+14,w,42,amount+" / "+max,24,Paper,true,TextAnchor.MiddleRight);
            Fill(x,y+58,w,4,Ink);
            Fill(x,y+58,w*amount/max,4,accent);
        }

        void ActionBar()
        {
            Fill(0,607,1280,113,new Color32(17,36,46,255));
            Fill(0,607,1280,1,Edge);
            if (game.Won || game.Lost)
            {
                string title = game.Won ? (progress.current+1 == ShiftLevels.All.Count ? "查看交班单" : "接下一单") : "撤回一步";
                if (Button(24,625,295,52,title+"    Enter",true))
                { if (game.Won) Advance(); else Undo(); }
            }
            else
            {
                if (Button(24,625,118,52,"左移   A")) Act(ShiftAction.Left);
                if (Button(150,625,118,52,"右移   D")) Act(ShiftAction.Right);
                var tail = game.Preview(ShiftAction.Unload).Piece;
                bool deliver = tail != null && game.Bin.All(p => !p.Target);
                if (Button(282,625,215,52,"吸取    Space",!deliver)) Act(ShiftAction.Pulse);
                string unload = tail == null ? "卸下链尾" : (tail.Target ? "交付" : "卸下") + tail.Name;
                if (Button(506,625,201,52,unload+"   Enter",deliver)) Act(ShiftAction.Unload);
            }
            if (Button(740,625,146,52,"撤销   Z",false,game.History.Length>0)) Undo();
            if (Button(894,625,146,52,"重开   R")) page = Page.Reset;
            if (Button(1048,625,208,52,"提示一步   H",false,!game.Won && !game.Lost)) Hint();
            Label(25,681,1228,28,"横移、吸取、卸货各耗一格电。撤销退回电量。",15,Quiet);
        }

        void DrawReset()
        {
            Fill(0,0,1280,720,new Color(0,0,0,.78f));
            Fill(381,218,518,255,Paper);
            Fill(381,218,518,7,Amber);
            Label(412,247,455,52,"重开这张工单？",29,Ink,true);
            Label(413,303,450,57,"零件和电量将回到开工时的位置，通关记录保留。",18,Ink);
            if (Button(413,389,200,49,"返回    Esc")) page=Page.Work;
            if (Button(625,389,238,49,"重新开工    Enter",true)) Restart();
        }

        void DrawLedger()
        {
            bool end = page == Page.End;
            Fill(0,0,1280,720,Ink);
            if (yard != null) GUI.DrawTexture(new Rect(0,0,1280,246),yard,ScaleMode.ScaleAndCrop);
            Fill(24,45,441,131,new Color(.025f,.10f,.13f,.90f));
            Fill(24,45,5,131,Amber);
            Label(49,56,398,36,"磁铁拾荒者   /   NIGHT SHIFT",18,Mint,true);
            Label(45,91,408,72,end ? "夜班交班单" : "回收工单簿",44,Paper,true);
            Fill(0,243,1280,4,Amber);
            int complete = progress.records.Count(r => r.Length > 0);
            Label(43,253,725,44,end ? "本班全部交齐" : "选择工单",24,Paper,true);
            Label(813,254,418,43,"已交齐   " + complete + " / " + ShiftLevels.All.Count,20,Amber,true,TextAnchor.MiddleRight);
            for (int i=0;i<ShiftLevels.All.Count;i++)
            {
                int y = 307+i*54;
                bool open = progress.Open(i);
                Fill(42,y,1194,48,i==selected ? Panel : Ink);
                if (i==selected) Fill(42,y,5,48,Mint);
                Fill(42,y+47,1194,1,Edge);
                Label(62,y,70,47,(i+1).ToString("00"),22,open ? Amber : Quiet,true);
                Label(148,y,590,47,ShiftLevels.All[i].Title,21,open ? Paper : Quiet,true);
                string record = progress.records[i];
                string state = record.Length>0 ? "个人 " + record.Length + " 步   /   最短 " + ShiftLevels.Par(i) + " 步" :
                    !open ? "待上一单交货" : progress.histories[i].Length>0 ? "接着开工" : "可以开工";
                Label(793,y,407,47,state,17,open ? Mint : Quiet,false,TextAnchor.MiddleRight);
                if (!end && open && GUI.Button(new Rect(42,y,1194,48),GUIContent.none,GUIStyle.none)) SwitchJob(i);
            }
            if (end)
            {
                if (Button(43,605,308,55,"翻看工单簿    Enter",true)) Ledger();
            }
            else
            {
                if (Button(43,605,308,55,"进入所选工单    Enter",true)) SwitchJob(selected);
                if (Button(368,605,199,55,"返回现场   Esc")) page=Page.Work;
                if (Button(583,605,200,55,progress.muted ? "开启声音   M" : "静音   M")) ToggleSound();
                if (Button(1001,605,235,55,"保存退出   Q")) Quit();
            }
            Label(45,672,1175,32,end ? "所有工单均可重接，个人最少步数会随交货更新。" : "上下键选单，回车进入。每一步都会自动保存。",16,Quiet);
        }
    }
}
