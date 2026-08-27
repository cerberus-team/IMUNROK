using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 종이·확인음 절차 합성 (2026-08-24) — 서고 장부 퍼즐용.
    /// <see cref="StoneSfx"/>(돌)·<see cref="BrassSfx"/>(놋쇠)·<see cref="GlassSfx"/>(유리)와
    /// **같은 도구**를 쓴다. 프로젝트에 쓸 만한 효과음 에셋이 없다는 사정도 그대로다.
    ///
    /// ■ 종이는 무엇이 다른가
    ///   금속·유리는 **울리는 몸통**이 있어 감쇠하는 사인 몇 줄이면 되지만, 한지는 울리지 않는다.
    ///   들리는 것은 섬유가 스치며 나는 **넓은 대역의 잡음**뿐이고, 그 잡음의 세기가
    ///   불규칙하게 오르내리는 것이 "종이스러움"이다. 그래서 톤을 아예 안 쓰고,
    ///   잡음에 **거친 진폭 요동**을 곱하는 방식으로 만든다.
    ///
    /// ⚠️ 여기서 만든 AudioClip은 런타임 오브젝트다. static으로 캐시하지 말 것 —
    ///    도메인 리로드가 꺼진 프로젝트에서 참조만 다음 세션으로 살아남고 내용은 죽는다.
    /// </summary>
    public static class PaperSfx
    {
        public const int Rate = 44100;

        /// <summary>「사락」 — 표찰을 판에 내려놓는 소리. 짧고 바스락거린다.</summary>
        public static AudioClip Place()
        {
            var d = Buf(0.20f);
            uint s = 77113u;
            Rustle(d, 0.9f, 0.0f, 0.16f, 2600f, 9000f, ref s);
            // 판에 닿는 아주 옅은 앞머리 — 나무 위에 놓았음을 알린다
            Thud(d, 0.10f, 0.0035f, 0f, 900f, ref s);
            return Finish("장부_사락", d, 0.42f);
        }

        /// <summary>「스륵」 — 표찰을 집어 드는 소리. 더 짧고 여운이 없다.</summary>
        public static AudioClip Pick()
        {
            var d = Buf(0.13f);
            uint s = 4409u;
            Rustle(d, 0.75f, 0f, 0.10f, 3200f, 11000f, ref s);
            return Finish("장부_스륵", d, 0.30f);
        }

        /// <summary>
        /// 「댕—」 단계 성공 확인음. 놋쇠 방울 하나.
        /// 승리 팡파르가 아니라 **알아차렸다는 신호**라 한 번만 치고 길게 여운을 끈다.
        /// 배음을 정수배가 아니라 살짝 어긋나게 잡아야 종이 아니라 방울로 들린다.
        /// </summary>
        public static AudioClip Chime()
        {
            var d = Buf(2.4f);
            uint s = 20260824u;
            Thud(d, 0.22f, 0.0022f, 0f, 7200f, ref s);     // 채가 닿는 앞머리
            Tone(d, 587f, 0.55f, 1.35f);                    // 기음
            Tone(d, 1482f, 0.34f, 0.95f);                   // 2.525배 — 방울의 비조화 배음
            Tone(d, 2610f, 0.20f, 0.62f);
            Tone(d, 3940f, 0.11f, 0.38f);
            Tone(d, 293f, 0.24f, 1.60f);                    // 낮은 웅웅거림
            return Finish("장부_확인음", d, 0.66f);
        }

        /// <summary>「투둑」 — 어긋났을 때. 짧고 낮게, 울리지 않는다.</summary>
        public static AudioClip Reject()
        {
            var d = Buf(0.30f);
            uint s = 6151u;
            Thud(d, 0.55f, 0.012f, 0f, 620f, ref s);
            Tone(d, 96f, 0.42f, 0.055f);
            Tone(d, 148f, 0.22f, 0.038f);
            return Finish("장부_투둑", d, 0.60f);
        }

        // ── 합성 도구 ────────────────────────────────────────

        static float[] Buf(float sec) => new float[Mathf.CeilToInt(Rate * sec)];

        /// <summary>
        /// 종이 스침 — **띠통과 잡음 × 거친 요동**. 요동이 없으면 그냥 "치익" 하는 잡음이 된다.
        /// </summary>
        static void Rustle(float[] d, float amp, float start, float dur, float lowHz, float highHz, ref uint seed)
        {
            int s0 = Mathf.Clamp(Mathf.RoundToInt(start * Rate), 0, d.Length);
            int s1 = Mathf.Clamp(s0 + Mathf.RoundToInt(dur * Rate), 0, d.Length);
            float aL = Mathf.Clamp01(2f * Mathf.PI * lowHz / Rate);
            float aH = Mathf.Clamp01(2f * Mathf.PI * highHz / Rate);
            float lo = 0f, hi = 0f;
            // 요동 — 60~200Hz로 세기가 들쭉날쭉해야 섬유가 하나씩 튕기는 소리로 들린다
            float g = 0f;
            int hold = 0;
            for (int i = s0; i < s1; i++)
            {
                float t = (i - s0) / (float)Mathf.Max(1, s1 - s0);
                float n = Rand(ref seed) * 2f - 1f;
                hi += aH * (n - hi);
                lo += aL * (hi - lo);
                float band = hi - lo;
                if (--hold <= 0) { g = 0.25f + 0.75f * Rand(ref seed); hold = 90 + (int)(Rand(ref seed) * 220f); }
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t));   // 부드럽게 들고 나기
                d[i] += amp * band * g * env;
            }
        }

        /// <summary>한 극 저역통과를 먹인 잡음 버스트 (닿는 앞머리).</summary>
        static void Thud(float[] d, float amp, float decay, float start, float cutoff, ref uint seed)
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

        static void Tone(float[] d, float freq, float amp, float decay, float start = 0f)
        {
            int s0 = Mathf.Clamp(Mathf.RoundToInt(start * Rate), 0, d.Length);
            float w = 2f * Mathf.PI * freq / Rate;
            for (int i = s0; i < d.Length; i++)
            {
                float t = (i - s0) / (float)Rate;
                float env = Mathf.Exp(-t / decay) * (1f - Mathf.Exp(-t / 0.0008f));
                d[i] += amp * env * Mathf.Sin(w * (i - s0));
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
