using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Gyeonu.EditorTools
{
    /// <summary>
    /// **한지에 먹으로 쓴 글**을 텍스처로 굽는다 (2026-08-24, 서고 장부 표찰·기록).
    ///
    /// ■ 왜 절차 생성인가
    ///   표찰 일곱 장, 기록 세 칸, 처리 딱지 셋 — 열댓 장의 작은 그림이 필요하다. 이미지를
    ///   손으로 만들어 넣으면 저장소에 바이너리가 늘고, 문구가 한 글자 바뀔 때마다 다시 그려야 한다.
    ///   글자 모양만 OS 글꼴에서 빌리고 종이·먹·번짐은 코드로 만든다.
    ///
    /// ■ 글자를 어떻게 가져오는가 (레거시 동적 글꼴)
    ///   프로젝트에는 TextMeshPro 한글 글꼴 에셋이 없고, 소지품 UI도
    ///   <c>Font.CreateDynamicFontFromOSFont</c> 로 한글을 그린다. 같은 길을 쓴다:
    ///   글자를 요청해 글꼴 아틀라스에 굽게 한 뒤, <see cref="CharacterInfo"/>의 UV로 잘라 옮긴다.
    ///
    /// ⚠️ 아틀라스는 읽을 수 없는 텍스처다 — <c>GetPixels</c>가 안 통한다.
    ///    RenderTexture로 <c>Blit</c>한 뒤 <c>ReadPixels</c>로 사본을 뜬다.
    /// ⚠️ 아틀라스 형식이 Alpha8인지 R8인지 유니티 판·플랫폼마다 다르다. 어느 채널에 글자가
    ///    들어 있는지 **읽어 보고 정한다**(변화가 있는 쪽). 한쪽을 가정하면 빈 판이 나온다.
    /// </summary>
    public static class HanjiTextBaker
    {
        // 조선 문방의 색 — InventorySkin과 같은 팔레트
        static readonly Color 한지 = new Color(0.858f, 0.796f, 0.672f);
        static readonly Color 한지그늘 = new Color(0.741f, 0.672f, 0.545f);
        static readonly Color 먹 = new Color(0.098f, 0.082f, 0.075f);
        static readonly Color 주칠 = new Color(0.639f, 0.196f, 0.153f);

        /// <summary>한 줄의 글과 그 크기·색.</summary>
        public class Line
        {
            public string text;
            /// <summary>글자 높이 (텍스처 픽셀)</summary>
            public int size = 44;
            public Color color = 먹;
            /// <summary>줄 아래 여백 (픽셀)</summary>
            public int gap = 10;
            public Line(string t, int s = 44) { text = t; size = s; }
            public Line(string t, int s, Color c) { text = t; size = s; color = c; }
        }

        /// <summary>
        /// 텍스처를 구워 에셋으로 저장하고 돌려준다.
        /// </summary>
        /// <param name="assetPath">Assets/… .png</param>
        /// <param name="w">가로 픽셀</param>
        /// <param name="h">세로 픽셀</param>
        /// <param name="lines">가운데 정렬로 위에서부터 쌓을 줄</param>
        /// <param name="seed">종이 얼룩 난수</param>
        /// <param name="border">테두리 먹줄을 그릴지</param>
        /// <param name="seal">오른쪽 아래에 붉은 관인을 찍을지</param>
        public static Texture2D Bake(string assetPath, int w, int h, IList<Line> lines,
                                     int seed = 20260824, bool border = true, bool seal = false)
        {
            var px = new Color[w * h];
            PaintPaper(px, w, h, seed);
            if (border) PaintBorder(px, w, h);
            PaintLines(px, w, h, lines);
            if (seal) PaintSeal(px, w, h, seed);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            tex.Apply();

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(
                System.IO.Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length))));
            System.IO.File.WriteAllBytes(
                System.IO.Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length)),
                tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            if (imp != null)
            {
                imp.textureType = TextureImporterType.Default;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.filterMode = FilterMode.Bilinear;
                imp.mipmapEnabled = true;
                imp.maxTextureSize = 512;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        // ── 종이 ─────────────────────────────────────────────
        static void PaintPaper(Color[] px, int w, int h, int seed)
        {
            float ox = (seed % 97) * 3.7f, oy = (seed % 61) * 5.1f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                    // ⚠️ 섬유 결의 주기를 너무 촘촘히 잡으면 안 된다. 170으로 두었더니 340px짜리
                    //    작은 판에서 **2픽셀 간격 세로 줄무늬**가 되고, 그 위에 얹은 먹이 조금이라도
                    //    비치면 글자 속이 빗살처럼 갈라져 보였다 (문서 C1·C3에서 실측).
                    float fiber = Mathf.PerlinNoise(ox + u * 62f, oy + v * 22f) * 0.05f;   // 가로 섬유
                    float grain = Mathf.PerlinNoise(ox + u * 70f, oy + v * 70f) * 0.05f;
                    float stain = Mathf.PerlinNoise(ox + u * 3.5f, oy + v * 3.5f) * 0.14f;
                    var c = Color.Lerp(한지, 한지그늘, stain + grain * 0.6f);
                    c.r += fiber * 0.5f; c.g += fiber * 0.45f; c.b += fiber * 0.35f;
                    // 가장자리가 살짝 삭았다
                    float e = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                    c = Color.Lerp(c * 0.82f, c, Ramp(0f, 0.055f, e));
                    c.a = 1f;
                    px[y * w + x] = c;
                }
        }

        static void PaintBorder(Color[] px, int w, int h)
        {
            int m = Mathf.Max(3, Mathf.RoundToInt(Mathf.Min(w, h) * 0.055f));
            int t = Mathf.Max(1, Mathf.RoundToInt(Mathf.Min(w, h) * 0.012f));
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool onX = (x >= m && x < m + t) || (x >= w - m - t && x < w - m);
                    bool onY = (y >= m && y < m + t) || (y >= h - m - t && y < h - m);
                    bool inX = x >= m && x < w - m, inY = y >= m && y < h - m;
                    if ((onX && inY) || (onY && inX))
                    {
                        // 붓으로 그은 줄이라 진하기가 고르지 않다
                        float k = 0.55f + 0.30f * Mathf.PerlinNoise(x * 0.11f, y * 0.11f);
                        px[y * w + x] = Color.Lerp(px[y * w + x], 먹, k);
                    }
                }
        }

        // ── 붉은 관인 ────────────────────────────────────────
        static void PaintSeal(Color[] px, int w, int h, int seed)
        {
            int s = Mathf.RoundToInt(Mathf.Min(w, h) * 0.22f);
            int x0 = w - s - Mathf.RoundToInt(w * 0.075f), y0 = Mathf.RoundToInt(h * 0.055f);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    int px_ = x0 + x, py = y0 + y;
                    if (px_ < 0 || px_ >= w || py < 0 || py >= h) continue;
                    float u = x / (float)s, v = y / (float)s;
                    bool frame = u < 0.13f || u > 0.87f || v < 0.13f || v > 0.87f;
                    // 안쪽에 十자로 칸을 나눈 전서 흉내
                    bool cross = (Mathf.Abs(u - 0.5f) < 0.05f || Mathf.Abs(v - 0.5f) < 0.05f)
                                 && u > 0.13f && u < 0.87f && v > 0.13f && v < 0.87f;
                    bool tick = ((u > 0.22f && u < 0.42f && Mathf.Abs(v - 0.72f) < 0.05f)
                              || (u > 0.58f && u < 0.78f && Mathf.Abs(v - 0.28f) < 0.05f));
                    if (!frame && !cross && !tick) continue;
                    // 인주는 고르게 안 묻는다
                    float ink = 0.55f + 0.45f * Mathf.PerlinNoise(seed * 0.13f + x * 0.35f, y * 0.35f);
                    px[py * w + px_] = Color.Lerp(px[py * w + px_], 주칠, ink);
                }
        }

        // ── 글자 ─────────────────────────────────────────────
        static void PaintLines(Color[] px, int w, int h, IList<Line> lines)
        {
            if (lines == null || lines.Count == 0) return;
            var font = MakeFont();
            if (font == null) { Debug.LogWarning("[장부] 한글 글꼴을 찾지 못했다 — 글자 없이 굽는다"); return; }

            // 줄이 판보다 넓으면 **글자를 줄여 맞춘다**. 안 그러면 양끝이 잘려 나간다
            // (세로로 긴 문서 판에서 한 줄이 통째로 잘렸다, 실측).
            int inner = Mathf.RoundToInt(w * 0.86f);
            foreach (var l in lines)
            {
                if (string.IsNullOrEmpty(l.text)) continue;
                for (int guard = 0; guard < 24 && l.size > 10; guard++)
                {
                    font.RequestCharactersInTexture(l.text, l.size, FontStyle.Normal);
                    float wide = 0f;
                    foreach (char c in l.text)
                        if (font.GetCharacterInfo(c, out var ci, l.size)) wide += ci.advance;
                    if (wide <= inner) break;
                    l.size = Mathf.Max(10, Mathf.FloorToInt(l.size * Mathf.Min(0.94f, inner / wide)));
                }
            }

            // ⚠️ 아틀라스 사본은 **필요한 글자를 모두 요청한 뒤** 한 번만 뜬다.
            //    뜨고 나서 또 요청하면 아틀라스가 다시 짜여 사본과 UV가 어긋난다.
            foreach (var l in lines)
                if (!string.IsNullOrEmpty(l.text)) font.RequestCharactersInTexture(l.text, l.size, FontStyle.Normal);

            var atlas = ReadAtlas(font, out int ch);
            if (atlas == null) return;

            int totalH = 0;
            foreach (var l in lines) totalH += l.size + l.gap;
            totalH -= lines[lines.Count - 1].gap;
            int top = (h + totalH) / 2;                     // 글 뭉치의 위끝 (y는 위쪽이 큼)

            foreach (var l in lines)
            {
                DrawLine(px, w, h, font, atlas, ch, l, top);
                top -= l.size + l.gap;
            }
            Object.DestroyImmediate(atlas);
        }

        /// <summary>
        /// 한 줄을 <paramref name="lineTop"/> 아래로 그린다.
        ///
        /// ⚠️ <see cref="CharacterInfo"/>의 minY/maxY는 **글줄바닥(baseline) 기준**이고 위가 양수다.
        ///    글줄 높이만큼 위로 올리면(baseline = 바닥 + size) 글자가 줄 위로 삐져나가 잘린다
        ///    (첫 판에서 표찰의 「매화」가 위로 잘려 나갔다). 그 줄에서 **가장 높이 솟은 글자**의
        ///    maxY를 재어 그만큼 내린 자리를 baseline으로 잡으면 어떤 글꼴에서도 안 넘친다.
        /// </summary>
        static void DrawLine(Color[] px, int w, int h, Font font, Texture2D atlas, int ch, Line l, int lineTop)
        {
            if (string.IsNullOrEmpty(l.text)) return;

            float total = 0f;
            int maxTop = 1;
            foreach (char c in l.text)
                if (font.GetCharacterInfo(c, out var ci, l.size))
                {
                    total += ci.advance;
                    maxTop = Mathf.Max(maxTop, ci.maxY);
                }
            float penX = (w - total) * 0.5f;
            int baseline = lineTop - maxTop;

            foreach (char c in l.text)
            {
                if (!font.GetCharacterInfo(c, out var ci, l.size)) continue;
                Blit(px, w, h, atlas, ch, ci, penX + ci.minX, baseline + ci.minY, l.color);
                penX += ci.advance;
            }
        }

        /// <summary>글리프 하나를 종이 위에 얹는다. 먹은 가장자리가 번지므로 알파를 살짝 부풀린다.</summary>
        static void Blit(Color[] px, int w, int h, Texture2D atlas, int ch, CharacterInfo ci,
                         float dx, float dy, Color ink)
        {
            int gw = ci.maxX - ci.minX, gh = ci.maxY - ci.minY;
            if (gw <= 0 || gh <= 0) return;
            var uvBL = ci.uvBottomLeft; var uvBR = ci.uvBottomRight; var uvTL = ci.uvTopLeft;

            for (int y = 0; y < gh; y++)
                for (int x = 0; x < gw; x++)
                {
                    float fx = (x + 0.5f) / gw, fy = (y + 0.5f) / gh;
                    // 아틀라스는 회전돼 들어갈 수 있다 — 세 귀퉁이 UV로 실제 축을 만든다
                    var uv = uvBL + (uvBR - uvBL) * fx + (uvTL - uvBL) * fy;
                    var s = atlas.GetPixelBilinear(uv.x, uv.y);
                    float a = ch == 0 ? s.a : (ch == 1 ? s.r : s.g);
                    if (a <= 0.004f) continue;
                    // 먹은 번진다 — 옅은 자리를 조금 진하게 끌어올린다
                    a = Mathf.Clamp01(Mathf.Pow(a, 0.78f) * 1.08f);
                    int tx = Mathf.RoundToInt(dx + x), ty = Mathf.RoundToInt(dy + y);
                    if (tx < 0 || tx >= w || ty < 0 || ty >= h) continue;
                    // 붓 자국 — 획의 **가장자리에서만** 진하기가 흔들린다.
                    // 획 속까지 흔들면 종이의 섬유 결이 비쳐 글자가 빗살처럼 갈라진다.
                    float k = a >= 0.90f ? 1f : a * (0.88f + 0.12f * Mathf.PerlinNoise(tx * 0.09f, ty * 0.09f));
                    px[ty * w + tx] = Color.Lerp(px[ty * w + tx], ink, k);
                }
        }

        /// <summary>
        /// 종이에 얹을 글자를 빌려 올 글꼴 (2026-08-26 교체).
        ///
        /// 전에는 OS의 바탕·궁서를 빌렸다. 지금은 <b>팀 공용 조선 궁서체</b>를 쓴다 —
        /// 화면 UI(<see cref="UiSkin.Font"/>)와 표찰의 글씨체가 갈라지지 않게 하기 위해서다.
        ///
        /// ⚠️ 여기서 쓰는 것은 TMP 에셋이 아니라 <b>레거시 동적 <see cref="Font"/></b> 다.
        ///    이 도구는 글꼴 아틀라스의 글리프를 직접 잘라 붙이는 방식이라 TMP 로는 못 한다.
        ///    같은 .ttf 를 두 갈래로 쓰는 셈이고, 그래서 글씨체는 화면 UI와 똑같이 나온다.
        /// ⚠️ .ttf 는 gitignore 대상이다 — 없으면 OS 글꼴로 내려가 <b>표찰만 글씨체가 다르게</b>
        ///    구워진다. 그때는 경고를 남긴다.
        /// </summary>
        static Font MakeFont()
        {
            const string path = "Assets/_Project/_Common/Art/Fonts/ChosunCentennial_ttf.ttf";
            var own = AssetDatabase.LoadAssetAtPath<Font>(path);
            if (own != null) return own;

            Debug.LogWarning("[장부] 조선 궁서체를 찾지 못했다 (" + path + ") — OS 글꼴로 굽는다. "
                             + "표찰 글씨체가 화면 UI와 달라진다. .ttf 를 받아 넣고 다시 구울 것.");
            return Font.CreateDynamicFontFromOSFont(
                new[] { "Batang", "바탕", "BatangChe", "궁서", "Gungsuh", "Malgun Gothic", "맑은 고딕",
                        "NanumGothic", "나눔고딕", "Gulim", "굴림", "Arial Unicode MS" }, 64);
        }

        /// <summary>
        /// 글꼴 아틀라스의 읽을 수 있는 사본. 글자가 든 채널 번호를 함께 돌려준다
        /// (0=알파, 1=적, 2=녹).
        /// </summary>
        static Texture2D ReadAtlas(Font font, out int channel)
        {
            channel = 0;
            var src = font.material != null ? font.material.mainTexture : null;
            if (src == null) return null;

            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32,
                                                RenderTextureReadWrite.Linear);
            var prev = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            var copy = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            copy.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            // 어느 채널이 실제로 변하는가 — 형식(Alpha8/R8)에 따라 다르다
            var p = copy.GetPixels();
            float minA = 2f, maxA = -1f, minR = 2f, maxR = -1f;
            int step = Mathf.Max(1, p.Length / 40000);
            for (int i = 0; i < p.Length; i += step)
            {
                minA = Mathf.Min(minA, p[i].a); maxA = Mathf.Max(maxA, p[i].a);
                minR = Mathf.Min(minR, p[i].r); maxR = Mathf.Max(maxR, p[i].r);
            }
            channel = (maxA - minA) >= (maxR - minR) ? 0 : 1;
            return copy;
        }

        static float Ramp(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / Mathf.Max(1e-6f, e1 - e0));
            return t * t * (3f - 2f * t);
        }
    }
}
