using System;
using Arsenal.Visual;
using UnityEngine;

namespace Arsenal.Weapons
{
    internal enum WeaponKind : byte
    {
        Pistol = 1,
        Rifle = 2,
        Marksman = 3,
        Grenade = 4,
        Charge = 5,
        LaserPistol = 7,
        Gauss = 8,
        Smg = 9,
        PlasmaShotgun = 10,
        Railgun = 11,
        Minigun = 12,
        EmpCarbine = 13,
        GrenadeLauncher = 14,
        RocketLauncher = 15,
        Flamethrower = 16,
        Tesla = 17,
        Disintegrator = 18,
        SamLauncher = 19,
        HyperLauncher = 33,
        // ship-mounted
        ShipGun = 20,
        ShipLaser = 21,
        ShipMissile = 22,
        PulseTurret = 23,
        PlasmaCannon = 24,
        ShipRailgun = 25,
        Torpedo = 26,
        IonCannon = 27,
        Pdc = 28,
        Capital = 29,
        ShipRockets = 30,
        ShipSam = 31,
        HyperMissile = 32,
        // hand blasters (bolts like the laser cannons')
        BlasterPistol = 34,
        BlasterRifle = 35,
        BlasterCarbine = 36,
        HeavyBlaster = 37,
        // rotating turrets
        QuadLaser = 38,
        Turbolaser = 39,
        IonTurret = 40,
        HeavyTurbolaser = 41,
        Annihilator = 42,
    }

    internal enum FireMode : byte
    {
        Bullet,      // hit-scan rounds with penetration (one or several pellets)
        Charge,      // hold to charge, fires a hit-scan round when released or full
        Beam,        // continuous beam that burns
        Projectile,  // flying projectile (grenades, rockets)
        Arc,         // electric arc that jumps between targets
        Flame,       // short cone of fire
        Bolt,        // blaster bolts: glowing bolts you see fly (the laser cannons' kind), style in BoltStyle
    }

    internal enum AmmoMode : byte
    {
        Magazine, // rounds + reload
        Heat,     // no reload: overheats, cools down by itself
    }

    internal enum TracerStyle : byte
    {
        Bullet,
        Laser,
        Plasma,
        Rail,
        Ion,
        None,
    }

