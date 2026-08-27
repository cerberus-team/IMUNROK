using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 말을 걸 수 있는 NPC (2026-08-25). 퍼즐(혼천의·암문·렌즈·서고)과 <b>같은 포커스 구조</b>다 —
    /// <see cref="FocusInteractable"/> 를 그대로 물려받아, 클릭하면 <see cref="DebugFocusRig"/> 가
    /// 카메라를 대상 앞으로 미끄러뜨리고 걷기·조준·커서를 잠근다. 그래서 대화만을 위한
    /// 입력 코드가 따로 필요 없고, VR 컨트롤러 리그로 갈아끼울 때도 같은 API를 탄다.
    ///
    /// ■ 퍼즐과 다른 점 — "마주 본다"
    ///   퍼즐은 물건을 들여다보는 것이라 지금 보고 있는 방향에서 다가서면 그만이다. 사람은
    ///   그렇지 않다. 여기서는 두 가지를 더 한다.
    ///     ① 카메라 높이를 <b>지금 눈높이 그대로</b> 두고 NPC의 얼굴을 본다 —
    ///        기물 중심(가슴께)을 보면 시선이 아래로 처져 대화가 아니라 검시처럼 보인다.
    ///     ② NPC가 <b>플레이어 쪽으로 몸을 돌린다.</b> 물러나면 원래 방향으로 되돌아간다.
    ///
    /// ■ 드래그·스크롤은 쓰지 않는다
    ///   대화에서 손가락이 하는 일은 가리키기와 누르기뿐이다. <see cref="HandleDrag"/> 는 빈 구현이고,
    ///   <see cref="HandlePoint"/>·<see cref="HandleClick"/> 만 대화창으로 넘긴다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class NpcDialogue : FocusInteractable, IDialogueSpeaker
    {
        [Header("인물")]
        [Tooltip("성격·프롬프트·신뢰도 구간이 담긴 정의. Create ▸ 이문록 ▸ 견우 NPC")]
        public NpcProfile profile;

        [Tooltip("얼굴 대신 볼 지점. 비우면 발밑에서 profile.eyeHeight 만큼 올린 자리")]
        public Transform faceAnchor;

        [Tooltip("자세가 바뀌면 얼굴 높이도 바뀐다 — 음수면 프로필의 값을 쓴다. " +
                 "쓰러져 있던 선아가 몸을 일으키면 SeonaRescue 가 이 값을 갈아 끼운다")]
        public float eyeHeightOverride = -1f;

        [Header("몸 돌리기")]
        [Tooltip("말을 걸면 플레이어 쪽으로 몸을 돌린다")]
        public bool turnToPlayer = true;

        [Tooltip("몸을 돌리는 데 걸리는 시간(초)")]
        public float turnTime = 0.45f;

        [Tooltip("이 자세의 몸이 오브젝트 정면과 어긋나 있을 때의 보정각(도). " +
                 "⚠️ 상인의 SitDrinking 은 몸이 오브젝트 정면의 <b>반대쪽</b>을 본다(2026-08-25 실측) — " +
                 "보정 없이 말을 걸면 등을 돌린 채 대화한다. 그 자리만 180을 준다")]
        public float faceYawOffset = 0f;

        DialogueSession session;
        Quaternion homeRot;          // 대화 전 방향 — 물러나면 여기로 돌아간다
        Quaternion turnTarget;
        float turnT = 1f;
        bool turning;

        public DialogueSession Session => session;

        void Awake()
        {
            homeRot = transform.rotation;
            // 대화는 사람과 마주 서는 일이라 퍼즐보다 가까이 붙지 않는다.
            if (profile != null) focusDistance = profile.talkDistance;

            aimBody = GetComponent<CapsuleCollider>();
            anim = GetComponent<Animator>();
        }

        public override string Prompt => profile != null ? profile.talkVerb : "말 걸기";

        public override Vector3 FocusPoint
        {
            get
            {
                if (faceAnchor != null) return faceAnchor.position;
                float h = eyeHeightOverride >= 0f ? eyeHeightOverride
                        : (profile != null ? profile.eyeHeight : 1.55f);
                return transform.position + Vector3.up * h;
            }
        }

        [Header("대화창 배치안")]
        [Tooltip("확정안 = 화면 아래 가로 바. 나머지는 지난 시안이니 새로 고르지 말 것")]
        public DialogueLayout layout = DialogueLayout.하단바_확정;

        [Tooltip("목소리로 물었을 때, 받아 적은 글을 바로 보낼지. 끄면 입력칸에 올려 두고 Enter를 기다린다")]
        public bool voiceAutoSend = true;

        // ── IDialogueSpeaker ─────────────────────────────────
        //   대화창이 묻는 것을 우리 필드에 이어 준다 (2026-08-26). 화면은 이제 NpcDialogue 를
        //   모르고 이 인터페이스만 안다. FocusPoint 는 이미 이름·형이 맞아 그대로 쓰인다.
        DialogueLayout IDialogueSpeaker.Layout => layout;
        bool IDialogueSpeaker.VoiceAutoSend => voiceAutoSend;

        [Tooltip("얼굴보다 이만큼(m) 아래를 겨눈다. 얼굴을 화면 위쪽으로 올려 대화창 위에 남긴다")]
        public float aimBelowFace = 0.10f;

        /// <summary>
        /// 배치안마다 다른 <b>서는 거리</b>와 <b>화면에서 NPC가 앉을 자리</b>.
        ///   <b>확정안(하단 바)</b> — 판이 화면 아래쪽만 덮으므로 NPC는 <b>한가운데 그대로</b> 두고
        ///     거리도 안 물린다. 아래 <c>default</c> 갈래가 이것이다.
        ///   A — 판이 화면 왼쪽을 넓게 덮으므로 NPC를 오른쪽(+22°)으로 밀고 조금 물러선다.
        ///   B — 판이 아래 1/4만 덮으므로 NPC는 한가운데 그대로.
        ///   C — 말풍선이 NPC 왼쪽에 서야 하므로 NPC를 살짝 오른쪽(+14°)으로 민다.
        /// </summary>
        void LayoutFraming(out float distance, out float yawOffset, out float aimDrop)
        {
            float baseDist = profile != null ? profile.talkDistance : focusDistance;
            switch (layout)
            {
                case DialogueLayout.A_좌측판: distance = baseDist + 0.35f; yawOffset = 22f; aimDrop = aimBelowFace; break;
                case DialogueLayout.C_말풍선: distance = baseDist + 0.10f; yawOffset = 14f; aimDrop = 0.06f; break;
                default: distance = baseDist; yawOffset = 0f; aimDrop = aimBelowFace; break;
            }
        }

        /// <summary>
        /// 마주 서는 자세. 세 가지를 지킨다.
        ///   ① <b>눈높이는 지금 그대로</b> — 카메라가 오르내리면 VR에서 발이 뜬 것처럼 느껴진다.
        ///   ② 얼굴이 아니라 <b>턱 조금 아래</b>를 겨눈다 — 정확히 얼굴을 겨누면 얼굴이 화면
        ///      한가운데에 오고, 화면 아래 절반을 덮는 대화창의 윗변에 턱이 걸린다(2026-08-25 실측).
        ///      <see cref="aimBelowFace"/> 만큼 낮춰 겨누면 얼굴이 시야 +3.5° 로 올라온다.
        ///   ③ 상대의 <b>정면 쪽</b>이 아니라 <b>내가 서 있던 쪽</b>으로 자리를 잡는다 — 말을 걸면
        ///      상대가 이쪽으로 몸을 돌리므로, 등 뒤로 돌아 들어갈 이유가 없다.
        /// </summary>
        public override void GetFocusPose(Vector3 currentEyePos, out Vector3 pos, out Quaternion rot)
        {
            Vector3 face = FocusPoint;
            Vector3 dir = currentEyePos - face;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = -transform.forward;   // 겹쳐 서 있으면 NPC 정면으로
            dir.Normalize();

            LayoutFraming(out float d, out float yawOffset, out float aimDrop);
            pos = face + dir * d;
            pos.y = currentEyePos.y;                 // 눈높이 유지
            rot = Quaternion.LookRotation(face - Vector3.up * aimDrop - pos);
            // 카메라를 왼쪽으로 틀면 상대는 화면 오른쪽에 앉는다 — 판이 덮는 쪽을 비워 준다.
            if (Mathf.Abs(yawOffset) > 0.01f)
                rot = Quaternion.AngleAxis(-yawOffset, Vector3.up) * rot;
        }

        public override void OnFocusChanged(bool focused)
        {
            if (focused) Begin();
            else End();
        }

        void Begin()
        {
            if (profile == null)
            {
                Debug.LogError("[대화] " + name + " 에 NpcProfile이 없다 — 대화를 열 수 없다.");
                return;
            }

            session = new DialogueSession(profile, this);
            session.SecretTold += OnSecretTold;
            session.Thanked += OnThanked;
            DialogueUI.Ensure().Open(this, session);

            BeginTalkMotion();

            if (turnToPlayer)
            {
                var cam = Camera.main != null ? Camera.main.transform : null;
                if (cam != null)
                {
                    Vector3 to = cam.position - transform.position;
                    to.y = 0f;
                    if (to.sqrMagnitude > 0.001f)
                    {
                        homeRot = transform.rotation;
                        turnTarget = Quaternion.LookRotation(to.normalized, Vector3.up)
                                   * Quaternion.Euler(0f, -faceYawOffset, 0f);
                        turnT = 0f;
                        turning = true;
                    }
                }
            }

            Debug.Log("[대화] " + profile.displayName + " — 시작 (대답기: " + session.BackendName
                      + (profile.useTrustBands ? ", 신뢰도 " + GyeonuCase.Trust + " / " + profile.BandLabelFor(GyeonuCase.Trust) : "") + ")");
        }

        void End()
        {
            EndTalkMotion();

            if (session != null)
            {
                Debug.Log("[대화] " + profile.displayName + " — 끝\n" + session.Dump());
                session.Dispose();
                session = null;
            }
            if (DialogueUI.Instance != null) DialogueUI.Instance.Close();

            if (turnToPlayer)
            {
                turnTarget = homeRot;
                turnT = 0f;
                turning = true;
            }
        }

        // ── 모션 (문서 「23. 모션 운용 원칙」) ─────────────────────
        //
        //  ■ 왜 여기서 부르는가
        //    대화의 시작·끝을 아는 곳은 여기뿐이다. 모션 고르기 자체는 NpcActor 한 곳에 모여 있고
        //    여기서는 "말하기 시작했다 / 끝났다 / 비밀을 털어놨다 / 고마워한다"만 알린다.
        //
        //  ■ 대화 전용 모션이 없는 인물 (견우·주모·아이02)
        //    문서가 "대화 중 Idle 유지"라고 못 박은 인물들이다. 이 경우 <b>랜덤만 멈춘다</b> —
        //    말하는 도중에 LookAround 로 두리번거리거나 Jump 로 뛰면 대화가 아니라 딴짓이 된다.

        NpcActor actor;
        public NpcActor Actor => actor != null ? actor : (actor = GetComponent<NpcActor>());

        void BeginTalkMotion()
        {
            var a = Actor;
            if (a == null) return;
            a.PauseRandom(true);
            if (!string.IsNullOrEmpty(profile.talkState))
                a.Play(profile.talkState, NpcActor.Pri.Talk, hold: true);
        }

        void EndTalkMotion()
        {
            var a = Actor;
            if (a == null) return;
            a.Release();
            a.PauseRandom(false);
        }

        /// <summary>비밀을 털어놨다 — 아이02의 Secret 이 여기 붙는다.</summary>
        void OnSecretTold()
        {
            var ev = GetComponent<NpcSecretEvent>();
            if (ev != null) ev.Fire();
        }

        /// <summary>고마움 — 어머니·최초의 직녀의 Thank. 문서가 '필수 반응'이라 못 박았다.</summary>
        void OnThanked()
        {
            var a = Actor;
            if (a != null && a.Has("Thank")) a.Play("Thank", NpcActor.Pri.Story);
        }

        // ── 조준 몸통 (2026-08-27) ───────────────────────────────
        //
        //  ■ 왜 여기인가
        //    <see cref="NpcAimBody"/> 가 재는 캡슐은 <b>말을 걸 수 있게 하는 판정</b>이다.
        //    NpcDialogue 는 11인 스무 자리에 이미 전부 붙어 있고 <c>[RequireComponent(Collider)]</c>
        //    라 캡슐이 있는 것도 보장된다 — 씬을 한 줄도 고치지 않고 전원에게 닿는 자리다.
        //
        //  ■ 자세가 바뀌면 다시 잰다
        //    수령은 순찰 때 <c>SitToStand</c> 로 일어서고 선아는 구출되면 몸을 일으킨다.
        //    앉은 자세에 맞춘 캡슐 그대로 두면 일어선 순간 다시 머리가 밖으로 나온다.
        //    ⚠️ 상태가 바뀐 <b>그 프레임</b>에 재면 안 된다 — 전이 중이라 몸이 두 자세 사이에 있다.
        //       <see cref="RefitDelay"/> 만큼 두고 잰다.

        CapsuleCollider aimBody;
        Animator anim;
        int lastStateHash;                 // 0 = 아직 한 번도 안 쟀다 (첫 Update에서 반드시 잰다)
        float refitAt = -1f;

        const float RefitDelay = 0.35f;

        void Update()
        {
            FitAimBody();

            if (!turning) return;
            turnT += Time.deltaTime / Mathf.Max(0.05f, turnTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, turnTarget, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(turnT)));
            if (turnT >= 1f) { transform.rotation = turnTarget; turning = false; }
        }

        void FitAimBody()
        {
            if (aimBody == null) return;

            if (anim != null && anim.isActiveAndEnabled && anim.runtimeAnimatorController != null)
            {
                int hash = anim.GetCurrentAnimatorStateInfo(0).shortNameHash;
                if (hash != lastStateHash) { lastStateHash = hash; refitAt = Time.time + RefitDelay; }
            }
            else if (lastStateHash == 0) { lastStateHash = -1; refitAt = Time.time + RefitDelay; }

            if (refitAt < 0f || Time.time < refitAt) return;
            refitAt = -1f;
            NpcAimBody.Fit(gameObject, aimBody);
        }

        // ── 포커스 리그가 넘겨 주는 것 ────────────────────────────
        public override void HandleDrag(Vector2 delta) { }

        public override void HandlePoint(Ray ray)
        {
            if (DialogueUI.Instance != null) DialogueUI.Instance.PointAt(ray);
        }

        public override void HandleClick(Ray ray)
        {
            if (DialogueUI.Instance != null) DialogueUI.Instance.ClickHovered();
        }

        public override void HandleScroll(float direction)
        {
            if (DialogueUI.Instance != null) DialogueUI.Instance.Scroll(direction);
        }

        /// <summary>
        /// 단서 목록이 떠 있는 동안에는 Esc가 <b>목록만</b> 닫아야 한다.
        /// 여기서 false를 주면 포커스 리그가 그 프레임의 Esc를 흘려보내고,
        /// 대화창이 <c>LateUpdate</c> 에서 목록을 닫는다 (같은 프레임에 둘 다 먹지 않게).
        /// </summary>
        public override bool CanExitFocus =>
            DialogueUI.Instance == null || !DialogueUI.Instance.IsPresenting;

        /// <summary>포커스 리그의 IMGUI 안내는 쓰지 않는다 — 대화창이 판 위에 직접 적는다.
        /// IMGUI는 VR HMD에 보이지 않는데, 안내가 두 벌로 갈라지면 나중에 한쪽만 고치게 된다.</summary>
        public override string FocusHint => "";

        public override string FocusStatus => null;
    }
}
