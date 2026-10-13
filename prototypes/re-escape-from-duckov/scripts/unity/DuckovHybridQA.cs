using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Duckov.UI.MainMenu;
using Duckov.Rules.UI;
using Duckov.Utilities;
using Cysharp.Threading.Tasks;
using Saves;
using System.Reflection;

// Opt-in integration instrumentation; observes ordinary game flow and invokes
// the existing New Game button. No game rules, platform checks or saves change.
public sealed class DuckovHybridQA : MonoBehaviour
{
    string output;
    bool full;
    bool suite;
    bool pixel;
    bool resume;
    bool combat;
    bool damagePassed;
    bool evacuationObserved;
    int characterCount;
    Vector3 movementStart;
    float movementDistance;
    bool saveRoundTrip;
    readonly List<string> errors = new List<string>();
    readonly List<string> milestones = new List<string>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Begin()
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "--duckov-hybrid-qa");
        if (index < 0 || index + 1 >= args.Length) return;
        var go = new GameObject("Hybrid source integration QA"); DontDestroyOnLoad(go);
        var qa = go.AddComponent<DuckovHybridQA>();
        qa.output = args[index + 1];
        qa.full = Array.IndexOf(args, "--duckov-hybrid-new-game") >= 0;
        qa.suite = Array.IndexOf(args, "--duckov-hybrid-suite") >= 0;
        qa.pixel = Array.IndexOf(args, "--duckov-hybrid-pixel") >= 0;
        qa.resume = Array.IndexOf(args, "--duckov-hybrid-resume") >= 0;
        qa.combat = Array.IndexOf(args, "--duckov-hybrid-combat") >= 0;
        qa.resume |= qa.combat;
        if (qa.pixel) { Time.captureFramerate = 60; QualitySettings.vSyncCount = 0; Application.targetFrameRate = 60; }
        qa.full |= qa.suite;
        Directory.CreateDirectory(qa.output);
        qa.StartCoroutine(qa.Run());
    }
    void OnEnable()
    {
        Application.logMessageReceived += Log;
        Application.wantsToQuit += WantsToQuit;
    }
    void OnDisable()
    {
        Application.logMessageReceived -= Log;
        Application.wantsToQuit -= WantsToQuit;
    }
    void Log(string message, string stack, LogType type)
    {
        if ((type == LogType.Exception || type == LogType.Error || type == LogType.Assert) && errors.Count < 200)
            errors.Add(message + "\n" + stack);
    }
    bool WantsToQuit()
    {
        File.WriteAllText(Path.Combine(output, "quit-stack.txt"), Environment.StackTrace);
        WriteReport("quitting");
        return true;
    }
    void WriteReport(string phase)
    {
        var loaded = new List<string>();
        for (int i = 0; i < SceneManager.sceneCount; i++) loaded.Add(SceneManager.GetSceneAt(i).path);
        var report = new Report {phase = phase, unity = Application.unityVersion,
            scene = SceneManager.GetActiveScene().path, scenes = loaded.ToArray(), errors = errors.ToArray(),
            milestones = milestones.ToArray(), persistentDataPath = Application.persistentDataPath,
            cameras = Camera.allCamerasCount, objects = FindObjectsOfType<Transform>().Length,
            focused = Application.isFocused, width = Screen.width, height = Screen.height,
            elapsed = Time.realtimeSinceStartup, levelManager = LevelManager.Instance != null};
        report.movementDistance = movementDistance;
        report.saveRoundTrip = saveRoundTrip;
        report.mainCharacter = CharacterMainControl.Main != null;
        report.damagePassed = damagePassed;
        report.evacuationObserved = evacuationObserved;
        report.characterCount = characterCount;
        File.WriteAllText(Path.Combine(output, "hybrid-qa.json"), JsonUtility.ToJson(report, true));
    }
    IEnumerator Run()
    {
        float start = Time.realtimeSinceStartup;
        while (SceneManager.GetActiveScene().name != "MainMenu" && Time.realtimeSinceStartup - start < 45)
        {
            WriteReport("startup"); yield return new WaitForSecondsRealtime(1);
        }
        milestones.Add("main-menu");
        yield return new WaitForSecondsRealtime(8);
        if (pixel)
        {
            while (Time.frameCount < 900) yield return null;
            // Both reference and candidate use this explicit frozen sample.
            foreach (var director in FindObjectsOfType<UnityEngine.Playables.PlayableDirector>()) director.Pause();
            Time.timeScale = 0;
            foreach (var text in FindObjectsOfType<TMPro.TMP_Text>()) text.alpha = 1;
            yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "pixel-title.png"));
            milestones.Add("frozen-title-sample");
            yield return null; yield return null;
            WriteReport("pixel-complete"); Application.Quit(errors.Count == 0 ? 0 : 1); yield break;
        }
        if (resume)
        {
            SavesSystem.SetFile(6);
            string marker = SavesSystem.Load<string>("DuckovHybridQA_RoundTrip");
            saveRoundTrip = !string.IsNullOrEmpty(marker);
            if (!saveRoundTrip) errors.Add("QA save marker missing after process restart");
            else milestones.Add("save-marker-read-in-new-process");
            SceneLoader.Instance.LoadScene(GameplayDataSettings.SceneManagement.BaseScene, clickToConinue: false).Forget();
            yield return WaitForLevel("saved-base");
            milestones.Add("saved-character-loaded-in-new-process");
            yield return new WaitForSecondsRealtime(5);
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "resumed-base.png"));
            if (combat) yield return Combat();
            WriteReport("resume-complete");
            yield return null; yield return null;
            Application.Quit(errors.Count == 0 ? 0 : 1); yield break;
        }
        ScreenCapture.CaptureScreenshot(Path.Combine(output, "main-menu.png"));
        WriteReport("main-menu");
        if (full)
        {
            if (suite) SavesSystem.SetFile(6);
            var title = FindObjectOfType<Title>();
            if (title != null) title.OnPointerClick(null);
            yield return new WaitForSecondsRealtime(2);
            var target = FindObjectOfType<ContinueButton>();
            if (target == null) { errors.Add("New Game button component missing"); }
            else
            {
                var button = target.GetComponent<Button>();
                if (button == null) button = target.GetComponentInChildren<Button>();
                if (button == null) errors.Add("New Game Unity UI Button missing");
                else { button.onClick.Invoke(); milestones.Add("new-game-button-invoked"); }
            }
        }
        if (suite)
        {
            yield return Suite();
            WriteReport("suite-complete");
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "final.png"));
            yield return null; yield return null;
            Application.Quit(errors.Count == 0 ? 0 : 1);
            yield break;
        }
        for (int i = 0; i < (full ? 90 : 25); i++)
        {
            WriteReport(full ? "new-game-observe" : "menu-observe");
            if (i % 15 == 0) ScreenCapture.CaptureScreenshot(Path.Combine(output, "stage-" + i + ".png"));
            yield return new WaitForSecondsRealtime(1);
        }
        WriteReport("complete");
        ScreenCapture.CaptureScreenshot(Path.Combine(output, "final.png"));
        yield return null; yield return null;
        Application.Quit(errors.Count == 0 ? 0 : 1);
    }
    IEnumerator Suite()
    {
        float deadline = Time.realtimeSinceStartup + 65;
        bool confirmed = false;
        while ((!LevelManager.LevelInited || CharacterMainControl.Main == null) && Time.realtimeSinceStartup < deadline)
        {
            var selection = FindObjectOfType<DifficultySelection>();
            if (selection != null && !confirmed)
            {
                var field = typeof(DifficultySelection).GetField("confirmButton", BindingFlags.Instance | BindingFlags.NonPublic);
                var button = (Button)field.GetValue(selection);
                button.onClick.Invoke(); confirmed = true; milestones.Add("default-difficulty-confirmed");
            }
            WriteReport("waiting-for-new-game");
            yield return new WaitForSecondsRealtime(1);
        }
        if (CharacterMainControl.Main == null) { errors.Add("New game did not initialize its character before timeout"); yield break; }
        milestones.Add("new-game-character-initialized");
        yield return new WaitForSecondsRealtime(3);
        ScreenCapture.CaptureScreenshot(Path.Combine(output, "prologue.png"));
        var input = FindObjectOfType<InputManager>();
        var axis = typeof(InputManager).GetField("moveAxisInput", BindingFlags.NonPublic | BindingFlags.Instance);
        movementStart = CharacterMainControl.Main.transform.position;
        float stop = Time.realtimeSinceStartup + 2;
        while (Time.realtimeSinceStartup < stop)
        {
            // Input layer integration test, distinct from native keyboard evidence.
            axis.SetValue(input, Vector2.up);
            yield return null;
        }
        axis.SetValue(input, Vector2.zero);
        movementDistance = Vector3.Distance(movementStart, CharacterMainControl.Main.transform.position);
        if (movementDistance < 0.05f) errors.Add("Input-to-character movement did not advance the character");
        else milestones.Add("input-layer-movement");
        Duckov.UI.InventoryView.Show();
        yield return new WaitForSecondsRealtime(2);
        ScreenCapture.CaptureScreenshot(Path.Combine(output, "inventory.png"));
        Duckov.UI.InventoryView.Hide(); milestones.Add("inventory-open-close");
        yield return new WaitForSecondsRealtime(1);
        SceneLoader.Instance.LoadScene(GameplayDataSettings.SceneManagement.BaseScene, clickToConinue: false).Forget();
        yield return WaitForLevel("base");
        if (CharacterMainControl.Main == null) { errors.Add("Base scene character missing"); yield break; }
        milestones.Add("base-loaded");
        yield return new WaitForSecondsRealtime(5);
        ScreenCapture.CaptureScreenshot(Path.Combine(output, "base.png"));
        string marker = Guid.NewGuid().ToString("N");
        SavesSystem.Save("DuckovHybridQA_RoundTrip", marker);
        LevelManager.Instance.NotifySaveBeforeLoadScene(true);
        SavesSystem.SaveFile();
        milestones.Add("save-written");
        SceneLoader.LoadMainMenu();
        yield return new WaitForSecondsRealtime(8);
        SceneLoader.Instance.LoadScene(GameplayDataSettings.SceneManagement.BaseScene, clickToConinue: false).Forget();
        yield return WaitForLevel("base-reload");
        saveRoundTrip = SavesSystem.Load<string>("DuckovHybridQA_RoundTrip") == marker;
        if (!saveRoundTrip) errors.Add("Save marker did not survive scene reload");
        else milestones.Add("save-scene-reload");
        ScreenCapture.CaptureScreenshot(Path.Combine(output, "base-reloaded.png"));
        yield return new WaitForSecondsRealtime(4);
    }
    IEnumerator WaitForLevel(string name)
    {
        yield return new WaitForSecondsRealtime(2);
        float deadline = Time.realtimeSinceStartup + 70;
        while ((SceneLoader.IsSceneLoading || !LevelManager.LevelInited || CharacterMainControl.Main == null) && Time.realtimeSinceStartup < deadline)
        {
            WriteReport("loading-" + name); yield return new WaitForSecondsRealtime(1);
        }
        if (SceneLoader.IsSceneLoading || CharacterMainControl.Main == null) errors.Add("Level load timeout: " + name);
    }
    IEnumerator Combat()
    {
        SceneLoader.Instance.LoadScene("Level_GroundZero_Main", clickToConinue: false).Forget();
        yield return WaitForLevel("ground-zero");
        if (CharacterMainControl.Main == null) yield break;
        yield return new WaitForSecondsRealtime(5);
        characterCount = FindObjectsOfType<CharacterMainControl>().Length;
        milestones.Add("ground-zero-loaded");
        ScreenCapture.CaptureScreenshot(Path.Combine(output, "ground-zero.png"));
        var health = CharacterMainControl.Main.Health;
        float before = health.CurrentHealth;
        var damage = new DamageInfo(null) {damageValue = 5, damageType = DamageTypes.realDamage,
            ignoreDifficulty = true, ignoreArmor = true};
        health.Hurt(damage);
        damagePassed = Mathf.Abs(before - health.CurrentHealth - 5) < 0.001f;
        if (!damagePassed) errors.Add("Health damage pipeline did not apply exactly 5 real damage");
        else milestones.Add("damage-pipeline-five-hp");
        health.AddHealth(5);
        LevelManager.OnEvacuated += ObserveEvacuation;
        SceneLoader.Instance.LoadScene(GameplayDataSettings.SceneManagement.BaseScene,
            clickToConinue: false, notifyEvacuation: true).Forget();
        yield return WaitForLevel("evacuated-base");
        LevelManager.OnEvacuated -= ObserveEvacuation;
        if (!evacuationObserved) errors.Add("Ordinary evacuation event was not raised");
        else milestones.Add("evacuation-event-and-base-return");
        yield return new WaitForSecondsRealtime(5);
        ScreenCapture.CaptureScreenshot(Path.Combine(output, "evacuated-base.png"));
    }
    void ObserveEvacuation(EvacuationInfo info) { evacuationObserved = true; }
    [Serializable] class Report
    {
        public string phase, unity, scene, persistentDataPath;
        public string[] scenes, errors, milestones;
        public int cameras, objects, width, height;
        public bool focused, levelManager;
        public bool mainCharacter, saveRoundTrip;
        public float movementDistance;
        public bool damagePassed, evacuationObserved;
        public int characterCount;
        public float elapsed;
    }
}