    internal enum Rarity : byte
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary,
    }

    internal enum ArmoryCategory : byte
    {
        Sidearms,
        Rifles,
        Heavy,
        Energy,
        Explosives,
    }

    /// <summary>Ballistic numbers of one projectile type.</summary>
    internal sealed class BulletSpec
    {
        public float PlayerDamage;   // health removed from a player (max 100)
        public float PanelDamage;    // hit points removed from a hull panel
        public float Penetration;    // compared with the panel material resistance
        public float HoleRadius;     // metres
        public float Range = 400f;
        public float ObjectDamage;   // trees, rocks
        public float BurnSeconds;    // sets players on fire
        public float EmpSeconds;     // knocks ships (and players' gear) out
        public bool ThroughPlayers;  // keeps going after hitting a player
    }

    internal sealed class HandWeaponDef
    {
        public WeaponKind Kind;
        public uint ItemId;
        public string NameFr, NameEn;
        public string DescFr, DescEn;
        public ArmoryCategory Category;
        public Rarity Rarity;
        public (uint item, byte qty)[] Cost;

        public FireMode Mode = FireMode.Bullet;
        public AmmoMode Ammo = AmmoMode.Magazine;
        public bool Automatic;
        public float RoundsPerMinute;
        public int Pellets = 1;
        public int Magazine;
        public float ReloadTime;
        public float HeatPerShot;      // Heat ammo: fraction of the gauge per shot (beams: per second)
        public float CoolPerSecond = 0.35f;
        public float SpinUp;           // seconds before full rate (minigun)
        public float ChargeTime;       // Charge mode
        public float SpreadDegrees;
        public float AimSpreadDegrees;
        public float RecoilKick;       // metres of visual kick-back
        public float AimFov;           // camera FOV while aiming (0 = no zoom)
        public ScopeKind Scope;        // aimed: hides the model and looks through this scope
        public Color ReticleColor = new Color(1f, 0.22f, 0.15f);
        public float Mass;
        public BulletSpec Bullet;
        public ProjectileKind Projectile;
        public float Reach = 30f;      // beams, flames and arcs
        public TracerStyle Tracer = TracerStyle.Bullet;
        public Color TracerColor = new Color(1f, 0.55f, 0.2f);
        public float MuzzleSize = 0.18f;
        public SoundStyle Sound = SoundStyle.Rifle;
        public Func<Model> Model;
        public Vector3 HoldPosition;   // model offset in the tool visuals
        public Vector3 HoldEuler = new Vector3(5f, 18f, 0f);
        public float AimDistance = 0.28f;
        public byte BoltStyle;          // Bolt mode: the bolt's look and damage (Laser.Styles)

        public string Name => Loc.T(NameFr, NameEn);
        public string Description => Loc.T(DescFr, DescEn);
    }

    internal static class WeaponDefs
    {
        // Item ids far from the game's (0-3, 100-112).
        public const uint PistolId = 9101, RifleId = 9102, MarksmanId = 9103, GrenadeId = 9104, ChargeId = 9105;
        public const uint LaserPistolId = 9110, GaussId = 9111, SmgId = 9112, PlasmaShotgunId = 9113, RailgunId = 9114, MinigunId = 9115,
            EmpId = 9116, GrenadeLauncherId = 9117, RocketLauncherId = 9118, FlamethrowerId = 9119, TeslaId = 9120, DisintegratorId = 9121, SamLauncherId = 9122,
            HyperLauncherId = 9123, BlasterPistolId = 9124, BlasterRifleId = 9125, BlasterCarbineId = 9126, HeavyBlasterId = 9127;

        private const uint Iron = 101, Glass = 102, Shard = 103, Aluminium = 104, Wood = 110, Copper = 112;

        private static readonly Vector3 Sidearm = new Vector3(-0.13f, 0.12f, -0.1f), SidearmRot = new Vector3(6f, 24f, 0f);
        private static readonly Vector3 Long = new Vector3(-0.08f, 0.07f, 0.02f);

        public static readonly HandWeaponDef Pistol = new HandWeaponDef
        {
            Kind = WeaponKind.Pistol, ItemId = PistolId, Category = ArmoryCategory.Sidearms, Rarity = Rarity.Common,
            NameFr = "Pistolet de bord", NameEn = "Sidearm",
            DescFr = "Arme de poing semi-automatique fiable. Traverse le verre et le bois.", DescEn = "Reliable semi-automatic sidearm. Goes through glass and wood.",
            Cost = new[] { (Iron, (byte)4), (Copper, (byte)1) },
            Automatic = false, RoundsPerMinute = 360, Magazine = 12, ReloadTime = 1.3f,
            SpreadDegrees = 0.6f, AimSpreadDegrees = 0.2f, RecoilKick = 0.05f, AimFov = 55f, Mass = 1.2f,
            Bullet = new BulletSpec { PlayerDamage = 20, PanelDamage = 6, Penetration = 18, HoleRadius = 0.03f, ObjectDamage = 0.4f },
            Sound = SoundStyle.Pistol, MuzzleSize = 0.15f, Model = WeaponModels.Pistol, HoldPosition = Sidearm, HoldEuler = SidearmRot, AimDistance = 0.32f,
        };

        public static readonly HandWeaponDef LaserPistol = new HandWeaponDef
        {
            Kind = WeaponKind.LaserPistol, ItemId = LaserPistolId, Category = ArmoryCategory.Energy, Rarity = Rarity.Common,
            NameFr = "Pistolet laser PX-9", NameEn = "PX-9 laser pistol",
            DescFr = "Pistolet énergétique léger et précis. Pas de chargeur : il chauffe à chaque tir et refroidit tout seul.", DescEn = "Light, accurate energy pistol. No magazine: it heats up with each shot and cools down on its own.",
            Cost = new[] { (Iron, (byte)4), (Copper, (byte)2), (Glass, (byte)1) },
            Ammo = AmmoMode.Heat, HeatPerShot = 0.075f, CoolPerSecond = 0.4f,
            Automatic = false, RoundsPerMinute = 420,
            SpreadDegrees = 0.3f, AimSpreadDegrees = 0.05f, RecoilKick = 0.02f, AimFov = 55f, Mass = 1f,
            Bullet = new BulletSpec { PlayerDamage = 17, PanelDamage = 5, Penetration = 9, HoleRadius = 0.026f, ObjectDamage = 0.5f },
            Tracer = TracerStyle.Laser, TracerColor = new Color(0.3f, 0.9f, 1f), Sound = SoundStyle.Laser, MuzzleSize = 0.1f,
            Model = GunModels.LaserPistol, HoldPosition = Sidearm, HoldEuler = SidearmRot, AimDistance = 0.32f,
        };

        public static readonly HandWeaponDef Gauss = new HandWeaponDef
        {
            Kind = WeaponKind.Gauss, ItemId = GaussId, Category = ArmoryCategory.Sidearms, Rarity = Rarity.Uncommon,
            NameFr = "Revolver Gauss « Marshal »", NameEn = "\"Marshal\" gauss revolver",
            DescFr = "Six projectiles magnétiques. Gros dégâts, cadence lente, perce une plaque de fer.", DescEn = "Six magnetic slugs. Heavy damage, slow rate, punches through an iron plate.",
            Cost = new[] { (Iron, (byte)6), (Copper, (byte)3) },
            Automatic = false, RoundsPerMinute = 110, Magazine = 6, ReloadTime = 2.2f,
            SpreadDegrees = 0.5f, AimSpreadDegrees = 0.1f, RecoilKick = 0.1f, AimFov = 50f, Mass = 2f,
            Bullet = new BulletSpec { PlayerDamage = 48, PanelDamage = 22, Penetration = 58, HoleRadius = 0.042f, ObjectDamage = 1.5f },
            Tracer = TracerStyle.Ion, TracerColor = new Color(0.55f, 0.75f, 1f), Sound = SoundStyle.Gauss, MuzzleSize = 0.22f,
            Model = GunModels.Gauss, HoldPosition = Sidearm, HoldEuler = SidearmRot, AimDistance = 0.32f,
        };

        public static readonly HandWeaponDef Smg = new HandWeaponDef
        {
            Kind = WeaponKind.Smg, ItemId = SmgId, Category = ArmoryCategory.Sidearms, Rarity = Rarity.Uncommon,
            NameFr = "Mitraillette « Viper »", NameEn = "\"Viper\" SMG",
            DescFr = "Rafales très rapides, peu précises. Faite pour le combat dans les coursives.", DescEn = "Very fast, loose bursts. Made for corridor fights.",
            Cost = new[] { (Iron, (byte)6), (Copper, (byte)2) },
            Automatic = true, RoundsPerMinute = 950, Magazine = 40, ReloadTime = 1.6f,
            SpreadDegrees = 3f, AimSpreadDegrees = 1.4f, RecoilKick = 0.025f, AimFov = 55f, Mass = 2.4f,
            Bullet = new BulletSpec { PlayerDamage = 9, PanelDamage = 3, Penetration = 10, HoleRadius = 0.025f, ObjectDamage = 0.25f },
            Sound = SoundStyle.Smg, MuzzleSize = 0.14f, Model = GunModels.Smg, HoldPosition = new Vector3(-0.1f, 0.1f, -0.06f), HoldEuler = new Vector3(5f, 20f, 0f), AimDistance = 0.3f,
        };

        public static readonly HandWeaponDef Rifle = new HandWeaponDef
        {
            Kind = WeaponKind.Rifle, ItemId = RifleId, Category = ArmoryCategory.Rifles, Rarity = Rarity.Common,
            NameFr = "Fusil d'assaut AR-47", NameEn = "AR-47 assault rifle",
            DescFr = "Fusil polyvalent en tir automatique. Perce une plaque de fer.", DescEn = "All-round automatic rifle. Punches through one iron plate.",
            Cost = new[] { (Iron, (byte)8), (Copper, (byte)3) },
            Automatic = true, RoundsPerMinute = 600, Magazine = 30, ReloadTime = 2.0f,
            SpreadDegrees = 1.4f, AimSpreadDegrees = 0.45f, RecoilKick = 0.04f, AimFov = 48f, Mass = 3.5f,
            Bullet = new BulletSpec { PlayerDamage = 14, PanelDamage = 7, Penetration = 46, HoleRadius = 0.03f, ObjectDamage = 0.4f },
            Sound = SoundStyle.Rifle, MuzzleSize = 0.2f, Model = WeaponModels.Rifle, HoldPosition = Long,
        };

        public static readonly HandWeaponDef PlasmaShotgun = new HandWeaponDef
        {
            Kind = WeaponKind.PlasmaShotgun, ItemId = PlasmaShotgunId, Category = ArmoryCategory.Rifles, Rarity = Rarity.Rare,
            NameFr = "Fusil à plasma « Vulcan »", NameEn = "\"Vulcan\" plasma shotgun",
            DescFr = "Projette une gerbe de charges de plasma. Dévastateur de près, enflamme les cibles.", DescEn = "Sprays a fan of plasma charges. Devastating up close, sets targets on fire.",
            Cost = new[] { (Iron, (byte)8), (Copper, (byte)4), (Glass, (byte)2) },
            Automatic = false, RoundsPerMinute = 70, Pellets = 8, Magazine = 6, ReloadTime = 2.4f,
            SpreadDegrees = 5.5f, AimSpreadDegrees = 4f, RecoilKick = 0.12f, AimFov = 55f, Mass = 4f,
            Bullet = new BulletSpec { PlayerDamage = 10, PanelDamage = 6, Penetration = 6, HoleRadius = 0.032f, Range = 45f, ObjectDamage = 0.5f, BurnSeconds = 3f },
            Tracer = TracerStyle.Plasma, TracerColor = new Color(1f, 0.5f, 0.15f), Sound = SoundStyle.Plasma, MuzzleSize = 0.3f,
            Model = GunModels.PlasmaShotgun, HoldPosition = Long,
        };

        public static readonly HandWeaponDef Marksman = new HandWeaponDef
        {
            Kind = WeaponKind.Marksman, ItemId = MarksmanId, Category = ArmoryCategory.Rifles, Rarity = Rarity.Uncommon,
            NameFr = "Fusil anti-matériel", NameEn = "Anti-materiel rifle",
            DescFr = "Gros calibre à lunette. Traverse plusieurs plaques de fer. Clic droit pour viser.", DescEn = "Scoped heavy calibre. Goes through several iron plates. Right click to aim.",
            Cost = new[] { (Iron, (byte)12), (Copper, (byte)4), (Glass, (byte)2) },
            Automatic = false, RoundsPerMinute = 50, Magazine = 5, ReloadTime = 2.8f,
            SpreadDegrees = 2.5f, AimSpreadDegrees = 0.02f, RecoilKick = 0.16f, AimFov = 18f, Scope = ScopeKind.Sniper, Mass = 9f,
            Bullet = new BulletSpec { PlayerDamage = 85, PanelDamage = 60, Penetration = 125, HoleRadius = 0.06f, Range = 900f, ObjectDamage = 3f },
            Sound = SoundStyle.Heavy, MuzzleSize = 0.35f, Model = WeaponModels.Marksman, HoldPosition = new Vector3(-0.08f, 0.06f, 0f),
        };

        public static readonly HandWeaponDef Railgun = new HandWeaponDef
        {
            Kind = WeaponKind.Railgun, ItemId = RailgunId, Category = ArmoryCategory.Rifles, Rarity = Rarity.Rare,
            NameFr = "Railgun M-90", NameEn = "M-90 railgun",
            DescFr = "Maintenez le clic pour charger, relâchez pour tirer. Le projectile traverse les coques et les ennemis alignés.", DescEn = "Hold to charge, release to fire. The slug goes through hulls and lined-up enemies.",
            Cost = new[] { (Iron, (byte)12), (Copper, (byte)6), (Shard, (byte)1) },
            Mode = FireMode.Charge, ChargeTime = 1.1f,
            Automatic = false, RoundsPerMinute = 40, Magazine = 4, ReloadTime = 3f,
            SpreadDegrees = 0.8f, AimSpreadDegrees = 0f, RecoilKick = 0.2f, AimFov = 20f, Scope = ScopeKind.Rail, ReticleColor = new Color(0.4f, 0.9f, 1f), Mass = 10f,
            Bullet = new BulletSpec { PlayerDamage = 110, PanelDamage = 90, Penetration = 320, HoleRadius = 0.075f, Range = 1200f, ObjectDamage = 6f, ThroughPlayers = true },
            Tracer = TracerStyle.Rail, TracerColor = new Color(0.5f, 0.85f, 1f), Sound = SoundStyle.Rail, MuzzleSize = 0.4f,
            Model = GunModels.Railgun, HoldPosition = new Vector3(-0.08f, 0.06f, 0f),
        };

        public static readonly HandWeaponDef Minigun = new HandWeaponDef
        {
            Kind = WeaponKind.Minigun, ItemId = MinigunId, Category = ArmoryCategory.Heavy, Rarity = Rarity.Rare,
            NameFr = "Minigun laser « Cyclone »", NameEn = "\"Cyclone\" laser minigun",
            DescFr = "Les canons doivent d'abord prendre de la vitesse. Cadence énorme, surchauffe rapide.", DescEn = "The barrels have to spin up first. Huge rate of fire, overheats fast.",
            Cost = new[] { (Iron, (byte)16), (Copper, (byte)8), (Glass, (byte)4) },
            Ammo = AmmoMode.Heat, HeatPerShot = 0.012f, CoolPerSecond = 0.3f, SpinUp = 0.7f,
            Automatic = true, RoundsPerMinute = 1300,
            SpreadDegrees = 2.2f, AimSpreadDegrees = 2.2f, RecoilKick = 0.012f, AimFov = 0f, Mass = 14f,
            Bullet = new BulletSpec { PlayerDamage = 7, PanelDamage = 4, Penetration = 16, HoleRadius = 0.027f, ObjectDamage = 0.3f },
            Tracer = TracerStyle.Laser, TracerColor = new Color(1f, 0.3f, 0.25f), Sound = SoundStyle.Laser, MuzzleSize = 0.16f,
            Model = GunModels.Minigun, HoldPosition = new Vector3(-0.06f, 0.0f, 0.02f), HoldEuler = new Vector3(4f, 14f, 0f),
        };

        public static readonly HandWeaponDef Flamethrower = new HandWeaponDef
        {
            Kind = WeaponKind.Flamethrower, ItemId = FlamethrowerId, Category = ArmoryCategory.Heavy, Rarity = Rarity.Uncommon,
            NameFr = "Lance-flammes « Inferno »", NameEn = "\"Inferno\" flamethrower",
            DescFr = "Cône de feu à courte portée. Enflamme les joueurs et consume le bois.", DescEn = "Short-range cone of fire. Sets players ablaze and eats through wood.",
            Cost = new[] { (Iron, (byte)8), (Copper, (byte)2), (Wood, (byte)6) },
            Mode = FireMode.Flame, Automatic = true, RoundsPerMinute = 600, Magazine = 100, ReloadTime = 2.5f, Reach = 9f,
            SpreadDegrees = 0f, RecoilKick = 0.004f, Mass = 9f,
            Bullet = new BulletSpec { PlayerDamage = 1.6f, PanelDamage = 2.2f, Penetration = 0, ObjectDamage = 0.3f, BurnSeconds = 4f },
            Tracer = TracerStyle.None, Sound = SoundStyle.Flame, MuzzleSize = 0f,
            Model = GunModels.Flamethrower, HoldPosition = new Vector3(-0.07f, 0.03f, 0.02f),
        };

        public static readonly HandWeaponDef GrenadeLauncher = new HandWeaponDef
        {
            Kind = WeaponKind.GrenadeLauncher, ItemId = GrenadeLauncherId, Category = ArmoryCategory.Explosives, Rarity = Rarity.Uncommon,
            NameFr = "Lance-grenades « Havoc »", NameEn = "\"Havoc\" grenade launcher",
            DescFr = "Six grenades qui explosent à l'impact. Arc de tir en cloche.", DescEn = "Six grenades that blow up on impact. Lobbed trajectory.",
            Cost = new[] { (Iron, (byte)10), (Copper, (byte)4) },
            Mode = FireMode.Projectile, Projectile = ProjectileKind.Grenade,
            Automatic = false, RoundsPerMinute = 80, Magazine = 6, ReloadTime = 3f,
            SpreadDegrees = 0.5f, AimSpreadDegrees = 0.3f, RecoilKick = 0.08f, AimFov = 0f, Mass = 6f,
            Tracer = TracerStyle.None, Sound = SoundStyle.Launcher, MuzzleSize = 0.25f,
            Model = GunModels.GrenadeLauncher, HoldPosition = Long,
        };

        public static readonly HandWeaponDef RocketLauncher = new HandWeaponDef
        {
            Kind = WeaponKind.RocketLauncher, ItemId = RocketLauncherId, Category = ArmoryCategory.Explosives, Rarity = Rarity.Rare,
            NameFr = "Lance-roquettes « Titan »", NameEn = "\"Titan\" rocket launcher",
            DescFr = "Une roquette par chargement. Ouvre les coques et les véhicules.", DescEn = "One rocket per load. Tears hulls and vehicles open.",
            Cost = new[] { (Iron, (byte)14), (Copper, (byte)6), (Shard, (byte)1) },
            Mode = FireMode.Projectile, Projectile = ProjectileKind.Rocket,
            Automatic = false, RoundsPerMinute = 30, Magazine = 1, ReloadTime = 3f,
            SpreadDegrees = 0.3f, AimSpreadDegrees = 0f, RecoilKick = 0.12f, AimFov = 30f, Scope = ScopeKind.Launcher, ReticleColor = new Color(1f, 0.62f, 0.18f), Mass = 11f,
            Tracer = TracerStyle.None, Sound = SoundStyle.Launcher, MuzzleSize = 0.4f,
            Model = GunModels.RocketLauncher, HoldPosition = new Vector3(-0.12f, 0.02f, -0.05f), HoldEuler = new Vector3(3f, 12f, 0f), AimDistance = 0.22f,
        };

        public static readonly HandWeaponDef EmpCarbine = new HandWeaponDef
        {
            Kind = WeaponKind.EmpCarbine, ItemId = EmpId, Category = ArmoryCategory.Energy, Rarity = Rarity.Rare,
            NameFr = "Carabine EMP « Blackout »", NameEn = "\"Blackout\" EMP carbine",
            DescFr = "Presque inoffensive pour la coque, mais coupe les commandes et les armes d'un vaisseau pendant quelques secondes. Brouille aussi l'équipement des joueurs.", DescEn = "Barely scratches hulls, but knocks a ship's controls and weapons out for a few seconds. Also scrambles players' gear.",
            Cost = new[] { (Copper, (byte)8), (Glass, (byte)3), (Shard, (byte)1) },
            Ammo = AmmoMode.Heat, HeatPerShot = 0.3f, CoolPerSecond = 0.25f,
            Automatic = false, RoundsPerMinute = 90,
            SpreadDegrees = 0.4f, AimSpreadDegrees = 0.1f, RecoilKick = 0.05f, AimFov = 45f, Mass = 4f,
            Bullet = new BulletSpec { PlayerDamage = 6, PanelDamage = 1, Penetration = 0, HoleRadius = 0f, Range = 250f, EmpSeconds = 6f },
            Tracer = TracerStyle.Ion, TracerColor = new Color(0.35f, 0.6f, 1f), Sound = SoundStyle.Emp, MuzzleSize = 0.25f,
            Model = GunModels.EmpCarbine, HoldPosition = Long,
        };

        public static readonly HandWeaponDef Tesla = new HandWeaponDef
        {
            Kind = WeaponKind.Tesla, ItemId = TeslaId, Category = ArmoryCategory.Energy, Rarity = Rarity.Rare,
            NameFr = "Arc électrique « Tesla »", NameEn = "\"Tesla\" arc gun",
            DescFr = "Décharge qui saute d'une cible à l'autre (jusqu'à 4) dans un rayon de quelques mètres.", DescEn = "A discharge that jumps from target to target (up to 4) within a few metres.",
            Cost = new[] { (Copper, (byte)10), (Glass, (byte)2), (Shard, (byte)1) },
            Mode = FireMode.Arc, Ammo = AmmoMode.Heat, HeatPerShot = 0.18f, CoolPerSecond = 0.3f, Reach = 28f,
            Automatic = true, RoundsPerMinute = 150,
            RecoilKick = 0.03f, Mass = 5f,
            Bullet = new BulletSpec { PlayerDamage = 20, PanelDamage = 12, Penetration = 0, ObjectDamage = 1f },
            Tracer = TracerStyle.None, TracerColor = new Color(0.55f, 0.8f, 1f), Sound = SoundStyle.Arc, MuzzleSize = 0.2f,
            Model = GunModels.Tesla, HoldPosition = Long,
        };

        public static readonly HandWeaponDef Disintegrator = new HandWeaponDef
        {
            Kind = WeaponKind.Disintegrator, ItemId = DisintegratorId, Category = ArmoryCategory.Energy, Rarity = Rarity.Legendary,
            NameFr = "Désintégrateur « Néant »", NameEn = "\"Void\" disintegrator",
            DescFr = "Technologie inconnue. Rayon instable dont les dégâts augmentent tant qu'il reste sur la même cible. Fait fondre les coques.", DescEn = "Unknown technology. An unstable beam whose damage grows while it stays on the same target. Melts hulls.",
            Cost = new[] { (Shard, (byte)4), (Copper, (byte)10), (Glass, (byte)6) },
            Mode = FireMode.Beam, Ammo = AmmoMode.Heat, HeatPerShot = 0.16f, CoolPerSecond = 0.22f, Reach = 60f,
            Automatic = true, RoundsPerMinute = 600,
            RecoilKick = 0.006f, Mass = 6f,
            Bullet = new BulletSpec { PlayerDamage = 2.5f, PanelDamage = 5f, Penetration = 0, ObjectDamage = 0.4f },
            Tracer = TracerStyle.None, TracerColor = new Color(0.75f, 0.3f, 1f), Sound = SoundStyle.Beam, MuzzleSize = 0f,
            Model = GunModels.Disintegrator, HoldPosition = Long,
        };

        public static readonly HandWeaponDef SamLauncher = new HandWeaponDef
        {
            Kind = WeaponKind.SamLauncher, ItemId = SamLauncherId, Category = ArmoryCategory.Explosives, Rarity = Rarity.Epic,
            NameFr = "Lance-missiles sol-air « Aigle »", NameEn = "\"Eagle\" surface-to-air launcher",
            DescFr = "Visez un vaisseau : le verrouillage se fait tout seul (bips puis tonalité continue). Le missile suit sa cible, anticipe sa trajectoire et explose à proximité.",
            DescEn = "Point it at a ship: it locks on by itself (blips, then a steady tone). The missile chases its target, leads it and blows up close to it.",
            Cost = new[] { (Iron, (byte)18), (Copper, (byte)10), (Shard, (byte)2), (Glass, (byte)2) },
            Mode = FireMode.Projectile, Projectile = ProjectileKind.Sam,
            Automatic = false, RoundsPerMinute = 30, Magazine = 1, ReloadTime = 3.5f,
            SpreadDegrees = 0.2f, AimSpreadDegrees = 0f, RecoilKick = 0.1f, AimFov = 26f, Scope = ScopeKind.Launcher, ReticleColor = new Color(1f, 0.3f, 0.25f), Mass = 14f,
            Tracer = TracerStyle.None, Sound = SoundStyle.Launcher, MuzzleSize = 0.4f,
            Model = GunModels.SamLauncher, HoldPosition = new Vector3(-0.12f, -0.03f, 0.0f), HoldEuler = new Vector3(3f, 12f, 0f), AimDistance = 0.24f,
        };

        public static readonly HandWeaponDef HyperLauncher = new HandWeaponDef
        {
            Kind = WeaponKind.HyperLauncher, ItemId = HyperLauncherId, Category = ArmoryCategory.Explosives, Rarity = Rarity.Legendary,
            NameFr = "Lance-missiles hypersonique « Foudre »", NameEn = "\"Lightning\" hypersonic launcher",
            DescFr = "La version portable du « Foudre ». Visez un vaisseau pour le verrouiller : le missile accélère jusqu'à 20 000 m/s, rattrape les vaisseaux les plus rapides et les projette violemment sans les abîmer.",
            DescEn = "The shoulder-fired \"Lightning\". Point it at a ship to lock on: the missile accelerates up to 20,000 m/s, catches the fastest ships and sends them flying without damaging them.",
            Cost = new[] { (Iron, (byte)20), (Copper, (byte)14), (Shard, (byte)5), (Glass, (byte)4) },
            Mode = FireMode.Projectile, Projectile = ProjectileKind.Hyper,
            Automatic = false, RoundsPerMinute = 20, Magazine = 1, ReloadTime = 6f,
            SpreadDegrees = 0.2f, AimSpreadDegrees = 0f, RecoilKick = 0.14f, AimFov = 30f, Mass = 16f,
            Tracer = TracerStyle.None, Sound = SoundStyle.Launcher, MuzzleSize = 0.5f,
            Model = GunModels.HyperLauncher, HoldPosition = new Vector3(-0.12f, -0.03f, -0.02f), HoldEuler = new Vector3(3f, 12f, 0f), AimDistance = 0.24f,
        };

        // Blasters: they fire glowing bolts you see fly, like the laser cannons, with the films' struck-wire "pew".
        // A bolt melts a hole wherever it lands, wrecks machinery around it and pushes the ship a little.
        private static BulletSpec BoltSpec(byte style)
        {
            var s = Arsenal.Vehicles.Laser.Style(style);
            return new BulletSpec { PlayerDamage = s.PlayerDamage, PanelDamage = s.PanelDamage, Penetration = 0f, HoleRadius = s.HoleRadius, Range = Arsenal.Vehicles.Laser.Range, ObjectDamage = s.ObjectDamage };
        }

        public static readonly HandWeaponDef BlasterPistol = new HandWeaponDef
        {
            Kind = WeaponKind.BlasterPistol, ItemId = BlasterPistolId, Category = ArmoryCategory.Sidearms, Rarity = Rarity.Uncommon,
            NameFr = "Blaster lourd « Contrebandier »", NameEn = "\"Smuggler\" heavy blaster",
            DescFr = "Pistolet blaster lourd à lunette. Ses traits rouges frappent fort et font fondre la coque. Chauffe à chaque tir.",
            DescEn = "Scoped heavy blaster pistol. Its red bolts hit hard and melt through hulls. Heats up with each shot.",
            Cost = new[] { (Iron, (byte)6), (Copper, (byte)3), (Glass, (byte)1) },
            Mode = FireMode.Bolt, BoltStyle = Arsenal.Vehicles.Laser.PistolBolt, Ammo = AmmoMode.Heat, HeatPerShot = 0.16f, CoolPerSecond = 0.3f,
            Automatic = false, RoundsPerMinute = 200,
            SpreadDegrees = 0.5f, AimSpreadDegrees = 0.1f, RecoilKick = 0.06f, AimFov = 36f, Scope = ScopeKind.Tactical, Mass = 1.6f,
            Bullet = BoltSpec(Arsenal.Vehicles.Laser.PistolBolt),
            Tracer = TracerStyle.None, TracerColor = new Color(1f, 0.15f, 0.08f), Sound = SoundStyle.Blaster, MuzzleSize = 0.12f,
            Model = GunModels.BlasterPistol, HoldPosition = Sidearm, HoldEuler = SidearmRot, AimDistance = 0.3f,
        };

        public static readonly HandWeaponDef BlasterRifle = new HandWeaponDef
        {
            Kind = WeaponKind.BlasterRifle, ItemId = BlasterRifleId, Category = ArmoryCategory.Rifles, Rarity = Rarity.Uncommon,
            NameFr = "Fusil blaster « Légion »", NameEn = "\"Legion\" blaster rifle",
            DescFr = "Fusil blaster automatique à crosse pliante. Rafales de traits rouges. Chauffe vite en rafale.",
            DescEn = "Automatic blaster rifle with a folding stock. Bursts of red bolts. Heats up fast on full auto.",
            Cost = new[] { (Iron, (byte)8), (Copper, (byte)4), (Glass, (byte)1) },
            Mode = FireMode.Bolt, BoltStyle = Arsenal.Vehicles.Laser.RifleBolt, Ammo = AmmoMode.Heat, HeatPerShot = 0.06f, CoolPerSecond = 0.2f,
            Automatic = true, RoundsPerMinute = 420,
            SpreadDegrees = 1.4f, AimSpreadDegrees = 0.45f, RecoilKick = 0.03f, AimFov = 32f, Scope = ScopeKind.Tactical, Mass = 3.2f,
            Bullet = BoltSpec(Arsenal.Vehicles.Laser.RifleBolt),
            Tracer = TracerStyle.None, TracerColor = new Color(1f, 0.15f, 0.08f), Sound = SoundStyle.Blaster, MuzzleSize = 0.14f,
            Model = GunModels.BlasterRifle, HoldPosition = Long,
        };

        public static readonly HandWeaponDef BlasterCarbine = new HandWeaponDef
        {
            Kind = WeaponKind.BlasterCarbine, ItemId = BlasterCarbineId, Category = ArmoryCategory.Energy, Rarity = Rarity.Rare,
            NameFr = "Carabine blaster « Éclaireur »", NameEn = "\"Scout\" blaster carbine",
            DescFr = "Carabine légère d'éclaireur : traits verts rapides, cadence élevée, chauffe lentement.",
            DescEn = "Light scout carbine: fast green bolts, high rate of fire, slow to heat up.",
            Cost = new[] { (Iron, (byte)6), (Copper, (byte)4), (Glass, (byte)2) },
            Mode = FireMode.Bolt, BoltStyle = Arsenal.Vehicles.Laser.CarbineBolt, Ammo = AmmoMode.Heat, HeatPerShot = 0.045f, CoolPerSecond = 0.25f,
            Automatic = true, RoundsPerMinute = 540,
            SpreadDegrees = 1.2f, AimSpreadDegrees = 0.35f, RecoilKick = 0.022f, AimFov = 36f, Scope = ScopeKind.Tactical, ReticleColor = new Color(0.3f, 1f, 0.25f), Mass = 2.5f,
            Bullet = BoltSpec(Arsenal.Vehicles.Laser.CarbineBolt),
            Tracer = TracerStyle.None, TracerColor = new Color(0.2f, 1f, 0.15f), Sound = SoundStyle.Blaster, MuzzleSize = 0.12f,
            Model = GunModels.BlasterCarbine, HoldPosition = Long,
        };

        public static readonly HandWeaponDef HeavyBlaster = new HandWeaponDef
        {
            Kind = WeaponKind.HeavyBlaster, ItemId = HeavyBlasterId, Category = ArmoryCategory.Heavy, Rarity = Rarity.Epic,
            NameFr = "Blaster lourd à répétition « Tonnerre »", NameEn = "\"Thunder\" heavy repeating blaster",
            DescFr = "Blaster d'appui à répétition : un déluge de gros traits bleus qui percent et secouent les vaisseaux. Surchauffe après une longue rafale.",
            DescEn = "Repeating support blaster: a torrent of big blue bolts that burn through and rock ships. Overheats after a long burst.",
            Cost = new[] { (Iron, (byte)16), (Copper, (byte)8), (Glass, (byte)3), (Shard, (byte)1) },
            Mode = FireMode.Bolt, BoltStyle = Arsenal.Vehicles.Laser.HeavyBolt, Ammo = AmmoMode.Heat, HeatPerShot = 0.05f, CoolPerSecond = 0.2f,
            Automatic = true, RoundsPerMinute = 600,
            SpreadDegrees = 2f, AimSpreadDegrees = 1.4f, RecoilKick = 0.02f, AimFov = 0f, Mass = 13f,
            Bullet = BoltSpec(Arsenal.Vehicles.Laser.HeavyBolt),
            Tracer = TracerStyle.None, TracerColor = new Color(0.15f, 0.45f, 1f), Sound = SoundStyle.HeavyBlaster, MuzzleSize = 0.18f,
            Model = GunModels.HeavyBlaster, HoldPosition = new Vector3(-0.06f, 0.0f, 0.02f), HoldEuler = new Vector3(4f, 14f, 0f),
        };

        public static readonly HandWeaponDef[] Guns =
        {
            Pistol, LaserPistol, Gauss, Smg, Rifle, PlasmaShotgun, Marksman, Railgun, Minigun, Flamethrower,
            GrenadeLauncher, RocketLauncher, SamLauncher, HyperLauncher, EmpCarbine, Tesla, Disintegrator,
            BlasterPistol, BlasterRifle, BlasterCarbine, HeavyBlaster,
        };

        public static HandWeaponDef ByItem(uint itemId)
        {
            foreach (HandWeaponDef d in Guns)
                if (d.ItemId == itemId) return d;
            return null;
        }

        public static HandWeaponDef ByKind(WeaponKind kind)
        {
            foreach (HandWeaponDef d in Guns)
                if (d.Kind == kind) return d;
            return null;
        }

        public static Color RarityColor(Rarity r) => r switch
        {
            Rarity.Uncommon => new Color(0.45f, 0.85f, 0.5f),
            Rarity.Rare => new Color(0.35f, 0.7f, 1f),
            Rarity.Epic => new Color(0.75f, 0.45f, 1f),
            Rarity.Legendary => new Color(1f, 0.62f, 0.2f),
            _ => new Color(0.8f, 0.84f, 0.88f),
        };

        public static string RarityName(Rarity r) => r switch
        {
            Rarity.Uncommon => Loc.T("Peu commun", "Uncommon"),
            Rarity.Rare => Loc.T("Rare", "Rare"),
            Rarity.Epic => Loc.T("Épique", "Epic"),
            Rarity.Legendary => Loc.T("Légendaire", "Legendary"),
            _ => Loc.T("Commun", "Common"),
        };

        public static string CategoryName(ArmoryCategory c) => c switch
        {
            ArmoryCategory.Sidearms => Loc.T("Armes de poing", "Sidearms"),
            ArmoryCategory.Rifles => Loc.T("Fusils", "Rifles"),
            ArmoryCategory.Heavy => Loc.T("Armes lourdes", "Heavy"),
            ArmoryCategory.Energy => Loc.T("Énergie", "Energy"),
            _ => Loc.T("Explosifs", "Explosives"),
        };
    }
}
