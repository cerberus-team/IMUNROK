using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 대화창 배치안. <b>A로 확정</b>되었다 (2026-08-25). B·C는 견주어 본 안으로 남겨 둔다 —
    /// 나중에 다시 저울질할 일이 생기면 <c>Tools ▸ 이문록 ▸ 대화 검증</c> 으로 갈아 끼워 볼 수 있다.
    /// </summary>
    public enum DialogueLayout
    {
        /// <summary>A — 판을 왼쪽 아래에 세우고 NPC를 오른쪽에 둔다. <b>확정안.</b></summary>
        A_좌측판 = 0,
        /// <summary>B — 화면 아래에 얇은 띠. NPC 전신이 거의 다 보인다.</summary>
        B_하단띠 = 1,
        /// <summary>C — NPC 대사는 얼굴 옆 말풍선, 내 입력·제시만 아래 작은 채팅바.</summary>
        C_말풍선 = 2,
    }

    /// <summary>
    /// 대화창 (2026-08-25 개편) — 배치안 셋을 갈아 끼울 수 있는 한지 판.
    ///
    /// ■ 판은 <b>화면과 나란히</b> 선다 (2026-08-25 수정)
    ///   전에는 판을 18° 눕혀 놨더니 원근 때문에 사다리꼴로 일그러져 글을 읽기 불편했다.
    ///   렌즈 퍼즐이 같은 이유로 판 면에 수직으로 서듯(<see cref="LensPuzzle"/>),
    ///   여기서는 <b>판을 카메라 회전 그대로</b> 세우고 자리만 카메라의 위·오른쪽 축으로 밀어낸다.
    ///   그러면 판 면이 화면과 평행해져 어디에 놓든 <b>반듯한 직사각형</b>으로 보인다.
    ///   ⚠️ 자리를 '각도로 회전한 방향 × 거리'로 잡으면 안 된다 — 판 면까지의 거리가 달라져
    ///      가장자리 판이 작아 보인다. <c>forward*dist + up*tan(θ)*dist</c> 로 밀어야 한다.
    ///
    /// ■ 목소리로 묻기
    ///   <b>왼쪽 Ctrl</b> 을 누르고 있는 동안 녹음, 떼면 받아 적어 보낸다.
    ///   글쇠 칸이 잡혀 있으므로 <b>글자가 찍히는 키는 쓸 수 없다</b>(V를 쓰면 입력칸에 'v'가 남는다).
    ///   Ctrl은 글자를 만들지 않고, 왼손으로 누른 채 있기 편하며, 이 프로젝트에서 비어 있다.
    ///
    /// ■ 증거 제시는 <b>소지품 판</b>이 맡는다
    ///   전에는 대화창 안에 이름만 나열했다. 지금은 <see cref="InventoryUI.OpenForPresent"/> 로
    ///   소지품 판을 그대로 열어 물건을 보고 고른다. 정보 단서는 <see cref="DialogueSession"/> 이
    ///   임시 소지품으로 지어 함께 올린다.
    /// </summary>
    public class DialogueUI : MonoBehaviour
    {
        /// <summary>한 배치안의 판 치수와 화면상 자리. 각도는 시야 중심에서의 어긋남(°).</summary>
        public struct Geom
        {
            public float w, h, scale, dist, rightDeg, upDeg;
            public Geom(float w, float h, float scale, float dist, float rightDeg, float upDeg)
            { this.w = w; this.h = h; this.scale = scale; this.dist = dist; this.rightDeg = rightDeg; this.upDeg = upDeg; }
        }

        //  세로 화각 60°(±30°), 16:9 기준 가로 ±45.8° 안에 들어가게 잡은 값들.
        static Geom GeomOf(DialogueLayout l)
        {
            switch (l)
            {
                // 확정안 — 1.50×0.62m 판을 왼쪽 20°·아래 11°로 밀어 둔다.
                //   가로 −40.8°~+7.8° / 세로 −22.7°~+0.7° = **화면 왼쪽 아래 한 귀퉁이**.
                //   ⚠️ 처음엔 세로 1.00m(37°)였는데 판이 화면 왼쪽 절반을 통째로 차지해
                //      "상자가 너무 크다"는 지적을 받았다. 지금은 23°로 줄여 위쪽·오른쪽을 다 비운다.
                //      대신 대사가 길면 잘리므로 **틀 안에서 휠로 굴려 읽는다**(LineViewport).
                case DialogueLayout.A_좌측판: return new Geom(1500f, 620f, 0.001f, 1.5f, -20f, -11f);
                // 2.20×0.40m 띠 → 세로 −27.8°~−14.6°. 화면 아래 1/4만 덮는다.
                case DialogueLayout.B_하단띠: return new Geom(2200f, 400f, 0.001f, 1.5f, 0f, -21.5f);
                // 1.90×0.26m 채팅바 → 세로 −28.5°~−20.3°. 대사는 말풍선이 맡는다.
                default: return new Geom(1900f, 260f, 0.001f, 1.5f, 0f, -24.5f);
            }
        }

        /// <summary>말풍선 치수 (C안). 캔버스 단위 × 0.001 = m.</summary>
        const float BubbleW = 900f, BubbleH = 420f, BubbleScale = 0.001f;

        public static DialogueUI Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        public bool IsOpen { get; private set; }
        public DialogueHotspot Hovered { get; private set; }
        public DialogueLayout Layout { get; private set; } = DialogueLayout.A_좌측판;

        /// <summary>소지품 판(증거 고르기)이 떠 있는가 — Esc가 어디로 갈지를 이 값이 정한다.</summary>
        public bool IsPresenting => InventoryUI.Instance != null && InventoryUI.Instance.IsOpen;

        readonly InventorySkin skin = new InventorySkin();
        readonly VoiceInput voice = new VoiceInput();

        NpcDialogue owner;
        DialogueSession session;
        Transform eye;
        Geom geom;
        bool built;

        Font font;
        RectTransform root;                    // 판 본체 (캔버스 = 이 오브젝트)
        Text nameText, lineText, hintText, voiceText, overflowHint;
        RectTransform cursor, voiceBar, voiceBarFill;
        RectTransform lineViewport, lineRect;   // 대사 — 틀 안에서 굴려 읽는다
        string shownLine = "";
        float lineScroll;
        float scrollReadyAt;
        InputField field;
        DialogueHotspot askSpot, presentSpot, closeSpot;
        Text askLabel;

        // C안 — 말풍선 (자기 캔버스를 따로 가진다: 월드에서 NPC 곁에 떠 있어야 한다)
        GameObject bubbleGo;
        RectTransform bubbleRoot, bubbleTail;
        Text bubbleName, bubbleLine;

        bool eventSystemMine;
        bool prevNavigation;
        bool voiceBusy;

        // ─────────────────────────────────────────────────────────
        public static DialogueUI Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("대화_판");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<DialogueUI>();
            go.SetActive(false);
            return Instance;
        }

        public void Open(NpcDialogue npc, DialogueSession s)
        {
            owner = npc;
            session = s;
            eye = Camera.main != null ? Camera.main.transform : null;
            if (eye == null) { Debug.LogError("[대화] Camera.main이 없다 — 대화창을 세울 수 없다."); return; }

            if (!built || Layout != npc.layout) Rebuild(npc.layout);

            gameObject.SetActive(true);
            if (bubbleGo != null) bubbleGo.SetActive(Layout == DialogueLayout.C_말풍선);
            ApplyPose();
            IsOpen = true;

            session.Changed += Refresh;
            EnsureEventSystem();
            if (EventSystem.current != null)
            {
                prevNavigation = EventSystem.current.sendNavigationEvents;
                // ⚠️ 방향키·WASD가 선택을 옮기면 글을 치다 말고 칸이 풀린다. 대화 중에는 끈다.
                EventSystem.current.sendNavigationEvents = false;
            }

            hintText.text = HintLine;
            shownLine = "";              // 지난 대화의 마지막 줄을 물려받지 않게
            lineScroll = 0f;
            Refresh();
            field.text = "";
            field.ActivateInputField();
        }

        public void Close()
        {
            if (!IsOpen) return;
            voice.Cancel();
            voiceBusy = false;
            if (session != null) session.Changed -= Refresh;
            if (field != null) { field.text = ""; field.DeactivateInputField(); }
            if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = prevNavigation;
            IsOpen = false;
            Hovered = null;
            owner = null;
            session = null;
            if (bubbleGo != null) bubbleGo.SetActive(false);
            gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (eventSystemMine && EventSystem.current != null) Destroy(EventSystem.current.gameObject);
            skin.Dispose();
            if (Instance == this) Instance = null;
        }

        // ── 자리 ─────────────────────────────────────────────
        /// <summary>
        /// 판을 <b>화면과 나란히</b> 세운다. 회전은 카메라 그대로, 자리만 카메라 축으로 민다 —
        /// 그래야 어디에 놓아도 반듯한 직사각형으로 보인다.
        /// </summary>
        void ApplyPose()
        {
            if (eye == null) return;

            float dist = geom.dist;
            // 실내에서 벽·가구에 판이 박히지 않게 막힌 만큼 당겨 온다 (보이는 각은 배율로 지킨다).
            if (Physics.Raycast(eye.position, eye.forward, out var hit, geom.dist * 1.2f, ~0, QueryTriggerInteraction.Ignore))
                dist = Mathf.Min(dist, hit.distance - 0.08f);
            dist = Mathf.Max(0.35f, dist);

            Vector3 pos = eye.position
                        + eye.forward * dist
                        + eye.up * (dist * Mathf.Tan(geom.upDeg * Mathf.Deg2Rad))
                        + eye.right * (dist * Mathf.Tan(geom.rightDeg * Mathf.Deg2Rad));

            transform.SetPositionAndRotation(pos, eye.rotation);
            transform.localScale = Vector3.one * geom.scale * (dist / geom.dist);

            if (bubbleGo != null && bubbleGo.activeSelf) PlaceBubble();
        }

        /// <summary>말풍선을 NPC 얼굴 <b>왼쪽 위</b>에 띄운다. 판과 같이 화면과 나란히 세운다.</summary>
        void PlaceBubble()
        {
            if (owner == null || eye == null) return;
            Vector3 head = owner.FocusPoint + Vector3.up * 0.22f;
            // NPC에서 화면 왼쪽으로 밀어낸다 — 얼굴을 가리지 않게
            Vector3 pos = head - eye.right * 0.62f;
            // 카메라와의 거리를 판보다 조금 뒤로 둬서 판과 겹쳐도 앞뒤가 헷갈리지 않게 한다
            bubbleGo.transform.SetPositionAndRotation(pos, eye.rotation);
            bubbleGo.transform.localScale = Vector3.one * BubbleScale;
            // 꼬리는 NPC 쪽(오른쪽 아래)으로 절반쯤 튀어나오게 둔다 — 안쪽에만 있으면 바탕에 묻힌다.
            if (bubbleTail != null)
                bubbleTail.anchoredPosition = new Vector2(BubbleW * 0.5f + 6f, -BubbleH * 0.5f + 56f);
        }

        void LateUpdate()
        {
            if (!IsOpen) return;
            ApplyPose();

            var kb = Keyboard.current;
            if (kb == null || session == null) return;

            // 소지품 판(증거 고르기)이 떠 있는 동안에는 입력을 통째로 그쪽에 내준다.
            if (IsPresenting)
            {
                if (field.isFocused) field.DeactivateInputField();
                voice.Cancel();
                return;
            }

            // ── 목소리 (왼쪽 Ctrl 누르고 말하기) ──
            HandleVoice(kb);

            // ── 글쇠 칸 되살리기 ──
            // 빈 곳 클릭 한 번으로 선택이 풀린다. 대화 화면에서는 늘 잡혀 있어야 한다.
            if (!voice.Recording && !voiceBusy && !field.isFocused && !session.Busy) field.ActivateInputField();

            // ── Enter — 묻기 ──
            //   InputField의 이벤트를 쓰지 않는다: 판이 선택을 잃을 때도 함께 발화하는 판이라
            //   되살리기와 부딪힌다. 글쇠를 직접 보면 규칙이 하나로 단순해진다.
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Ask();
        }

        // ── 목소리 ────────────────────────────────────────────
        void HandleVoice(Keyboard kb)
        {
            bool held = kb.leftCtrlKey.isPressed;

            if (!voice.Recording && kb.leftCtrlKey.wasPressedThisFrame && !session.Busy && !voiceBusy)
            {
                if (!VoiceInput.HasMicrophone) { DebugToast.Show("마이크를 찾지 못했다.", 2f); return; }
                if (!GyeonuGeminiResponder.HasKey) { DebugToast.Show("API 키가 없어 목소리를 쓸 수 없다.", 2.5f); return; }
                if (voice.Begin())
                {
                    field.DeactivateInputField();
                    Refresh();
                }
                return;
            }

            if (voice.Recording)
            {
                voice.Tick();
                UpdateVoiceMeter();
                if (!held || kb.leftCtrlKey.wasReleasedThisFrame) EndVoice();
            }
        }

        void EndVoice()
        {
            var wav = voice.End();
            UpdateVoiceMeter();
            if (wav == null)
            {
                Refresh();
                field.ActivateInputField();
                return;
            }

            voiceBusy = true;
            Refresh();
            GyeonuGeminiResponder.Transcribe(this, wav,
                text =>
                {
                    voiceBusy = false;
                    field.text = text;
                    Refresh();
                    // 문서 요구: 떼면 곧바로 전달한다. 받아 적은 글은 대화 기록에 그대로 남아
                    // 무엇으로 전해졌는지 확인할 수 있다.
                    if (owner != null && owner.voiceAutoSend) Ask();
                    else field.ActivateInputField();
                },
                err =>
                {
                    voiceBusy = false;
                    Refresh();
                    DebugToast.Show("받아 적지 못했다 — " + err, 2.5f);
                    field.ActivateInputField();
                });
        }

        void UpdateVoiceMeter()
        {
            if (voiceBar == null) return;
            bool on = voice.Recording;
            voiceBar.gameObject.SetActive(on);
            if (!on) return;
            float w = voiceBar.sizeDelta.x - 8f;
            voiceBarFill.sizeDelta = new Vector2(Mathf.Max(4f, w * voice.Level), voiceBar.sizeDelta.y - 8f);
            // ⚠️ 흐른 시간은 여기서 갱신한다. Refresh는 대사가 바뀔 때만 도는데, 녹음 중에는
            //    아무 대사도 오지 않아 "0.0초"에서 멈춰 있었다(2026-08-25 실측).
            if (voiceText != null)
                voiceText.text = "<color=#AA3728>●</color>  듣는 중  " + voice.ElapsedSeconds.ToString("F1") + "초";
        }

        // ── 갱신 ─────────────────────────────────────────────
        void Refresh()
        {
            if (session == null) return;
            string npcName = session.Profile.displayName;
            string line = session.Busy ? "…" : session.CurrentNpcLine;

            if (nameText != null) nameText.text = npcName;
            if (lineText != null && line != shownLine)
            {
                lineText.text = line;
                shownLine = line;
                lineScroll = 0f;          // 새 대사는 늘 첫 줄부터
                ApplyLineScroll();
            }
            if (bubbleName != null) bubbleName.text = npcName;
            if (bubbleLine != null) bubbleLine.text = line;

            if (voiceText != null)
            {
                if (voice.Recording) voiceText.text = "<color=#AA3728>●</color>  듣는 중  " + voice.ElapsedSeconds.ToString("F1") + "초";
                else if (voiceBusy) voiceText.text = "받아 적는 중…";
                else voiceText.text = "";
            }

            bool idle = !session.Busy && !voice.Recording && !voiceBusy;
            if (askSpot != null) { askSpot.interactable = idle; askLabel.text = idle ? "묻 기" : "…"; }
            if (presentSpot != null) presentSpot.interactable = idle;
        }

        // ⚠️ 점수가 움직였다는 표시를 화면에 그리지 않는다 (2026-08-25, 문서 「9. 설계 원칙」).
        //    전에는 여기서 "마음이 조금 열렸다" 같은 문구를 오른쪽 위에 띄웠다. 그러면
        //    플레이어가 **한 마디마다 표시를 보고 점수를 역산**하게 되고, 그 순간 대화가
        //    추리가 아니라 최적화가 된다. 신뢰도는 견우의 태도와 대사로만 느끼게 둔다.
        //    (등급 자체는 그대로 매겨져 GyeonuCase에 반영된다 — 감추는 것은 표시뿐이다.)

        string HintLine =>
            "Enter — 묻기     <color=#8E2C20>왼쪽 Ctrl — 누르고 말하기</color>     좌클릭 — 누르기     Esc / 우클릭 — 대화 끝내기";

        // ── 행동 ─────────────────────────────────────────────
        void Ask()
        {
            if (session == null || session.Busy || field == null || voice.Recording || voiceBusy) return;
            string text = field.text;
            if (string.IsNullOrWhiteSpace(text)) return;
            field.text = "";
            session.Ask(text);
            field.ActivateInputField();
        }

        /// <summary>증거 제시 — 소지품 판을 그대로 연다.</summary>
        void OpenPresentPanel()
        {
            if (session == null || eye == null) return;
            var input = eye.GetComponent<InventoryInput>();
            if (input == null) { DebugToast.Show("소지품 입력이 없다.", 2f); return; }
            field.DeactivateInputField();
            input.OpenPresent(session.Presentables, OnPresentChosen);
        }

        void OnPresentChosen(InventoryItem item)
        {
            if (item == null || session == null) return;
            var input = eye != null ? eye.GetComponent<InventoryInput>() : null;
            if (input != null) input.ClosePanel();
            if (ClueTable.TryParse(item.Key, out var id)) session.Present(id);
            else DebugToast.Show("이건 내밀 것이 못 된다.", 2f);
            field.ActivateInputField();
        }

        void RequestExit()
        {
            // 포커스 리그가 물러나기를 맡는다 — 카메라 복귀·잠금 해제가 거기 다 있다.
            var rig = eye != null ? eye.GetComponent<DebugFocusRig>() : null;
            if (rig != null) rig.ExitFocus();
        }

        // ── 포인터 ───────────────────────────────────────────
        public void PointAt(Ray ray)
        {
            if (!IsOpen) return;
            Hovered = null;
            var hits = Physics.RaycastAll(ray, 8f, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                var spot = h.collider.GetComponent<DialogueHotspot>();
                if (spot != null && spot.interactable && spot.gameObject.activeInHierarchy) { Hovered = spot; break; }
            }

            // 조준점은 콜라이더가 아니라 판 평면과의 교점으로 잡는다 — 판 밖을 겨눠도
            // 커서가 사라지지 않고 가장자리로 따라간다.
            var plane = new Plane(-transform.forward, transform.position);
            if (plane.Raycast(ray, out float dist))
            {
                var local = transform.InverseTransformPoint(ray.GetPoint(dist));
                float hw = geom.w * 0.5f, hh = geom.h * 0.5f;
                cursor.anchoredPosition = new Vector2(Mathf.Clamp(local.x, -hw, hw), Mathf.Clamp(local.y, -hh, hh));
                cursor.gameObject.SetActive(true);
            }
            else cursor.gameObject.SetActive(false);

            askSpot.SetHovered(askSpot == Hovered);
            presentSpot.SetHovered(presentSpot == Hovered);
            closeSpot.SetHovered(closeSpot == Hovered);
        }

        public void ClickHovered()
        {
            if (!IsOpen || Hovered == null || !Hovered.interactable) return;
            switch (Hovered.kind)
            {
                case DialogueHotspot.Kind.묻기: Ask(); break;
                case DialogueHotspot.Kind.단서열기: OpenPresentPanel(); break;
                case DialogueHotspot.Kind.입력칸: field.ActivateInputField(); break;
                case DialogueHotspot.Kind.끝내기: RequestExit(); break;
            }
        }

        /// <summary>
        /// 휠 — 대사가 틀보다 길 때 굴려 읽는다.
        ///
        /// ⚠️ 포커스 리그는 휠 값을 <b>매 프레임 날것으로</b> 넘긴다(소지품 판처럼 한 칸씩 끊어
        ///    주지 않는다). 그대로 받으면 한 번 굴릴 때 수십 번 불려 글이 끝까지 튄다 —
        ///    소지품 상세에서 겪은 것과 같은 실패다. 여기서 짧은 쉼으로 끊는다.
        /// </summary>
        public void Scroll(float direction)
        {
            if (!IsOpen || lineViewport == null) return;
            if (Time.unscaledTime < scrollReadyAt) return;
            scrollReadyAt = Time.unscaledTime + 0.06f;
            lineScroll += direction > 0f ? -LineHeight() : LineHeight();
            ApplyLineScroll();
        }

        /// <summary>글줄 하나의 높이 — 굴림 한 칸이 딱 한 줄이 되게 실제로 잰다.
        /// 글꼴·글자크기가 바뀌어도 따라온다.</summary>
        float LineHeight()
        {
            var gen = lineText != null ? lineText.cachedTextGenerator : null;
            int n = gen != null ? gen.lineCount : 0;
            if (n > 0) return lineText.preferredHeight / n;
            return lineText != null ? lineText.fontSize * lineText.lineSpacing : 46f;
        }

        /// <summary>굴린 만큼 글을 올리고, 아직 남았으면 알려 준다.</summary>
        void ApplyLineScroll()
        {
            if (lineViewport == null || lineRect == null || lineText == null) return;
            Canvas.ForceUpdateCanvases();
            float overflow = Mathf.Max(0f, lineText.preferredHeight - lineViewport.rect.height);
            lineScroll = Mathf.Clamp(lineScroll, 0f, overflow);
            lineRect.anchoredPosition = new Vector2(lineRect.anchoredPosition.x, lineScroll);
            // 굴릴 것이 있는지 모르면 아무도 굴리지 않는다 — 남았을 때만 화살표를 띄운다.
            if (overflowHint != null)
                overflowHint.gameObject.SetActive(overflow > 1f && lineScroll < overflow - 1f);
        }

        // ─────────────────────────────────────────────────────
        //  판 짓기 — 배치안마다 자리만 다르고 부품은 같다
        // ─────────────────────────────────────────────────────
        void Rebuild(DialogueLayout layout)
        {
            Layout = layout;
            geom = GeomOf(layout);

            // 있던 것을 통째로 비우고 새로 짓는다 (배치안을 바꿔 가며 견주어야 한다)
            for (int i = transform.childCount - 1; i >= 0; i--) DestroyImmediate(transform.GetChild(i).gameObject);
            if (bubbleGo != null) { DestroyImmediate(bubbleGo); bubbleGo = null; }
            nameText = lineText = hintText = voiceText = overflowHint = null;
            bubbleName = bubbleLine = null;
            voiceBar = voiceBarFill = null;
            lineViewport = lineRect = null;
            shownLine = "";
            lineScroll = 0f;

            if (font == null) font = MakeFont();
            var canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                gameObject.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 1f;
            }
            canvas.renderMode = RenderMode.WorldSpace;
            root = (RectTransform)transform;
            root.sizeDelta = new Vector2(geom.w, geom.h);
            root.localScale = Vector3.one * geom.scale;

            // 목재 틀 ▸ 한지 ▸ 안쪽 테선 ▸ 한지 — 소지품 판과 같은 네 겹
            Stretch(MakeImage(root, "틀", skin.Wood_, Color.white), 22f);
            var paper = MakeImage(root, "바탕", skin.Hanji_, Color.white);
            Stretch(paper, 0f);
            var board = paper.gameObject.AddComponent<BoxCollider>();
            board.isTrigger = true;
            board.size = new Vector3(geom.w, geom.h, 2f);
            Stretch(MakeImage(root, "테선", skin.Wood_, InventorySkin.Wood), -12f);
            Stretch(MakeImage(root, "속지", skin.Hanji_, Color.white), -16f);

            switch (layout)
            {
                case DialogueLayout.A_좌측판: BuildA(); break;
                case DialogueLayout.B_하단띠: BuildB(); break;
                default: BuildC(); break;
            }

            // 조준점 — 판 위에 찍히는 커서 (VR에서도 그대로 보인다)
            var dot = MakeImage(root, "조준", skin.Dot_, new Color(1f, 0.95f, 0.8f, 0.95f));
            Place(dot, Vector2.zero, new Vector2(34f, 34f));
            cursor = dot;
            cursor.SetAsLastSibling();

            built = true;
        }

        // ── A — 좌측 아래 판 (1500 × 620) · 확정안 ───────────
        //   세로 620 = 반 310. 위에서부터 쌓은 자리:
        //     이름패 264 / 구분선 228 / 대사틀 −51~207 / 입력줄 −195~−121 / 안내 −289~−263
        //   ⚠️ 속지가 −294~294 이므로 그 밖으로 나가면 글이 틀에 물린다.
        void BuildA()
        {
            float hw = geom.w * 0.5f, hh = geom.h * 0.5f;
            Header(new Vector2(-hw + 215f, hh - 46f), 300f, 54f, 32,
                   new Vector2(hw - 380f, hh - 46f), 560f, 26, hh - 82f, geom.w - 80f);

            // ⚠️ 대사틀 윗변을 구분선(228) 바로 밑에 붙이면, 굴렸을 때 **반쯤 잘린 글줄**이
            //    구분선에 맞닿아 고장난 것처럼 보인다(2026-08-25 실측). 38단위를 띄운다.
            LineViewport(new Vector2(0f, 70f), new Vector2(geom.w - 100f, 240f), 36);

            InputRow(-158f, geom.w - 100f, 74f, 0.60f);
            Footer(22, -hh + 34f, 26f);
        }

        // ── B — 하단 얇은 띠 (2200 × 400) ────────────────────
        void BuildB()
        {
            float hw = geom.w * 0.5f, hh = geom.h * 0.5f;
            Header(new Vector2(-hw + 230f, hh - 54f), 330f, 56f, 34,
                   new Vector2(hw - 470f, hh - 54f), 620f, 30, hh - 88f, geom.w - 70f);

            LineViewport(new Vector2(0f, 30f), new Vector2(geom.w - 90f, 116f), 34);

            // ⚠️ 띠가 얇아 입력줄과 안내줄이 겹치기 쉽다 — 아래 두 값은 함께 계산할 것.
            //    입력 −146~−70, 안내 −187~−161 (판 아래끝 −200, 틀 22 안쪽).
            InputRow(-108f, geom.w - 90f, 76f, 0.60f);
            Footer(20, -geom.h * 0.5f + 26f, 26f);
        }

        // ── C — 말풍선 + 하단 채팅바 (1900 × 260) ────────────
        void BuildC()
        {
            float hw = geom.w * 0.5f, hh = geom.h * 0.5f;

            // 채팅바에는 이름·대사가 없다 — 말풍선이 맡는다. 반응만 짧게 얹는다.

            voiceText = MakeText(root, "녹음", 26, TextAnchor.MiddleRight, InventorySkin.InkSoft);
            Place(voiceText.rectTransform, new Vector2(hw - 330f, hh - 44f), new Vector2(560f, 38f));

            CloseButton(new Vector2(hw - 46f, hh - 44f), 52f, 30);
            VoiceMeter(new Vector2(-hw + 120f, hh - 44f), 140f, 22f);

            InputRow(-4f, geom.w - 90f, 72f, 0.60f);
            Footer(20, -hh + 38f, 26f);

            BuildBubble();
        }

        void BuildBubble()
        {
            bubbleGo = new GameObject("대화_말풍선", typeof(RectTransform));
            bubbleGo.transform.SetParent(transform.parent, false);
            DontDestroyOnLoad(bubbleGo);
            var c = bubbleGo.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            bubbleGo.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 1f;
            bubbleRoot = (RectTransform)bubbleGo.transform;
            bubbleRoot.sizeDelta = new Vector2(BubbleW, BubbleH);
            bubbleRoot.localScale = Vector3.one * BubbleScale;

            Stretch(MakeImage(bubbleRoot, "틀", skin.Wood_, InventorySkin.Wood), 12f);
            Stretch(MakeImage(bubbleRoot, "바탕", skin.Hanji_, Color.white), 0f);

            // 꼬리 — 네모를 45° 돌려 NPC 쪽을 가리키게 한다
            bubbleTail = MakeImage(bubbleRoot, "꼬리", skin.Hanji_, Color.white);
            Place(bubbleTail, new Vector2(BubbleW * 0.5f + 6f, -BubbleH * 0.5f + 56f), new Vector2(84f, 84f));
            bubbleTail.localRotation = Quaternion.Euler(0f, 0f, 45f);

            var plate = MakeImage(bubbleRoot, "이름패", skin.Wood_, InventorySkin.Vermilion);
            Place(plate, new Vector2(-BubbleW * 0.5f + 130f, BubbleH * 0.5f - 52f), new Vector2(200f, 58f));
            bubbleName = MakeText(plate, "이름", 34, TextAnchor.MiddleCenter, InventorySkin.Hanji);
            Stretch(bubbleName.rectTransform, -6f);
            bubbleName.fontStyle = FontStyle.Bold;

            bubbleLine = MakeText(bubbleRoot, "대사", 40, TextAnchor.UpperLeft, InventorySkin.Ink);
            Place(bubbleLine.rectTransform, new Vector2(0f, -30f), new Vector2(BubbleW - 90f, 260f));
            bubbleLine.lineSpacing = 1.28f;
        }

        // ── 공통 부품 ────────────────────────────────────────
        /// <summary>
        /// 대사 자리 — <b>틀에 가두고 넘치면 굴려 읽는다</b> (2026-08-25).
        ///
        /// 판을 낮추면서 대사 자리가 다섯 줄 남짓으로 줄었다. 글자를 더 줄이면 VR에서 읽기 어렵고,
        /// 넘치는 만큼 잘라 버리면 말끝이 사라진다. 그래서 소지품 상세의 설명칸과 같은 방식으로
        /// <see cref="RectMask2D"/> 안에 넣고 휠로 굴린다 — 남은 글이 있을 때만 화살표가 뜬다.
        /// </summary>
        void LineViewport(Vector2 pos, Vector2 size, int fontSize)
        {
            lineViewport = MakeRect(root, "대사틀");
            Place(lineViewport, pos, size);
            lineViewport.gameObject.AddComponent<RectMask2D>();

            lineText = MakeText(lineViewport, "대사", fontSize, TextAnchor.UpperLeft, InventorySkin.Ink);
            lineRect = lineText.rectTransform;
            lineRect.anchorMin = new Vector2(0f, 1f);
            lineRect.anchorMax = new Vector2(1f, 1f);
            lineRect.pivot = new Vector2(0.5f, 1f);
            lineRect.anchoredPosition = Vector2.zero;
            // ⚠️ 가로는 앵커가 늘려 준다. 여기 폭을 넣으면 두 배가 된다(소지품 판에서 실측).
            lineRect.sizeDelta = new Vector2(0f, size.y);
            lineText.verticalOverflow = VerticalWrapMode.Overflow;
            lineText.lineSpacing = 1.28f;

            // ⚠️ 오른쪽 맞춤 글은 **rect의 오른쪽 끝**에 붙는다. 중심을 틀 오른쪽 끝에 두면
            //    글이 판 밖으로 삐져나간다(2026-08-25 실측). 중심을 폭의 절반만큼 당겨 온다.
            const float HintW = 300f;
            overflowHint = MakeText(root, "더있음", 22, TextAnchor.MiddleRight, InventorySkin.Vermilion);
            Place(overflowHint.rectTransform,
                  new Vector2(pos.x + size.x * 0.5f - HintW * 0.5f, pos.y - size.y * 0.5f - 16f),
                  new Vector2(HintW, 26f));
            overflowHint.text = "▼  휠을 굴려 더 보기";
            overflowHint.gameObject.SetActive(false);
        }

        /// <summary>이름패 · 반응 · 녹음 표시 · 닫기 ✕ · 구분선.</summary>
        void Header(Vector2 namePos, float nameW, float nameH, int nameSize,
                    Vector2 notePos, float noteW, int noteSize, float ruleY, float ruleW)
        {
            float hw = geom.w * 0.5f, hh = geom.h * 0.5f;

            var plate = MakeImage(root, "이름패", skin.Wood_, InventorySkin.Vermilion);
            Place(plate, namePos, new Vector2(nameW, nameH));
            nameText = MakeText(plate, "이름", nameSize, TextAnchor.MiddleCenter, InventorySkin.Hanji);
            Stretch(nameText.rectTransform, -6f);
            nameText.fontStyle = FontStyle.Bold;

            VoiceMeter(new Vector2(namePos.x + nameW * 0.5f + 90f, namePos.y), 140f, 22f);

            // ⚠️ 녹음 문구의 폭은 **이름패와 반응 사이에 남는 만큼**으로 잰다. 고정폭으로 두었더니
            //    판이 좁아진 A안에서 반응 문구와 겹쳤다(2026-08-25). 판 치수가 바뀌어도 안 겹친다.
            float voiceLeft = namePos.x + nameW * 0.5f + 170f;
            float voiceRight = notePos.x - noteW * 0.5f - 20f;
            float voiceW = Mathf.Max(120f, voiceRight - voiceLeft);
            voiceText = MakeText(root, "녹음", noteSize, TextAnchor.MiddleLeft, InventorySkin.InkSoft);
            Place(voiceText.rectTransform, new Vector2(voiceLeft + voiceW * 0.5f, namePos.y), new Vector2(voiceW, 38f));

            // notePos·noteW 는 이제 **오른쪽 여백의 경계**로만 쓴다 — 점수 반응 문구를 없앴다.
            // (윗줄의 녹음 문구가 어디까지 늘어날 수 있는지를 이 값이 정한다.)

            var rule = MakeImage(root, "구분선", skin.Wood_, InventorySkin.Wood);
            Place(rule, new Vector2(0f, ruleY), new Vector2(ruleW, 3f));

            // ⚠️ 닫기는 **오른쪽 위**다 (2026-08-25). 창을 닫는 ✕는 어디서나 오른쪽 위에 있고,
            //    왼쪽에 두면 이름패와 붙어 이름의 일부처럼 읽힌다.
            CloseButton(new Vector2(hw - 52f, hh - 52f), 60f, 34);
        }

        void CloseButton(Vector2 pos, float size, int fontSize)
        {
            Text l;
            closeSpot = MakeButton(root, "끝내기", pos, new Vector2(size, size),
                                   DialogueHotspot.Kind.끝내기, InventorySkin.Wood, fontSize, out l);
            l.text = "✕";
        }

        /// <summary>녹음 중 소리 크기 막대 — "듣고 있다"를 눈으로 보여 준다.</summary>
        void VoiceMeter(Vector2 pos, float w, float h)
        {
            voiceBar = MakeImage(root, "소리막대", skin.Wood_, InventorySkin.Wood);
            Place(voiceBar, pos, new Vector2(w, h));
            voiceBarFill = MakeImage(voiceBar, "채움", skin.Hanji_, InventorySkin.Vermilion);
            voiceBarFill.anchorMin = voiceBarFill.anchorMax = new Vector2(0f, 0.5f);
            voiceBarFill.pivot = new Vector2(0f, 0.5f);
            voiceBarFill.anchoredPosition = new Vector2(4f, 0f);
            voiceBarFill.sizeDelta = new Vector2(4f, h - 8f);
            voiceBar.gameObject.SetActive(false);
        }

        /// <summary>글쇠 칸 + [묻기] + [증거 제시] 한 줄. <paramref name="fieldFrac"/> 만큼을 칸이 쓴다.</summary>
        void InputRow(float y, float totalW, float h, float fieldFrac)
        {
            float left = -totalW * 0.5f;
            float fw = totalW * fieldFrac;
            float askW = totalW * 0.13f;
            float presentW = totalW - fw - askW - 40f;

            var box = MakeImage(root, "글쇠칸", skin.Slot_, Color.white);
            Place(box, new Vector2(left + fw * 0.5f, y), new Vector2(fw, h));
            Stretch(MakeImage(box, "칸테", skin.Wood_, InventorySkin.Wood), 4f);
            Stretch(MakeImage(box, "칸속", skin.Slot_, Color.white), 0f);

            int fs = Mathf.RoundToInt(h * 0.42f);
            var txt = MakeText(box, "글", fs, TextAnchor.MiddleLeft, InventorySkin.Ink);
            Place(txt.rectTransform, new Vector2(10f, 0f), new Vector2(fw - 40f, h - 18f));
            txt.supportRichText = false;

            var ph = MakeText(box, "안내글", fs, TextAnchor.MiddleLeft, new Color(0.45f, 0.40f, 0.35f, 0.75f));
            Place(ph.rectTransform, new Vector2(10f, 0f), new Vector2(fw - 40f, h - 18f));
            ph.text = "묻고 싶은 것을 치거나, 왼쪽 Ctrl을 누르고 말하시오…";
            ph.supportRichText = false;

            field = box.gameObject.AddComponent<InputField>();
            field.textComponent = txt;
            field.placeholder = ph;
            field.lineType = InputField.LineType.SingleLine;
            field.characterLimit = 120;
            field.customCaretColor = true;
            field.caretColor = InventorySkin.Ink;
            field.selectionColor = new Color(0.67f, 0.22f, 0.16f, 0.35f);
            field.targetGraphic = box.GetComponent<Image>();
            field.transition = Selectable.Transition.None;
            AddSpot(box, DialogueHotspot.Kind.입력칸, new Vector2(fw, h), null, InventorySkin.Wood);

            Text pl;
            askSpot = MakeButton(root, "묻기", new Vector2(left + fw + 20f + askW * 0.5f, y), new Vector2(askW, h),
                                 DialogueHotspot.Kind.묻기, InventorySkin.Vermilion, Mathf.RoundToInt(h * 0.40f), out pl);
            askLabel = pl; askLabel.text = "묻 기";

            presentSpot = MakeButton(root, "증거제시", new Vector2(left + fw + 40f + askW + presentW * 0.5f, y),
                                     new Vector2(presentW, h), DialogueHotspot.Kind.단서열기,
                                     InventorySkin.Wood, Mathf.RoundToInt(h * 0.38f), out pl);
            pl.text = "증거 제시";
        }

        void Footer(int fontSize, float y, float h)
        {
            hintText = MakeText(root, "안내", fontSize, TextAnchor.MiddleCenter, InventorySkin.InkSoft);
            Place(hintText.rectTransform, new Vector2(0f, y), new Vector2(geom.w - 60f, h));
        }

        DialogueHotspot MakeButton(RectTransform parent, string name, Vector2 pos, Vector2 size,
                                   DialogueHotspot.Kind kind, Color baseColor, int fontSize, out Text label)
        {
            var btn = MakeImage(parent, name, skin.Wood_, baseColor);
            Place(btn, pos, size);
            label = MakeText(btn, "글", fontSize, TextAnchor.MiddleCenter, InventorySkin.Hanji);
            Stretch(label.rectTransform, -6f);
            label.text = name;
            var spot = AddSpot(btn, kind, size, btn.GetComponent<Image>(), baseColor);
            spot.label = label;
            return spot;
        }

        static DialogueHotspot AddSpot(RectTransform target, DialogueHotspot.Kind kind, Vector2 size,
                                       Image frame, Color baseColor)
        {
            var box = target.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(size.x, size.y, 2f);
            box.center = new Vector3(0f, 0f, -6f);   // 바탕보다 앞 — 광선이 먼저 맞는다
            var spot = target.gameObject.AddComponent<DialogueHotspot>();
            spot.kind = kind;
            spot.frame = frame;
            spot.idleColor = baseColor;
            spot.hoverColor = Color.Lerp(baseColor, InventorySkin.Gold, 0.55f);
            return spot;
        }

        /// <summary>글쇠 입력을 받으려면 EventSystem이 하나 있어야 한다. 없으면 만든다(런타임 전용).</summary>
        void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("대화_EventSystem", typeof(EventSystem));
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            DontDestroyOnLoad(go);
            eventSystemMine = true;
        }

        // ── 작은 도구들 (소지품 판과 같은 것들) ──────────────────
        static Font MakeFont()
        {
            var f = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "맑은 고딕", "NanumGothic", "나눔고딕", "Gulim", "굴림", "Batang", "Arial Unicode MS" }, 56);
            return f != null ? f : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        static RectTransform MakeRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static RectTransform MakeImage(Transform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;   // UGUI 이벤트는 안 쓴다 — 판정은 콜라이더 광선이 한다
            return (RectTransform)go.transform;
        }

        Text MakeText(Transform parent, string name, int size, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.supportRichText = true;
            return t;
        }

        static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-inset, -inset);
            rt.offsetMax = new Vector2(inset, inset);
        }

        static void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }
}
