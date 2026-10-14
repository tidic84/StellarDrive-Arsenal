#if DEV
using System.Collections.Generic;
using System.IO;
using Arsenal.Core;
using Arsenal.Visual;
using Arsenal.Weapons;
using MelonLoader.Utils;
using Rendering.Services;
using Ships.Interface.Settings;
using Ships;
using Ships.Interface.Model.Parts;
using Ships.Interface.Services;
using Ships.Services;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Arsenal.Dev
{
    /// <summary>Dev: renders models with the game's thumbnail renderer into one PNG to review them side by side.</summary>
    internal static class ModelSheet
    {
        private const int Tile = 360;

        public static void Render(string what, string filter)
        {
            var items = new List<(string name, System.Func<GameObject> make, Quaternion[] views)>();
            Quaternion side = Quaternion.Euler(0, -90f, 0), threeQ = Quaternion.Euler(-20f, -130f, 0), front = Quaternion.Euler(-15f, -160f, 0);
            if (what == "guns")
            {
                foreach (HandWeaponDef d in WeaponDefs.Guns)
                {
                    HandWeaponDef def = d;
                    items.Add((def.Kind.ToString(), () => def.Model().Root, new[] { side, threeQ }));
                }
                items.Add(("Grenade", () => WeaponModels.Grenade().Root, new[] { side, threeQ }));
                items.Add(("Charge", () => WeaponModels.Charge().Root, new[] { side, threeQ }));
            }
            else
            {
                var provider = new PartSettingsProvider();
                foreach (ushort id in new ushort[] { 9201, 9210, 9211, 9212, 9213, 9214, 9215, 9216, 9217, 9218, 9219, 9220, 9221, 9222 })
                {
                    PartSettings ps;
                    try { ps = provider.GetPartSettingById(id); } catch { continue; }
                    if (ps == null) continue;
                    items.Add((id.ToString(), () =>
                    {
                        GameObject go = Object.Instantiate(ps.part);
                        foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
                        return go;
                    }, new[] { Quaternion.Euler(25f, 150f, 0f) * Quaternion.identity, Quaternion.Euler(20f, 230f, 0f) }));
                }
            }
            if (!string.IsNullOrEmpty(filter)) items.RemoveAll(i => !i.name.ToLowerInvariant().Contains(filter.ToLowerInvariant()));
            int perRow = items.Count > 0 ? items[0].views.Length : 1;
            int cols = Mathf.Min(items.Count, what == "guns" ? 3 : 3) * perRow;
            int rows = Mathf.CeilToInt(items.Count / (float)(cols / perRow));
            var sheet = new Texture2D(cols * Tile, rows * Tile, TextureFormat.RGBA32, false);
            Color32[] bg = new Color32[sheet.width * sheet.height];
            for (int i = 0; i < bg.Length; i++) bg[i] = new Color32(38, 52, 64, 255);
            sheet.SetPixels32(bg);
            var renderer = Svc.Get<ObjectThumbnailRenderer>();
            for (int n = 0; n < items.Count; n++)
                for (int v = 0; v < perRow; v++)
                {
                    var tex = new Texture2D(Tile, Tile, GraphicsFormat.R8G8B8A8_UNorm, TextureCreationFlags.None);
                    GameObject go = items[n].make();
                    go.transform.position = new Vector3(0, -20000f, 0);
                    go.SetActive(true);
                    renderer.Render(go, items[n].views[v], tex);
                    var rt = RenderTexture.GetTemporary(Tile, Tile);
                    Graphics.Blit(tex, rt);
                    RenderTexture prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    int col = (n % (cols / perRow)) * perRow + v, row = rows - 1 - n / (cols / perRow);
                    var tmp = new Texture2D(Tile, Tile, TextureFormat.RGBA32, false);
                    tmp.ReadPixels(new Rect(0, 0, Tile, Tile), 0, 0);
                    RenderTexture.active = prev;
                    RenderTexture.ReleaseTemporary(rt);
                    Color32[] px = tmp.GetPixels32();
                    Color32[] dst = sheet.GetPixels32();
                    for (int y = 0; y < Tile; y++)
                        for (int x = 0; x < Tile; x++)
                        {
                            Color32 s = px[y * Tile + x];
                            int di = (row * Tile + y) * sheet.width + col * Tile + x;
                            float a = s.a / 255f;
                            Color32 d0 = dst[di];
                            dst[di] = new Color32((byte)(s.r * a + d0.r * (1 - a)), (byte)(s.g * a + d0.g * (1 - a)), (byte)(s.b * a + d0.b * (1 - a)), 255);
                        }
                    sheet.SetPixels32(dst);
                    Object.Destroy(tmp);
                    Object.Destroy(tex);
                }
            sheet.Apply();
            string path = Path.Combine(MelonEnvironment.UserDataDirectory, "sheet_" + what + (filter ?? "") + ".png");
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            ArsenalMod.Log.Msg($"[DEV] sheet {path} ({items.Count} models)");
        }

        /// <summary>Dev: each ship weapon part alone on a transparent background (cutp_<id>.png).</summary>
        public static void PartCutouts(int size, Quaternion view)
        {
            var renderer = Svc.Get<ObjectThumbnailRenderer>();
            var provider = new PartSettingsProvider();
            foreach (ushort id in new ushort[] { 9201, 9210, 9211, 9212, 9213, 9214, 9215, 9216, 9217, 9218, 9219, 9220, 9221, 9222, 9223, 9224 })
            {
                PartSettings ps;
                try { ps = provider.GetPartSettingById(id); } catch { continue; }
                if (ps == null) continue;
                GameObject go = Object.Instantiate(ps.part);
                foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
                Save(renderer, go, view, size, "cutp_" + id);
            }
        }

        private static void Save(ObjectThumbnailRenderer renderer, GameObject go, Quaternion view, int size, string name)
        {
            var tex = new Texture2D(size, size, GraphicsFormat.R8G8B8A8_UNorm, TextureCreationFlags.None);
            go.transform.position = new Vector3(0, -20000f, 0);
            go.SetActive(true);
            renderer.Render(go, view, tex);
            var rt = RenderTexture.GetTemporary(size, size);
            Graphics.Blit(tex, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tmp = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tmp.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            string path = Path.Combine(MelonEnvironment.UserDataDirectory, name + ".png");
            File.WriteAllBytes(path, tmp.EncodeToPNG());
            Object.Destroy(tmp);
            Object.Destroy(tex);
            Object.Destroy(go);
            ArsenalMod.Log.Msg($"[DEV] cutout {path}");
        }

        /// <summary>Dev: each hand weapon alone on a transparent background (cut_<kind>.png), for promo images.</summary>
        public static void Cutouts(string filter, int size, Quaternion view)
        {
            var renderer = Svc.Get<ObjectThumbnailRenderer>();
            foreach (HandWeaponDef def in WeaponDefs.Guns)
            {
                if (!string.IsNullOrEmpty(filter) && !def.Kind.ToString().ToLowerInvariant().Contains(filter.ToLowerInvariant())) continue;
                Save(renderer, def.Model().Root, view, size, "cut_" + def.Kind);
            }
        }
    }
}
#endif
