using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using FMODUnity;
using UnityEngine;
using Arsenal.Weapons;

namespace Arsenal.Visual
{
    /// <summary>
    /// Weapon sounds synthesised at load time and played in 3D through the game's FMOD system,
    /// routed to the game's SFX bus when it exists so the in-game volume slider applies.
    /// </summary>
    internal static class Sounds
    {
        private const int Rate = 44100;
        private static readonly Dictionary<string, FMOD.Sound> Cache = new Dictionary<string, FMOD.Sound>();
        private static FMOD.ChannelGroup _group;
        private static bool _groupResolved;
        private static readonly System.Random Rng = new System.Random(1234);
        public static float Volume = 1f;

        // ------------------------------------------------------------------ public sounds

        public static void Shot(WeaponKind w, Vector3 pos)
        {
            switch (w)
            {
                case WeaponKind.Pistol: Play("pistol", () => Gunshot(0.22f, 2400f, 120f, 0.8f), pos, 1f, Jitter(0.06f), 3f, 250f); break;
                case WeaponKind.Rifle: Play("rifle", () => Gunshot(0.26f, 1900f, 95f, 1f), pos, 1f, Jitter(0.05f), 3f, 320f); break;
                case WeaponKind.Marksman: Play("marksman", () => Gunshot(0.75f, 1300f, 60f, 1.6f), pos, 1.15f, Jitter(0.03f), 5f, 700f); break;
                case WeaponKind.ShipGun: Play("shipgun", () => Gunshot(0.32f, 1500f, 70f, 1.3f), pos, 1f, Jitter(0.05f), 5f, 600f); break;
            }
        }

        public static void Explosion(Vector3 pos, float radius) =>
            Play("explosion", Boom, pos, Mathf.Clamp(radius / 5f, 0.6f, 1.4f), Jitter(0.08f) - Mathf.Clamp01(radius / 20f) * 0.2f, 8f, 1500f);

        public static void DryFire(Vector3 pos) => Play("dry", () => Click(0.02f, 5000f), pos, 0.5f, 1f, 1f, 30f);

        public static void Reload(Vector3 pos) => Play("reload", ReloadClicks, pos, 0.6f, 1f, 1f, 30f);

        public static void Throw(Vector3 pos) => Play("throw", () => Whoosh(0.35f, false), pos, 0.5f, Jitter(0.1f), 1f, 40f);

        public static void Bounce(Vector3 pos) => Play("bounce", () => Click(0.05f, 1800f), pos, 0.4f, Jitter(0.2f), 1f, 40f);

        public static void Beep(Vector3 pos) => Play("beep", () => Tone(0.08f, 1650f), pos, 0.35f, 1f, 1f, 30f);

        public static void MissileLaunch(Vector3 pos) => Play("missile", () => Whoosh(1.4f, true), pos, 1f, Jitter(0.05f), 4f, 500f);

        public static FMOD.Channel LaserLoop(Vector3 pos) => Play("laser", LaserHum, pos, 0.8f, 1f, 4f, 300f, loop: true);

        public static void Move(FMOD.Channel ch, Vector3 pos)
        {
            if (!ch.hasHandle()) return;
            var p = new FMOD.VECTOR { x = pos.x, y = pos.y, z = pos.z };
            var v = new FMOD.VECTOR();
            ch.set3DAttributes(ref p, ref v);
        }

        public static void Stop(ref FMOD.Channel ch)
        {
            if (ch.hasHandle()) ch.stop();
            ch = default;
        }

        private static float Jitter(float amount) => 1f + ((float)Rng.NextDouble() * 2f - 1f) * amount;

        // ------------------------------------------------------------------ playback

