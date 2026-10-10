using Arsenal.Content;
using Arsenal.Core;
using Arsenal.Damage;
using Arsenal.Net;
using Arsenal.Visual;
using Arsenal.Weapons;
using HarmonyLib;
using MelonLoader;
using Ships.Interface.Model;
using Ships.Interface.Model.Placement;
using UnityEngine.SceneManagement;

[assembly: MelonInfo(typeof(Arsenal.ArsenalMod), "Arsenal", "1.0.0", "tidic84")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]

namespace Arsenal
{
    public partial class ArsenalMod : MelonMod
    {
        internal const string MainGameScene = "MainGame";
        internal const int ProtocolVersion = 1;

        internal static ArsenalMod Instance { get; private set; }
        internal static MelonLogger.Instance Log => Instance.LoggerInstance;

        internal static bool InGame => SceneManager.GetActiveScene().name == MainGameScene;

        public override void OnInitializeMelon()
        {
            Instance = this;
            HoleVisuals.Patch(HarmonyInstance);
            HarmonyInstance.Patch(AccessTools.Method(typeof(ShipState), nameof(ShipState.RemoveHullPart)),
                postfix: new HarmonyMethod(typeof(ArsenalMod), nameof(OnHullPartRemoved)));
            HullDamage.Init();
            PlayerDamage.Init();
            FxNet.Init();
            Explosives.Init();
#if DEV
            DevInit();
#endif
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            Svc.Clear();
            HullDamage.ResetAll();
            HoleVisuals.ClearCaches();
            Fx.ClearAll();
            Assets.ClearSceneRefs();
            Sounds.Reset();
            Explosives.Reset();
            if (sceneName == MainGameScene)
                ArsenalContent.RegisterItems();
#if DEV
            DevSceneLoaded(buildIndex, sceneName);
#endif
        }

        public override void OnUpdate()
        {
            ArsenalNet.Tick();
#if DEV
            DevUpdate();
#endif
        }

        private static void OnHullPartRemoved(ShipState __instance, HullPartPosition hullPartPos)
        {
            HullDamage.Forget(__instance.Id, hullPartPos);
        }
    }
}
