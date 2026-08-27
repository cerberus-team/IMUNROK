using UnityEngine;

namespace IMUNROK.Ui
{
    /// <summary>
    /// 소지품 판의 재질 — 한지·목재를 **절차 텍스처**로 만든다 (2026-08-23).
    ///
    /// 왜 그리는가: 이미지 에셋을 새로 들이면 팀 저장소에 바이너리가 늘고, 외부 팩에서
    /// 가져오면 원본 폴더 규칙에 걸린다. 판 UI에 필요한 건 결 정도라 코드로 충분하다.
    ///
    /// ⚠️ 정적 캐시 금지 — 도메인 리로드가 꺼진 프로젝트에서 정적 텍스처 참조가 플레이 세션을
    ///    넘겨 살아남는데 내용은 죽는다(DebugFocusRig 비네트에서 실측). 인스턴스로 들고 다닌다.
    /// </summary>
    public class InventorySkin
    {
        // 한지·먹·주칠 — 조선 문방의 색조
        public static readonly Color Hanji = new Color(0.870f, 0.812f, 0.698f);
        public static readonly Color HanjiDim = new Color(0.757f, 0.694f, 0.580f);
        public static readonly Color Wood = new Color(0.259f, 0.184f, 0.129f);
        public static readonly Color WoodLit = new Color(0.412f, 0.298f, 0.204f);
        public static readonly Color Ink = new Color(0.106f, 0.086f, 0.075f);
        public static readonly Color InkSoft = new Color(0.318f, 0.267f, 0.227f);
        public static readonly Color Vermilion = new Color(0.667f, 0.216f, 0.161f);
        public static readonly Color Gold = new Color(0.847f, 0.706f, 0.427f);

        Sprite hanji, wood, slot, dot, glass, mic;

        public Sprite Hanji_ => hanji ?? (hanji = Make("한지", 256, PaperPixel));
        public Sprite Wood_ => wood ?? (wood = Make("목재", 128, WoodPixel));
        public Sprite Slot_ => slot ?? (slot = Make("칸", 128, SlotPixel));
        public Sprite Dot_ => dot ?? (dot = Make("점", 64, DotPixel));
        /// <summary>돋보기 — 글꼴에 돋보기 글리프가 없는 경우가 많아 직접 그린다.</summary>
        public Sprite Glass_ => glass ?? (glass = Make("돋보기", 128, GlassPixel));

        /// <summary>
        /// 마이크 — 대화창의 「말하기」 단추 (2026-08-27).
        ///
        /// ⚠️ 그림글자(🎤)는 <b>조선 궁서체·방송체 어디에도 없다</b> — 네모로 뜬다.
        ///    잠시 「말」 이라는 낱자로 대신했는데, 단추 하나에 글자가 들어앉으니
        ///    옆의 「묻 기」·「증거 제시」와 켜가 섞여 읽혔다. 돋보기와 같은 길로 직접 그린다.
        /// </summary>
        public Sprite Mic_ => mic ?? (mic = Make("마이크", 128, MicPixel));