        private static FMOD.Channel Play(string key, Func<float[]> synth, Vector3 pos, float volume, float pitch, float minDist, float maxDist, bool loop = false)
        {
            try
            {
                FMOD.System core = RuntimeManager.CoreSystem;
                if (!Cache.TryGetValue(key, out FMOD.Sound sound) || !sound.hasHandle())
                {
                    sound = Create(core, synth(), loop);
                    Cache[key] = sound;
                }
                ResolveGroup(core);
                core.playSound(sound, _group, true, out FMOD.Channel ch);
                var p = new FMOD.VECTOR { x = pos.x, y = pos.y, z = pos.z };
                var v = new FMOD.VECTOR();
                ch.set3DAttributes(ref p, ref v);
                ch.set3DMinMaxDistance(minDist, maxDist);
                ch.setVolume(volume * Volume);
                ch.setPitch(pitch);
                ch.setPaused(false);
                return ch;
            }
            catch (Exception ex)
            {
                ArsenalMod.Log.Warning($"Sound {key} failed: {ex.Message}");
                return default;
            }
        }

        private static void ResolveGroup(FMOD.System core)
        {
            if (_groupResolved && _group.hasHandle()) return;
            _groupResolved = true;
            core.getMasterChannelGroup(out _group);
            try
            {
                RuntimeManager.StudioSystem.getBankList(out FMOD.Studio.Bank[] banks);
                foreach (FMOD.Studio.Bank bank in banks)
                {
                    bank.getBusList(out FMOD.Studio.Bus[] buses);
                    foreach (FMOD.Studio.Bus bus in buses)
                    {
                        bus.getPath(out string path);
                        if (path == null || path.IndexOf("SFX", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        bus.lockChannelGroup();
                        RuntimeManager.StudioSystem.flushCommands();
                        if (bus.getChannelGroup(out FMOD.ChannelGroup g) == FMOD.RESULT.OK && g.hasHandle())
                        {
                            _group = g;
                            ArsenalMod.Log.Msg($"Sounds routed to {path}");
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ArsenalMod.Log.Warning($"SFX bus lookup failed: {ex.Message}");
            }
        }

        public static void Reset()
        {
            _groupResolved = false;
            _group = default;
        }

        private static FMOD.Sound Create(FMOD.System core, float[] samples, bool loop)
        {
            byte[] wav = ToWav(samples);
            var info = new FMOD.CREATESOUNDEXINFO
            {
                cbsize = Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO)),
                length = (uint)wav.Length,
            };
            FMOD.MODE mode = FMOD.MODE.OPENMEMORY | FMOD.MODE.CREATESAMPLE | FMOD.MODE._3D | FMOD.MODE._3D_LINEARSQUAREROLLOFF;
            mode |= loop ? FMOD.MODE.LOOP_NORMAL : FMOD.MODE.LOOP_OFF;
            core.createSound(wav, mode, ref info, out FMOD.Sound sound);
            return sound;
        }

        private static byte[] ToWav(float[] s)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + s.Length * 2);
                w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                w.Write(16);
                w.Write((short)1);
                w.Write((short)1);
                w.Write(Rate);
                w.Write(Rate * 2);
                w.Write((short)2);
                w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(s.Length * 2);
                foreach (float f in s)
                    w.Write((short)(Mathf.Clamp(f, -1f, 1f) * 32000f));
                w.Flush();
                return ms.ToArray();
            }
        }

        // ------------------------------------------------------------------ synthesis

        private static float Noise() => (float)Rng.NextDouble() * 2f - 1f;

        private static float[] Normalise(float[] s, float peak = 0.95f)
        {
            float max = 0.0001f;
            foreach (float f in s) max = Mathf.Max(max, Mathf.Abs(f));
            for (int i = 0; i < s.Length; i++) s[i] = s[i] / max * peak;
            return s;
        }

