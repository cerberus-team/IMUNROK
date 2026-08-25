using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>자리를 채우는 소리</b>를 빚는다. 메뉴: [이문록 ▸ 소리 ▸ 임시 효과음 굽기]
    ///
    /// <b>왜 만드나</b>: 소리계(<see cref="NoiseMeter"/>)는 다 지었는데 정작 <b>들을 소리가
    /// 없다</b>. 소리를 구할 때까지 기다리면 그동안 잠행이 무성영화가 된다 — 눈금만
    /// 오르내리고 귀에는 아무것도 안 들리니, 시끄러운지 조용한지 몸으로 익힐 수가 없다.
    ///
    /// <b>왜 만들 수 있나</b>: 여기 필요한 소리는 대개 <b>결이 있는 잡음</b>이다.
    /// 재를 헤집는 소리는 잔모래가 서로 쓸리는 소리이고, 옷이 스치는 소리도, 서랍이
    /// 긁히는 소리도 마찬가지다. 잡음을 만들고 <b>어느 결을 남길지</b>와 <b>어떻게
    /// 여닫을지</b>만 정하면 제법 그럴듯해진다. 종소리나 사람 목소리라면 어림없지만,
    /// 이 집에서 나는 소리는 다행히 그런 것이 아니다.
    ///
    /// <b>진짜 소리를 구하면 갈아 끼운다.</b> 파일 이름을 그대로 두면 물려 둔 자리가
    /// 안 끊긴다. 그러라고 이름에 '임시'를 안 붙였다.
    ///
    /// 만드는 곳: Assets/_Project/_Common/Audio/SFX/
    /// </summary>
    public static class PlaceholderSfx
    {
        private const string Folder = "Assets/_Project/_Common/Audio/SFX";
        private const int Rate = 44100;

        [MenuItem("이문록/소리/임시 효과음 굽기")]
        public static void Bake()
        {
            Directory.CreateDirectory(Folder);
            var log = new System.Text.StringBuilder("[소리] 임시 효과음을 구웠다\n");

            // 재 헤집기 — 잔모래가 쓸린다. 긁는 결이 <b>느리게 밀려왔다 물러간다</b>.
            log.AppendLine(Write("재_헤집기", Rake(1.30f)));

            // 서랍 — 나무가 나무에 긁힌다. 재보다 거칠고 낮은 결이 섞인다.
            log.AppendLine(Write("서랍_긁힘", Drawer(1.10f)));

            // 보료 — 솜과 무명이 스친다. 높은 결이 거의 없다.
            log.AppendLine(Write("보료_스침", Cloth(0.85f)));

            // 문 — 마른 돌쩌귀가 운다. 잡음 위에 <b>흔들리는 한 음</b>이 얹힌다.
            log.AppendLine(Write("문_삐걱_열림", Creak(0.95f, 300f, 470f)));
            log.AppendLine(Write("문_삐걱_닫힘", Creak(0.80f, 430f, 260f)));   // 음이 내려간다

            // 발소리 — 마루는 <b>퍽</b> 하고 낮게 울리고 끝이 짧다. 네 켤레를 조금씩 달리 굽는다.
            for (int i = 0; i < 4; i++)
                log.AppendLine(Write("발소리_마루_" + i, Step(0.22f, 96f + i * 11f, 0.9f + i * 0.07f)));

            // 발소리 — <b>흙</b>은 울리는 몸통이 없다. 잔모래가 눌리고 끝난다.
            // 마당을 가로지르는 소리가 널을 밟는 소리와 같으면 어디에 서 있는지 귀로 알 수 없다.
            for (int i = 0; i < 4; i++)
                log.AppendLine(Write("발소리_흙_" + i, Dirt(0.18f, 0.9f + i * 0.06f, i)));

            // 발소리 — <b>돌</b>(기단·댓돌)은 짧고 딱딱하다. 높은 결이 톡 튀고 곧 죽는다.
            for (int i = 0; i < 3; i++)
                log.AppendLine(Write("발소리_돌_" + i, Stone(0.16f, 210f + i * 34f, i)));

            // 문 두드리기 — 창틀을 손마디로 친다. 두 번, 그리고 세 번.
            log.AppendLine(Write("문_두드림_둘", Knock(0.70f, new[] { 0f, 0.26f })));
            log.AppendLine(Write("문_두드림_셋", Knock(1.00f, new[] { 0f, 0.24f, 0.50f })));

            AssetDatabase.Refresh();
            Debug.Log(log.ToString());
        }

        // ── 결 짓기 ────────────────────────────────

        /// <summary>재 헤집기 — 잡음을 느린 물결로 여닫는다. 몇 번 밀었다 당긴 것처럼.</summary>
        private static float[] Rake(float sec)
        {
            int n = (int)(sec * Rate);
            var a = new float[n];
            var rnd = new System.Random(11);
            float lp = 0f, hp = 0f, prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float raw = (float)(rnd.NextDouble() * 2.0 - 1.0);

                // 낮은 것만 남긴다 — 재는 쉿 소리가 아니라 <b>쓸리는</b> 소리다
                lp += (raw - lp) * 0.28f;
                // 아주 낮은 웅웅거림은 뺀다
                hp = 0.92f * (hp + lp - prev); prev = lp;

                // 세 번 밀었다 당긴다. 사람 손이 하는 짓이라 고르지 않다.
                float stroke = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f * 3f - 1.4f);
                stroke = Mathf.Pow(stroke, 1.6f);
                a[i] = hp * stroke * Fade(t, 0.10f, 0.30f) * 0.55f;
            }
            return a;
        }

        /// <summary>서랍 — 재보다 거칠게. 나무가 걸리며 잘게 튄다.</summary>
        private static float[] Drawer(float sec)
        {
            int n = (int)(sec * Rate);
            var a = new float[n];
            var rnd = new System.Random(23);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float raw = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += (raw - lp) * 0.42f;

                // 걸렸다 풀리는 자리 — 드문드문 튄다
                float catchy = (rnd.NextDouble() < 0.0016) ? 1.8f : 1f;
                float pull = Mathf.Pow(Mathf.Sin(t * Mathf.PI), 0.7f);   // 빼는 내내 이어진다
                a[i] = lp * pull * catchy * Fade(t, 0.04f, 0.18f) * 0.5f;
            }
            return a;
        }

        /// <summary>보료 — 무명이 스친다. 높은 결을 거의 다 깎는다.</summary>
        private static float[] Cloth(float sec)
        {
            int n = (int)(sec * Rate);
            var a = new float[n];
            var rnd = new System.Random(37);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float raw = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += (raw - lp) * 0.10f;
                lp2 += (lp - lp2) * 0.10f;      // 두 번 깎아 뭉근하게
                float stroke = Mathf.Pow(Mathf.Sin(t * Mathf.PI), 1.2f);
                a[i] = lp2 * stroke * 2.2f * Fade(t, 0.12f, 0.35f);
            }
            return a;
        }

        /// <summary>문 — 마른 돌쩌귀. 잡음 위에 흔들리는 한 음을 얹고 음높이를 끌어간다.</summary>
        private static float[] Creak(float sec, float fromHz, float toHz)
        {
            int n = (int)(sec * Rate);
            var a = new float[n];
            var rnd = new System.Random(53);
            float phase = 0f, lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float hz = Mathf.Lerp(fromHz, toHz, Mathf.Pow(t, 0.7f));

                // 돌쩌귀는 고르게 울지 않는다 — 떨림을 준다
                hz *= 1f + 0.06f * Mathf.Sin(t * Mathf.PI * 2f * 7.3f);
                phase += hz / Rate * Mathf.PI * 2f;

                // 톱니에 가까운 소리라야 나무가 된다. 사인 하나면 피리가 된다.
                float tone = Mathf.Sin(phase) * 0.6f + Mathf.Sin(phase * 2f) * 0.25f + Mathf.Sin(phase * 3f) * 0.12f;

                float raw = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += (raw - lp) * 0.25f;

                // 끊겼다 이어지는 울음 — 나무가 미끄러지다 걸린다
                float grip = 0.55f + 0.45f * Mathf.Sin(t * Mathf.PI * 2f * 4.1f + 0.7f);
                a[i] = (tone * 0.5f + lp * 0.5f) * grip * Fade(t, 0.06f, 0.40f) * 0.42f;
            }
            return a;
        }

        /// <summary>발소리 — 마루가 낮게 울리고 끝이 짧다.</summary>
        private static float[] Step(float sec, float hz, float bright)
        {
            int n = (int)(sec * Rate);
            var a = new float[n];
            var rnd = new System.Random((int)(hz * 7));
            float phase = 0f, lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                phase += hz / Rate * Mathf.PI * 2f;

                float body = Mathf.Sin(phase) * Mathf.Exp(-t * 26f);          // 퍽 — 마루가 운다
                float raw = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += (raw - lp) * 0.5f;
                float scuff = lp * Mathf.Exp(-t * 70f) * 0.5f * bright;        // 닿는 순간의 잔소리

                a[i] = (body + scuff) * 0.6f;
            }
            return a;
        }

        /// <summary>
        /// 문 두드리기 — 마른 나무를 손마디로 친다.
        ///
        /// 나무는 <b>몇 개의 결</b>로 함께 운다(하나면 북이 된다). 셋을 겹쳐 각각 다른
        /// 빠르기로 사그라뜨리면 나무 소리가 된다 — 높은 결이 먼저 죽고 낮은 결이 남는다.
        /// 사람이 치는 것이라 <b>매번 세기가 다르다</b>. 똑같으면 기계가 두드리는 것이다.
        /// </summary>
        /// <summary>
        /// <b>흙</b>을 딛는 소리. 마루와 달리 <b>울리는 몸통이 없다</b> —
        /// 잔모래가 한 번 눌렸다가 그대로 죽는다. 그래서 낮은 음 대신 잡음만 남기고,
        /// 그 잡음도 마루의 절반쯤 되는 시간에 끊는다.
        /// </summary>
        private static float[] Dirt(float sec, float bright, int seed)
        {
            int n = (int)(sec * Rate);
            var a = new float[n];
            var rnd = new System.Random(1700 + seed * 13);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float raw = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += (raw - lp) * 0.22f;        // 흙은 높은 결이 거의 없다
                lp2 += (lp - lp2) * 0.30f;
                // 밟는 순간 눌리고(빠른 감쇠) 뒤꿈치가 끌리는 꼬리가 조금 남는다
                float env = Mathf.Exp(-t * 40f) + Mathf.Exp(-t * 11f) * 0.22f;
                a[i] = lp2 * env * 0.85f * bright;
            }
            return a;
        }

        /// <summary>
        /// <b>돌</b>(기단·댓돌)을 딛는 소리. 짧고 딱딱하다 — 높은 결이 톡 튀고 곧 죽는다.
        /// 마루처럼 낮게 울리는 뒷맛이 없어야 밟는 순간 발밑이 바뀐 것이 들린다.
        /// </summary>
        private static float[] Stone(float sec, float hz, int seed)
        {
            int n = (int)(sec * Rate);
            var a = new float[n];
            var rnd = new System.Random(2300 + seed * 29);
            float phase = 0f, hpPrev = 0f, hp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                phase += hz / Rate * Mathf.PI * 2f;
                float raw = (float)(rnd.NextDouble() * 2.0 - 1.0);
                hp = raw - hpPrev;               // 성긴 고역 통과 — 돌은 밝다
                hpPrev = raw;
                float click = hp * Mathf.Exp(-t * 120f) * 0.5f;
                float tick = Mathf.Sin(phase) * Mathf.Exp(-t * 60f) * 0.35f;
                a[i] = (click + tick) * 0.8f;
            }
            return a;
        }

        private static float[] Knock(float sec, float[] beats)
        {
            int n = (int)(sec * Rate);
            var a = new float[n];
            var rnd = new System.Random(71);

            foreach (var beat in beats)
            {
                int at = (int)(beat * Rate);
                if (at >= n) continue;
                float hit = 0.75f + (float)rnd.NextDouble() * 0.35f;      // 세기가 매번 다르다
                float detune = 0.94f + (float)rnd.NextDouble() * 0.12f;

                // 나무의 결 셋 — 낮은 것이 오래 남는다
                float[] hz = { 168f * detune, 402f * detune, 731f * detune };
                float[] decay = { 26f, 48f, 90f };
                float[] mix = { 0.55f, 0.30f, 0.15f };

                for (int i = at; i < n; i++)
                {
                    float t = (i - at) / (float)Rate;
                    float v = 0f;
                    for (int k = 0; k < 3; k++)
                        v += Mathf.Sin(t * hz[k] * Mathf.PI * 2f) * Mathf.Exp(-t * decay[k]) * mix[k];

                    // 마디가 닿는 순간의 딱 소리
                    float tick = (float)(rnd.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 320f) * 0.35f;
                    a[i] += (v + tick) * hit;
                }
            }
            return a;
        }

        /// <summary>앞뒤를 부드럽게 여닫는다. 뚝 끊기면 '틱' 소리가 붙는다.</summary>
        private static float Fade(float t, float inPart, float outPart)
        {
            float a = t < inPart ? t / inPart : 1f;
            float b = t > 1f - outPart ? (1f - t) / outPart : 1f;
            return Mathf.Clamp01(a) * Mathf.Clamp01(b);
        }

        // ── 파일로 굽기 ────────────────────────────

        private static string Write(string name, float[] samples)
        {
            // 가장 큰 마루를 -3dB 에 맞춘다. 소리마다 크기가 들쭉날쭉하면 매겨 둔
            // 소리 크기(NoiseMeter 의 0~1)와 귀로 듣는 크기가 따로 논다.
            float peak = 0f;
            foreach (var s in samples) peak = Mathf.Max(peak, Mathf.Abs(s));
            float gain = peak > 0.0001f ? 0.707f / peak : 1f;

            string path = Path.Combine(Folder, name + ".wav");
            using (var fs = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(fs))
            {
                int bytes = samples.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + bytes);
                w.Write(new[] { 'W', 'A', 'V', 'E' });
                w.Write(new[] { 'f', 'm', 't', ' ' }); w.Write(16);
                w.Write((short)1); w.Write((short)1); w.Write(Rate);
                w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(bytes);
                foreach (var s in samples)
                    w.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * gain * 32767f), -32768, 32767));
            }
            return $"  {name}.wav  {samples.Length / (float)Rate:F2}초";
        }
    }
}
