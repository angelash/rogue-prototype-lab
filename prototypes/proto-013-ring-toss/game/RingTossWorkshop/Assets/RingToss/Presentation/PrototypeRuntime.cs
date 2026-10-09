using System;
using System.Collections.Generic;
using System.IO;
using RingToss.Application;
using RingToss.Core;
using UnityEngine;

namespace RingToss.Presentation
{
    public sealed class PrototypeRuntime : MonoBehaviour
    {
        private StageSession session;
        private Flight3D replay;
        private StallSceneView view;
        private readonly List<Vector3> trail = new List<Vector3>();
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private AudioSource sound;
        private Font font;
        private GUIStyle label, title, button, small;
        private double azimuth, elevation = 45, speed = 5.6, accumulated;
        private int selected, transaction, observedEvents, lastCommandFrame = -1;
        private bool paused, slow, anglePicker;
        private bool showTools, captureOnce;
        private int captureFrames;
        private double pendingAngle;
        private string notice;
        private const float Width = 1280, Height = 720;
        private static readonly Color Ink = new Color(.97f, .94f, .85f);
        private static readonly Color Panel = new Color(.055f, .09f, .13f, .92f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<PrototypeRuntime>() == null)
                new GameObject("Ring Toss 3D").AddComponent<PrototypeRuntime>();
        }
        private void Awake()
        {
            captureOnce = Array.IndexOf(Environment.GetCommandLineArgs(), "-ringCaptureOnce") >= 0;
            if (captureOnce) UnityEngine.Application.runInBackground = true;
            font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            view = new StallSceneView(font);
            sound = gameObject.AddComponent<AudioSource>(); sound.playOnAwake = false;
            foreach (var id in new[] { "launch", "hit", "bounce", "cash", "retain", "miss", "ui_confirm" })
            {
                var clip = Resources.Load<AudioClip>("Audio/" + id); if (clip != null) clips.Add(id, clip);
            }
            ResetStage();
            Debug.Log("RING_PLAYER_READY mode=3D font=" + (font != null) + " audio=" + clips.Count);
        }
        private void ResetStage()
        {
            session = StageSession.CreatePrototype3D(Guid.NewGuid().ToString("N"));
            replay = null; trail.Clear(); paused = false; anglePicker = false; showTools = false; accumulated = 0;
            observedEvents = 0; transaction = 0;
            notice = "桥脚第一摊：向前套中旧物，兑现赚回款，或留下改成机关。";
        }
        private void Play(string id) { if (clips.TryGetValue(id, out var clip)) sound.PlayOneShot(clip, .55f); }
        private bool Dispatch(Func<string, CommandResult> command, string success, string audio = "ui_confirm")
        {
            if (lastCommandFrame == Time.frameCount) return false;
            lastCommandFrame = Time.frameCount;
            string tx = session.StageId + ":ui:" + (++transaction);
            var result = command(tx);
            notice = result.Success ? success : result.Message;
            Debug.Log("RING_COMMAND frame=" + Time.frameCount + " tx=" + tx + " success=" + result.Success +
                " phase=" + session.Phase + " rings=" + session.RingsRemaining + " wallet=" + session.Wallet +
                " receipts=" + session.StageReceipts + " message=" + notice);
            if (result.Success && !result.AlreadyApplied) Play(audio);
            return result.Success && !result.AlreadyApplied;
        }
        private void Launch()
        {
            if (paused || replay != null || anglePicker) return;
            var input = new ThrowInput3D(azimuth, elevation, speed);
            if (!Dispatch(tx => session.Launch3D(input, tx), "圈已向前抛出，等待落点。", "launch")) return;
            showTools = false;
            Debug.Log("RING_THROW3D azimuth=" + input.AzimuthDegrees + " elevation=" + input.ElevationDegrees + " speed=" + input.Speed);
            trail.Clear(); observedEvents = 0; accumulated = 0;
        }
        private void OpenOrEnd()
        {
            if (session.Phase == StagePhase.Preparation) Dispatch(session.OpenStage, "开摊！瞄准前方奖品再抛圈。");
            else Dispatch(session.EndStage, session.StageReceipts >= session.Target ? "达标！这一摊完成了。" : "本摊结束，可重新练习。");
        }
        private void Replay()
        {
            if (session.Phase == StagePhase.Flying || session.Phase == StagePhase.AwaitingDisposition) return;
            if (session.LastFlight3D == null) { notice = "先完成一次投掷。"; return; }
            replay = session.CreateReplayLastThrow3D(); trail.Clear(); accumulated = 0; paused = false;
            notice = "上一投回放：不领奖、不扣圈，不改变机关耐久。";
            Debug.Log("RING_REPLAY3D started rings=" + session.RingsRemaining + " wallet=" + session.Wallet);
        }
        private void RotateSelected()
        {
            var item = session.Slots[selected];
            if (item == null || item.Occupancy != Occupancy.Mechanism || session.Phase == StagePhase.Flying ||
                session.Phase == StagePhase.AwaitingDisposition || session.Phase == StagePhase.Ended)
            { notice = "先选择可以调整的留场机关。"; return; }
            pendingAngle = item.AngleDegrees; anglePicker = true;
        }
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F5)) Capture();
            if (!anglePicker)
            {
                if (Input.GetKeyDown(KeyCode.P)) paused = !paused;
                if (Input.GetKeyDown(KeyCode.Tab)) showTools = !showTools;
                if (Input.GetKeyDown(KeyCode.F2) && session.Phase != StagePhase.Flying) ResetStage();
            }
            if (!paused && replay == null && !anglePicker)
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow)) azimuth = Math.Max(-30, azimuth - .25);
                if (Input.GetKeyDown(KeyCode.RightArrow)) azimuth = Math.Min(30, azimuth + .25);
                if (Input.GetKeyDown(KeyCode.UpArrow)) elevation = Math.Min(75, elevation + .25);
                if (Input.GetKeyDown(KeyCode.DownArrow)) elevation = Math.Max(20, elevation - .25);
                if (Input.GetKeyDown(KeyCode.Q)) speed = Math.Max(4, speed - .02);
                if (Input.GetKeyDown(KeyCode.E)) speed = Math.Min(12, speed + .02);
                if (Input.GetKeyDown(KeyCode.Space)) Launch();
                if (Input.GetKeyDown(KeyCode.Return)) OpenOrEnd();
                if (Input.GetKeyDown(KeyCode.C)) Dispatch(session.Cash, "已兑现奖品，回款增加。", "cash");
                if (Input.GetKeyDown(KeyCode.R)) Dispatch(session.Retain, "旧物留场，下一投可借它改变路线。", "retain");
                if (Input.GetKeyDown(KeyCode.T)) RotateSelected();
                if (!anglePicker)
                {
                if (Input.GetKeyDown(KeyCode.X)) Dispatch(tx => session.Salvage(selected, tx), "已按公开残值拆卸。", "cash");
                for (int i = 0; i < 6; i++)
                    if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)))
                    {
                        int destination = i;
                        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                            Dispatch(tx => session.Slots[destination] == null ? session.Move(selected, destination, tx) :
                                session.Swap(selected, destination, tx), "机关位置已调整。");
                        else selected = i;
                    }
                var guiPoint = GuiPoint(Input.mousePosition);
                if (Input.GetMouseButtonDown(0) && guiPoint.x < 930 && guiPoint.y > 100 && guiPoint.y < 505)
                { int pick = view.Pick(Input.mousePosition); if (pick >= 0) selected = pick; }
                }
            }
            var flight = replay ?? session.ActiveFlight3D;
            if (!paused && flight != null && flight.State == FlightState.Flying)
            {
                accumulated += Time.unscaledDeltaTime * (slow ? .5 : 1);
                while (accumulated >= Rules.Dt && flight.State == FlightState.Flying)
                {
                    if (replay != null) replay.Step(); else session.Step();
                    accumulated -= Rules.Dt; trail.Add(ToUnity(flight.Position));
                    if (replay == null)
                        while (observedEvents < flight.Events.Count)
                        {
                            var evt = flight.Events[observedEvents++];
                            if (evt.Kind == PhysicsEventKind.PrizeHit) { Play("hit"); notice = "套中了！兑现拿回款，还是留下改造？"; }
                            else if (evt.Kind == PhysicsEventKind.Miss) { Play("miss"); notice = "这一投没套中，试着调整方向和力度。"; }
                            else if (evt.Kind == PhysicsEventKind.BoardBounce) { Play("bounce"); notice = "反弹板接住了圈，路线发生改变。"; }
                            else notice = "风扇吹动了圈，路线发生改变。";
                            Debug.Log("RING_PHYSICS3D " + evt.Kind + " t=" + evt.Time.ToString("F6") + " at=" + evt.Position.X + "," + evt.Position.Y + "," + evt.Position.Z);
                        }
                }
                if (replay != null && replay.State != FlightState.Flying)
                {
                    replay = null; notice = "回放结束，正式圈数、钱包和耐久未变。";
                    Debug.Log("RING_REPLAY3D finished rings=" + session.RingsRemaining + " wallet=" + session.Wallet);
                }
            }
            var displayed = replay ?? session.ActiveFlight3D ?? session.LastFlight3D;
            view.Render(replay != null ? replay.Slots : session.Slots, displayed == null ? ToUnity(Rules3D.LaunchPoint) : ToUnity(displayed.Position),
                trail, selected, session.Phase == StagePhase.Flying || replay != null, azimuth, elevation, speed);
            // Supplemental visual evidence only; this never issues gameplay commands.
            if (captureOnce && ++captureFrames == 30) CaptureSceneOnly();
            if (captureOnce && captureFrames == 90) UnityEngine.Application.Quit();
        }
        private static Vector3 ToUnity(Double3 p) { return new Vector3((float)p.X, (float)p.Y, (float)p.Z); }
        private static Vector2 GuiPoint(Vector3 p)
        {
            float scale = Math.Min(Screen.width / Width, Screen.height / Height);
            return new Vector2((p.x - (Screen.width - Width * scale) / 2) / scale,
                (Screen.height - p.y - (Screen.height - Height * scale) / 2) / scale);
        }
        private void Capture()
        {
            string directory = Path.Combine(UnityEngine.Application.persistentDataPath, "Screenshots");
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-ringEvidence") directory = Path.GetFullPath(args[i + 1]);
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "ring-toss-3d-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".png");
            ScreenCapture.CaptureScreenshot(path); Debug.Log("RING_SCREENSHOT " + path);
        }
        private void Styles()
        {
            if (label != null) return;
            label = new GUIStyle(GUI.skin.label) { font = font, fontSize = 18, wordWrap = true };
            label.normal.textColor = Ink;
            title = new GUIStyle(label) { fontSize = 30, fontStyle = FontStyle.Bold };
            small = new GUIStyle(label) { fontSize = 15 };
            button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 17 };
        }
        private void CaptureSceneOnly()
        {
            string directory = Path.Combine(UnityEngine.Application.persistentDataPath, "Screenshots");
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-ringEvidence") directory = Path.GetFullPath(args[i + 1]);
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "ring-toss-3d-scene-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".png");
            var target = RenderTexture.GetTemporary(1600, 900, 24, RenderTextureFormat.ARGB32);
            var oldTarget = view.Camera.targetTexture; var oldActive = RenderTexture.active;
            var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            try
            {
                view.Camera.targetTexture = target; view.Camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                Debug.Log("RING_SCENE_RENDER " + path + " cameraOnly=true inputQA=false");
            }
            finally
            {
                view.Camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
                RenderTexture.ReleaseTemporary(target); Destroy(pixels);
            }
        }
        private static void Fill(Rect r, Color c) { var old = GUI.color; GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = old; }
        private void OnGUI()
        {
            Styles();
            // Keyboard commands have one owner; a focused IMGUI button cannot fire again.
            if (Event.current.type == EventType.KeyDown || Event.current.type == EventType.KeyUp) Event.current.Use();
            float scale = Math.Min(Screen.width / Width, Screen.height / Height);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - Width * scale) / 2, (Screen.height - Height * scale) / 2, 0), Quaternion.identity, Vector3.one * scale);
            Fill(new Rect(24, 18, 910, 75), Panel);
            GUI.Label(new Rect(42, 28, 430, 42), "桥脚 · 套圈改造摊", title);
            GUI.Label(new Rect(43, 68, 700, 23), "夜市第一摊  /  站在线后，向前套住旧物", small);
            GUI.Label(new Rect(550, 35, 345, 28), "钱包 " + session.Wallet + "    回款 " + session.StageReceipts + " / " + session.Target, label);
            if (showTools || session.Phase == StagePhase.AwaitingDisposition)
            {
            Fill(new Rect(950, 18, 306, 667), Panel);
            GUI.Label(new Rect(970, 36, 270, 34), PhaseName(), label);
            GUI.Label(new Rect(970, 81, 270, 28), "普通圈 " + session.RingsRemaining + "   调整 " + session.AdjustmentsRemaining, label);
            GUI.Label(new Rect(970, 125, 265, 74), "套中后可兑现回款，也可留下旧物改造路线。先选下面的槽位。", small);
            GUI.enabled = !anglePicker;
            for (int i = 0; i < 6; i++)
            {
                var item = session.Slots[i];
                string text = (i + 1) + " " + (item == null ? "空槽" : item.Kind == ObjectKind.Fan ? "台扇" : "板材");
                if (item != null) text += "\n" + (item.Occupancy == Occupancy.Prize ? item.PrizeValue + "币" : "耐久 " + item.Durability);
                var r = new Rect(968 + i % 3 * 92, 214 + i / 3 * 74, 85, 66);
                if (i == selected) Fill(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), new Color(.9f, .68f, .35f));
                if (GUI.Button(r, text, button)) selected = i;
            }
            GUI.enabled = !paused && replay == null && !anglePicker;
            if (GUI.Button(new Rect(968, 373, 130, 37), "兑现 [C]", button)) Dispatch(session.Cash, "奖品已兑现。", "cash");
            if (GUI.Button(new Rect(1106, 373, 130, 37), "留场 [R]", button)) Dispatch(session.Retain, "奖品已留场成为机关。", "retain");
            if (GUI.Button(new Rect(968, 421, 85, 33), "转向", button)) RotateSelected();
            if (GUI.Button(new Rect(1060, 421, 85, 33), "开关", button)) Dispatch(tx => session.Toggle(selected, tx), "开关已调整。");
            if (GUI.Button(new Rect(1152, 421, 84, 33), "拆卸", button)) Dispatch(tx => session.Salvage(selected, tx), "机关已按残值拆卸。", "cash");
            if (GUI.Button(new Rect(968, 466, 130, 33), "补2圈 / 18", button)) Dispatch(session.BuyRescue, "已购买两个补救普通圈。");
            GUI.enabled &= session.Phase != StagePhase.Preparation;
            if (GUI.Button(new Rect(1106, 466, 130, 33), "结束本摊", button)) OpenOrEnd();
            GUI.enabled = !paused && replay == null && !anglePicker;
            if (GUI.Button(new Rect(968, 510, 130, 33), "上一投回放", button)) Replay();
            if (GUI.Button(new Rect(1106, 510, 130, 33), "重开测试", button) && session.Phase != StagePhase.Flying) ResetStage();
            GUI.enabled = !anglePicker;
            if (GUI.Button(new Rect(968, 554, 130, 33), paused ? "继续 [P]" : "暂停 [P]", button)) paused = !paused;
            if (GUI.Button(new Rect(1106, 554, 130, 33), slow ? "速度 0.5×" : "速度 1×", button)) slow = !slow;
            GUI.enabled = true;
            if (GUI.Button(new Rect(968, 598, 268, 33), "保存画面", button)) Capture();
            GUI.Label(new Rect(970, 643, 264, 28), "1–6选槽 · T转向 · X拆卸", small);
            }
            else
            {
                Fill(new Rect(950, 18, 306, 153), Panel);
                GUI.enabled = !anglePicker;
                if (GUI.Button(new Rect(968, 34, 268, 34), "选槽 / 改造 [Tab]", button)) showTools = true;
                GUI.Label(new Rect(970, 77, 270, 26), PhaseName() + " · 剩" + session.RingsRemaining + "圈", small);
                GUI.enabled = true;
                if (GUI.Button(new Rect(968, 119, 130, 33), "保存画面", button)) Capture();
                GUI.enabled = !anglePicker;
                if (GUI.Button(new Rect(1106, 119, 130, 33), paused ? "继续 [P]" : "暂停 [P]", button)) paused = !paused;
            }
            Fill(new Rect(24, 571, 910, 114), Panel);
            GUI.enabled = !paused && replay == null && !anglePicker;
            GUI.Label(new Rect(43, 587, 264, 26), "方向 " + azimuth.ToString("F2") + "°", label);
            azimuth = Math.Round(GUI.HorizontalSlider(new Rect(45, 624, 235, 20), (float)azimuth, -30, 30) * 4) / 4;
            GUI.Label(new Rect(317, 587, 250, 26), "仰角 " + elevation.ToString("F2") + "°", label);
            elevation = Math.Round(GUI.HorizontalSlider(new Rect(319, 624, 225, 20), (float)elevation, 20, 75) * 4) / 4;
            GUI.Label(new Rect(583, 587, 175, 26), "力度 " + speed.ToString("F2"), label);
            speed = Math.Round(GUI.HorizontalSlider(new Rect(585, 624, 158, 20), (float)speed, 4, 12) * 50) / 50;
            if (GUI.Button(new Rect(775, 592, 138, 60), session.Phase == StagePhase.Preparation ? "开摊 [Enter]" : "抛圈 [空格]", button))
            { if (session.Phase == StagePhase.Preparation) OpenOrEnd(); else Launch(); }
            GUI.Label(new Rect(43, 652, 710, 23), "←→左右瞄准  ↑↓仰角  Q/E力度  Shift+1–6移动/交换", small);
            GUI.enabled = true;
            Fill(new Rect(24, 506, 910, 51), new Color(.055f, .09f, .13f, .8f));
            GUI.Label(new Rect(42, 517, 868, 32), notice, label);
            if (anglePicker) DrawAnglePicker();
            GUI.enabled = true; GUI.matrix = Matrix4x4.identity;
        }
        private void DrawAnglePicker()
        {
            var item = session.Slots[selected];
            if (item == null || item.Occupancy != Occupancy.Mechanism) { anglePicker = false; return; }
            Fill(new Rect(365, 218, 472, 260), Panel);
            GUI.Label(new Rect(385, 231, 440, 30), "预选朝向，确认后才消耗一次调整", label);
            if (item.Kind == ObjectKind.Fan)
            {
                var angles = new[] { -30.0, 0, 30, 150, 180, 210 };
                for (int i = 0; i < angles.Length; i++)
                    if (GUI.Button(new Rect(385 + i % 3 * 145, 278 + i / 3 * 42, 135, 34),
                        (pendingAngle == angles[i] ? "● " : "") + angles[i] + "°", button)) pendingAngle = angles[i];
            }
            else
            {
                GUI.Label(new Rect(385, 281, 440, 28), "板面 " + pendingAngle + "°", label);
                pendingAngle = Math.Round(GUI.HorizontalSlider(new Rect(389, 326, 419, 22), (float)pendingAngle, 15, 165) / 5) * 5;
            }
            GUI.Label(new Rect(385, 372, 440, 28), "准备阶段免费，开摊后有两次调整。", small);
            if (GUI.Button(new Rect(385, 419, 205, 36), "确认", button) && Dispatch(tx => session.Rotate(selected, pendingAngle, tx), "机关朝向已确认。")) anglePicker = false;
            if (GUI.Button(new Rect(610, 419, 205, 36), "取消", button)) anglePicker = false;
        }
        private string PhaseName()
        {
            if (paused) return "暂时休息";
            if (replay != null) return "上一投回放";
            switch (session.Phase)
            {
                case StagePhase.Preparation: return "准备 · 摆摊开张";
                case StagePhase.Flying: return "抛圈 · 看准落点";
                case StagePhase.AwaitingDisposition: return "套中 · 兑现或改造";
                case StagePhase.Ended: return session.IsSuccess ? "达标 · 这一摊完成" : "本摊已结束";
                default: return session.RingsRemaining > 0 ? "瞄准 · 向前抛圈" : "圈用完 · 清算或补救";
            }
        }
    }
}
