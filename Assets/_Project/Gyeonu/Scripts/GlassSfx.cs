using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 유리·나무 절차 합성 효과음 (2026-08-24) — 문갑 상판 렌즈 퍼즐용.
    /// <see cref="StoneSfx"/>(돌)·<see cref="BrassSfx"/>(놋쇠)와 **같은 도구**를 쓴다.
    /// 프로젝트에 쓸 만한 효과음 에셋이 없다는 사정도 그대로다 —
    /// 진짜 에셋이 들어오면 <see cref="LensPuzzle"/>의 AudioClip 칸에 꽂기만 하면 이쪽은 안 쓰인다.
    ///
    /// ⚠️ 여기서 만든 AudioClip은 런타임 오브젝트다. **static으로 캐시하지 말 것** —
    ///    도메인 리로드가 꺼진 프로젝트에서 참조만 다음 세션으로 살아남고 내용은 죽는다.
    ///    반드시 컴포넌트가 들고 있다가 OnDestroy에서 버린다.
    /// </summary>
    public static class GlassSfx
    {
        public const int Rate = 44100;

        // ── 퍼즐이 쓰는 네 가지 ─────────────────────────────

        /// <summary>「또각」 — 유리 구슬을 나무 상판에 내려놓는 소리.
        /// 앞머리는 나무를 두드리는 둔탁한 소리, 뒤에 유리의 맑고 긴 배음이 남는다.</summary>
        public static AudioClip Place()
        {
            var d = Buf(0.42f);
            uint s = 20260824u;
            Noise(d, 0.35f, 0.0028f, 0f, 5200f, ref s);   // 나무에 닿는 앞머리
            Tone(d, 2960f, 0.30f, 0.140f);                // 유리 몸통 — 비조화 배음이라야 유리로 들린다
            Tone(d, 4430f, 0.22f, 0.095f);
            Tone(d, 6180f, 0.13f, 0.062f);
            Tone(d, 1180f, 0.16f, 0.048f);
            Tone(d, 320f, 0.24f, 0.022f);                 // 나무 판의 낮은 응답
            return Finish("렌즈_또각", d, 0.72f);
        }

        /// <summary>「톡」 — 구슬을 집어 드는 소리. Place보다 짧고 여운이 없다.</summary>
        public static AudioClip Pick()
        {
            var d = Buf(0.16f);
            uint s = 5507u;
            Noise(d, 0.30f, 0.0016f, 0f, 6000f, ref s);
            Tone(d, 3380f, 0.24f, 0.030f);
            Tone(d, 5120f, 0.14f, 0.020f);
            Tone(d, 620f, 0.14f, 0.016f);
            return Finish("렌즈_톡", d, 0.55f);
        }

        /// <summary>「덜컥」 — 상판 속에서 빗장이 풀리는 소리. 낮고 묵직하고, 나무통이 함께 울린다.</summary>
        public static AudioClip Clunk()
        {
            var d = Buf(0.55f);
            uint s = 31771u;
            Noise(d, 0.52f, 0.010f, 0f, 2100f, ref s);    // 쇠가 걸렸다 풀리는 앞머리
            Tone(d, 132f, 0.78f, 0.150f);                 // 문갑 몸통의 저역
            Tone(d, 198f, 0.42f, 0.110f);
            Tone(d, 86f, 0.55f, 0.190f);
            Tone(d, 470f, 0.20f, 0.045f);
            Tone(d, 940f, 0.10f, 0.022f);
            return Finish("문갑_덜컥", d, 0.90f);
        }

        /// <summary>「드르륵」 — 나무 서랍이 밀려 나오는 소리(0.85초).
        /// 알갱이 잡음을 서서히 잦아들게 하고 끝에 가벼운 멈춤 소리를 얹는다.</summary>
        public const float StopAt = 0.72f;

        public static AudioClip DrawerSlide()
        {
            var d = Buf(1.00f);
            uint s = 8812u;

            var grain = new float[d.Length];
            Noise(grain, 1f, 10f, 0f, 900f, ref s);       // 저역 위주 — 나무끼리 쓸리는 소리
            const float gStart = 0.02f, gEnd = StopAt;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                if (t < gStart || t > gEnd) continue;
                float u = (t - gStart) / (gEnd - gStart);
                float pulse = Mathf.Repeat(t * 41f, 1f);                  // 41Hz 알갱이 = 나뭇결 턱
                float g = Mathf.Clamp01(1f - pulse * 1.7f);
                float swell = Mathf.Sin(Mathf.PI * Mathf.Clamp01(u * 1.15f)) * 0.9f + 0.1f;
                d[i] += grain[i] * (0.55f + 0.45f * g) * swell * 0.42f;
            }
            Tone(d, 112f, 0.14f, 0.55f, gStart);                          // 끌리는 동안의 저역 지짐
            Noise(d, 0.30f, 0.006f, StopAt, 1700f, ref s);                // 멈춤
            Tone(d, 154f, 0.34f, 0.090f, StopAt);
            Tone(d, 74f, 0.28f, 0.130f, StopAt);
            return Finish("서랍_드르륵", d, 0.80f);
        }

        // ── 합성 도구 (StoneSfx와 같은 것) ─────────────────

        static float[] Buf(float sec) => new float[Mathf.CeilToInt(Rate * sec)];

        static void Tone(float[] d, float freq, float amp, float decay, float start = 0f)
        {
            int s0 = Mathf.Clamp(Mathf.RoundToInt(start * Rate), 0, d.Length);
            float w = 2f * Mathf.PI * freq / Rate;
            for (int i = s0; i < d.Length; i++)
            {
                float t = (i - s0) / (float)Rate;
                float env = Mathf.Exp(-t / decay) * (1f - Mathf.Exp(-t / 0.0006f));
                d[i] += amp * env * Mathf.Sin(w * (i - s0));
            }
        }

        static void Noise(float[] d, float amp, float decay, float start, float cutoff, ref uint seed)
        {
            int s0 = Mathf.Clamp(Mathf.RoundToInt(start * Rate), 0, d.Length);
            float a = Mathf.Clamp01(2f * Mathf.PI * cutoff / Rate);
            float lp = 0f;
            for (int i = s0; i < d.Length; i++)
            {
                float t = (i - s0) / (float)Rate;
                float n = Rand(ref seed) * 2f - 1f;
                lp += a * (n - lp);
                d[i] += amp * Mathf.Exp(-t / decay) * lp;
            }
        }

        static float Rand(ref uint s)
        {
            s ^= s << 13; s ^= s >> 17; s ^= s << 5;
            return (s & 0xFFFFFF) / 16777215f;
        }

        static AudioClip Finish(string name, float[] d, float peak)
        {
            float mx = 0f;
            for (int i = 0; i < d.Length; i++) mx = Mathf.Max(mx, Mathf.Abs(d[i]));
            float k = mx > 1e-5f ? peak / mx : 1f;
            int fade = Mathf.Min(d.Length, Rate / 200);
            for (int i = 0; i < d.Length; i++)
            {
                float f = i >= d.Length - fade ? (d.Length - 1 - i) / (float)fade : 1f;
                d[i] = Mathf.Clamp(d[i] * k * f, -1f, 1f);
            }
            var c = AudioClip.Create(name, d.Length, 1, Rate, false);
            c.SetData(d, 0);
            return c;
        }
    }
}