        /// <summary>Crack (filtered noise burst) + thump (falling sine) + tail.</summary>
        private static float[] Gunshot(float length, float cutoff, float thumpHz, float body)
        {
            int n = (int)(length * Rate);
            var s = new float[n];
            float lp = 0f, lp2 = 0f, phase = 0f;
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float crack = Noise() * Mathf.Exp(-t * 55f);
                float tail = Noise() * Mathf.Exp(-t * 9f / body) * 0.35f;
                lp += a * (crack + tail - lp);
                lp2 += a * 0.5f * (lp - lp2);
                float f = thumpHz * (1f + 2.5f * Mathf.Exp(-t * 40f));
                phase += 2f * Mathf.PI * f / Rate;
                float thump = Mathf.Sin(phase) * Mathf.Exp(-t * 18f / body) * 0.9f * body;
                float attack = Mathf.Clamp01(i / 30f);
                s[i] = (lp2 * 1.6f + thump) * attack;
            }
            return Normalise(s);
        }

        private static float[] Boom()
        {
            int n = (int)(2.6f * Rate);
            var s = new float[n];
            float brown = 0f, lp = 0f, phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                brown = Mathf.Clamp(brown + Noise() * 0.06f, -1f, 1f);
                lp += 0.08f * (Noise() - lp);
                float rumble = brown * Mathf.Exp(-t * 1.6f);
                float crack = lp * Mathf.Exp(-t * 14f) * 1.4f;
                phase += 2f * Mathf.PI * (38f + 50f * Mathf.Exp(-t * 6f)) / Rate;
                float sub = Mathf.Sin(phase) * Mathf.Exp(-t * 2.4f) * 0.8f;
                s[i] = (rumble * 1.2f + crack + sub) * Mathf.Clamp01(i / 60f);
            }
            return Normalise(s);
        }

        private static float[] Click(float length, float hz)
        {
            int n = (int)(length * Rate);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                s[i] = (Mathf.Sin(2f * Mathf.PI * hz * t) * 0.6f + Noise() * 0.4f) * Mathf.Exp(-t * 120f);
            }
            return Normalise(s, 0.6f);
        }

        private static float[] ReloadClicks()
        {
            int n = (int)(0.5f * Rate);
            var s = new float[n];
            float[] at = { 0f, 0.18f, 0.36f };
            foreach (float start in at)
            {
                int o = (int)(start * Rate);
                for (int i = 0; i < (int)(0.05f * Rate) && o + i < n; i++)
                {
                    float t = (float)i / Rate;
                    s[o + i] += (Mathf.Sin(2f * Mathf.PI * 2600f * t) * 0.5f + Noise() * 0.5f) * Mathf.Exp(-t * 90f);
                }
            }
            return Normalise(s, 0.6f);
        }

        private static float[] Tone(float length, float hz)
        {
            int n = (int)(length * Rate);
            var s = new float[n];
            for (int i = 0; i < n; i++)
                s[i] = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * hz * i / Rate)) * 0.3f * Mathf.Clamp01((n - i) / 200f) * Mathf.Clamp01(i / 50f);
            return s;
        }

        private static float[] Whoosh(float length, bool rocket)
        {
            int n = (int)(length * Rate);
            var s = new float[n];
            float lp = 0f, bp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float k = t / length;
                float cutoff = rocket ? 600f + 2400f * Mathf.Exp(-k * 3f) : 400f + 2500f * Mathf.Sin(k * Mathf.PI);
                float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate);
                lp += a * (Noise() - lp);
                bp += a * (lp - bp);
                float env = rocket ? Mathf.Clamp01(t * 30f) * (1f - k) : Mathf.Sin(k * Mathf.PI);
                s[i] = (lp - bp * 0.5f) * env;
            }
            return Normalise(s, rocket ? 0.95f : 0.5f);
        }

        private static float[] LaserHum()
        {
            // exactly 0.5 s so the loop is seamless at 220 Hz harmonics
            int n = Rate / 2;
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float vib = Mathf.Sin(2f * Mathf.PI * 6f * t) * 0.003f;
                float saw = 2f * ((220f * t + vib * 50f) % 1f) - 1f;
                s[i] = saw * 0.35f + Mathf.Sin(2f * Mathf.PI * 440f * t) * 0.3f + Mathf.Sin(2f * Mathf.PI * 1320f * t) * 0.12f + Noise() * 0.05f;
            }
            return Normalise(s, 0.7f);
        }
    }
}
