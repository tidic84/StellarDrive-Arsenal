namespace Arsenal.Weapons
{
    internal enum WeaponKind : byte
    {
        Pistol = 1,
        Rifle = 2,
        Marksman = 3,
        Grenade = 4,
        Charge = 5,
        Detonator = 6,
        // ship-mounted
        ShipGun = 20,
        ShipLaser = 21,
        ShipMissile = 22,
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
    }

    internal sealed class HandWeaponDef
    {
        public WeaponKind Kind;
        public uint ItemId;
        public string NameFr, NameEn;
        public string DescFr, DescEn;
        public bool Automatic;
        public float RoundsPerMinute;
        public int Magazine;
        public float ReloadTime;
        public float SpreadDegrees;
        public float AimSpreadDegrees;
        public float RecoilKick;     // metres of visual kick-back
        public float AimFov;         // camera FOV while aiming (0 = no zoom)
        public float Mass;
        public BulletSpec Bullet;
        public UnityEngine.Vector3 HoldPosition;  // model offset in the tool visuals
        public UnityEngine.Vector3 HoldEuler;
    }

    internal static class WeaponDefs
    {
        // Item ids far from the game's (0-3, 100-112).
        public const uint PistolId = 9101, RifleId = 9102, MarksmanId = 9103, GrenadeId = 9104, ChargeId = 9105, DetonatorId = 9106;

        public static readonly HandWeaponDef Pistol = new HandWeaponDef
        {
            Kind = WeaponKind.Pistol, ItemId = PistolId,
            NameFr = "Pistolet de bord", NameEn = "Sidearm",
            DescFr = "Arme de poing semi-automatique. Traverse le verre et le bois.", DescEn = "Semi-automatic sidearm. Goes through glass and wood.",
            HoldPosition = new UnityEngine.Vector3(-0.13f, 0.12f, -0.1f), HoldEuler = new UnityEngine.Vector3(6f, 24f, 0f),
            Automatic = false, RoundsPerMinute = 360, Magazine = 12, ReloadTime = 1.3f,
            SpreadDegrees = 0.6f, AimSpreadDegrees = 0.2f, RecoilKick = 0.05f, AimFov = 55f, Mass = 1.2f,
            Bullet = new BulletSpec { PlayerDamage = 20, PanelDamage = 6, Penetration = 13, HoleRadius = 0.03f, ObjectDamage = 0.4f },
        };

        public static readonly HandWeaponDef Rifle = new HandWeaponDef
        {
            Kind = WeaponKind.Rifle, ItemId = RifleId,
            NameFr = "Fusil d'assaut", NameEn = "Assault rifle",
            DescFr = "Tir automatique. Perce une plaque de fer.", DescEn = "Fully automatic. Punches through one iron plate.",
            HoldPosition = new UnityEngine.Vector3(-0.08f, 0.07f, 0.02f), HoldEuler = new UnityEngine.Vector3(5f, 18f, 0f),
            Automatic = true, RoundsPerMinute = 600, Magazine = 30, ReloadTime = 2.0f,
            SpreadDegrees = 1.4f, AimSpreadDegrees = 0.45f, RecoilKick = 0.04f, AimFov = 48f, Mass = 3.5f,
            Bullet = new BulletSpec { PlayerDamage = 14, PanelDamage = 7, Penetration = 36, HoleRadius = 0.03f, ObjectDamage = 0.4f },
        };

        public static readonly HandWeaponDef Marksman = new HandWeaponDef
        {
            Kind = WeaponKind.Marksman, ItemId = MarksmanId,
            NameFr = "Fusil anti-matériel", NameEn = "Anti-materiel rifle",
            DescFr = "Gros calibre. Traverse plusieurs plaques de fer. Clic droit pour viser.", DescEn = "Heavy calibre. Goes through several iron plates. Right click to aim.",
            HoldPosition = new UnityEngine.Vector3(-0.08f, 0.06f, 0.0f), HoldEuler = new UnityEngine.Vector3(5f, 18f, 0f),
            Automatic = false, RoundsPerMinute = 50, Magazine = 5, ReloadTime = 2.8f,
            SpreadDegrees = 2.5f, AimSpreadDegrees = 0.02f, RecoilKick = 0.16f, AimFov = 18f, Mass = 9f,
            Bullet = new BulletSpec { PlayerDamage = 85, PanelDamage = 60, Penetration = 110, HoleRadius = 0.07f, Range = 900f, ObjectDamage = 3f },
        };

        public static readonly HandWeaponDef[] Guns = { Pistol, Rifle, Marksman };

        public static HandWeaponDef ByItem(uint itemId)
        {
            foreach (HandWeaponDef d in Guns)
                if (d.ItemId == itemId) return d;
            return null;
        }
    }
}
