using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 절차적 효과음 생성기 (2026-08-23).
    ///
    /// ■ 왜 절차적인가
    ///   프로젝트에 쓸 만한 효과음 에셋이 없다. 있는 것이라고는 가구 팩
    ///   (<c>Assets/KTinteractiveProp/Volum 02/Audio/*.wav</c>)의 **나무 서랍 여닫는 소리**
    ///   10종뿐이고, 그건 gitignore된 원본 폴더인 데다 돌·쇠 소리와는 결이 다르다.
    ///   암문 퍼즐은 **소리가 유일한 확정 피드백**이라(UI로 정답 여부를 알리지 않는다)
    ///   무음으로 둘 수 없어, 파형을 코드로 합성해 쓴다.
    ///
    /// ■ 나중에 진짜 효과음이 들어오면
    ///   <see cref="AmmunLockPuzzle"/> 의 AudioClip 필드에 에셋을 꽂기만 하면 된다 —
    ///   비어 있을 때만 이 합성음으로 물러선다. 코드는 손댈 필요가 없다.
    ///
    /// ■ 수명 주의
    ///   여기서 만든 AudioClip은 **런타임 오브젝트**다. static으로 캐시하면 도메인 리로드가
    ///   꺼진 프로젝트에서 참조만 다음 플레이 세션으로 살아남고 내용은 죽는다
    ///   (DebugFocusRig 비네트 텍스처에서 이미 당한 함정). 반드시 컴포넌트가 들고 있다가
    ///   OnDestroy에서 버린다.
    /// </summary>
    public static class StoneSfx
    {
        public const int Rate = 44100;

        // ── 퍼즐이 쓰는 다섯 가지 ──────────────────────────

        /// <summary>「달칵」 — 잠금장치가 한 칸 물리는 소리. 정답·오답 구분 없이 늘 같다.</summary>
        public static AudioClip Latch()
        {
            var d = Buf(0.16f);
            uint s = 20260823u;
            Noise(d, 0.55f, 0.0035f, 0f, 4200f, ref s);   // 돌 부딪는 앞머리
            Tone(d, 1650f, 0.28f, 0.016f);
            Tone(d, 780f, 0.42f, 0.035f);
            Tone(d, 330f, 0.34f, 0.070f);                 // 몸통 울림
            Tone(d, 168f, 0.16f, 0.090f);
            return Finish("암문_달칵", d, 0.85f);
        }

        /// <summary>「툭」 — 잘못 눌렀을 때. 낮고 짧고, 울림이 거의 없다(죽은 소리).</summary>
        public static AudioClip DeadThud()
        {
            var d = Buf(0.14f);
            uint s = 771u;
            Noise(d, 0.30f, 0.006f, 0f, 900f, ref s);
            Tone(d, 116f, 0.75f, 0.038f);
            Tone(d, 173f, 0.30f, 0.026f);
            return Finish("암문_툭", d, 0.80f);
        }

        /// <summary>「덜컥」 — 양쪽이 함께 안으로 주저앉는 소리. 거칠고 짧다.</summary>
        public static AudioClip Clunk()
        {
            var d = Buf(0.26f);
            uint s = 4113u;
            Noise(d, 0.50f, 0.030f, 0f, 2600f, ref s);
            Tone(d, 92f, 0.70f, 0.075f);
            Tone(d, 146f, 0.36f, 0.055f);
            Tone(d, 420f, 0.22f, 0.030f);
            return Finish("암문_덜컥", d, 0.88f);
        }

        /// <summary>「툭」(바깥으로) — 쌓인 것이 한꺼번에 풀려 돌이 튀어나오는 소리.
        /// <see cref="DeadThud"/>보다 조금 높고 울림이 있다 — 튕겨 나오는 느낌.</summary>
        public static AudioClip PopOut()
        {
            var d = Buf(0.22f);
            uint s = 90210u;
            Noise(d, 0.42f, 0.004f, 0f, 3000f, ref s);
            Tone(d, 245f, 0.62f, 0.055f);
            Tone(d, 368f, 0.30f, 0.040f);
            Tone(d, 130f, 0.28f, 0.075f);
            return Finish("암문_툭_튀어나옴", d, 0.85f);
        }

        /// <summary>「드르륵—철컥」 — 성공. 앞 0.42초는 빗장이 끌리는 소리,
        /// 짧은 사이를 두고 <see cref="ClackAt"/>(0.52초 지점)에서 묵직하게 걸린다.</summary>
        public const float ClackAt = 0.52f;

        public static AudioClip Release()
        {
            var d = Buf(1.05f);
            uint s = 33101u;

            // 드르륵 — 잡음을 톱니 펄스로 게이트해 알갱이를 만든다 (돌 위를 끌리는 쇠빗장)
            var grain = new float[d.Length];
            Noise(grain, 1f, 10f, 0f, 1500f, ref s);      // 감쇠 없는 광대역 → 아래에서 포락선을 씌운다
            float gStart = 0.02f, gEnd = 0.44f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                if (t < gStart || t > gEnd) continue;
                float u = (t - gStart) / (gEnd - gStart);
                float pulse = Mathf.Repeat(t * 26f, 1f);              // 26Hz 알갱이
                float g = Mathf.Clamp01(1f - pulse * 2.2f);           // 톱니 게이트
                float swell = Mathf.Lerp(0.35f, 0.85f, u) * (1f - u * u * 0.35f);
                d[i] += grain[i] * g * swell * 0.5f;
            }
            Tone(d, 74f, 0.18f, 0.35f, gStart);                       // 끌리는 동안의 저역 지짐

            // 철컥 — 묵직한 저역 + 쇠 걸리는 앞머리
            Noise(d, 0.55f, 0.008f, ClackAt, 5200f, ref s);
            Tone(d, 880f, 0.20f, 0.110f, ClackAt);
            Tone(d, 300f, 0.35f, 0.180f, ClackAt);
            Tone(d, 138f, 0.62f, 0.300f, ClackAt);
            Tone(d, 69f, 0.80f, 0.360f, ClackAt);
            return Finish("암문_드르륵_철컥", d, 0.92f);
        }

        // ── 합성 도구 ─────────────────────────────────────

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

        /// <summary>피크 정규화 + 꼬리 페이드 → AudioClip.</summary>
        static AudioClip Finish(string name, float[] d, float peak)
        {
            float mx = 0f;
            for (int i = 0; i < d.Length; i++) mx = Mathf.Max(mx, Mathf.Abs(d[i]));
            float k = mx > 1e-5f ? peak / mx : 1f;
            int fade = Mathf.Min(d.Length, Rate / 200);          // 5ms
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
