// NPC 에 붙여 ★서천 대화 바(팀 대화창의 사본)를 여는 화자.
using IMUNROK.Seocheon.AI;
using IMUNROK.Seocheon.Player;
using IMUNROK.Ui;
using UnityEngine;

namespace IMUNROK.Seocheon
{
    /// <summary>
    /// 서천 NPC 화자 — 팀 <see cref="IDialogueSpeaker"/> 구현 (2026-08-27).
    ///
    /// ■ 무엇을 하는가
    ///   E 를 누르면 <c>SeocheonDialogueBar.Ensure().Open(this, 서천백엔드)</c> 를 부른다.
    ///   그 다음은 <b>전부 화면이 한다</b> — 판 짓기·PC/VR 전환·굴려 읽기·입력칸·마이크 숨김까지.
    ///
    /// ■ 왜 팀 <see cref="DialogueUI"/> 가 아니라 사본(<see cref="SeocheonDialogueBar"/>)인가
    ///   서천에는 <b>선택지 4개</b>와 <b>어절 지목</b>이 있어야 한다. 둘 다 팀 판에는 없고,
    ///   판 높이가 여백의 <b>합으로 계산</b>돼 있어 밖에서 끼워 넣으면 배치가 어긋난다.
    ///   꾸러미 파일을 고치면 다음 배포본에 덮이므로 <b>사본을 우리 폴더에 두고 거기서만</b> 고친다.
    ///   ★색·글꼴·단추 짓는 함수는 사본도 <b>팀 것을 그대로 부른다</b> — 한 게임으로 보여야 하므로.
    ///
    /// ■ <see cref="FocusInteractable"/> 을 상속하지 않은 까닭
    ///   그쪽은 <b>눈앞으로 당기기·비네트</b>가 딸려 오는 퍼즐용 베이스다. 대화는 그럴 필요가 없고,
    ///   <see cref="IDialogueSpeaker"/> 가 요구하는 것은 <b>세 가지</b>(Layout·VoiceAutoSend·FocusPoint)뿐이라
    ///   평범한 MonoBehaviour 로 충분하다.
    ///
    /// ■ 배치안은 손대지 않는다
    ///   README: <c>IDialogueSpeaker.Layout</c> 은 <b>기본값 하단바_확정 그대로 둘 것</b>. 나머지는 지난 시안이다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SeocheonTeamNpc : MonoBehaviour, IDialogueSpeaker
    {
        [Header("데이터")]
        [SerializeField] private SeocheonNpcData npcData;
        [SerializeField] private SeocheonAiConfig aiConfig;

        [Header("동작")]
        [SerializeField] private bool listenForInteract = true;
        [Tooltip("E 키가 닿는 거리")]
        [SerializeField] private float interactDistance = 3.5f;
        [Tooltip("판이 바라볼 지점의 높이 보정(m)")]
        [SerializeField] private float lookHeight = 1.55f;

        private SeocheonUiBackend backend;
        private bool locked;

        public SeocheonNpcData Data { get { return npcData; } }
        public SeocheonUiBackend Backend { get { return backend; } }

        // ── IDialogueSpeaker ─────────────────────────────
        /// <summary>★기본값 그대로. 팀 문서가 바꾸지 말라고 못박은 자리다.</summary>
        DialogueLayout IDialogueSpeaker.Layout { get { return DialogueLayout.하단바_확정; } }
        bool IDialogueSpeaker.VoiceAutoSend { get { return true; } }
        Vector3 IDialogueSpeaker.FocusPoint { get { return transform.position + Vector3.up * lookHeight; } }

        private void Update()
        {
            if (!listenForInteract) return;
            var ui = SeocheonDialogueBar.Instance;
            if (ui != null && ui.IsOpen) { TrackLock(true); return; }
            TrackLock(false);

            if (!SeocheonInput.InteractPressedThisFrame) return;
            if (!IsPlayerNear()) return;
            Begin();
        }

        /// <summary>대화가 열려 있는 동안에는 걷지 못하게 한다 — 판이 눈앞에 붙어 있기 때문이다.</summary>
        private void TrackLock(bool want)
        {
            if (want == locked) return;
            locked = want;
            if (want) PlayerControlLock.Push(); else PlayerControlLock.Pop();
        }

        private bool IsPlayerNear()
        {
            Camera cam = Camera.main;
            if (cam == null) return false;
            Vector3 target = transform.position + Vector3.up * lookHeight;
            return (cam.transform.position - target).sqrMagnitude <= interactDistance * interactDistance;
        }

        /// <summary>대화를 연다. 바깥에서 직접 불러도 된다(시험용).</summary>
        public void Begin()
        {
            if (npcData == null) { Debug.LogError("[서천] " + name + " 에 NPC 데이터가 없다.", this); return; }
            if (backend == null) backend = new SeocheonUiBackend(npcData, aiConfig, this);
            SeocheonDialogueBar.Ensure().Open(this, backend);
        }

        private void OnDisable() { TrackLock(false); }
        private void OnDestroy() { TrackLock(false); }
    }
}
