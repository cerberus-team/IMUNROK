using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace IMUNROK.Ui
{
    /// <summary>
    /// 전체 화면 조사 (2026-08-25) — 어두운 막 위에 물건 하나만 크게 띄우고 돌려 보는 화면.
    /// 상세 창의 돋보기, 그리고 **물건을 처음 손에 넣은 순간**이 이 화면을 연다.
    ///
    /// ■ 왜 소지품 판과 따로인가
    ///   판은 벽을 피해 눈앞 1.5m에 서고 시야를 따라 **천천히** 움직인다. 조사는 반대다 —
    ///   물건을 눈앞에 들어 올린 상태라 화면을 꽉 채워야 하고, 어두운 막이 시야를 **빈틈없이**
    ///   덮어야 한다. 그래서 이 리그는 눈(카메라)에 그대로 붙는다.
    ///
    /// ■ 막은 눈앞 0.12m, 물건은 0.86m
    ///   실내에서는 벽·가구가 1m 안쪽에 있어 막을 뚫고 나온다. 막만 근거리 클립 바로 뒤로
    ///   당겨 **무엇보다 앞**에 두고, 물건 판은 편안한 거리에 두되 정렬 순서로 막 위에 올린다.
    ///
    /// ■ VR
    ///   막·물건 모두 머리에 붙는다. 균일한 어두운 면과 "손에 든 물건"은 머리 고정이 자연스럽고,
    ///   읽을 글이 없어 시선 피로도 없다. 조준은 소지품 판과 같은 광선 규약을 쓴다.
    /// </summary>
    public class InventoryInspect : MonoBehaviour
    {
        // 캔버스 단위 — 실제 크기는 눈앞 거리에서 시야각으로 환산해 정한다
        const float CanvasUnits = 1400f;

        const float CurtainZ = 0.12f;     // 어두운 막까지 (m) — 어떤 벽·가구보다 앞
        const float BoardZ = 0.86f;       // 물건·버튼 판까지 (m)
        const float FillDegrees = 52f;    // 물건 판이 덮는 세로 시야각


        RawImage curtain;
        TextMeshProUGUI hintLabel;

        Canvas board;
        RectTransform boardRt;
        RawImage image;
        RectTransform cursor;
        InventorySkin skin;
        TMP_FontAsset font;

        public bool IsOpen => gameObject.activeSelf;
        /// <summary>조사 판의 평면 — 조준점을 찍을 면 (InventoryUI가 커서를 여기 올린다).</summary>
        public Transform Surface => boardRt;

        public static InventoryInspect Ensure(Transform eye, InventorySkin skin, TMP_FontAsset font)
        {
            var found = eye.GetComponentInChildren<InventoryInspect>(true);
            if (found != null) return found;

            var go = new GameObject("소지품_조사");
            go.transform.SetParent(eye, false);
            var self = go.AddComponent<InventoryInspect>();
            self.skin = skin;
            self.font = font;

            self.Build();
            go.SetActive(false);
            return self;
        }

        void Build()
        {
            // ── 어두운 막 ──
            // ⚠️ 세 번 헛디딘 자리다 (2026-08-25 실측). 결론은 **기본 UI 재질을 쓰고,
            //    막을 눈에서 아주 가까이(0.12m) 두어 깊이로 이기는 것**이다.
            //    ① Quad + UI/Default → 아예 안 보인다. UI 셰이더는 색을 정점 색으로 실어
            //       나르는데 기본 Quad 메시에는 색 채널이 없다.
            //    ② 스프라이트 없는 Image → 역시 안 보인다. RawImage + 흰 텍스처라야 확실하다.
            //    ③ `unity_GUIZTestMode=Always` 재질로 깊이를 무시하려 했더니, 색이 엉키거나
            //       아예 사라졌다. 물건 판(0.86m)만큼은 벽이 앞을 가릴 수 있으므로
            //       **막만** 앞으로 당기고, 물건 판은 정렬 순서로 막 위에 올린다.
            //    막은 균일한 어두운 면이라 눈앞 0.12m라도 초점을 요구하지 않는다(VR 페이드와 같은 방식).
            var curCanvasGo = new GameObject("막", typeof(RectTransform), typeof(Canvas));
            curCanvasGo.transform.SetParent(transform, false);
            curCanvasGo.transform.localPosition = new Vector3(0f, 0f, CurtainZ);
            var curCanvas = curCanvasGo.GetComponent<Canvas>();
            curCanvas.renderMode = RenderMode.WorldSpace;
            curCanvas.sortingOrder = 0;
            var curRt = (RectTransform)curCanvasGo.transform;
            curRt.sizeDelta = new Vector2(CanvasUnits, CanvasUnits);
            curRt.localScale = Vector3.one * (CurtainZ * 12f / CanvasUnits);   // 가로 ±80°까지 덮는다

            var curGo = new GameObject("천", typeof(RectTransform), typeof(RawImage));
            curGo.transform.SetParent(curRt, false);
            curtain = curGo.GetComponent<RawImage>();
            curtain.texture = Texture2D.whiteTexture;
            // ⚠️ 색을 (0.02,0.02,0.03)처럼 "거의 검정"으로 주면 화면이 어두워지지 않고 **뿌옇게 뜬다**
            //    (2026-08-25 실측). 이 프로젝트 색공간에서 UI 알파가 약하게 먹어, 옅은 색이 남아
            //    바탕을 덮는 대신 얹힌다. **순수 검정 + 높은 알파**라야 의도대로 깔린다.
            curtain.color = new Color(0f, 0f, 0f, 0.96f);
            curtain.raycastTarget = false;
            Place((RectTransform)curGo.transform, Vector2.zero, new Vector2(CanvasUnits, CanvasUnits));

            // ── 물건 판 ── (막보다 뒤에 있지만 정렬 순서로 위에 그린다)
            var bg = new GameObject("판", typeof(RectTransform), typeof(Canvas));
            bg.transform.SetParent(transform, false);
            bg.transform.localPosition = new Vector3(0f, 0f, BoardZ);
            board = bg.GetComponent<Canvas>();
            board.renderMode = RenderMode.WorldSpace;
            board.sortingOrder = 1;
            bg.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 1f;
            boardRt = (RectTransform)bg.transform;
            boardRt.sizeDelta = new Vector2(CanvasUnits, CanvasUnits);
            ResizeBoard();

            var img = new GameObject("그림", typeof(RectTransform), typeof(RawImage));
            img.transform.SetParent(boardRt, false);
            image = img.GetComponent<RawImage>();
            image.raycastTarget = false;
            Place((RectTransform)img.transform, new Vector2(0f, 40f), new Vector2(1120f, 1120f));

            MakeButton("닫기", new Vector2(600f, 600f), new Vector2(120f, 120f), InventoryHotspot.Kind.조사닫기, 56, "×");

            var hint = MakeText("안내", 40, TextAnchor.MiddleCenter, new Color(0.86f, 0.82f, 0.74f, 0.85f));
            Place(hint.rectTransform, new Vector2(0f, -628f), new Vector2(1360f, 60f));
            hintLabel = hint;   // 조작 이름이 모드 따라 갈린다 — 매 프레임 다시 적는다

            var dot = new GameObject("조준", typeof(RectTransform), typeof(Image));
            dot.transform.SetParent(boardRt, false);
            var di = dot.GetComponent<Image>();
            di.sprite = skin.Dot_;

            di.raycastTarget = false;
            cursor = (RectTransform)dot.transform;
            Place(cursor, Vector2.zero, new Vector2(34f, 34f));
            cursor.SetAsLastSibling();
        }

        void ResizeBoard()
        {
            if (boardRt == null) return;
            // ⚠️ PC에서는 판 크기를 **지금 카메라의 화각**으로 잡는다 (2026-08-24).
            //    포커스 퍼즐 도중에 조사를 열면 화각이 38°까지 좁아져 있는데, 52°에 맞춘
            //    고정 크기를 쓰면 판이 화면보다 커져 **닫기(✕) 버튼과 조작 안내가 화면 밖으로
            //    밀려난다** (서고 장부에서 실측). 평소(60°)에는 52°가 그대로 뽑힌다.
            //
            // ⚠️⚠️ VR에서는 이 계산을 하면 **안 된다** (2026-08-26).
            //    HMD가 붙으면 투영은 기기가 정하고 <c>cam.fieldOfView</c> 는 뜻을 잃는다 —
            //    직렬화된 값(대개 60)이 그대로 읽혀 판이 엉뚱한 크기로 뜬다. 게다가 VR에서는
            //    화각을 좁히는 연출(FocusFov) 자체가 먹지 않으므로 좁혀질 일도 없다.
            //    그래서 VR에서는 <see cref="FillDegrees"/> 를 그대로 쓴다.
            float deg;
            if (UiModes.IsVr) deg = FillDegrees;
            else
            {
                float fov = 60f;
                var cam = GetComponentInParent<Camera>();
                if (cam != null) fov = cam.fieldOfView;
                deg = Mathf.Min(FillDegrees, fov * 0.92f);
            }
            float h = 2f * BoardZ * Mathf.Tan(deg * 0.5f * Mathf.Deg2Rad);
            boardRt.localScale = Vector3.one * (h / CanvasUnits);
        }

        public void Show(RenderTexture rt, string[] traits = null)
        {
            gameObject.SetActive(true);

            ResizeBoard();
            image.texture = rt;
            image.enabled = rt != null;
            ShowTraits(traits);
        }

        /// <summary>조작 안내를 지금 모드의 이름으로 적는다 (F8로 바꿔도 곧바로 따라온다).</summary>
        void LateUpdate()
        {
            if (hintLabel == null) return;
            string h = "드래그 — 돌리기        " + UiWords.Wheel + " — 확대·축소        "
                     + UiWords.Back + " — 그만 보기";
            if (hintLabel.text != h) hintLabel.text = h;
        }

        /// <summary>
        /// 물건에서 **본 것**을 왼쪽에 글로 적는다 (2026-08-24, 서고 장부).
        /// 미세한 흔적으로만 갈리는 물건을 화면에서 찾아내게 하면 추리가 눈싸움이 된다 —
        /// 본 것을 적어 주고, 어느 것이 기록과 맞는지 **고르는 일**만 남긴다.
        ///
        /// 판이 아니라 조사 화면에 두는 까닭: 돌려 보는 동안 늘 곁에 있어야 견줄 수 있다.
        /// </summary>
        void ShowTraits(string[] traits)
        {
            bool on = traits != null && traits.Length > 0;
            if (traitPanel == null)
            {
                if (!on) return;
                traitPanel = new GameObject("특징", typeof(RectTransform)).GetComponent<RectTransform>();
                traitPanel.SetParent(boardRt, false);
                Place(traitPanel, new Vector2(-470f, 60f), new Vector2(520f, 460f));

                var bg = new GameObject("바탕", typeof(RectTransform), typeof(RawImage));
                bg.transform.SetParent(traitPanel, false);
                var bgi = bg.GetComponent<RawImage>();
                bgi.texture = Texture2D.whiteTexture;
                bgi.color = new Color(0f, 0f, 0f, 0.42f);
                bgi.raycastTarget = false;
                var bgr = (RectTransform)bg.transform;
                bgr.anchorMin = Vector2.zero; bgr.anchorMax = Vector2.one;
                bgr.offsetMin = Vector2.zero; bgr.offsetMax = Vector2.zero;

                traitHead = MakeText("머리", 40, TextAnchor.UpperLeft, InventorySkin.Gold);
                traitHead.transform.SetParent(traitPanel, false);
                Place(traitHead.rectTransform, new Vector2(6f, 186f), new Vector2(480f, 52f));
                traitHead.text = "살 펴 본  것";

                traitBody = MakeText("줄", 36, TextAnchor.UpperLeft, new Color(0.90f, 0.86f, 0.78f, 0.95f));
                traitBody.transform.SetParent(traitPanel, false);
                Place(traitBody.rectTransform, new Vector2(6f, -32f), new Vector2(480f, 360f));
                traitBody.lineSpacing = UiSkin.LineSpacing(1.35f);
            }
            traitPanel.gameObject.SetActive(on);
            if (!on) return;

            var sb = new System.Text.StringBuilder();
            foreach (var t in traits) sb.Append("· ").Append(t).Append('\n');
            traitBody.text = sb.ToString();
        }

        RectTransform traitPanel;
        TextMeshProUGUI traitHead, traitBody;

        public void Hide() => gameObject.SetActive(false);

        /// <summary>조준점을 판 평면 위로 옮긴다. 판 안이면 true.</summary>
        public bool PointAt(Ray ray)
        {
            var plane = new Plane(-boardRt.forward, boardRt.position);
            if (!plane.Raycast(ray, out float d)) { cursor.gameObject.SetActive(false); return false; }
            var local = boardRt.InverseTransformPoint(ray.GetPoint(d));
            float half = CanvasUnits * 0.5f;
            cursor.gameObject.SetActive(true);
            cursor.anchoredPosition = new Vector2(Mathf.Clamp(local.x, -half, half), Mathf.Clamp(local.y, -half, half));
            return Mathf.Abs(local.x) <= half && Mathf.Abs(local.y) <= half;
        }

        // ── 작은 도구들 ───────────────────────────────────────
        InventoryHotspot MakeButton(string name, Vector2 pos, Vector2 size,
                                    InventoryHotspot.Kind kind, int fontSize, string label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(boardRt, false);
            var img = go.GetComponent<Image>();
            img.sprite = skin.Wood_;
            img.color = InventorySkin.Wood;

            img.raycastTarget = false;
            Place((RectTransform)go.transform, pos, size);

            var t = MakeText("글", fontSize, TextAnchor.MiddleCenter, InventorySkin.Hanji);
            t.transform.SetParent(go.transform, false);
            var trt = t.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(4f, 4f); trt.offsetMax = new Vector2(-4f, -4f);
            t.text = label;

            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(size.x, size.y, 2f);
            box.center = new Vector3(0f, 0f, -6f);
            var spot = go.AddComponent<InventoryHotspot>();
            spot.kind = kind;
            spot.frame = img;
            spot.idleColor = InventorySkin.Wood;
            spot.hoverColor = Color.Lerp(InventorySkin.Wood, InventorySkin.Gold, 0.55f);
            return spot;
        }

        TextMeshProUGUI MakeText(string name, int size, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(boardRt, false);
            var t = UiSkin.Dress(go.GetComponent<TextMeshProUGUI>(), size, anchor, color);
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        static void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        void OnDestroy()
        {


        }
    }
}
