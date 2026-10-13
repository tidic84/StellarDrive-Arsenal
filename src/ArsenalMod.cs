using Arsenal.Content;
using Arsenal.Core;
using Arsenal.Damage;
using Arsenal.Net;
using Arsenal.Ui;
using Arsenal.Vehicles;
using Arsenal.Visual;
using Arsenal.Weapons;
using HarmonyLib;
using MelonLoader;
using Ships.Interface.Model;
using Ships.Interface.Model.Placement;
using UnityEngine.SceneManagement;

[assembly: MelonInfo(typeof(Arsenal.ArsenalMod), "Arsenal", "1.9.1", "tidic84")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]

namespace Arsenal
{
    public partial class ArsenalMod : MelonMod
    {
        internal const string MainGameScene = "MainGame";
        internal const int ProtocolVersion = 10;

        internal static ArsenalMod Instance { get; private set; }
        internal static MelonLogger.Instance Log => Instance.LoggerInstance;

        internal static bool InGame => SceneManager.GetActiveScene().name == MainGameScene;

        private MelonPreferences_Entry<float> _volume;

        public override void OnInitializeMelon()
        {
            Instance = this;
            MelonPreferences_Category prefs = MelonPreferences.CreateCategory("Arsenal", "Arsenal");
            _volume = prefs.CreateEntry("SoundVolume", 1f, "Weapon sound volume", "0 = silent, 1 = default.");

            HoleVisuals.Patch(HarmonyInstance);
            ShipContent.Patch(HarmonyInstance);
            GunnerSeat.Patch(HarmonyInstance);
            ShipKnockback.Patch(HarmonyInstance);
            HarmonyInstance.Patch(AccessTools.Method(typeof(ShipState), nameof(ShipState.RemoveHullPart)),
                postfix: new HarmonyMethod(typeof(ArsenalMod), nameof(OnHullPartRemoved)));

            HullDamage.Init();
            PlayerDamage.Init();
            FxNet.Init();
            Explosives.Init();
            Missiles.Init();
            Projectiles.Init();
            Flares.Init();
            ThreatWarning.Init();
            Effects.Init(HarmonyInstance);
            CombatHud.Init();
            Armory.Armory.Init();
            GunnerSeat.Init(prefs);
            DamageSaves.Init(HarmonyInstance);
            TargetCleanup.Patch(HarmonyInstance);
            Compat.ForceFields.Init();
            Laser.Init();
#if DEV
            DevInit();
#endif
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            Svc.Clear();
            HullDamage.ResetClient();
            ShipKnockback.Reset();
            HoleVisuals.ClearCaches();
            Fx.ClearAll();
            Assets.ClearSceneRefs();
            UiKit.ClearCache();
            Sounds.Reset();
            Sounds.Volume = _volume.Value;
            Explosives.Reset();
            Missiles.Reset();
            Projectiles.Reset();
            Flares.Reset();
            ThreatWarning.Reset();
            Effects.Reset();
            GunnerSeat.Reset();
            Compat.ForceFields.Reset();
            ShipWeapons.ResetAll();
            CombatHud.Reset();
            WeaponHud.Reset();
            Armory.ArmoryUi.ResetAll();
            if (sceneName == MainGameScene)
            {
                ArsenalContent.RegisterItems();
                ShipContent.EnsureParts();
                ShipContent.RegisterResearch();
                ShipWeapons.Ensure();
                CombatHud.Ensure();
                LockHud.Ensure();
                ThreatWarning.Ensure();
                Armory.ArmoryUi.Ensure();
                _ = Compat.ForceFields.Present; // logs whether the TF Advanced force fields are there
            }
#if DEV
            DevSceneLoaded(buildIndex, sceneName);
#endif
        }

        public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
        {
            // server damage lives as long as the world (it is reloaded from the save with the next world)
            if (sceneName == MainGameScene)
            {
                HullDamage.ResetAll();
                PartDamage.Reset();
            }
        }

        public override void OnUpdate()
        {
            ArsenalNet.Tick();
            if (InGame)
            {
                Effects.ServerUpdate();
                Effects.ClientUpdate();
            }
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
