using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 혼천의 고리 소리 — 절차 합성 (2026-08-23). <see cref="StoneSfx"/>와 같은 방식이다.
    ///
    /// ■ 왜 또 합성인가
    ///   프로젝트에 쓸 만한 금속 효과음 에셋이 없다(있는 건 나무 서랍 소리 10종뿐).
    ///   돌(StoneSfx)과 놋쇠는 소리가 아주 다르다 — 돌은 둔탁하고 배음이 빨리 죽지만,
    ///   놋쇠 고리는 **높은 배음이 길게 남는다**. 그래서 같은 도구로 다른 배음 구성을 쓴다.
    ///
    /// ⚠️ 여기서 만든 AudioClip은 **런타임 오브젝트**다. static으로 캐시하면 도메인 리로드가
    ///   꺼진 프로젝트에서 참조만 다음 플레이 세션으로 살아남고 내용은 죽는다.
    ///   반드시 컴포넌트가 들고 있다가 OnDestroy에서 버린다.
    /// </summary>
    public static class BrassSfx
    {
        public const int Rate = 44100;

        /// <summary>
        /// 고리가 서로 스치는 낮은 마찰음. **이어 붙여 트는 소리**라 앞뒤가 매끄럽게 물려야 한다 —
        /// 잡음 버스트가 아니라 잡음을 **띠통과**시킨 정상 상태로 만들고, 양 끝을 크로스페이드한다.
        /// 세기·높이는 부르는 쪽이 AudioSource.volume/pitch로 드래그 속도에 맞춰 흔든다.
        /// </summary>
        public static AudioClip Friction()
        {
            const float sec = 1.0f;
            var d = Buf(sec);
            uint s = 90210u;
            float lp = 0f, hp = 0f, prev = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float n = Rand(ref s) * 2f - 1f;
                lp += 0.055f * (n - lp);              // 1.5kHz 언저리 저역
                float band = lp - hp;                  // 고역 성분을 덜어 낸 좁은 띠
                hp += 0.004f * (lp - hp);              // 아주 낮은 웅웅거림 제거
                // 결이 고르면 기계음처럼 들린다 — 아주 느린 흔들림으로 손맛을 준다
                float wob = 0.75f + 0.25f * Mathf.Sin(i * 0.00042f) * Mathf.Sin(i * 0.00017f);
                d[i] = band * wob;
                prev = band;
            }
            // 끝을 앞머리에 겹쳐 이음매를 지운다 (루프 재생용)
            int x = Rate / 20;                          // 50ms
            for (int i = 0; i < x; i++)
            {
                float k = i / (float)x;
                d[i] = Mathf.Lerp(d[d.Length - x + i], d[i], k);
            }
            return Finish("혼천의_마찰", d, 0.55f, fadeTail: false);
        }

        /// <summary>「덜컥」 — 고리가 여덟 방위 한 칸에 물리는 소리. 짧고 또렷하되 가볍다.</summary>
        public static AudioClip Detent()
        {
            var d = Buf(0.22f);
            uint s = 31771u;
            Noise(d, 0.42f, 0.0030f, 0f, 6000f, ref s);   // 금속끼리 닿는 앞머리
            Tone(d, 2960f, 0.20f, 0.030f);                // 놋쇠는 높은 배음이 남는다
            Tone(d, 1840f, 0.30f, 0.055f);
            Tone(d, 1180f, 0.34f, 0.080f);
            Tone(d, 620f, 0.26f, 0.110f);
            Tone(d, 295f, 0.14f, 0.090f);                 // 몸통
            return Finish("혼천의_덜컥", d, 0.80f);
        }

        /// <summary>
        /// 「철컥」 — 여섯이 다 맞아 고리 전체가 물리는 소리.
        /// 앞의 「덜컥」보다 **무겁고 길다**: 낮은 몸통을 더 얹고 여운을 세 배로 끈다.
        /// 두 단으로 친다(0초 물림 → 0.055초 잠김) — 한 방이면 그냥 큰 덜컥으로 들린다.
        /// </summary>
        public static AudioClip Latch()
        {
            var d = Buf(1.35f);
            uint s = 5150u;
            Noise(d, 0.50f, 0.0045f, 0f, 5200f, ref s);
            Tone(d, 1480f, 0.22f, 0.045f);
            Tone(d, 880f, 0.30f, 0.080f);
            Tone(d, 196f, 0.55f, 0.130f);                 // 무거운 앞머리

            Noise(d, 0.34f, 0.0060f, 0.055f, 3400f, ref s);
            Tone(d, 1176f, 0.26f, 0.320f, 0.055f);        // 여운 — 놋쇠 울림
            Tone(d, 784f, 0.34f, 0.430f, 0.055f);
            Tone(d, 392f, 0.42f, 0.560f, 0.055f);
            Tone(d, 131f, 0.50f, 0.420f, 0.055f);
            return Finish("혼천의_철컥", d, 0.92f);
        }

        // ── 합성 도구 (StoneSfx와 같은 구성) ────────────────

        static float[] Buf(float sec) => new float[Mathf.CeilToInt(Rate * sec)];

        /// <summary>감쇠하는 사인 한 줄. start초부터 얹는다.</summary>
        static void Tone(float[] d, float freq, float amp, float decay, float start = 0f)
        {
            int s0 = Mathf.Clamp(Mathf.RoundToInt(start * Rate), 0, d.Length);
            float w = 2f * Mathf.PI * freq / Rate;
            for (int i = s0; i < d.Length; i++)
            {
                float t = (i - s0) / (float)Rate;
                float env = Mathf.Exp(-t / decay) * (1f - Mathf.Exp(-t / 0.0006f));   // 딸깍 방지 어택
                d[i] += amp * env * Mathf.Sin(w * (i - s0));
            }
        }

        /// <summary>한 극 저역통과를 먹인 잡음 버스트.</summary>
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

        /// <summary>피크 정규화 (+ 꼬리 페이드) → AudioClip. 루프 클립은 꼬리를 건드리면 안 된다.</summary>
        static AudioClip Finish(string name, float[] d, float peak, bool fadeTail = true)
        {
            float mx = 0f;
            for (int i = 0; i < d.Length; i++) mx = Mathf.Max(mx, Mathf.Abs(d[i]));
            float k = mx > 1e-5f ? peak / mx : 1f;
            int fade = fadeTail ? Mathf.Min(d.Length, Rate / 200) : 0;   // 5ms
            for (int i = 0; i < d.Length; i++)
            {
                float f = (fade > 0 && i >= d.Length - fade) ? (d.Length - 1 - i) / (float)fade : 1f;
                d[i] = Mathf.Clamp(d[i] * k * f, -1f, 1f);
            }
            var c = AudioClip.Create(name, d.Length, 1, Rate, false);
            c.SetData(d, 0);
            return c;
        }
    }
}
