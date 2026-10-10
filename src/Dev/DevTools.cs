#if DEV
using System;
using System.Collections;
using System.IO;
using Managers.Interface;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arsenal
{
    // Dev-only remote control: the game polls UserData/arsenal_cmd.txt and runs each line as a command.
    public partial class ArsenalMod
    {
        private const string TestWorld = "ARS_Test";
        private string _cmdFile;
        private float _nextPoll;
        private bool _launched;

        private void DevInit()
        {
            _cmdFile = Path.Combine(MelonEnvironment.UserDataDirectory, "arsenal_cmd.txt");
            if (File.Exists(_cmdFile)) File.Delete(_cmdFile);
        }

        private void DevSceneLoaded(int buildIndex, string sceneName)
        {
            LoggerInstance.Msg($"[DEV] scene loaded {buildIndex} {sceneName}");
            if (buildIndex == 0 && !_launched && Environment.GetEnvironmentVariable("ARSENAL_AUTOLAUNCH") != "0")
            {
                _launched = true;
                MelonCoroutines.Start(LaunchTestWorld());
            }
            if (sceneName == MainGameScene)
                MelonCoroutines.Start(AfterGameLoaded());
        }

        private IEnumerator LaunchTestWorld()
        {
            for (int i = 0; i < 30; i++) yield return null;
            string saves = Path.Combine(Application.persistentDataPath, "WorldSaves");
            string file = Path.Combine(saves, TestWorld, "world.json");
            if (!File.Exists(file))
            {
                LoggerInstance.Error($"[DEV] test world missing: {file}");
                yield break;
            }
            MainGameSceneLoadData.Mode = MainGameSceneLoadData.GameMode.SinglePlayer;
            MainGameSceneLoadData.SaveFileFullName = new FileInfo(file).FullName;
            LoggerInstance.Msg($"[DEV] launching {TestWorld}");
            SceneManager.LoadScene(MainGameScene, LoadSceneMode.Single);
        }

        private IEnumerator AfterGameLoaded()
        {
            float end = Time.realtimeSinceStartup + 12f;
            while (Time.realtimeSinceStartup < end) yield return null;
            if (Environment.GetEnvironmentVariable("ARSENAL_PROBE") == "1")
                Dev.Probe.Dump("ingame");
            Dev.Probe.DumpMaterials();
        }

        private void DevUpdate()
        {
            if (Time.realtimeSinceStartup < _nextPoll) return;
            _nextPoll = Time.realtimeSinceStartup + 0.5f;
            if (!File.Exists(_cmdFile)) return;
            string[] lines;
            try
            {
                lines = File.ReadAllLines(_cmdFile);
                File.Delete(_cmdFile);
            }
            catch (IOException)
            {
                return;
            }
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                LoggerInstance.Msg($"[DEV] cmd: {line}");
                try
                {
                    RunDevCommand(line.Split(' '));
                }
                catch (Exception ex)
                {
                    LoggerInstance.Error($"[DEV] cmd failed: {ex}");
                }
            }
        }

        private void RunDevCommand(string[] a)
        {
            switch (a[0])
            {
                case "probe":
                    Dev.Probe.Dump("cmd");
                    break;
                case "shot":
                    string path = Path.Combine(MelonEnvironment.UserDataDirectory, "shot_" + (a.Length > 1 ? a[1] : "x") + ".png");
                    ScreenCapture.CaptureScreenshot(path);
                    LoggerInstance.Msg($"[DEV] screenshot {path}");
                    break;
                case "quit":
                    Application.Quit();
                    break;
                default:
                    DevCommandExtra(a);
                    break;
            }
        }

        partial void DevCommandExtra(string[] a);
    }
}
#endif