        /// <summary>
        /// 가장자리 두 값 사이를 0→1로 부드럽게 넘기는 함수 (셰이더의 smoothstep).
        ///
        /// ⚠️ `Mathf.SmoothStep(a, b, t)`를 이 뜻으로 쓰면 안 된다 — 그건 **a와 b 사이를 보간**한다.
        ///    `1 - Mathf.SmoothStep(0.07, 0.09, 먼거리)`는 0이 아니라 0.91을 준다. 그래서 돋보기가
        ///    투명 바탕 대신 **꽉 찬 밝은 사각형**으로, 조준점이 동그라미 대신 네모로 나왔다
        ///    (2026-08-25 실측).
        /// </summary>
        static float Ramp(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / Mathf.Max(1e-6f, edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        Sprite Make(string name, int res, System.Func<float, float, Color> f)
        {
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false)
            {
                name = "소지품_" + name,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var px = new Color[res * res];
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                    px[y * res + x] = f((x + 0.5f) / res, (y + 0.5f) / res);
            tex.SetPixels(px);
            tex.Apply();
            // 100 pixelsPerUnit + 전면 border = 9슬라이스처럼 늘어나도 결이 뭉개지지 않는다
            return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>한지 — 옅은 베이지에 섬유결(가로로 늘인 노이즈)과 얼룩.</summary>
        static Color PaperPixel(float u, float v)
        {
            float fiber = Mathf.PerlinNoise(u * 180f, v * 26f) * 0.06f;      // 가로 섬유
            float grain = Mathf.PerlinNoise(u * 64f, v * 64f) * 0.05f;
            float stain = Mathf.PerlinNoise(u * 4f + 11f, v * 4f + 7f) * 0.10f;
            var c = Color.Lerp(Hanji, HanjiDim, stain + grain * 0.5f);
            c.r += fiber * 0.5f; c.g += fiber * 0.45f; c.b += fiber * 0.35f;
            // 가장자리를 살짝 눅여 종이가 판에 얹힌 느낌
            float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            c = Color.Lerp(c * 0.86f, c, Mathf.SmoothStep(0f, 1f, edge / 0.06f));
            c.a = 1f;
            return c;
        }

        /// <summary>목재 — 세로 나뭇결. 테두리 틀에 쓴다.</summary>
        static Color WoodPixel(float u, float v)
        {
            float rings = Mathf.Sin((u * 9f + Mathf.PerlinNoise(u * 6f, v * 1.5f) * 2.2f) * Mathf.PI) * 0.5f + 0.5f;
            float fine = Mathf.PerlinNoise(u * 90f, v * 12f) * 0.25f;
            var c = Color.Lerp(Wood, WoodLit, rings * 0.55f + fine * 0.4f);
            c.a = 1f;
            return c;
        }

        /// <summary>칸 바탕 — 한지보다 조금 어둡고, 위가 밝은 얕은 그러데이션.</summary>
        static Color SlotPixel(float u, float v)
        {
            float grain = Mathf.PerlinNoise(u * 70f, v * 70f) * 0.06f;
            var c = Color.Lerp(HanjiDim * 0.94f, Hanji, v * 0.35f + grain);
            c.a = 1f;
            return c;
        }

        /// <summary>조준점 — 먹빛 심 + 밝은 테. 한지 위에서 어느 쪽으로도 묻히지 않게 두 색을 겹쳤다.</summary>
        static Color DotPixel(float u, float v)
        {
            float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            float core = 1f - Ramp(0.34f, 0.50f, d);            // 가운데 먹점
            float halo = (1f - Ramp(0.72f, 0.92f, d)) * Ramp(0.48f, 0.62f, d);
            var c = Color.Lerp(new Color(1f, 0.97f, 0.88f), Vermilion, core);
            c.a = Mathf.Clamp01(core + halo * 0.85f);
            return c;
        }

        /// <summary>
        /// 돋보기 — 테 있는 원 + 비스듬한 손잡이. 투명 바탕에 한지빛으로 그린다.
        /// ⚠️ **굵게** 그린다. 판 위에서 이 아이콘은 화면 30px 남짓이라, 가는 선으로 그리면
        ///    축소되며 뭉개져 **밝은 사각형**으로만 보인다(2026-08-25 실측).
        /// </summary>
        static Color GlassPixel(float u, float v)
        {
            var p = new Vector2(u, v);
            // 알 — 왼쪽 위
            var c0 = new Vector2(0.40f, 0.58f);
            float ring = Mathf.Abs(Vector2.Distance(p, c0) - 0.25f);
            float a = 1f - Ramp(0.070f, 0.092f, ring);
            // 손잡이 — 알 오른쪽 아래로 뻗는 굵은 선분
            var h0 = new Vector2(0.58f, 0.40f);
            var h1 = new Vector2(0.86f, 0.12f);
            Vector2 hd = h1 - h0;
            float t = Mathf.Clamp01(Vector2.Dot(p - h0, hd) / hd.sqrMagnitude);
            float hDist = Vector2.Distance(p, h0 + hd * t);
            a = Mathf.Max(a, 1f - Ramp(0.062f, 0.084f, hDist));
            var c = new Color(0.96f, 0.93f, 0.84f);
            c.a = Mathf.Clamp01(a);
            return c;
        }

        /// <summary>
        /// 마이크 — 통(둥근 머리) + 대 + 받침, 그리고 통을 감싸는 반쪽 테.
        /// 돋보기와 같은 이유로 <b>굵게</b> 그린다 — 단추 위에서 화면 30px 남짓이라
        /// 가는 선은 축소되며 뭉개진다.
        /// </summary>
        static Color MicPixel(float u, float v)
        {
            var p = new Vector2(u, v);
            float a = 0f;

            // ① 통 — 위아래가 둥근 기둥 (선분에서의 거리로 그린다)
            var b0 = new Vector2(0.5f, 0.50f);
            var b1 = new Vector2(0.5f, 0.76f);
            Vector2 bd = b1 - b0;
            float bt = Mathf.Clamp01(Vector2.Dot(p - b0, bd) / bd.sqrMagnitude);
            float bDist = Vector2.Distance(p, b0 + bd * bt);
            a = Mathf.Max(a, 1f - Ramp(0.105f, 0.130f, bDist));

            // ② 감싸는 테 — 통 아래 절반을 두르는 반원 (마이크임을 알아보게 하는 획)
            float rDist = Mathf.Abs(Vector2.Distance(p, new Vector2(0.5f, 0.545f)) - 0.205f);
            float ring = 1f - Ramp(0.030f, 0.052f, rDist);
            if (v > 0.545f) ring = 0f;                       // 위쪽 반은 지운다
            a = Mathf.Max(a, ring);

            // ③ 대 — 받침까지 내려오는 짧은 기둥
            float stem = 1f - Ramp(0.030f, 0.050f, Mathf.Abs(u - 0.5f));
            if (v < 0.185f || v > 0.345f) stem = 0f;
            a = Mathf.Max(a, stem);

            // ④ 받침 — 가로 막대
            float baseBar = 1f - Ramp(0.150f, 0.178f, Mathf.Abs(u - 0.5f));
            if (v < 0.150f || v > 0.190f) baseBar = 0f;
            a = Mathf.Max(a, baseBar);

            var c = new Color(0.96f, 0.93f, 0.84f);
            c.a = Mathf.Clamp01(a);
            return c;
        }

        public void Dispose()
        {
            Kill(ref hanji); Kill(ref wood); Kill(ref slot); Kill(ref dot); Kill(ref glass); Kill(ref mic);
        }

        static void Kill(ref Sprite s)
        {
            if (s == null) return;
            if (s.texture != null) Object.Destroy(s.texture);
            Object.Destroy(s);
            s = null;
        }
    }
}
