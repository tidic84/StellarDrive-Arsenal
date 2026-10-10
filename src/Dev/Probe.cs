#if DEV
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using Items.Model;
using MelonLoader;
using Research.Interface.Values;
using Ships.Interface.Model.Parts;
using Ships.Interface.Settings;
using Ships.Parts.Common.Hull;
using UI.BuildMenu;
using UnityEngine;

namespace Arsenal.Dev
{
    // Dev-only: dumps the game's items, parts, prefabs and hull meshes to UserData/ArsenalProbe.txt.
    internal static class Probe
    {
        private static readonly string[] DetailedStates =
        {
            "SignalDisplayState", "PilotSeatAdvState", "StorageState", "TargetingComputerState", "LeverState", "ConstantState", "ThrusterState"
        };

        public static void Dump(string tag)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"===== PROBE {tag} {DateTime.Now:T}");

            sb.AppendLine("--- layers");
            for (int i = 0; i < 32; i++)
            {
                string n = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(n)) sb.AppendLine($"  {i}: {n}");
            }

            sb.AppendLine("--- items");
            foreach (var list in Resources.FindObjectsOfTypeAll<ItemSettingsList>())
                sb.AppendLine($"  ItemSettingsList '{list.name}'");
            foreach (var item in Resources.FindObjectsOfTypeAll<ItemSettings>().OrderBy(i => i.id))
            {
                sb.AppendLine($"  item {item.id} '{item.itemName}' asset={item.name} stack={item.maxStackSize} mass={item.mass} icon={(item.icon ? item.icon.name + " " + item.icon.width + "x" + item.icon.height + " " + item.icon.format : "-")} obj={(item.itemObject ? item.itemObject.name : "-")} tool={(item.toolSettings ? item.toolSettings.name : "-")}");
                if (item.toolSettings != null && item.toolSettings.prefab != null)
                    DumpHierarchy(sb, item.toolSettings.prefab.transform, "    ");
                if (item.itemObject != null && item.id < 5)
                    DumpHierarchy(sb, item.itemObject.transform, "    [obj] ");
            }

            sb.AppendLine("--- parts");
            foreach (var p in Resources.FindObjectsOfTypeAll<PartSettings>().OrderBy(p => p.id))
            {
                string extra = p is HullPartSettings h ? $" hull={h.hullMaterialType}/{h.hullMeshSegmentType} ownMesh={h.hasItsOwnMesh}" : "";
                sb.AppendLine($"  part {p.id} '{p.fullLabel}' asset={p.name} type={p.GetType().Name} state={p.internalStateClass} create={p.customCreationClass} snap={p.snappingStyle} size={p.size} mass={p.mass} prefab={(p.part ? p.part.name : "-")} cost={string.Join(",", p.BuildingCost.Select(c => c.ItemId + "x" + c.Quantity))}{extra}");
                if (p.part != null && DetailedStates.Contains(p.internalStateClass))
                    DumpHierarchy(sb, p.part.transform, "    ");
            }

            sb.AppendLine("--- hull segment meshes");
            foreach (var seg in Resources.FindObjectsOfTypeAll<HullMeshSegmentsSettings>())
            {
                sb.AppendLine($"  HullMeshSegmentsSettings '{seg.name}'");
                foreach (HullMeshSegmentType t in Enum.GetValues(typeof(HullMeshSegmentType)))
                {
                    try
                    {
                        Mesh m = seg.GetSegmentMesh(t);
                        if (m == null) { sb.AppendLine($"    {t}: null"); continue; }
                        sb.AppendLine($"    {t}: '{m.name}' verts={m.vertexCount} tris={m.triangles.Length / 3} bounds={m.bounds.center}/{m.bounds.size} readable={m.isReadable} sub={m.subMeshCount}");
                        if (t == HullMeshSegmentType.Flat)
                        {
                            var v = m.vertices; var n = m.normals;
                            for (int i = 0; i < v.Length; i++)
                                sb.AppendLine($"      v{i} {v[i].x:F4},{v[i].y:F4},{v[i].z:F4} n {n[i].x:F2},{n[i].y:F2},{n[i].z:F2}");
                            var tr = m.triangles;
                            sb.AppendLine("      tris " + string.Join(",", tr));
                        }
                    }
                    catch (Exception ex) { sb.AppendLine($"    {t}: {ex.GetType().Name}"); }
                }
            }

            sb.AppendLine("--- build menu");
            foreach (var bm in Resources.FindObjectsOfTypeAll<BuildMenu>())
            {
                var tabs = HarmonyLib.Traverse.Create(bm).Field("tabsConfig").GetValue<BuildMenuTabConfig[]>();
                sb.AppendLine($"  BuildMenu '{bm.name}' scene={bm.gameObject.scene.name} tabs={tabs?.Length}");
                if (tabs == null) continue;
                foreach (var t in tabs)
                    sb.AppendLine($"    tab '{t.name}' icon={(t.icon ? t.icon.name + " " + t.icon.width + "x" + t.icon.height : "-")} rows={string.Join(" | ", t.rows.Select(r => string.Join(",", r.parts.Select(p => p ? p.id.ToString() : "null"))))}");
            }

            sb.AppendLine("--- research");
            foreach (var rl in Resources.FindObjectsOfTypeAll<ResearchSettingsList>())
            {
                sb.AppendLine($"  list '{rl.name}' count={rl.list.Count}");
                foreach (var r in rl.list)
                    sb.AppendLine($"    {r.id} '{r.techName}' pos={r.positionInTechTree} demo={r.availableInDemo} hide={r.hideFromTechTree} parts={string.Join(",", r.unlockedParts.Select(p => p ? p.id.ToString() : "null"))} req={string.Join(",", r.requirements.Select(q => q ? q.id.ToString() : "null"))} cost={string.Join(",", r.resourceCost.Select(c => c.ItemId + "x" + c.Quantity))}");
            }

            string path = Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "ArsenalProbe.txt");
            File.AppendAllText(path, sb.ToString());
            Melon<ArsenalMod>.Logger.Msg($"Probe written to {path}");
        }

        public static void DumpMaterials()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"===== MATERIALS {DateTime.Now:T}");
            string[] wanted = { "PartMetal", "LogicGates", "MiningLaser", "PilotSeatMat", "StorageChest", "Plugs", "RecyclerTool", "BuildTool", "LaserMat", "LaserSparkMat", "ParticlesUnlit", "VelocityHologram", "ArrowDirection" };
            foreach (var m in Resources.FindObjectsOfTypeAll<Material>().Where(m => wanted.Contains(m.name)).GroupBy(m => m.name).Select(g => g.First()))
            {
                var sh = m.shader;
                sb.AppendLine($"  mat '{m.name}' shader={sh.name} queue={m.renderQueue} keywords={string.Join(",", m.shaderKeywords)}");
                for (int i = 0; i < sh.GetPropertyCount(); i++)
                {
                    string pn = sh.GetPropertyName(i);
                    var pt = sh.GetPropertyType(i);
                    string val = pt switch
                    {
                        UnityEngine.Rendering.ShaderPropertyType.Color => m.GetColor(pn).ToString(),
                        UnityEngine.Rendering.ShaderPropertyType.Float or UnityEngine.Rendering.ShaderPropertyType.Range => m.GetFloat(pn).ToString(),
                        UnityEngine.Rendering.ShaderPropertyType.Vector => m.GetVector(pn).ToString(),
                        UnityEngine.Rendering.ShaderPropertyType.Texture => m.GetTexture(pn) ? m.GetTexture(pn).name + " " + m.GetTexture(pn).width + "x" + m.GetTexture(pn).height : "null",
                        _ => "?"
                    };
                    sb.AppendLine($"    {pn} ({pt}) = {val}");
                }
            }
            foreach (var mesh in Resources.FindObjectsOfTypeAll<Mesh>().Where(x => x.name == "MiningLaser" || x.name == "SignalDisplay" || x.name == "PaintTool" || x.name == "StorageChest"))
            {
                if (!mesh.isReadable) { sb.AppendLine($"  mesh {mesh.name} not readable"); continue; }
                var uv = mesh.uv; var col = mesh.colors;
                sb.AppendLine($"  mesh {mesh.name} uv={uv.Length} colors={col.Length} uv2={mesh.uv2.Length}");
                var uniq = uv.Select(u => $"{u.x:F3},{u.y:F3}").Distinct().Take(40);
                sb.AppendLine("    uvs " + string.Join(" ", uniq));
                if (col.Length > 0) sb.AppendLine("    cols " + string.Join(" ", col.Select(c => c.ToString()).Distinct().Take(20)));
                var uv2 = mesh.uv2;
                if (uv2.Length > 0)
                    sb.AppendLine("    uv2 " + string.Join(" ", uv2.GroupBy(u => $"{u.x:F4},{u.y:F4}").OrderByDescending(g => g.Count()).Take(30).Select(g => g.Key + "x" + g.Count())));
            }
            string path = Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "ArsenalProbe.txt");
            File.AppendAllText(path, sb.ToString());
            // Save the palette textures to look at them.
            foreach (var m in Resources.FindObjectsOfTypeAll<Material>().Where(m => m.name == "PartMetal" || m.name == "MiningLaser"))
            {
                foreach (string pn in m.GetTexturePropertyNames())
                {
                    var tex = m.GetTexture(pn) as Texture2D;
                    if (tex == null) continue;
                    var rt = RenderTexture.GetTemporary(tex.width, tex.height);
                    Graphics.Blit(tex, rt);
                    var prev = RenderTexture.active; RenderTexture.active = rt;
                    var copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                    copy.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0); copy.Apply();
                    RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
                    File.WriteAllBytes(Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, $"tex_{m.name}_{pn}.png"), copy.EncodeToPNG());
                    if (pn == "_ColorPalette")
                    {
                        var px = new StringBuilder($"  palette {tex.name} fmt={tex.format} filter={tex.filterMode}:");
                        for (int y = 0; y < tex.height; y++)
                            for (int x = 0; x < tex.width; x++)
                            {
                                Color c = copy.GetPixel(x, y);
                                if (c.r + c.g + c.b > 0.01f) px.Append($" [{x},{y}]={c.r:F2},{c.g:F2},{c.b:F2}");
                            }
                        File.AppendAllText(path, px + "\n");
                    }
                }
            }
            Melon<ArsenalMod>.Logger.Msg("Materials probe written");
        }

        private static void DumpHierarchy(StringBuilder sb, Transform t, string indent, int depth = 0)
        {
            if (depth > 7) return;
            var comps = t.GetComponents<Component>().Where(c => c != null && !(c is Transform)).Select(Describe);
            sb.AppendLine($"{indent}{new string(' ', depth * 2)}{t.name} [layer {t.gameObject.layer} tag {t.tag} active {t.gameObject.activeSelf}] pos={t.localPosition} rot={t.localEulerAngles} scale={t.localScale} :: {string.Join("; ", comps)}");
            for (int i = 0; i < t.childCount; i++)
                DumpHierarchy(sb, t.GetChild(i), indent, depth + 1);
        }

        private static string Describe(Component c)
        {
            switch (c)
            {
                case MeshFilter mf:
                    return mf.sharedMesh ? $"MeshFilter({mf.sharedMesh.name} {mf.sharedMesh.vertexCount}v {mf.sharedMesh.bounds.size})" : "MeshFilter(null)";
                case Renderer r:
                    return $"{r.GetType().Name}({string.Join(",", r.sharedMaterials.Select(m => m ? m.name + "/" + (m.shader ? m.shader.name : "?") : "null"))})";
                case BoxCollider b:
                    return $"BoxCollider({b.center} {b.size} trig={b.isTrigger})";
                case Collider col:
                    return $"{col.GetType().Name}(trig={col.isTrigger})";
                default:
                    return c.GetType().FullName;
            }
        }
    }
}
#endif
