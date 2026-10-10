using System;
using System.Collections;
using Arsenal.Content;
using Arsenal.Core;
using Arsenal.Damage;
using Arsenal.Net;
using Arsenal.Vehicles;
using Arsenal.Visual;
using Arsenal.Weapons;
using HarmonyLib;
using MelonLoader;
using Ships.Interface.Model;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Placement;
using Ships.Services;
using UnityEngine;
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

        private MelonPreferences_Entry<float> _volume;

        public override void OnInitializeMelon()
        {
            Instance = this;
            MelonPreferences_Category prefs = MelonPreferences.CreateCategory("Arsenal", "Arsenal");
            _volume = prefs.CreateEntry("SoundVolume", 1f, "Weapon sound volume", "0 = silent, 1 = default.");

            HoleVisuals.Patch(HarmonyInstance);
            ShipContent.Patch(HarmonyInstance);
            GunnerSeat.Patch(HarmonyInstance);
            HarmonyInstance.Patch(AccessTools.Method(typeof(ShipState), nameof(ShipState.RemoveHullPart)),
                postfix: new HarmonyMethod(typeof(ArsenalMod), nameof(OnHullPartRemoved)));

            HullDamage.Init();
            PlayerDamage.Init();
            FxNet.Init();
            Explosives.Init();
            Missiles.Init();
            GunnerSeat.Init(prefs);
            DamageSaves.Init(HarmonyInstance);
#if DEV
            DevInit();
#endif
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            Svc.Clear();
            HullDamage.ResetClient();
            HoleVisuals.ClearCaches();
            Fx.ClearAll();
            Assets.ClearSceneRefs();
            Sounds.Reset();
            Sounds.Volume = _volume.Value;
            Explosives.Reset();
            Missiles.Reset();
            GunnerSeat.Reset();
            ShipWeapons.ResetAll();
            if (sceneName == MainGameScene)
            {
                ArsenalContent.RegisterItems();
                ShipContent.EnsureParts();
                ShipContent.RegisterResearch();
                ShipWeapons.Ensure();
                MelonCoroutines.Start(HookServer());
            }
#if DEV
            DevSceneLoaded(buildIndex, sceneName);
#endif
        }

        public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
        {
            // server damage lives as long as the world (it is reloaded from the save with the next world)
            if (sceneName == MainGameScene) HullDamage.ResetAll();
        }

        /// <summary>Server-side hooks once the scene's services are up (only does something on the host).</summary>
        private IEnumerator HookServer()
        {
            for (int i = 0; i < 300 && Svc.ShipsServer == null; i++) yield return null;
            ShipsServerTracker ships = Svc.ShipsServer;
            if (ships == null) yield break;
            ships.ShipStatefulPartAdded = (Action<uint, StatefulPart>)Delegate.Combine(ships.ShipStatefulPartAdded, new Action<uint, StatefulPart>(OnServerPartAdded));
        }

        private static void OnServerPartAdded(uint shipId, StatefulPart part)
        {
            try
            {
                ShipContent.OnServerPartAdded(shipId, part);
            }
            catch (Exception ex)
            {
                Log.Error($"Part added hook failed: {ex}");
            }
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
