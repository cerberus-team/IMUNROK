using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 종막 퍼즐 — 서고 찬장 속 **수령의 장부** (2026-08-24).
    ///
    /// <code>
    ///  찬장 문 열기 → 선반 조준 → 포커스
    ///    1단계 배열 : 표찰 7 + 기물 12 를 3×4 격자에 끌어다 놓는다  → F_장부복원
    ///    2단계 대조 : 아버지 기록 세 줄 옆 대조대에 맞는 기물을 올린다 → F_기물대조
    ///    3단계 추론 : 세 기물의 처리 정보가 드러난다 (崔 · 後庫)      → F_후고단서
    ///                → 창고방 서랍장에 목표 표시 → 최종 물증 C2
    /// </code>
    ///
    /// ■ 판정은 자리표만 본다
    ///   <see cref="LedgerSlot.occupant"/> 를 훑어 줄마다 속성이 같은지 볼 뿐, 정답 배열을
    ///   따로 들고 있지 않다. 그래서 **순서가 자유**다 — 어느 열이 매화든 상관없고,
    ///   어느 축이 4개짜리인지도 게임이 말하지 않는다(기획 요구).
    ///
    /// ■ 확대 조사는 소지품 조사 화면을 그대로 쓴다
    ///   기물마다 <see cref="InventoryItem"/> 정의를 하나씩 두고, 소지품에 넣지 않은 채
    ///   조사 화면만 연다. 돌려 보기·확대·어두운 막이 전부 이미 만들어져 있다.
    /// </summary>
    public class LedgerPuzzle : FocusInteractable, IInnerTarget
    {
        public enum Stage { 배열, 대조, 완료 }

        [Header("연결")]
        [Tooltip("자리(LedgerSlot)와 조각(LedgerPiece)이 모두 매달린 판 뿌리")]
        public Transform boardRoot;

        [Tooltip("이 찬장이 열려 있어야 조사할 수 있다")]
        public FurnitureParts chestDoors;

        [Tooltip("2단계에서 드러나는 아버지 기록 문서 (1단계 동안 꺼 둔다)")]
        public GameObject recordSheet;

        [Tooltip("2단계 대조대 표시 (1단계 동안 꺼 둔다)")]
        public GameObject trayMarks;

        [Tooltip("3단계에서 세 기물 곁에 뜨는 처리 정보 딱지 3장 (기록 순서대로)")]
        public List<GameObject> handlingTags = new List<GameObject>();

        [Tooltip("1단계에만 있는 표찰 보관 선반 (2단계부터는 그 자리를 기록·대조대가 쓴다)")]
        public GameObject poolShelf;

        [Tooltip("포커스 중에만 켜는 조사등 — 서고는 어두워 그냥 두면 기물이 분간되지 않는다")]
        public Light focusLight;

        [Tooltip("1단계에서 바라볼 지점 — 위 칸과 표찰 보관대만 담는다 (비우면 focusAnchor)")]
        public Transform arrangeAnchor;
        [Tooltip("1단계 카메라 거리(m). 2단계는 아래 칸까지 담아야 해 더 물러선다")]
        public float arrangeDistance = 1.21f;

        [Tooltip("3단계에서 바라볼 지점 — 대조대와 처리 딱지만 담아 「崔」「後庫」를 크게 보인다")]
        public Transform revealAnchor;
        [Tooltip("3단계 카메라 거리(m)")]
        public float revealDistance = 0.72f;

        [Header("진행 조건")]
        [Tooltip("이것이 없으면 장부를 읽을 줄 모른다 — 선아의 풀이표(C3)")]
        public string requireForStage1 = GyeonuWorld.F_선아풀이표;
        [Tooltip("이것이 없으면 대조할 것이 없다 — 아버지의 검수 기록(C1)")]
        public string requireForStage2 = GyeonuWorld.F_아버지검수기록;

        [Header("조작")]
        [Tooltip("자리에 붙는 최대 거리(m). 이보다 멀면 제자리로 돌아간다")]
        public float snapRadius = 0.085f;
        [Tooltip("끌고 다니는 동안 판에서 띄우는 거리(m)")]
        public float dragLift = 0.035f;
        [Tooltip("이보다 적게 움직이고 놓으면 '자세히 보기'로 친다(픽셀)")]
        public float clickSlop = 7f;

        [Header("판 범위 (조준점을 붙잡아 두는 자리, 찬장 로컬)")]
        public float 판반폭 = 0.360f;
        public float 판아래 = 0.590f;
        public float 판위 = 1.570f;

        // ── 상태 ─────────────────────────────────────────────
        Stage stage = Stage.배열;
        readonly List<LedgerSlot> slots = new List<LedgerSlot>();
        readonly List<LedgerPiece> pieces = new List<LedgerPiece>();

        LedgerPiece held;
        LedgerSlot heldFrom;
        float dragDist;
        bool labelsLocked;
        bool inspecting;
        bool focusing;
        string memo;
        NotePanel memoPanel;   // 선아의 메모 — 월드 판 (2026-08-26, 예전엔 OnGUI)

        // 조준점 — ⚠️ static 캐시 금지 (도메인 리로드가 꺼진 프로젝트, 비네트에서 물린 자리)
        GameObject reticle;
        Renderer reticleRend;
        MaterialPropertyBlock reticleMpb;
        Texture2D reticleTex;
        string flash;            // 잠깐 뜨는 판정 문구
        float flashUntil;

        AudioSource audioSrc;
        AudioClip sfxMetal, sfxGlass, sfxPaper, sfxPick, sfxChime, sfxReject;

        public Stage Now => stage;

        // ── 준비 ─────────────────────────────────────────────
        void Awake()
        {
            if (boardRoot == null) boardRoot = transform;
            boardRoot.GetComponentsInChildren(true, slots);
            boardRoot.GetComponentsInChildren(true, pieces);
            foreach (var p in pieces) p.Capture();

            audioSrc = GetComponent<AudioSource>();
            if (audioSrc == null) audioSrc = gameObject.AddComponent<AudioSource>();
            audioSrc.playOnAwake = false;
            audioSrc.spatialBlend = 1f;
            audioSrc.minDistance = 0.6f;
            audioSrc.maxDistance = 9f;

            sfxMetal = BrassSfx.Detent();
            sfxGlass = GlassSfx.Place();
            sfxPaper = PaperSfx.Place();
            sfxPick = PaperSfx.Pick();
            sfxChime = PaperSfx.Chime();
            sfxReject = PaperSfx.Reject();

            // 이미 풀어 둔 상태로 씬을 다시 들어온 경우 (플래그는 씬 밖에 남는다)
            if (GyeonuWorld.Has(GyeonuWorld.F_기물대조)) { stage = Stage.완료; }
            else if (GyeonuWorld.Has(GyeonuWorld.F_장부복원)) { stage = Stage.대조; }
            ApplyStageVisuals();
        }

        protected override void OnDestroy()
        {
            if (memoPanel != null) Destroy(memoPanel.gameObject);
            foreach (var c in new[] { sfxMetal, sfxGlass, sfxPaper, sfxPick, sfxChime, sfxReject })
                if (c != null) Destroy(c);
            if (reticleTex != null) Destroy(reticleTex);
            if (reticleRend != null && reticleRend.sharedMaterial != null) Destroy(reticleRend.sharedMaterial);
            base.OnDestroy();
        }

        // ── 조준·진입 ────────────────────────────────────────
        public override bool CanInteract(GameObject actor)
            => chestDoors == null || chestDoors.IsOpen;

        public override string Prompt => "장부 살피기";

        public override void Interact(GameObject actor)
        {
            if (chestDoors != null && !chestDoors.IsOpen) return;
            if (stage == Stage.배열 && !GyeonuWorld.HasAll(new[] { requireForStage1 }))
            {
                DebugToast.ShowPinned("표찰과 기물이 뒤섞여 있다. 무엇을 어디에 두어야 하는지 알 수 없다.");
                return;
            }
            base.Interact(actor);
        }

        // ── 포커스 자세 ──────────────────────────────────────
        /// <summary>
        /// 단계마다 담아야 할 세로 범위가 다르다.
        ///   배열 — 위 칸 + 표찰 보관대 / 대조 — 판 전체 / 완료 — 대조대와 처리 딱지만 바싹
        /// 마지막을 당겨 보는 까닭: 게임이 "후고로 가라"고 말하지 않는 대신 「崔」「後庫」
        /// 넉 자를 읽게 해야 하는데, 판 전체를 담은 화면에서는 그 글자가 서른 픽셀도 안 된다.
        /// </summary>
        Transform Anchor
        {
            get
            {
                if (stage == Stage.배열 && arrangeAnchor != null) return arrangeAnchor;
                if (stage == Stage.완료 && revealAnchor != null) return revealAnchor;
                return focusAnchor;
            }
        }

        float Distance
        {
            get
            {
                if (stage == Stage.배열) return arrangeDistance;
                if (stage == Stage.완료 && revealAnchor != null) return revealDistance;
                return focusDistance;
            }
        }

        public override Vector3 FocusPoint => Anchor != null ? Anchor.position : boardRoot.position;

        public override void GetFocusPose(Vector3 currentEyePos, out Vector3 pos, out Quaternion rot)
        {
            // 판을 **정면으로** 마주 본다. 판의 위쪽(+Y)을 화면 위로 삼으면 비스듬한 찬장에서도
            // 격자가 저절로 바로 선다 (렌즈 퍼즐에서 배운 것).
            var c = FocusPoint;
            pos = c + boardRoot.forward * Distance;
            rot = Quaternion.LookRotation(-boardRoot.forward, boardRoot.up);
        }

        public override float FocusFov => 38f;

        /// <summary>
        /// 확대 조사 중에는 **퍼즐에서 물러날 수 없다** (2026-08-25).
        ///
        /// 두 화면이 겹쳐 있는데 물러나기 키는 하나뿐이다. 조사 화면을 닫으려고 누른 Esc가
        /// 같은 프레임에 포커스 리그까지 닿아 퍼즐이 통째로 닫혔다 — 소지품 입력이 먼저 돌면
        /// 판이 닫힌 뒤라 리그의 "판이 열려 있으면 비켜라" 검사가 이미 통하지 않는다.
        /// 계층을 나누는 가장 확실한 자리가 여기다: 안쪽 화면이 살아 있는 동안 바깥은 잠근다.
        /// (<see cref="WaitInspect"/>가 판이 닫히고 **한 프레임 더** 지나서야 잠금을 푼다)
        /// </summary>
        public override bool CanExitFocus => !inspecting;

        public override string FocusHint =>
            stage == Stage.완료
                ? UiWords.Back + " — 물러나기"
                : "드래그 — 옮기기      " + UiWords.Press + " — 자세히 보기      " + UiWords.Back + " — 물러나기";

        /// <summary>
        /// 화면 아래 상태 줄은 **판정 문구가 뜰 때만** 쓴다.
        /// 늘 무언가를 적어 두면 판 아래쪽 표찰 줄과 겹쳐 둘 다 안 읽힌다(Play 실측).
        /// 지금 무엇을 해야 하는지는 왼쪽의 선아 메모가 말한다.
        /// </summary>
        public override string FocusStatus
            => (Time.time < flashUntil && !string.IsNullOrEmpty(flash)) ? flash : null;

        public override void OnFocusChanged(bool focused)
        {
            focusing = focused;
            if (focusLight != null) focusLight.enabled = focused;
            if (reticle != null) reticle.SetActive(focused);
            if (!focused) { DropHeld(true); memo = null; return; }
            RefreshMemo();
        }

        void RefreshMemo()
        {
            switch (stage)
            {
                case Stage.배열:
                    memo = GyeonuWorld.HasAll(new[] { requireForStage1 }) ? LedgerData.메모_1단계 : null;
                    break;
                case Stage.대조:
                    memo = GyeonuWorld.HasAll(new[] { requireForStage2 })
                         ? LedgerData.메모_2단계
                         : "배열은 맞았다. 그러나 견주어 볼 기록이 없다.";
                    break;
                default:
                    memo = LedgerData.문구_3단계;
                    break;
            }
        }

        /// <summary>
        /// 선아의 메모를 **왼쪽에 여러 줄로** 적는다.
        ///
        /// ⚠️ <see cref="DebugToast.ShowPinned"/>는 화면 아래 한 줄짜리다 — 넉 줄짜리 메모를
        ///    넘기면 가운데만 남고 앞뒤가 잘려 나간다(Play 실측). 퍼즐을 푸는 내내 곁에
        ///    두고 읽어야 하는 글이라 자리도 아래가 아니라 옆이 맞다.
        /// </summary>
        void LateUpdate()
        {
            // 확대 조사 중에는 물러난다 — 조사 화면의 「살펴본 것」이 같은 자리를 쓴다
            bool show = focusing && !inspecting && !string.IsNullOrEmpty(memo);
            if (!show)
            {
                if (memoPanel != null) memoPanel.body = null;
                return;
            }
            if (memoPanel == null)
            {
                memoPanel = NotePanel.Create(transform, "장부_메모판");
                memoPanel.topLeftPx = new Vector2(22f, 0f);
                memoPanel.topFraction = 0.22f;      // IMGUI의 y = Screen.height * 0.22 와 같다
                memoPanel.headSize = 15;
                memoPanel.bodySize = 14;
                memoPanel.padX = 14f; memoPanel.padTop = 10f; memoPanel.padBottom = 20f;
                memoPanel.titleBlock = 26f;
                memoPanel.backColor = UiSkin.NoteBack;
                memoPanel.headColor = UiSkin.NoteHead;
                memoPanel.bodyColor = UiSkin.NoteBody;
            }
            // 폭은 '화면' 너비를 따라간다 — IMGUI의 min(360, width*0.26) 을 그대로
            memoPanel.width = Mathf.Min(360f, memoPanel.ScreenWidthUnits * 0.26f);
            memoPanel.title = stage == Stage.완료 ? "장부에 드러난 것" : "선아의 메모";
            memoPanel.body = memo;
        }

        // ── 드래그 ───────────────────────────────────────────
        public override void HandleClick(Ray ray)
        {
            if (inspecting || stage == Stage.완료) return;
            dragDist = 0f;
            var p = PieceUnder(ray);
            if (p == null) return;
            if (p.isLabel && labelsLocked) { Flash("표찰은 이미 제자리에 있다."); return; }
            if (!p.isLabel && stage == Stage.대조 && !GyeonuWorld.HasAll(new[] { requireForStage2 }))
            {
                Flash("무엇과 견주어야 할지 모르겠다.");
                return;
            }
            held = p;
            heldFrom = p.slot;
            held.Grab(BoardPoint(ray));
            Play(sfxPick, 0.8f);
        }

        public override void HandleDrag(Ray ray, Vector2 delta)
        {
            if (inspecting) return;
            dragDist += delta.magnitude;
            if (held == null) return;
            held.DragTo(BoardPoint(ray), boardRoot.forward, dragLift);
        }

        public override void HandleDrag(Vector2 delta) { }

        /// <summary>겨누는 자리에 조준점을 옮긴다. 소지품 판과 같은 방식 — 하드웨어 커서 대신 우리가 그린다.</summary>
        public override void HandlePoint(Ray ray)
        {
            if (inspecting) { if (reticle != null) reticle.SetActive(false); return; }
            EnsureReticle();
            reticle.SetActive(true);
            // 판 평면 위 점. 선반널 앞면(판Z+0.055)보다 더 띄워야 널에 파묻히지 않는다.
            // ⚠️ 판 밖을 가리키면 **가장자리에 붙잡는다** — 소지품 판이 쓰는 방식 그대로다.
            //    놓치면 커서가 통째로 사라져 지금 어디를 겨누는지 알 수 없다.
            var local = boardRoot.InverseTransformPoint(BoardPoint(ray));
            local.x = Mathf.Clamp(local.x, -판반폭, 판반폭);
            local.y = Mathf.Clamp(local.y, 판아래, 판위);
            reticle.transform.position = boardRoot.TransformPoint(local) + boardRoot.forward * 0.080f;
            // ⚠️ 유니티 기본 Quad는 **−Z 면이 보인다**. forward를 판 앞쪽으로 두면 뒤통수를
            //    보여 주게 돼 통째로 안 보인다(실측). 카메라 반대쪽을 향하게 세운다.
            reticle.transform.rotation = Quaternion.LookRotation(-boardRoot.forward, boardRoot.up);
            // 집어 든 동안에는 옅게 — 조각이 이미 손을 따라오므로 조준점이 앞설 필요가 없다
            float a = held != null ? 0.32f : 0.85f;
            reticleMpb.SetColor("_Color", new Color(1f, 0.93f, 0.72f, a));
            reticleRend.SetPropertyBlock(reticleMpb);
        }

        void EnsureReticle()
        {
            // 먼저 만들어 둔다 — 아래에서 무엇이 걸려도 반쯤 지어진 채로 남지 않게
            if (reticleMpb == null) reticleMpb = new MaterialPropertyBlock();
            if (reticle != null) return;
            reticle = GameObject.CreatePrimitive(PrimitiveType.Quad);
            reticle.name = "장부_조준점";
            Destroy(reticle.GetComponent<Collider>());
            reticle.transform.SetParent(boardRoot, false);
            reticle.transform.localScale = Vector3.one * 0.042f;
            var mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = ReticleTex() };
            mat.renderQueue = 3850;   // 조각·판보다 뒤에 그려 늘 보인다
            reticleRend = reticle.GetComponent<Renderer>();
            reticleRend.sharedMaterial = mat;
            reticleRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            reticleMpb = new MaterialPropertyBlock();
        }

        /// <summary>가운데가 빈 동그라미 — 겨눈 자리를 가리지 않는다.</summary>
        Texture2D ReticleTex()
        {
            const int res = 64;
            reticleTex = new Texture2D(res, res, TextureFormat.RGBA32, false)
            { name = "장부_조준점_텍스처", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            float c = (res - 1) * 0.5f;
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    // 고리 하나 + 가운데 점 하나 — 어수선한 선반 위에서도 눈에 걸린다
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 0.78f) / 0.16f);
                    float dot = Mathf.Clamp01(1f - d / 0.16f);
                    reticleTex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Max(ring * ring, dot)));
                }
            reticleTex.Apply();
            return reticleTex;
        }

        public override void HandleRelease()
        {
            if (inspecting) return;
            if (held != null && dragDist <= clickSlop)
            {
                // 거의 안 움직였다 — 옮기려던 게 아니라 들여다보려던 것이다
                var p = held;
                DropHeld(true);
                Inspect(p);
                return;
            }
            DropHeld(false);
        }

        /// <summary>들고 있던 것을 놓는다. <paramref name="home"/>이면 무조건 제자리로.</summary>
        void DropHeld(bool home)
        {
            if (held == null) return;
            var p = held; held = null;

            if (home) { p.ReturnHome(); return; }

            var target = NearestSlot(p);
            if (target == null) { p.ReturnHome(); Play(sfxReject, 0.35f); return; }

            if (target.occupant != null && target.occupant != p)
            {
                // 자리 바꾸기 — 원래 있던 것이 이쪽으로 온다 (빈 자리를 찾아 헤매지 않게)
                var other = target.occupant;
                other.SitIn(heldFrom);
            }
            else if (heldFrom != null && heldFrom.occupant == p) heldFrom.occupant = null;

            p.SitIn(target);
            PlacementSound(p);
            Judge();
        }

        LedgerSlot NearestSlot(LedgerPiece p)
        {
            var here = boardRoot.InverseTransformPoint(p.transform.position);
            LedgerSlot best = null; float bestD = snapRadius * snapRadius;
            foreach (var s in slots)
            {
                if (!s.gameObject.activeInHierarchy || !s.Accepts(p)) continue;
                if (stage != Stage.대조 && s.kind == LedgerSlot.Kind.대조) continue;
                if (stage == Stage.대조 && s.kind == LedgerSlot.Kind.보관) continue;
                var d = (s.transform.localPosition - new Vector3(here.x, here.y, s.transform.localPosition.z)).sqrMagnitude;
                if (d < bestD) { bestD = d; best = s; }
            }
            return best;
        }

        /// <summary>
        /// 포인터 광선이 **조각이 놓이는 평면**과 만나는 점.
        ///
        /// ⚠️ 판 뿌리의 원점을 평면으로 삼으면 안 된다 (2026-08-24 실측). 판 뿌리는 찬장 원점에
        ///    있고 자리는 그보다 17.5cm 앞이라, 비스듬히 내려다보는 광선이 **엉뚱하게 먼 평면**을
        ///    뚫는다. 그만큼 조각이 커서보다 바깥으로 밀려, 화면 가장자리 자리일수록 어긋남이
        ///    커진다 — 맨 아래 표찰이 9.6cm 밀려 흡착 거리(8.5cm)를 넘겼다.
        ///    기준점(기준_배열·기준_대조·기준_처리)은 모두 그 평면 위에 있으므로 그것을 쓴다.
        /// </summary>
        Vector3 BoardPoint(Ray ray)
        {
            var origin = Anchor != null ? Anchor.position : boardRoot.position;
            var plane = new Plane(boardRoot.forward, origin);
            return plane.Raycast(ray, out float d) ? ray.GetPoint(d) : origin;
        }

        /// <summary>광선에 맞은 조각. 찬장 차단 상자에 늘 먼저 맞으므로 전부 훑어 고른다.</summary>
        LedgerPiece PieceUnder(Ray ray)
        {
            // 방금 자리를 바꾼 조각을 곧바로 집는 경우 — 물리 쪽 자세가 아직 옛것이다
            Physics.SyncTransforms();
            var hits = Physics.RaycastAll(ray, 6f, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                var p = h.collider.GetComponentInParent<LedgerPiece>();
                if (p != null) return p;
            }
            return null;
        }

        void PlacementSound(LedgerPiece p)
        {
            if (p.isLabel) { Play(sfxPaper, 1f); return; }
            var def = p.Def;
            Play(def != null && def.touch == LedgerTouch.유리 ? sfxGlass : sfxMetal, 0.85f);
        }

        void Play(AudioClip c, float vol)
        {
            if (c == null || audioSrc == null) return;
            audioSrc.pitch = Random.Range(0.94f, 1.07f);
            audioSrc.PlayOneShot(c, vol);
        }

        void Flash(string text) { flash = text; flashUntil = Time.time + 2.6f; }

        // ── 확대 조사 ────────────────────────────────────────
        void Inspect(LedgerPiece p)
        {
            if (p.isLabel || p.inspectItem == null) return;
            var def = p.Def;
            var input = GetComponentInEye<InventoryInput>();
            if (input == null) return;
            inspecting = true;
            // 1단계에는 분류에 필요한 것만, 2단계부터 미세 특징까지
            input.InspectExternal(p.inspectItem, LedgerData.TraitsFor(def, stage != Stage.배열));
            StartCoroutine(WaitInspect());
        }

        IEnumerator WaitInspect()
        {
            yield return null;
            while (InventoryUI.Instance != null && InventoryUI.Instance.IsOpen) yield return null;
            yield return null;                 // 판이 닫힌 프레임의 클릭이 판으로 새지 않게
            inspecting = false;
        }

        T GetComponentInEye<T>() where T : Component
        {
            var rig = FindFirstObjectByType<DebugFocusRig>();
            return rig != null ? rig.GetComponent<T>() : FindFirstObjectByType<T>();
        }

        /// <summary>단계가 바뀌어 담아야 할 범위가 달라졌다 — 포커스를 유지한 채 카메라만 다시 잡는다.</summary>
        void Reframe()
        {
            if (!focusing) return;
            var rig = FindFirstObjectByType<DebugFocusRig>();
            if (rig != null) rig.Reframe();
        }

        // ── 판정 ─────────────────────────────────────────────
        void Judge()
        {
            if (stage == Stage.배열) JudgeLedger();
            else if (stage == Stage.대조) JudgeMatch();
        }

        /// <summary>1단계 — 각 줄이 같은 속성을 나누어 가지는가. 순서는 보지 않는다.</summary>
        void JudgeLedger()
        {
            var cell = new LedgerPieceDef[LedgerData.Rows, LedgerData.Cols];
            var colLabel = new LedgerPiece[LedgerData.Cols];
            var rowLabel = new LedgerPiece[LedgerData.Rows];

            foreach (var s in slots)
            {
                if (s.kind == LedgerSlot.Kind.칸)
                {
                    if (s.occupant == null) return;             // 아직 다 안 찼다
                    cell[s.index / LedgerData.Cols, s.index % LedgerData.Cols] = s.occupant.Def;
                }
                else if (s.kind == LedgerSlot.Kind.열머리) colLabel[s.index] = s.occupant;
                else if (s.kind == LedgerSlot.Kind.행머리) rowLabel[s.index] = s.occupant;
            }
            foreach (var l in colLabel) if (l == null) return;
            foreach (var l in rowLabel) if (l == null) return;

            // 열 = 제작자 표식 / 행 = 처리 장소. 표찰이 그 뜻인지도 함께 본다.
            for (int c = 0; c < LedgerData.Cols; c++)
            {
                if (!colLabel[c].labelIsMark) { Reject("표찰이 어긋난다."); return; }
                var m = colLabel[c].Mark;
                for (int r = 0; r < LedgerData.Rows; r++)
                    if (cell[r, c] == null || cell[r, c].mark != m) { Reject("세로 줄의 표식이 고르지 않다."); return; }
            }
            for (int r = 0; r < LedgerData.Rows; r++)
            {
                if (rowLabel[r].labelIsMark) { Reject("표찰이 어긋난다."); return; }
                var st = rowLabel[r].Site;
                for (int c = 0; c < LedgerData.Cols; c++)
                    if (cell[r, c] == null || cell[r, c].site != st) { Reject("가로 줄의 흔적이 고르지 않다."); return; }
            }

            // 열 표찰 4종·행 표찰 3종이 겹치지 않아야 한다 (같은 표찰이 두 번 놓일 수는 없으므로
            // 자리표가 성립하는 한 자동으로 만족하지만, 판정을 자기완결적으로 둔다)
            var seen = new HashSet<int>();
            foreach (var l in colLabel) if (!seen.Add(l.id)) { Reject("같은 표찰이 두 번 놓였다."); return; }
            foreach (var l in rowLabel) if (!seen.Add(l.id)) { Reject("같은 표찰이 두 번 놓였다."); return; }

            Solve1();
        }

        void Solve1()
        {
            GyeonuWorld.Set(GyeonuWorld.F_장부복원);
            stage = Stage.대조;
            labelsLocked = true;
            Play(sfxChime, 1f);
            Flash("장부가 제자리를 찾았다.");
            ApplyStageVisuals();
            RefreshMemo();
            Reframe();          // 아래 칸의 기록·대조대까지 담도록 카메라를 물린다
        }

        /// <summary>2단계 — 대조대 세 자리가 다 차면 한 번에 판정한다.</summary>
        void JudgeMatch()
        {
            var tray = new LedgerPiece[LedgerData.Records.Length];
            foreach (var s in slots)
                if (s.kind == LedgerSlot.Kind.대조 && s.index < tray.Length) tray[s.index] = s.occupant;
            foreach (var t in tray) if (t == null) return;

            for (int i = 0; i < tray.Length; i++)
            {
                var def = tray[i].Def;
                if (def == null || def.answerOf != i)
                {
                    Reject("셋 가운데 아버지의 기록과 어긋나는 것이 있다.");
                    return;
                }
            }
            Solve2();
        }

        void Solve2()
        {
            GyeonuWorld.Set(GyeonuWorld.F_기물대조);
            GyeonuWorld.Set(GyeonuWorld.F_후고단서);
            stage = Stage.완료;
            Play(sfxChime, 1f);
            ApplyStageVisuals();
            // 딱지는 한 장씩 드러난다 — ApplyStageVisuals가 켜 둔 것을 도로 감추고 시작한다
            foreach (var g in handlingTags) if (g != null) g.SetActive(false);
            memo = null;
            StartCoroutine(RevealHandling());
        }

        /// <summary>3단계 — 세 기물의 처리 정보가 하나씩 드러난다. 게임은 어디로 가라 말하지 않는다.</summary>
        IEnumerator RevealHandling()
        {
            Reframe();                                   // 대조대 앞으로 바싹 — 넉 자를 읽어야 한다
            yield return new WaitForSeconds(0.45f);
            for (int i = 0; i < handlingTags.Count; i++)
            {
                if (handlingTags[i] != null) handlingTags[i].SetActive(true);
                Play(sfxPaper, 0.7f);
                yield return new WaitForSeconds(0.55f);
            }
            yield return new WaitForSeconds(0.5f);
            RefreshMemo();
        }

        void Reject(string why)
        {
            Play(sfxReject, 0.5f);
            Flash(why);
        }

        // ── 단계별 겉모습 ────────────────────────────────────
        void ApplyStageVisuals()
        {
            bool matching = stage != Stage.배열;
            if (recordSheet != null) recordSheet.SetActive(matching);
            if (trayMarks != null) trayMarks.SetActive(matching);
            if (poolShelf != null) poolShelf.SetActive(!matching);
            labelsLocked = matching;

            // 1단계에는 보관 자리를, 2단계부터는 대조대를 쓴다.
            foreach (var s in slots)
                if (s.kind == LedgerSlot.Kind.보관 && s.occupant == null)
                    s.gameObject.SetActive(!matching);

            for (int i = 0; i < handlingTags.Count; i++)
                if (handlingTags[i] != null) handlingTags[i].SetActive(stage == Stage.완료);
        }

        // ── 디버그·검증용 ────────────────────────────────────
        /// <summary>정답 배열로 즉시 채운다 (디버그 메뉴가 부른다).</summary>
        public void DebugSolveStage1()
        {
            var cells = new Dictionary<int, LedgerSlot>();
            var cols = new Dictionary<int, LedgerSlot>();
            var rows = new Dictionary<int, LedgerSlot>();
            foreach (var s in slots)
            {
                if (s.kind == LedgerSlot.Kind.칸) cells[s.index] = s;
                else if (s.kind == LedgerSlot.Kind.열머리) cols[s.index] = s;
                else if (s.kind == LedgerSlot.Kind.행머리) rows[s.index] = s;
                s.occupant = null;
            }
            foreach (var p in pieces)
            {
                if (p.isLabel)
                {
                    if (p.labelIsMark) p.SitIn(cols[p.id]);
                    else p.SitIn(rows[p.id - 4]);
                }
                else
                {
                    var d = p.Def;
                    p.SitIn(cells[(int)d.site * LedgerData.Cols + (int)d.mark]);
                }
            }
            JudgeLedger();
        }

        /// <summary>정답 셋을 대조대에 올린다 (디버그).</summary>
        public void DebugSolveStage2()
        {
            if (stage == Stage.배열) DebugSolveStage1();
            var tray = new Dictionary<int, LedgerSlot>();
            foreach (var s in slots) if (s.kind == LedgerSlot.Kind.대조) tray[s.index] = s;
            foreach (var p in pieces)
            {
                var d = p.Def;
                if (d != null && d.IsAnswer) p.SitIn(tray[d.answerOf]);
            }
            JudgeMatch();
        }
    }
}
