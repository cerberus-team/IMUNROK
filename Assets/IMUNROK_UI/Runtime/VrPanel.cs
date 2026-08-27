using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace IMUNROK.Ui
{
    /// <summary>
    /// 월드 스페이스 판의 공통 뼈대 (2026-08-26).
    ///
    /// ■ 무엇을 대신하는가
    ///   IMGUI(<c>OnGUI</c>)로 그리던 것들 — 안내 문구·조준점·조작 힌트·나침반·퍼즐 메모 —
    ///   을 전부 이 판으로 옮긴다. IMGUI는 <b>HMD에 아예 보이지 않는다</b>.
    ///
    /// ■ PC에서 지금과 똑같이 보이게 만드는 방법
    ///   <see cref="UiTuning"/> 이 <b>캔버스 1단위 = 화면 1픽셀</b>이 되도록 배율을 잡아 준다.
    ///   그래서 IMGUI가 쓰던 <c>Rect(x, y, w, h)</c>·<c>fontSize</c> 를 <b>숫자 그대로</b>
    ///   옮겨 담으면 같은 자리에 같은 크기로 뜬다. 자리는 화면 비율(<see cref="Anchor"/>)로
    ///   잡되, 크기는 픽셀로 잡는다 — IMGUI가 하던 방식 그대로다.
    ///
    /// ■ 모드에 따라 달라지는 것은 둘뿐
    ///   ① 배율·거리 — <see cref="UiTuning.Compute"/> 가 준다.
    ///   ② 회전 — PC는 <b>카메라에 붙박이</b>(IMGUI와 같다), VR은 <b>죽은 구간 + 지연</b>으로
    ///      시야를 느슨히 따라간다(멀미 방지). 소지품 판이 쓰던 방식과 같은 규약이다.
    ///
    /// ■ 벽 회피
    ///   IMGUI는 무엇에도 가리지 않았지만 월드 판은 가린다. PC 거리(0.22 m)는 걷기 캡슐
    ///   반지름(0.3 m)보다 가까워 애초에 가려질 수 없고, VR 거리(1 m)에서는 막힌 만큼
    ///   앞으로 당기되 <b>배율을 함께 줄여</b> 보이는 각을 지킨다 — 소지품 판과 같은 수법이다.
    /// </summary>
    public abstract class VrPanel : MonoBehaviour
    {
        protected Canvas canvas;
        protected RectTransform root;
        protected Transform eye;

        UiTuning.Layout layout;
        Camera cam;
        float followDist;
        bool following;
        bool built;
        Vector2 builtSize;

        /// <summary>판 크기 (캔버스 단위 = PC 화면 픽셀). <b>지을 때 한 번만</b> 쓰인다 —
        /// 글에 따라 높이가 달라지는 판은 <see cref="Refresh"/> 에서 <c>root.sizeDelta</c> 를 직접 고친다.</summary>
        protected abstract Vector2 PanelSize { get; }

        /// <summary>화면에서의 자리. 0~1 비율, <b>왼쪽 아래가 (0,0)</b>.
        /// IMGUI의 <c>Rect</c>는 왼쪽 <b>위</b>가 원점이므로 옮길 때 y를 뒤집을 것.</summary>
        protected abstract Vector2 Anchor { get; }

        /// <summary>판의 어느 점을 <see cref="Anchor"/> 에 맞출지. (0.5,0.5)=가운데, (0,1)=왼쪽 위.</summary>
        protected virtual Vector2 Pivot { get { return new Vector2(0.5f, 0.5f); } }

        /// <summary>판 내용을 짓는다. <see cref="root"/> 밑에 붙일 것.</summary>
        protected abstract void Build();

        /// <summary>지금 그릴 것이 있는가. false면 판을 통째로 감춘다.</summary>
        protected virtual bool Visible { get { return true; } }

        /// <summary>매 프레임 내용 갱신 (자리 잡기 전에 불린다).</summary>
        protected virtual void Refresh() { }

        // ─────────────────────────────────────────────────────
        protected virtual void Awake()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = UiTuning.PcPixelsPerUnit;

            root = (RectTransform)transform;
            root.pivot = Pivot;
            layout = UiTuning.Compute(Camera.main);   // 짓는 동안 RefW/RefH를 쓸 수 있게 미리 한 번
            EnsureBuilt();
            UiModes.Changed += OnModeChanged;
        }

        protected virtual void OnDestroy()
        {
            UiModes.Changed -= OnModeChanged;
        }

        void OnModeChanged(UiMode m)
        {
            following = false;   // 배치가 통째로 바뀐다 — 다음 프레임에 즉시 자리를 잡게
        }

        void EnsureBuilt()
        {
            if (built) return;
            built = true;
            builtSize = PanelSize;
            root.sizeDelta = builtSize;
            Build();
        }

        /// <summary>플레이어 눈을 찾는다 — 워커 카메라 우선, 없으면 MainCamera.</summary>
        protected Transform FindEye()
        {
            if (eye != null && eye.gameObject.activeInHierarchy) return eye;
            var walk = FindFirstObjectByType<DebugWalkController>();
            if (walk != null && walk.eye != null) { eye = walk.eye; cam = eye.GetComponent<Camera>(); return eye; }
            var c = Camera.main;
            if (c != null) { eye = c.transform; cam = c; return eye; }
            eye = null; cam = null;
            return null;
        }

        void LateUpdate()
        {
            var e = FindEye();
            bool show = e != null && Visible;
            if (root.childCount > 0 && root.GetChild(0).gameObject.activeSelf != show)
                for (int i = 0; i < root.childCount; i++) root.GetChild(i).gameObject.SetActive(show);
            if (!show) return;

            EnsureBuilt();
            Refresh();

            if (cam == null) cam = e.GetComponent<Camera>();
            layout = UiTuning.Compute(cam);
            var scaler = GetComponent<CanvasScaler>();
            if (scaler != null && !Mathf.Approximately(scaler.dynamicPixelsPerUnit, layout.pixelsPerUnit))
                scaler.dynamicPixelsPerUnit = layout.pixelsPerUnit;

            root.pivot = Pivot;
            Place(e);
        }

        void Place(Transform e)
        {
            // ── 회전 ──
            Quaternion want = e.rotation;
            Quaternion rot;
            if (!layout.follow)
            {
                rot = want;                                   // PC — 화면에 붙박이 (IMGUI와 같다)
                following = false;
            }
            else
            {
                float off = Quaternion.Angle(transform.rotation, want);
                if (off > layout.followDeadZone) following = true;
                else if (off < layout.followDeadZone * 0.35f) following = false;
                rot = following
                    ? Quaternion.Slerp(transform.rotation, want,
                                       1f - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(0.01f, layout.followLag)))
                    : transform.rotation;
                if (!IsFinite(rot)) rot = want;
            }

            // ── 거리 (앞을 막은 것 피하기) ──
            //
            // ⚠️ 판 <b>가운데</b>로만 한 번 재면 안 된다 (2026-08-26 VR 모드에서 실측).
            //    포커스 퍼즐에서는 대상이 눈앞 0.7~1.7 m 에 있는데, 화면 구석에 앉은 판은
            //    가운데 광선이 그 대상을 <b>비껴가</b> 막힌 줄을 모른다. 그러면 판이 대상 뒤로
            //    들어가 <b>혼천의 고리에 나침반이 잘려 나갔다</b>.
            //    판의 네 귀퉁이까지 재고, 가장 가까운 것에 맞춘다 — 소지품 판이 쓰던 수법이다.
            //    (당긴 만큼 배율도 함께 줄이므로 보이는 각은 그대로다)
            float dist = layout.distance;
            Vector3 ahead = rot * Vector3.forward;
            if (dist > 0.4f)   // PC의 0.22 m 에서는 잴 이유가 없다 — 걷기 캡슐이 이미 막아 준다
            {
                foreach (var dir in ProbeDirs(rot, layout))
                {
                    RaycastHit hit;
                    if (!Physics.Raycast(e.position, dir, out hit, layout.distance * 1.3f, ~0, QueryTriggerInteraction.Ignore))
                        continue;
                    // 비스듬한 광선의 거리를 판 면까지의 **수직** 거리로 환산한다
                    float along = hit.distance * Vector3.Dot(dir, ahead);
                    dist = Mathf.Min(dist, along - 0.06f);
                }
                dist = Mathf.Max(0.18f, dist);
            }
            followDist = dist;

            // ── 자리 ── 화면 비율 → 그 거리에서의 실제 오프셋
            float k = dist / layout.distance;                 // 당겨 온 만큼 함께 줄인다
            Vector2 off2 = layout.ViewportToMeters(Anchor) * k;
            Vector3 pos = e.position + ahead * dist + rot * new Vector3(off2.x, off2.y, 0f);

            transform.SetPositionAndRotation(pos, rot);

            // ⚠️ 판은 퍼즐 오브젝트 밑에 매달릴 수도 있는데 그런 부모는 스케일이 1이 아닐 수 있다
            //    (쌍학월도 음각판 등). 부모 배율을 나눠 주지 않으면 판이 엉뚱한 크기로 뜬다.
            float s = layout.unitScale * k;
            var p = transform.parent;
            if (p != null)
            {
                Vector3 ls = p.lossyScale;
                transform.localScale = new Vector3(
                    s / Mathf.Max(1e-5f, Mathf.Abs(ls.x)),
                    s / Mathf.Max(1e-5f, Mathf.Abs(ls.y)),
                    s / Mathf.Max(1e-5f, Mathf.Abs(ls.z)));
            }
            else transform.localScale = Vector3.one * s;
        }

        /// <summary>판 가운데 + 네 귀퉁이를 향하는 방향들 — 자리 확보용 레이캐스트에 쓴다.</summary>
        System.Collections.Generic.IEnumerable<Vector3> ProbeDirs(Quaternion rot, UiTuning.Layout l)
        {
            Vector2 off = l.ViewportToMeters(Anchor);
            Vector2 half = root.sizeDelta * 0.5f * l.unitScale;
            Vector2 pv = root.pivot - new Vector2(0.5f, 0.5f);       // 피벗이 가운데가 아니면 그만큼 밀린다
            Vector2 c = off - new Vector2(pv.x * half.x * 2f, pv.y * half.y * 2f);
            yield return (rot * new Vector3(c.x, c.y, l.distance)).normalized;
            yield return (rot * new Vector3(c.x - half.x, c.y - half.y, l.distance)).normalized;
            yield return (rot * new Vector3(c.x + half.x, c.y - half.y, l.distance)).normalized;
            yield return (rot * new Vector3(c.x - half.x, c.y + half.y, l.distance)).normalized;
            yield return (rot * new Vector3(c.x + half.x, c.y + half.y, l.distance)).normalized;
        }

        static bool IsFinite(Quaternion q)
        {
            return !(float.IsNaN(q.x) || float.IsNaN(q.y) || float.IsNaN(q.z) || float.IsNaN(q.w));
        }

        /// <summary>지금 배치 수치 — 자식이 글자 각도를 검산할 때 쓴다.</summary>
        protected UiTuning.Layout Layout { get { return layout; } }

        /// <summary>'화면'의 가로 단위 수. PC = Screen.width, VR = 고정 기준값.</summary>
        protected float RefW { get { return layout.refWidth > 1f ? layout.refWidth : Mathf.Max(1f, Screen.width); } }
        /// <summary>'화면'의 세로 단위 수. PC = Screen.height, VR = 고정 기준값.</summary>
        protected float RefH { get { return layout.refHeight > 1f ? layout.refHeight : Mathf.Max(1f, Screen.height); } }

        /// <summary>
        /// IMGUI의 <b>왼쪽 위 원점</b> 픽셀 좌표를 화면 비율(왼쪽 아래 원점)로 옮긴다.
        /// <c>GUI.Label(new Rect(x, y, ...))</c> 를 그대로 옮겨 담을 때 쓴다.
        /// </summary>
        protected Vector2 FromTopLeft(float x, float y)
        {
            return new Vector2(x / RefW, 1f - y / RefH);
        }

        // ── 짓기 도우미 ────────────────────────────────────────
        //   IMGUI의 Rect(x,y,w,h)를 그대로 옮겨 담을 수 있게 픽셀 좌표로 받는다.

        protected static RectTransform MakeRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>단색 상자 (IMGUI의 <c>GUI.DrawTexture(rect, whiteTexture)</c> 자리).</summary>
        protected Image MakeBox(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = UiSkin.White;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>
        /// 글 하나를 만든다. 2026-08-26 에 레거시 <c>UI.Text</c> 에서 <c>TextMeshProUGUI</c> 로 옮겼다.
        ///
        /// ■ 왜 옮겼나
        ///   ① OS 글꼴(맑은 고딕)을 빌려 쓰고 있었다 — Quest 스탠드얼론에는 그 글꼴이 없어
        ///      한글이 통째로 □ 가 된다. 예전 주석이 경고하던 바로 그 문제다.
        ///   ② 월드 판은 VR에서 눈앞으로 확대돼 붙는데, 레거시는 고정 크기로 구운 비트맵이라
        ///      확대하면 뭉개진다. TMP 는 거리장(SDF)이라 어느 크기에서도 날이 선다.
        ///
        /// ■ 자리·크기는 그대로다
        ///   IMGUI에서 물려받은 <c>fontSize</c>·<c>TextAnchor</c> 를 그대로 받아
        ///   <see cref="UiSkin.Dress"/> 가 TMP 값으로 환산한다. 부르는 쪽은 한 줄도 안 고쳤다.
        /// </summary>
        protected TextMeshProUGUI MakeText(Transform parent, string name, int fontSize, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            UiSkin.Dress(t, fontSize, anchor, color);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        /// <summary>판 안의 자리를 잡는다 — 판 가운데 기준 오프셋(px)과 크기(px).</summary>
        protected static void Place(RectTransform rt, Vector2 center, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = center;
            rt.sizeDelta = size;
        }

        /// <summary>판 전체를 덮는다.</summary>
        protected static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
