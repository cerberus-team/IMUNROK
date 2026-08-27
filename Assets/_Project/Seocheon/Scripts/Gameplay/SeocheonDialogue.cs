// NPC 에 붙여 씬 안에서 AI 대화를 진행하고, 지목된 어절을 수첩에 기록합니다.
using System;
using System.Collections;
using System.Collections.Generic;
using IMUNROK.Common;
using IMUNROK.Seocheon.AI;
using IMUNROK.Seocheon.Player;
using UnityEngine;

namespace IMUNROK.Seocheon
{
    /// <summary>
    /// 서천 씬 안에서 도는 대화 진행기.
    ///
    /// ★공통 InterrogationScene 을 로드하지 않습니다. 씬을 전환하면 Phase 상태가 날아가기 때문입니다.
    /// 대화·AI 호출·어절 수집이 전부 이 씬 안에서 끝납니다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class SeocheonDialogue : MonoBehaviour
    {
        [Header("데이터")]
        [SerializeField] private SeocheonNpcData npcData;
        [SerializeField] private SeocheonAiConfig aiConfig;

        [Header("연결")]
        [SerializeField] private SeocheonDialogueUI dialogueUI;
        [SerializeField] private CollectionMeter collectionMeter;
        [Tooltip("카메라가 바라볼 지점. 비우면 이 오브젝트의 머리 높이를 씁니다")]
        [SerializeField] private Transform lookTarget;

        [Header("동작")]
        [SerializeField] private bool listenForInteract = true;
        [Tooltip("E키가 닿는 거리")]
        [SerializeField] private float interactDistance = 3.5f;
        [Tooltip("카메라 회전 속도(도/초)")]
        [SerializeField] private float turnSpeedDeg = 220f;
        [Tooltip("바라볼 지점의 높이 보정(m)")]
        [SerializeField] private float lookHeight = 1.55f;

        private SeocheonGeminiResponder responder;
        private readonly List<string> transcript = new List<string>();
        private readonly List<string> collectedClueIds = new List<string>();

        private readonly List<string> askLabels = new List<string>();
        private readonly List<string> askTones = new List<string>();
        private string lastAskTone = string.Empty;

        private bool isRunning;
        private bool waitingReply;

        private Transform playerCamera;
        private Transform playerBody;
        private Quaternion savedCamRotation;
        private Quaternion savedBodyRotation;
        private Coroutine turnRoutine;

        public bool IsRunning { get { return isRunning; } }
        public event Action<SeocheonDialogue> DialogueEnded;

        private void Awake()
        {
            responder = new SeocheonGeminiResponder(aiConfig);
        }

        private void Update()
        {
            if (isRunning || !listenForInteract) return;
            if (!SeocheonInput.InteractPressedThisFrame) return;
            if (!IsPlayerNear()) return;
            Begin();
        }

        private bool IsPlayerNear()
        {
            // E키를 누른 프레임에만 도는 탐색입니다(매 프레임 아님).
            Camera cam = Camera.main;
            if (cam == null) return false;
            Vector3 target = LookPoint();
            return (cam.transform.position - target).sqrMagnitude <= interactDistance * interactDistance;
        }

        private Vector3 LookPoint()
        {
            if (lookTarget != null) return lookTarget.position;
            return transform.position + Vector3.up * lookHeight;
        }

        // ─────────────────────────────────────────────
        //  시작 / 종료
        // ─────────────────────────────────────────────

        public void Begin()
        {
            if (isRunning) return;
            if (npcData == null) { Debug.LogError("[서천대화] " + name + " 에 NPC 데이터가 없습니다.", this); return; }
            if (dialogueUI == null) { Debug.LogError("[서천대화] " + name + " 에 대화창이 연결되지 않았습니다.", this); return; }

            isRunning = true;
            transcript.Clear();
            RefreshCollectedClues();

            CachePlayerTransforms();
            PlayerControlLock.Push();
            if (turnRoutine != null) StopCoroutine(turnRoutine);
            turnRoutine = StartCoroutine(TurnTowards(LookPoint()));

            dialogueUI.WordPicked += OnWordPicked;
            dialogueUI.PlayerSubmitted += OnPlayerSubmitted;
            dialogueUI.AskChosen += OnAskChosen;
            dialogueUI.EndRequested += End;
            dialogueUI.Open(npcData.npcName);

            // 첫 대사
            List<WordPickNote.Sentence> opening = new List<WordPickNote.Sentence>();
            WordPickNote.Sentence s = new WordPickNote.Sentence();
            s.text = npcData.openingLine;
            s.options = Array.Empty<WordPickNote.WordOption>();
            opening.Add(s);
            dialogueUI.ShowNpcTurn(opening, npcData.npcName);
            transcript.Add(npcData.npcName + ": " + npcData.openingLine);

            // ★첫 선택지는 NPC 데이터의 고정 질문으로 시작합니다(아직 AI 응답이 없으므로).
            PushAsks(null);

            dialogueUI.SetStatus(responder.HasApiKey
                ? "무엇을 물어보시겠소."
                : "★AI 미연결 — 고정 대사로 진행합니다");
        }

        public void End()
        {
            if (!isRunning) return;
            isRunning = false;
            waitingReply = false;

            dialogueUI.WordPicked -= OnWordPicked;
            dialogueUI.PlayerSubmitted -= OnPlayerSubmitted;
            dialogueUI.AskChosen -= OnAskChosen;
            dialogueUI.EndRequested -= End;
            dialogueUI.Close();

            if (turnRoutine != null) StopCoroutine(turnRoutine);
            turnRoutine = StartCoroutine(RestoreView());

            if (DialogueEnded != null) DialogueEnded(this);
        }

        private void OnDisable()
        {
            if (isRunning) End();
        }

        // ─────────────────────────────────────────────
        //  대화
        // ─────────────────────────────────────────────

        /// <summary>★선택지를 골랐을 때. 자유 입력과 ★같은 경로로 흘러갑니다.</summary>
        private void OnAskChosen(string label, string tone)
        {
            if (!string.IsNullOrEmpty(tone)) lastAskTone = tone;
            OnPlayerSubmitted(label);
        }

        /// <summary>AI 가 준 선택지를 화면에 올립니다. 없으면 고정 질문으로 채웁니다.</summary>
        private void PushAsks(SeocheonReplyResult result)
        {
            askLabels.Clear();
            askTones.Clear();
            if (result != null && result.asks.Count > 0)
            {
                for (int i = 0; i < result.asks.Count; i++)
                { askLabels.Add(result.asks[i].label); askTones.Add(result.asks[i].tone); }
            }
            else if (npcData != null && npcData.fallbackAsks != null)
            {
                for (int i = 0; i < npcData.fallbackAsks.Length && askLabels.Count < 4; i++)
                {
                    if (string.IsNullOrEmpty(npcData.fallbackAsks[i])) continue;
                    askLabels.Add(npcData.fallbackAsks[i]); askTones.Add("probe");
                }
            }
            dialogueUI.SetAsks(askLabels, askTones);
        }

        private void OnPlayerSubmitted(string text)
        {
            if (!isRunning || waitingReply || string.IsNullOrEmpty(text)) return;

            transcript.Add("나: " + text);

            waitingReply = true;
            dialogueUI.SetBusy(true);
            RefreshCollectedClues();

            responder.GetReply(this, npcData, transcript, collectedClueIds, text, OnReply);
        }

        private void OnReply(SeocheonReplyResult result)
        {
            waitingReply = false;
            if (!isRunning) return;                 // 대기 중에 대화를 닫았을 수 있음

            dialogueUI.SetBusy(false);
            if (result == null || result.sentences.Count == 0)
            {
                dialogueUI.SetStatus("★응답 없음");
                return;
            }

            dialogueUI.ShowNpcTurn(result.sentences, npcData.npcName);
            for (int i = 0; i < result.sentences.Count; i++)
                transcript.Add(npcData.npcName + ": " + result.sentences[i].text);

            PushAsks(result);

            // ★폴백이 돌았으면 조용히 넘어가지 않고 화면에도 남깁니다.
            switch (result.source)
            {
                case SeocheonReplySource.Ai:
                    dialogueUI.SetStatus("(" + result.elapsedMs + " ms)");
                    break;
                case SeocheonReplySource.FallbackNoKey:
                    dialogueUI.SetStatus("★AI 미연결 (키 없음) — 고정 대사");
                    break;
                case SeocheonReplySource.FallbackNetwork:
                    dialogueUI.SetStatus("★AI 응답 실패 (" + result.note + ") — 고정 대사");
                    break;
                case SeocheonReplySource.FallbackEmpty:
                    dialogueUI.SetStatus("★AI 빈 응답 — 고정 대사");
                    break;
                case SeocheonReplySource.FallbackParse:
                    // ★원문은 콘솔에만 있습니다. 화면에는 회피 대사가 떴습니다.
                    dialogueUI.SetStatus("★응답 오류 — 다시 물어보시오");
                    break;
            }
        }

        // ─────────────────────────────────────────────
        //  수집
        // ─────────────────────────────────────────────

        /// <summary>
        /// ★어절을 지목했을 때. 유효/오답을 ★여기서 판정하지 않습니다.
        ///
        /// - 수첩에는 어느 쪽이든 ★그 어절이 들어 있던 문장 전체가, ★같은 형식의 key 로 들어갑니다.
        /// - clueId 는 저장소에만 남고 화면에는 나오지 않습니다.
        /// - 성공/실패 피드백(문구·색·소리)을 ★내지 않습니다.
        /// </summary>
        private void OnWordPicked(WordPickNote.WordOption option, string sentence)
        {
            if (option == null) return;

            string text = string.IsNullOrEmpty(sentence) ? option.word : sentence;
            SeocheonClueStore.Add(npcData != null ? npcData.npcName : string.Empty,
                                  text, option.word, option.clueId);

            if (collectionMeter != null) collectionMeter.RecordCollect();

            // ★어느 경우에도 같은 문구입니다.
            dialogueUI.SetStatus("수첩에 적어 두었소.");

            RefreshCollectedClues();
        }

        /// <summary>이 NPC 의 단서 중 이미 가진 것을 추립니다(AI 재제시 방지용. ★내부 전용).</summary>
        private void RefreshCollectedClues()
        {
            collectedClueIds.Clear();
            if (npcData == null || npcData.clues == null) return;

            for (int i = 0; i < npcData.clues.Length; i++)
            {
                SeocheonClue c = npcData.clues[i];
                if (c == null || string.IsNullOrEmpty(c.clueId)) continue;
                if (SeocheonClueStore.HasClue(c.clueId)) collectedClueIds.Add(c.clueId);
            }
        }

        // ─────────────────────────────────────────────
        //  화면 고정
        // ─────────────────────────────────────────────

        private void CachePlayerTransforms()
        {
            Camera cam = Camera.main;
            playerCamera = cam != null ? cam.transform : null;
            playerBody = null;
            if (playerCamera != null)
            {
                CharacterController cc = playerCamera.GetComponentInParent<CharacterController>();
                if (cc != null) playerBody = cc.transform;
            }
            if (playerCamera != null) savedCamRotation = playerCamera.localRotation;
            if (playerBody != null) savedBodyRotation = playerBody.localRotation;
        }

        private IEnumerator TurnTowards(Vector3 point)
        {
            if (playerCamera == null) yield break;

            Vector3 dir = point - playerCamera.position;
            if (dir.sqrMagnitude < 0.0001f) yield break;
            Quaternion target = Quaternion.LookRotation(dir.normalized, Vector3.up);

            while (Quaternion.Angle(playerCamera.rotation, target) > 0.2f)
            {
                playerCamera.rotation = Quaternion.RotateTowards(
                    playerCamera.rotation, target, turnSpeedDeg * Time.deltaTime);
                yield return null;
            }
            playerCamera.rotation = target;
            turnRoutine = null;
        }

        private IEnumerator RestoreView()
        {
            if (playerCamera != null)
            {
                while (Quaternion.Angle(playerCamera.localRotation, savedCamRotation) > 0.2f)
                {
                    playerCamera.localRotation = Quaternion.RotateTowards(
                        playerCamera.localRotation, savedCamRotation, turnSpeedDeg * Time.deltaTime);
                    yield return null;
                }
                playerCamera.localRotation = savedCamRotation;
            }
            if (playerBody != null) playerBody.localRotation = savedBodyRotation;

            PlayerControlLock.Pop();     // ★시점을 되돌린 뒤에 조작을 돌려줍니다
            turnRoutine = null;
        }
    }
}
