using Arsenal.Core;
using Rendering.Services;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Arsenal.Visual
{
    /// <summary>
    /// Inventory icons, drawn by the game's own thumbnail renderer (the one its items and parts use) so they
    /// get the same lighting, framing and transparent background.
    /// </summary>
    internal static class IconRenderer
    {
        private const int Size = 256;

        private static readonly System.Collections.Generic.List<(Texture2D tex, GameObject model, Vector3 euler)> Pending = new System.Collections.Generic.List<(Texture2D, GameObject, Vector3)>();

        /// <summary>
        /// The icon texture is handed out at once and filled a moment after the game scene starts drawing:
        /// rendering during the scene load gives empty pictures.
        /// </summary>
        public static void RenderLater(Items.Model.ItemSettings item, GameObject model, Vector3 euler)
        {
            var tex = new Texture2D(Size, Size, GraphicsFormat.R8G8B8A8_UNorm, TextureCreationFlags.None) { name = model.name + ".Icon" };
            item.icon = tex;
            Pending.Add((tex, model, euler));
            if (Pending.Count == 1) MelonLoader.MelonCoroutines.Start(Flush());
        }

        private static System.Collections.IEnumerator Flush()
        {
            ObjectThumbnailRenderer renderer = null;
            for (int i = 0; i < 600 && renderer == null; i++)
            {
                yield return null;
                if (ArsenalMod.InGame) renderer = Svc.Get<ObjectThumbnailRenderer>();
            }
            for (int i = 0; i < 10; i++) yield return null;
            foreach (var (tex, model, euler) in Pending)
                if (tex != null && model != null && renderer != null)
                    try
                    {
                        GameObject copy = Object.Instantiate(model);
                        copy.transform.position = new Vector3(0f, -20000f, 0f);
                        copy.SetActive(true);
                        renderer.Render(copy, Quaternion.Euler(euler), tex);
                    }
                    catch (System.Exception ex)
                    {
                        ArsenalMod.Log.Warning($"Icon render failed for {model.name}: {ex.Message}");
                    }
            Pending.Clear();
        }
    }
}
