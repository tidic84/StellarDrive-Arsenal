# Arsenal — StellarDrive mod

Firearms, explosives and ship weapons for **StellarDrive (Demo)**, made to look like the rest of the game (the models use the game's own hand-drawn atlas, colour palette and ink outlines). A MelonLoader mod, packaged for [Stellar Mod Manager](https://github.com/jollyname/StellarModManager).

![thumbnail](Mods/Thumbnail.png)

## Holes, penetration and breaking

Bullets and blasts damage hull panels for real:

- **Real holes.** Each flat panel is a 16 × 16 grid (6 cm cells). A bullet punches a small hole through it; a blast tears a ragged one sized by distance. The holes are cut into the game's own panel mesh, so they keep the panel's colours and edges, and you can see (and shoot) through them.
- **Penetration.** A round goes through a panel if it has enough penetration for the material and the angle: glass < wood < iron. It loses some on the way and carries on to the next panel; otherwise it stops and only dents the panel.
- **Breaking.** Every hit also wears the panel down. Once it has no strength left, or too much of it is gone, it breaks into the game's own debris.
- Damage is decided by the host and synced to every player, and it is **saved with the world** (`arsenal-damage.txt` next to `world.json`).

Slopes, corners and doors take damage and break, but only flat panels get holes.

![Bullet holes through an iron wall](docs/bullet_holes.jpg)

## Hand weapons

Use them like the game's tools (toolbar). Left click fires, **R** reloads, right click aims.

| Weapon | Fire | Goes through |
| --- | --- | --- |
| Sidearm | semi-auto, 12 rounds | glass, wood |
| Assault rifle | full auto, 30 rounds, red-dot sight | one iron plate |
| Anti-materiel rifle | bolt action, 5 rounds, scope | several iron plates, big holes |
| Grenade | left click throws (3 s fuse, bounces) | blast |
| Breaching charge | left click sticks it to a surface, right click detonates all yours | big blast |

![Breaching charge through an iron wall](docs/breaching_charge.jpg)
![Scope](docs/scope.jpg)

## Ship weapons

New tab **Arsenal** in the build menu, unlocked by two new technologies in the research tree: **Weaponry** (after *Powered Ship Controls*) and **Heavy weaponry**.

- **Gunner seat**: an advanced pilot seat with three more signal outputs that follow the pilot's buttons: *Fire 1* (left click), *Fire 2* (right click), *Fire 3* (middle click). Wire them with signal cables to the weapons — or to any logic, as with the game's other signals.
- **Heavy machine gun**: twin barrels, fires while its input signal is on, armour-piercing rounds.
- **Air-to-air missile pod**: locks the nearest ship in front of it, fires a homing missile on each signal pulse, 4 missiles that reload by themselves, proximity fuse.
- **Laser cannon**: a continuous beam that melts holes through hulls, overheats after a few seconds.
- **Armory**: a chest delivered stocked with the hand weapons, grenades and charges.

While you pilot, the HUD shows where each weapon points, the missile lock and the laser heat.

![Gunship](docs/gunship.jpg)
![Gunner seat HUD with a missile lock](docs/gunner_seat_hud.jpg)
![Missile hit](docs/missile_hit.jpg)
![Laser cannon](docs/laser.jpg)
![Build menu](docs/build_menu.jpg)

## Multiplayer

Works in multiplayer; **every player needs the mod** (the items, parts and research only exist with it). The player who fires does the aiming and hit detection, like the game's own tools; the host owns the damage (holes, health, explosions) and syncs it to everyone. Ship weapons are fired by whoever pilots that ship, or by the host when nobody does.

## Settings

`UserData/MelonPreferences.cfg`, section `[Arsenal]`:

| Key | Default | Meaning |
| --- | --- | --- |
| `SoundVolume` | `1` | Weapon sound volume (0 = silent) |
| `GunnerFire1` | `<Mouse>/leftButton` | Input path for *Fire 1* in the gunner seat |
| `GunnerFire2` | `<Mouse>/rightButton` | Input path for *Fire 2* |
| `GunnerFire3` | `<Mouse>/middleButton` | Input path for *Fire 3* |

## Building

Needs the .NET SDK (6+), the game, and MelonLoader 0.7.x installed in the game folder (Stellar Mod Manager installs it for you).

```powershell
./build.ps1                                   # default Steam path
./build.ps1 -GamePath "D:\SteamLibrary\steamapps\common\StellarDrive Demo"
```

This produces `dist/StellarDrive-Arsenal.zip` in the layout Stellar Mod Manager expects:

```
mod.json
Mods/Arsenal.dll
```

### Dev build

`dotnet build src/Arsenal.csproj -c Release -p:DefineConstants=DEV` adds test tools that are never in the release build: the game loads the `ARS_Test` world on its own and runs commands written to `UserData/arsenal_cmd.txt` (`give`, `wall`, `range`, `gun`, `sit`, `shot`, …). `dev.sh` builds, installs and starts the game; `net.sh` starts a host and a client on the same PC to test multiplayer.

## Publishing to Stellar Mod Manager

1. Push this folder to a GitHub repo named `StellarDrive-Arsenal` (branch `main`), keeping `Mods/mod.json` and `Mods/Thumbnail.png`.
2. Create a release tagged `v1.0.0` (must match `version` in `mod.json`) and attach `StellarDrive-Arsenal.zip`.
3. Open a PR on [jollyname/StellarModRepository](https://github.com/jollyname/StellarModRepository) adding to `mods.json`:

```json
{ "author": "<your GitHub user>", "repo": "StellarDrive-Arsenal", "metadata": "Mods" }
```
