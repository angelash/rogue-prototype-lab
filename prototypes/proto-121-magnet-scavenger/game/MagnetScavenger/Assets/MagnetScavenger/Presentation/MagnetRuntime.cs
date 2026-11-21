using System;
using System.IO;
using MagnetShift;
using PrototypeKit;
using UnityEngine;

namespace MagnetScavenger
{
    public sealed partial class MagnetRuntime : MonoBehaviour
    {
        enum Page { Work, Ledger, Reset, End }
        ShiftProgress progress;
        ShiftGame game;
        readonly ShiftBoard board = new ShiftBoard();
        CheckpointStore<ShiftProgress> store;
        SoundBank sound;
        Font font;
        Texture2D yard;
        Page page;
        int selected, lastActionFrame = -1;
        string note, storageError;
        bool recoveryWarning;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindObjectOfType<MagnetRuntime>() == null)
                new GameObject("Night Shift").AddComponent<MagnetRuntime>();
        }

        void Start()
        {
            Application.targetFrameRate = 60;
            var camera = new GameObject("Press bed").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Ink;
            camera.cullingMask = 0;
            camera.gameObject.AddComponent<AudioListener>();
            font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            yard = Resources.Load<Texture2D>("NightYard");
            sound = gameObject.AddComponent<SoundBank>();
            sound.Initialize();
            bool practice = Array.IndexOf(Environment.GetCommandLineArgs(), "-shiftPractice") >= 0;
            string path = Path.Combine(Application.persistentDataPath, practice ? "shift-practice-v1" : "night-shift-v1");
            store = new CheckpointStore<ShiftProgress>(path);
            if (store.TryLoad(ShiftProgress.Valid, out progress, out var message))
            {
                if (message.Contains("备份")) note = "已从上一次有效备份接班。";
            }
            else
            {
                progress = ShiftProgress.Fresh();
                if (Directory.Exists(path))
                {
                    note = "进度无法读取。已从首单开工。";
                    recoveryWarning = true;
                }
            }
            OpenJob(progress.current);
            AudioListener.volume = progress.muted ? 0 : 1;
            Debug.Log("SHIFT_READY level=" + game.Level.Id + " history=" + game.History +
                      " poster=" + (yard != null) + " font=" + (font != null));
        }

        void OpenJob(int index)
        {
            progress.current = selected = index;
            if (!ShiftGame.TryRestore(ShiftLevels.All[index], progress.histories[index], out game))
                throw new InvalidDataException("工单动作记录无效");
            board.Reset();
            page = Page.Work;
        }

        void Save()
        {
            progress.Record(game);
            if (!store.Save(progress, ShiftProgress.Valid, out var message))
            {
                storageError = message;
                Debug.LogWarning("SHIFT_SAVE " + message);
            }
            else storageError = null;
        }

        void Act(ShiftAction action)
        {
            if (page != Page.Work || lastActionFrame == Time.frameCount) return;
            lastActionFrame = Time.frameCount;
            var result = game.Act(action);
            note = result.Message;
            if (!result.Legal)
            {
                sound.Play("miss");
                return;
            }
            board.Notify(game, action, result);
            string cue = game.Won ? "cash" : game.Lost ? "miss" : action == ShiftAction.Pulse ? "hit" :
                action == ShiftAction.Unload ? "retain" : "ui_confirm";
            sound.Play(cue);
            Save();
            Debug.Log("SHIFT_ACTION level=" + game.Level.Id + " moves=" + game.History +
                      " delivered=" + game.Delivered + " won=" + game.Won + " lost=" + game.Lost);
        }

        void Undo()
        {
            if (!game.Undo()) { note = "吊车还停在开工位置。"; return; }
            note = "已撤回一步，电量也退回了。";
            page = Page.Work;
            board.Reset();
            sound.Play("bounce");
            Save();
        }

        void Restart()
        {
            game.Restart();
            board.Reset();
            page = Page.Work;
            note = null;
            Save();
        }

        void SwitchJob(int index)
        {
            if (!progress.Open(index)) { note = "先交齐前一张工单。"; return; }
            note = null;
            OpenJob(index);
            Save();
        }

        void Advance()
        {
            if (progress.current + 1 < ShiftLevels.All.Count) SwitchJob(progress.current + 1);
            else page = Page.End;
        }

        void Ledger() { page = Page.Ledger; selected = progress.current; }
        void Hint() { note = ShiftAdvice.Next(game); }
        void ToggleSound()
        {
            progress.muted = !progress.muted;
            AudioListener.volume = progress.muted ? 0 : 1;
            Save();
        }
        void Quit() { Save(); if (storageError == null) Application.Quit(); }

        void Update()
        {
            if (game == null) return;
            if (Input.GetKeyDown(KeyCode.F5))
                ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, "night-shift-window.png"));
            if (Input.GetKeyDown(KeyCode.M)) { ToggleSound(); return; }
            bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            bool escape = Input.GetKeyDown(KeyCode.Escape);
            bool left = Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
            bool right = Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
            if (page == Page.Reset)
            {
                if (escape) page = Page.Work;
                else if (enter) Restart();
                return;
            }
            if (page == Page.End) { if (enter || escape) Ledger(); return; }
            if (page == Page.Ledger)
            {
                if (escape || Input.GetKeyDown(KeyCode.Tab)) page = Page.Work;
                else if (left || Input.GetKeyDown(KeyCode.UpArrow)) selected = Mathf.Max(0,selected-1);
                else if ((right || Input.GetKeyDown(KeyCode.DownArrow)) && selected + 1 < ShiftLevels.All.Count && progress.Open(selected + 1)) selected++;
                else if (enter) SwitchJob(selected);
                else if (Input.GetKeyDown(KeyCode.Q)) Quit();
                return;
            }
            if (escape || Input.GetKeyDown(KeyCode.Tab)) { Ledger(); return; }
            if (Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Backspace)) { Undo(); return; }
            if (Input.GetKeyDown(KeyCode.R)) { page = Page.Reset; return; }
            if (Input.GetKeyDown(KeyCode.H)) { Hint(); return; }
            if (game.Won) { if (enter) Advance(); return; }
            if (game.Lost) { if (enter) Undo(); return; }
            if (left) Act(ShiftAction.Left);
            else if (right) Act(ShiftAction.Right);
            else if (Input.GetKeyDown(KeyCode.Space)) Act(ShiftAction.Pulse);
            else if (enter || Input.GetKeyDown(KeyCode.X)) Act(ShiftAction.Unload);
        }

        void OnDestroy() { board.Dispose(); }
    }
}
