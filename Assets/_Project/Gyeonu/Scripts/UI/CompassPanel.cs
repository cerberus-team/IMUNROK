using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 8방위 나침반 (2026-08-26) — <see cref="HoncheonuiPuzzle"/> 의 IMGUI를 월드로 옮긴 것.
    ///
    /// ■ 이것이 안 보이면 혼천의를 풀 방법이 없다
    ///   여섯 고리가 지금 어느 방위에 있는지 알려 주는 유일한 표시다. IMGUI라 HMD에는 아예
    ///   안 나왔다 — 조사에서 최우선(P0)으로 꼽힌 세 가지 중 하나다.
    ///
    /// ■ IMGUI 좌표를 그대로 옮겼다
    ///   중심 (116, 116) = 반지름 68 + 이름표 18 + 여백 30. 눈금은 반지름−9,
    ///   이름표는 반지름+18, 안내 줄은 중심 아래 110. 크기·색도 전부 같은 값이다.
    ///   ⚠️ GUI는 y가 아래로 커지고 캔버스는 위로 커진다 — <c>Polar</c> 의 부호 하나만 뒤집었다.
    ///
    /// ■ 바늘
    ///   IMGUI는 <c>RotateAroundPivot(bearing)</c> 으로 화면을 돌려 그렸다. 여기서는 바늘 사각형의
    ///   <b>피벗을 아래 끝(0.5, 0)</b>으로 두고 중심에 놓은 뒤 <c>Euler(0,0,−bearing)</c> 로 돌린다.
    ///   GUI의 시계 방향 = 캔버스의 음의 Z 회전이라 부호가 뒤집힌다.
    /// </summary>
    [AddComponentMenu("")]
    public class CompassPanel : VrPanel
    {
        // IMGUI 시절 값 그대로
        const float Radius = 68f;
        const float LabelOut = 18f;
        const float Margin = 30f;
        const float Pad = Radius + 14f;                 // 원판 반지름
        static readonly Vector2 CenterPx = new Vector2(Radius + LabelOut + Margin, Radius + LabelOut + Margin);

        // IMGUI 시절 색·알파 그대로. FromImgui 는 지금은 그냥 통과다 — 왜 그런지는 UiSkin 주석에 있다.
        static readonly Color BrassDim = UiSkin.FromImgui(new Color(0.62f, 0.50f, 0.30f, 0.75f));
        static readonly Color BrassLit = new Color(1.00f, 0.86f, 0.55f, 1f);
        static readonly Color SelColor = new Color(0.95f, 0.44f, 0.28f, 1f);
        static readonly Color Faint = UiSkin.FromImgui(new Color(0.80f, 0.74f, 0.62f, 0.55f));
        static readonly Color HintColor = UiSkin.FromImgui(new Color(1f, 0.92f, 0.78f, 0.8f));

        const int MaxNeedles = 8;

        /// <summary>어느 고리들이 어디를 보고 있는지 — 이 판은 읽기만 한다.</summary>
        public HoncheonuiFocusRings rings;
        /// <summary>다 맞췄는가 — 안내 줄 문구가 달라진다.</summary>
        public bool solved;

        Image dial, axis;
        RectTransform needleBox;
        Image[] ticks = new Image[8];
        TextMeshProUGUI[] labels = new TextMeshProUGUI[8];
        RectTransform[] needles = new RectTransform[MaxNeedles];
        Image[] needleImgs = new Image[MaxNeedles];
        TextMeshProUGUI hint;
        Sprite dialSprite, needleSprite;
        Texture2D dialTex, needleTex;

        public static CompassPanel Create(Transform parent, HoncheonuiFocusRings rings)
        {
            var go = new GameObject("나침반_판", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<CompassPanel>();
            p.rings = rings;
            return p;
        }

        protected override Vector2 PanelSize { get { return new Vector2(Pad * 2f + 80f, Pad * 2f + 100f); } }

        protected override Vector2 Anchor { get { return FromTopLeft(CenterPx.x, CenterPx.y); } }
        protected override bool Visible { get { return rings != null && rings.RingCount > 0; } }

        protected override void Build()
        {
            dial = MakeBox(root, "원판", Color.white);
            dial.sprite = DialSprite();
            Place(dial.rectTransform, Vector2.zero, new Vector2(Pad * 2f, Pad * 2f));

            // ⚠️ 바늘은 <b>따로 담는 그릇</b>에 넣는다. 고른 바늘을 맨 앞으로 올리려고
            //    root에서 SetSiblingIndex를 쓰면 <b>원판이 바늘 위로 밀려 올라간다</b> —
            //    실제로 그랬고, 원판(불투명도 95%)에 덮인 바늘이 절반 밝기로 어둡게 나왔다
            //    (2026-08-26 실측: 바늘 색 0.95,0.44,0.28 이 화면에서 0.48,0.35,0.20 으로 찍혔다).
            //    그릇 안에서만 순서를 바꾸면 원판은 건드려지지 않는다.
            needleBox = MakeRect(root, "바늘들");
            Stretch(needleBox, 0f);
            for (int i = 0; i < MaxNeedles; i++)
            {
                var img = MakeBox(needleBox, "바늘_" + i, BrassDim);
                img.sprite = NeedleSprite();
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0f);          // 아래 끝을 중심에 못 박는다
                rt.anchoredPosition = Vector2.zero;
                needles[i] = rt;
                needleImgs[i] = img;
                img.gameObject.SetActive(false);
            }

            for (int s = 0; s < 8; s++)
            {
                var t = MakeBox(root, "눈금_" + s, Faint);
                t.sprite = UiSkin.Disc;
                Place(t.rectTransform, Polar(s * 45f, Radius - 9f), new Vector2(9f, 9f));
                ticks[s] = t;

                bool card = CompassNames.Cardinal(s);
                var lab = MakeText(root, "이름_" + s, card ? 21 : 12, TextAnchor.MiddleCenter, Faint);
                if (card) lab.fontStyle = FontStyles.Bold;
                lab.text = CompassNames.Han[s];
                Place(lab.rectTransform, Polar(s * 45f, Radius + LabelOut),
                      card ? new Vector2(30f, 26f) : new Vector2(34f, 18f));
                labels[s] = lab;
            }

            axis = MakeBox(root, "축", BrassLit);
            axis.sprite = UiSkin.Disc;
            Place(axis.rectTransform, Vector2.zero, new Vector2(10f, 10f));

            hint = MakeText(root, "안내", 12, TextAnchor.MiddleCenter, HintColor);
            Place(hint.rectTransform, new Vector2(0f, -(Radius + LabelOut + 24f)), new Vector2(Pad * 2f, 20f));
        }

        protected override void Refresh()
        {
            if (rings == null || rings.RingCount == 0) return;

            int sel = rings.Selected;
            int selSlot = rings.Slot(sel);

            bool[] occupied = new bool[8];
            for (int i = 0; i < rings.RingCount; i++)
            {
                int s = rings.Slot(i);
                if (s >= 0 && s < 8) occupied[s] = true;
            }

            for (int s = 0; s < 8; s++)
            {
                bool isSel = (s == selSlot);
                Color c = isSel ? SelColor : (occupied[s] ? BrassLit : Faint);
                float dsz = isSel ? 11f : (occupied[s] ? 9f : 6f);
                ticks[s].color = c;
                ticks[s].rectTransform.sizeDelta = new Vector2(dsz, dsz);
                labels[s].color = c;
            }

            int n = Mathf.Min(rings.RingCount, MaxNeedles);
            for (int i = 0; i < MaxNeedles; i++)
            {
                bool on = i < n;
                if (needleImgs[i].gameObject.activeSelf != on) needleImgs[i].gameObject.SetActive(on);
                if (!on) continue;
                bool isSel = (i == sel);
                float len = isSel ? Radius - 16f : Radius - 30f;
                float w = isSel ? 13f : 8f;
                needles[i].sizeDelta = new Vector2(w, len);
                needles[i].localRotation = Quaternion.Euler(0f, 0f, -rings.Bearing(i));
                needleImgs[i].color = isSel ? SelColor : BrassDim;
                // 고른 바늘을 그릇 안에서 맨 앞으로 — IMGUI가 두 번 돌며 나중에 그리던 것과 같은 효과
                if (isSel) needles[i].SetAsLastSibling();
            }

            string want = solved ? "고리가 모두 물렸다" : UiWords.Wheel + " — 고리 전환";
            if (hint.text != want) hint.text = want;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (dialTex != null) Destroy(dialTex);
            if (needleTex != null) Destroy(needleTex);
        }

        /// <summary>0° = 위(北), 시계 방향. 캔버스는 y가 위로 커지므로 GUI판과 부호가 반대다.</summary>
        static Vector2 Polar(float bearingDeg, float r)
        {
            float a = bearingDeg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(a) * r, Mathf.Cos(a) * r);
        }

        // ── 절차 텍스처 (IMGUI판에서 그대로 옮김) ─────────────
        Sprite DialSprite()
        {
            if (dialSprite != null) return dialSprite;
            const int res = 192;
            dialTex = new Texture2D(res, res, TextureFormat.RGBA32, false)
            { name = "나침반_원판", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            float c = (res - 1) * 0.5f;
            var px = new Color[res * res];
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    Color col = new Color(0f, 0f, 0f, 0f);
                    if (d < 0.90f) col = UiSkin.FromImgui(new Color(0.055f, 0.048f, 0.042f, 0.74f));
                    if (d > 0.855f && d < 0.925f) col = UiSkin.FromImgui(new Color(0.72f, 0.56f, 0.30f, 0.95f));
                    if (d > 0.545f && d < 0.565f) col = UiSkin.FromImgui(new Color(0.55f, 0.45f, 0.28f, 0.55f));
                    // 가장자리 한 겹은 **계단 지우기(AA)** 지 투명 효과가 아니다 —
                    // 테두리 알파에 램프를 곱해 부드럽게만 끝낸다.
                    if (d > 0.925f && d < 0.955f)
                    {
                        var edge = UiSkin.FromImgui(new Color(0.72f, 0.56f, 0.30f, 0.95f));
                        edge.a *= 1f - (d - 0.925f) / 0.03f;
                        col = edge;
                    }
                    px[y * res + x] = col;
                }
            dialTex.SetPixels(px); dialTex.Apply();
            dialSprite = Sprite.Create(dialTex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), 100f);
            return dialSprite;
        }

        /// <summary>끝으로 갈수록 가늘어지는 바늘 (위가 끝).</summary>
        Sprite NeedleSprite()
        {
            if (needleSprite != null) return needleSprite;
            const int w = 16, h = 64;
            needleTex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { name = "나침반_바늘", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float u = y / (float)(h - 1);
                float half = Mathf.Lerp(3.4f, 0.9f, u * u);
                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Abs(x - (w - 1) * 0.5f);
                    px[y * w + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(half - dx + 0.5f));
                }
            }
            needleTex.SetPixels(px); needleTex.Apply();
            needleSprite = Sprite.Create(needleTex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 100f);
            return needleSprite;
        }
    }
}
