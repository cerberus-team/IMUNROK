using System.Collections;
using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 문갑 상판 「쌍학월도」 볼록렌즈 퍼즐 (2026-08-24).
    ///
    /// 상판을 조사하면 포커스로 들어가 음각판이 화면을 채우고, 유리 구슬 셋을 끌어다 놓는다.
    ///   ① 가운데 달 위에 하나 — 달이 부풀어 오른다
    ///   ②·③ 두 학의 머리 **바로 아래**에 하나씩 — 머리가 달 쪽으로 떠오른다
    /// 셋이 다 제자리면 「덜컥」, 포커스가 풀리고 서랍이 스스로 열린다.
    ///
    /// ■ 편심을 왜 "머리 아래"로 잡았나 (지시문의 광학을 그대로 따른 결과)
    ///   볼록렌즈 허상은  상 = C + M·(F − C)  다. 렌즈 중심 C를 한쪽으로 옮기면 상은
    ///   **그 반대쪽으로** 밀려난다 — 「치우쳐 놓으면 겉보기 위치가 반대쪽으로 이동」과 같은 말이다.
    ///   그러므로 **머리가 위로 떠오르려면 렌즈가 머리보다 아래**에 있어야 한다.
    ///   정답 좌표는 머리에서 달의 반대 방향으로 6mm — 머리가 달 쪽으로 6mm 떠오른다.
    ///
    /// ■ 확대 구현
    ///   커스텀 셰이더가 아니라 **URP Lit + 텍스처 타일/오프셋**이다. 까닭은 LensBead 머리말에 적었다.
    ///
    /// 관련: <see cref="LensBead"/>, <see cref="GlassSfx"/>, Editor/LensPuzzleBuilder.cs
    /// </summary>
    public class LensPuzzle : FocusInteractable
    {
        [Header("판")]
        [Tooltip("음각판 오브젝트. +X = 그림 가로, +Z = 그림 세로(위), +Y = 판 법선")]
        public Transform panel;
        [Tooltip("판 크기(m) — 그림 가로·세로")]
        public Vector2 panelSize = new Vector2(0.20f, 0.15f);
        [Tooltip("구슬 밑면이 판에서 뜨는 높이(m) — z-파이팅 회피")]
        public float beadLift = 0.0004f;

        [Header("렌즈")]
        public LensBead[] lenses = new LensBead[0];
        [Tooltip("셰이더에 넘길 확대 배율")]
        public float magnification = 2.0f;
        [Tooltip("정답 자리 (판 로컬 m) — 렌즈 수와 같아야 한다")]
        public Vector2[] answers = new Vector2[0];
        [Tooltip("정답으로 쳐 주는 반경(m). 너무 조이면 손맛이 죽는다")]
        public float tolerance = 0.010f;

        [Header("문구")]
        [Tooltip("포커스 중 좌측 상단에 뜨는 문제 제시. 괄호 없이, 쉼표 자리에서 줄을 바꾼다")]
        [TextArea] public string hintText = "달이 차오르고 두 학이 함께 그 빛을 우러를 때\n감춘 것이 모습을 드러내리라.";

        [Header("포커스 시점")]
        [Tooltip("포커스 중 카메라 화각(도). 다가서는 대신 화각을 좁힌다")]
        public float focusFov = 32f;

        [Header("연결")]
        [Tooltip("풀리면 스스로 열릴 가구 (문갑)")]
        public FurnitureParts chest;
        [Tooltip("풀렸음을 기억할 GyeonuWorld 플래그. 문갑의 unlockFlag와 같은 값")]
        public string solvedFlag = GyeonuWorld.F_렌즈퍼즐;

        [Header("조사등 — 포커스 중에만 켜진다")]
        [Tooltip("들여다볼 때 등잔을 끌어당겨 비추는 셈이다. 밤의 문갑 상판은 그냥 두면 " +
                 "거의 검게 나와 학도 달도 분간이 안 된다. ⚠️ URP 추가 광원 한도가 4라 " +
                 "포커스 중에만 켠다 (방의 추가 광원 3 + 이것 = 4).")]
        public Light inspectLight;
        public float inspectIntensity = 2.6f;
        public float inspectFade = 0.35f;

        [Header("소리 (비우면 절차 합성)")]
        public AudioClip placeClip, pickClip, clunkClip, drawerClip;
        [Range(0f, 1f)] public float volume = 0.85f;

        // ── 상태 ──────────────────────────────────────────
        bool focused, solved, solving;
        float focusedAt;
        LensBead grabbed;
        Vector2 grabOffset;
        GameObject lastActor;
        DebugFocusRig rig;
        AudioSource audioSrc;
        // ⚠️ 런타임 생성 클립을 static으로 캐시하지 말 것 — 다음 플레이 세션에 참조만 살고 내용이 죽는다
        AudioClip cPlace, cPick, cClunk, cDrawer;

        public Transform PanelTransform => panel != null ? panel : transform;
        public bool Solved => solved;

        public override string Prompt => "살펴보기";
        public override string FocusHint => solved
            ? "Esc/우클릭: 물러나기"
            : "좌클릭 드래그: 렌즈 옮기기   Esc/우클릭: 물러나기";
        public override bool CanExitFocus => !solving;
        public override float FocusFov => focusFov;
        public override Vector3 FocusPoint => PanelTransform.position;

        // ── 판 좌표계 ────────────────────────────────────

        public Vector3 PanelToWorld(Vector2 p)
        {
            var t = PanelTransform;
            return t.position + t.right * p.x + t.forward * p.y;
        }

        public Vector2 WorldToPanel(Vector3 w)
        {
            var t = PanelTransform;
            var rel = w - t.position;
            return new Vector2(Vector3.Dot(rel, t.right), Vector3.Dot(rel, t.forward));
        }

        // ── 수명 ─────────────────────────────────────────

        void Awake()
        {
            audioSrc = GetComponent<AudioSource>();
            if (audioSrc == null) audioSrc = gameObject.AddComponent<AudioSource>();
            audioSrc.playOnAwake = false;
            audioSrc.spatialBlend = 0.55f;   // 방 안 물건이되 조작음은 또렷하게
            audioSrc.minDistance = 0.7f;
            audioSrc.maxDistance = 8f;

            // 조준점 치수 — 판 크기에 맞춘다. 이 오브젝트의 바운즈는 문갑 전체라 저절로 잡으면 너무 크다.
            // 구슬 위로 뜨게 충분히 띄운다 — 구슬 속에 파묻히면 조준점이 안 보인다.
            if (reticleSize <= 0f) reticleSize = Mathf.Min(panelSize.x, panelSize.y) * 0.13f;
            if (reticleLift <= 0.005f) reticleLift = 0.015f;

            if (GyeonuWorld.Has(solvedFlag))
            {
                solved = true;
                for (int i = 0; i < lenses.Length && i < answers.Length; i++)
                    if (lenses[i] != null) lenses[i].SetPanelPos(answers[i]);
            }
        }

        protected override void OnDestroy()
        {
            if (cPlace != null) Destroy(cPlace);
            if (cPick != null) Destroy(cPick);
            if (cClunk != null) Destroy(cClunk);
            if (cDrawer != null) Destroy(cDrawer);
            base.OnDestroy();
        }

        public override void Interact(GameObject actor)
        {
            lastActor = actor;
            base.Interact(actor);
        }

        // ── 포커스 시점 — 판을 **정면으로** 마주 본다 ──

        /// <summary>
        /// 판 면에 수직으로 선다. 비스듬히 보면 원근 때문에 판이 사다리꼴로 일그러져
        /// 렌즈를 어디에 놓았는지 가늠하기 어렵다.
        ///
        /// ⚠️ `LookRotation(-t.up, Vector3.up)` 으로 쓰면 안 된다 — 수직에서는 forward와 up이
        ///    평행해져 회전이 정의되지 않는다(롤이 튄다). **판의 세로축(+Z = 그림 위)** 을
        ///    화면 위로 삼으면 수직에서도 안전하고, 그림도 저절로 바로 선다.
        /// </summary>
        public override void GetFocusPose(Vector3 currentEyePos, out Vector3 pos, out Quaternion rot)
        {
            var t = PanelTransform;
            pos = FocusPoint + t.up * focusDistance;
            rot = Quaternion.LookRotation(-t.up, t.forward);
        }

        void Update()
        {
            if (inspectLight == null) return;
            float target = focused ? inspectIntensity : 0f;
            if (Mathf.Approximately(inspectLight.intensity, target))
            {
                if (inspectLight.enabled != target > 0.001f) inspectLight.enabled = target > 0.001f;
                return;
            }
            inspectLight.enabled = true;
            inspectLight.intensity = Mathf.MoveTowards(inspectLight.intensity, target,
                                                       inspectIntensity * Time.deltaTime / Mathf.Max(0.05f, inspectFade));
            if (inspectLight.intensity <= 0.001f) inspectLight.enabled = false;
        }

        public override void OnFocusChanged(bool value)
        {
            focused = value;
            if (!value) { grabbed = null; return; }

            focusedAt = Time.time;
            rig = (lastActor != null ? lastActor.GetComponent<DebugFocusRig>() : null);
            if (rig == null) rig = FindFirstObjectByType<DebugFocusRig>();
            // 구슬은 **처음부터 판 위에 흩어져** 있다 (빌더가 놓는다). 가져오는 단계가 없다.
        }

        // ── 조작 ─────────────────────────────────────────

        public override void HandleClick(Ray ray)
        {
            if (solved) return;
            LensBead best = null;
            float bestD = float.MaxValue;
            foreach (var b in lenses)
            {
                if (b == null || b.Col == null) continue;
                // 판·가구 차단 콜라이더에 가로막히지 않게 콜라이더마다 직접 쏜다 (암문 자물쇠와 같은 방식)
                RaycastHit hit;
                if (b.Col.Raycast(ray, out hit, 20f) && hit.distance < bestD)
                {
                    bestD = hit.distance;
                    best = b;
                }
            }
            if (best == null) return;
            grabbed = best;
            Vector2 p;
            grabOffset = RayToPanel(ray, out p) ? best.PanelPos - p : Vector2.zero;
            Play(PickClip());
        }

        public override void HandleDrag(Vector2 delta) { }   // 레이를 받는 쪽을 쓴다

        public override void HandleDrag(Ray ray, Vector2 delta)
        {
            if (grabbed == null || solved) return;
            Vector2 p;
            if (!RayToPanel(ray, out p)) return;
            grabbed.SetPanelPos(ClampToPanel(p + grabOffset, grabbed.radius));
        }

        public override void HandleRelease()
        {
            if (grabbed == null) return;
            grabbed = null;
            Play(PlaceClip());
            CheckSolved();
        }

        /// <summary>
        /// 조준점을 <b>음각판 위</b>에 얹는다 (2026-08-25).
        ///
        /// 여기는 구슬을 끌어다 놓는 곳이라 어디를 겨누는지가 곧 조작이다. 그런데 포커스에
        /// 들어가면 하드웨어 커서가 숨겨져서, 그려 주지 않으면 <b>보이지 않는 커서로 조준</b>하게
        /// 된다 — 실제로 그랬다. 서고 장부가 먼저 겪고 판 위에 그려 해결한 것과 같은 방식이다.
        ///
        /// ⚠️ 판 밖을 겨눠도 <b>가장자리에 붙잡는다</b>. 놓치면 조준점이 사라져 다시 길을 잃는다.
        /// </summary>
        protected override bool ReticleSurface(Ray ray, out Vector3 pos, out Vector3 normal, out Vector3 up)
        {
            var t = PanelTransform;
            normal = t.up;          // 판 법선 = 보는 쪽
            up = t.forward;         // 그림 세로축을 화면 위로
            Vector2 p;
            if (!RayToPanel(ray, out p)) { pos = t.position; return false; }
            float hx = panelSize.x * 0.5f, hy = panelSize.y * 0.5f;
            pos = PanelToWorld(new Vector2(Mathf.Clamp(p.x, -hx, hx), Mathf.Clamp(p.y, -hy, hy)));
            return true;
        }

        bool RayToPanel(Ray ray, out Vector2 p)
        {
            var t = PanelTransform;
            var plane = new Plane(t.up, t.position);
            float d;
            if (plane.Raycast(ray, out d) && d > 0f) { p = WorldToPanel(ray.GetPoint(d)); return true; }
            p = Vector2.zero;
            return false;
        }

        Vector2 ClampToPanel(Vector2 p, float r)
        {
            float hx = Mathf.Max(0f, panelSize.x * 0.5f - r);
            float hy = Mathf.Max(0f, panelSize.y * 0.5f - r);
            return new Vector2(Mathf.Clamp(p.x, -hx, hx), Mathf.Clamp(p.y, -hy, hy));
        }

        // ── 판정 ─────────────────────────────────────────

        /// <summary>렌즈 하나가 정답 하나씩을 맡아 전부 반경 안에 들어야 통과.
        /// 셋뿐이라 순열을 전부 훑는다 — 어느 구슬을 어디에 놓든 상관없다.</summary>
        bool AllInPlace()
        {
            int n = Mathf.Min(lenses.Length, answers.Length);
            if (n == 0) return false;
            return Match(0, n, new bool[n]);
        }

        bool Match(int ai, int n, bool[] used)
        {
            if (ai >= n) return true;
            for (int li = 0; li < n; li++)
            {
                if (used[li] || lenses[li] == null || !lenses[li].onPanel) continue;
                if ((lenses[li].PanelPos - answers[ai]).sqrMagnitude > tolerance * tolerance) continue;
                used[li] = true;
                if (Match(ai + 1, n, used)) return true;
                used[li] = false;
            }
            return false;
        }

        void CheckSolved()
        {
            if (solved || solving || !AllInPlace()) return;
            solved = true;
            StartCoroutine(SolveRoutine());
        }

        IEnumerator SolveRoutine()
        {
            solving = true;
            yield return new WaitForSeconds(0.32f);

            Play(ClunkClip());
            GyeonuWorld.Set(solvedFlag);          // 문갑 잠금 해제 — 이 순간부터 손으로도 열린다
            yield return new WaitForSeconds(1.05f);

            if (rig != null) rig.ExitFocus();
            solving = false;
            yield return new WaitForSeconds(transitionTime + 0.30f);

            if (chest != null && !chest.IsOpen)
            {
                chest.SetOpen(true);
                Play(DrawerClip());
            }
        }

        // ── 소리 ─────────────────────────────────────────

        AudioClip PlaceClip()
        {
            if (placeClip != null) return placeClip;
            if (cPlace == null) cPlace = GlassSfx.Place();
            return cPlace;
        }

        AudioClip PickClip()
        {
            if (pickClip != null) return pickClip;
            if (cPick == null) cPick = GlassSfx.Pick();
            return cPick;
        }

        AudioClip ClunkClip()
        {
            if (clunkClip != null) return clunkClip;
            if (cClunk == null) cClunk = GlassSfx.Clunk();
            return cClunk;
        }

        AudioClip DrawerClip()
        {
            if (drawerClip != null) return drawerClip;
            if (cDrawer == null) cDrawer = GlassSfx.DrawerSlide();
            return cDrawer;
        }

        void Play(AudioClip c)
        {
            if (c == null || audioSrc == null) return;
            audioSrc.PlayOneShot(c, volume);
        }

        // ── 힌트 (좌측 상단) ─────────────────────────────

        /// <summary>좌측 상단 문제 제시. **머리말도 성공 문구도 없다** — 풀고 나면 그냥 사라진다.</summary>
        void OnGUI()
        {
            if (!focused || solved || string.IsNullOrEmpty(hintText)) return;
            float a = Mathf.Clamp01((Time.time - focusedAt) / 0.45f);
            if (a <= 0.01f) return;

            const float w = 520f, x = 26f, y = 26f, pad = 14f;
            var body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft,
                fontStyle = FontStyle.Bold,
            };
            body.normal.textColor = new Color(1f, 0.92f, 0.72f, a);
            float h = body.CalcHeight(new GUIContent(hintText), w - pad * 2f);

            GUI.color = new Color(0f, 0f, 0f, 0.52f * a);
            GUI.DrawTexture(new Rect(x, y, w, h + pad * 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + pad, y + pad, w - pad * 2f, h + 4f), hintText, body);
        }
    }
}
