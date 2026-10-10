using UnityEngine;

namespace Arsenal.Visual
{
    /// <summary>Renders a model to a small transparent texture for inventory icons.</summary>
    internal static class IconRenderer
    {
        private const int Size = 128;

        public static Texture2D Render(GameObject model, Vector3 euler)
        {
            int layer = LayerMask.NameToLayer("PartThumbnailRender");
            if (layer < 0) layer = 31;
            var holder = new GameObject("Arsenal.IconStage");
            holder.transform.position = new Vector3(0f, -20000f, 0f);
            GameObject copy = Object.Instantiate(model, holder.transform);
            copy.transform.localPosition = Vector3.zero;
            copy.transform.localRotation = Quaternion.Euler(euler);
            foreach (Transform t in copy.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;

            Bounds b = new Bounds(copy.transform.position, Vector3.zero);
            foreach (Renderer r in copy.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);

            var camGo = new GameObject("Arsenal.IconCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(b.extents.x, b.extents.y) * 1.1f + 0.005f;
            cam.transform.position = b.center + Vector3.back * (b.extents.z + 2f);
            cam.transform.rotation = Quaternion.identity;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = b.extents.z * 2f + 4f;
            cam.cullingMask = 1 << layer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);

            var lightGo = new GameObject("Arsenal.IconLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.cullingMask = 1 << layer;
            light.intensity = 1.2f;
            lightGo.transform.rotation = Quaternion.Euler(35f, 25f, 0f);

            var rt = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32);
            Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = model.name + ".Icon" };
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
            }
            catch (System.Exception ex)
            {
                ArsenalMod.Log.Warning($"Icon render failed for {model.name}: {ex.Message}");
            }
            finally
            {
                cam.targetTexture = null;
                RenderTexture.ReleaseTemporary(rt);
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(lightGo);
                Object.DestroyImmediate(holder);
            }
            return tex;
        }
    }
}
