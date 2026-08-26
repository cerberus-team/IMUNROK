// 1부 도입부. 몇 마디 자유롭게 말하게 하고, 그 말투를 AI 가 읽어 프로필을 만듭니다.
using System.Collections.Generic;
using IMUNROK.Seocheon.AI;
using IMUNROK.Seocheon.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IMUNROK.Seocheon
{
    /// <summary>
    /// 도입부 말투 수집 화면.
    ///
    /// ★스타일을 카드로 고르게 하지 않습니다. 실제로 몇 마디 말하게 하고
    ///   그 말투를 AI 가 읽어 프로필을 만듭니다. 이후 모든 선택지가 그 말투로 나옵니다.
    ///
    /// ★이 구간에서는 단서를 주지 않습니다. 목적은 정보가 아니라 말투입니다.
    ///
    /// ★VR: 이 구간에만 자유 입력이 필요합니다. Quest 가상 키보드를 쓸 수 없으면
    ///   (Meta XR Virtual Keyboard 패키지 미설치) useCardFallbackInVR 로
    ///   ★기존 스타일 카드 4장 화면을 대신 띄웁니다.
    /// </summary>
    public sealed class SeocheonIntroScreen : MonoBehaviour
    {
        [Header("데이터")]
        [SerializeField] private SeocheonIntroPrompts prompts;
        [SerializeField] private SeocheonAiConfig aiConfig;

        [Header("표시")]
        [SerializeField] private TMP_Text speakerLabel;
        [SerializeField] private TMP_Text npcLine;
        [SerializeField] private TMP_Text progressLabel;
        [SerializeField] private TMP_Text statusLabel;

        [Header("입력")]
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private TMP_Text placeholderLabel;
        [SerializeField] private Button sendButton;
        [SerializeField] private Button skipButton;

        [Header("프로필 확인")]
        [Tooltip("만들어진 프로필을 보여 주는 패널")]
        [SerializeField] private GameObject profilePanel;
        [SerializeField] private TMP_Text profileNameLabel;
        [SerializeField] private TMP_Text profileTraitsLabel;
        [SerializeField] private TMP_Text profileSampleLabel;
        [SerializeField] private Button profileConfirmButton;

        [Header("VR 폴백")]
        [Tooltip("자유 입력이 불가능한 빌드에서는 스타일 카드 화면을 대신 띄웁니다")]
        [SerializeField] private bool useCardFallbackInVR = true;
        [SerializeField] private SeocheonStyleScreen cardScreen;

        [Header("동작")]
        [SerializeField] private bool openOnStart = true;

        private readonly List<string> playerLines = new List<string>();
        private SeocheonGeminiResponder responder;
        private int index;
        private bool isOpen;
        private bool playerLocked;
        private bool busy;

        public bool IsOpen { get { return isOpen; } }

        private void Awake()
        {
            responder = new SeocheonGeminiResponder(aiConfig);
            if (sendButton != null) sendButton.onClick.AddListener(Submit);
            if (skipButton != null) skipButton.onClick.AddListener(Skip);
            if (profileConfirmButton != null) profileConfirmButton.onClick.AddListener(Close);
            if (inputField != null) inputField.onSubmit.AddListener(OnFieldSubmit);
            if (profilePanel != null) profilePanel.SetActive(false);

            // ★여기서 자기 자신을 끄면 Start() 가 오지 않습니다(Unity 는 꺼진 오브젝트의 Start 를 미룹니다).
            //   그래서 끄는 판단도 Start() 에서 합니다. Start 는 그 프레임이 그려지기 전에 도므로 화면이 번쩍이지 않습니다.
        }

        private void OnDestroy()
        {
            if (sendButton != null) sendButton.onClick.RemoveListener(Submit);
            if (skipButton != null) skipButton.onClick.RemoveListener(Skip);
            if (profileConfirmButton != null) profileConfirmButton.onClick.RemoveListener(Close);
            if (inputField != null) inputField.onSubmit.RemoveListener(OnFieldSubmit);
        }

        private void Start()
        {
            if (!openOnStart || SeocheonTalkStyleState.HasChosen) { Hide(); return; }

            // 카드 화면은 다른 프리팹에 있어 인스펙터로 이어 둘 수 없습니다(프리팹 간 참조 불가).
            // 씬에 있으면 시작할 때 한 번만 찾아 둡니다(매 프레임 아님).
            if (cardScreen == null)
                cardScreen = FindFirstObjectByType<SeocheonStyleScreen>(FindObjectsInactive.Include);

            // ★자유 입력이 불가능한 환경이면 카드 화면으로 대체합니다.
            if (useCardFallbackInVR && !FreeTypingAvailable())
            {
                Hide();
                if (cardScreen != null) { cardScreen.Open(); return; }
                Debug.LogWarning("[서천도입] 자유 입력 불가 + 카드 화면 미연결 → 기본 프로필을 씁니다.");
                SeocheonTalkStyleState.UseDefault();
                return;
            }
            Open();
        }

        /// <summary>열지 않기로 했을 때 조용히 감춥니다. isOpen 이 아니므로 조작 잠금도 건드리지 않습니다.</summary>
        private void Hide()
        {
            if (isOpen) return;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// ★자유 입력을 받을 수 있는 환경인가.
        ///   에디터·PC 는 물리 키보드가 있으니 true.
        ///   Quest(Android) 는 Meta XR Virtual Keyboard 패키지가 있어야 실용적입니다.
        /// </summary>
        public static bool FreeTypingAvailable()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            return true;
#else
            // OVRVirtualKeyboard 가 프로젝트에 있으면 true 로 바꾸십시오.
            return false;
#endif
        }

        // ─────────────────────────────────────────────
        //  진행
        // ─────────────────────────────────────────────

        public void Open()
        {
            if (isOpen) return;
            if (prompts == null || prompts.prompts == null || prompts.prompts.Length == 0)
            {
                Debug.LogError("[서천도입] 대본이 비어 있습니다 → 기본 프로필을 씁니다.", this);
                SeocheonTalkStyleState.UseDefault();
                Hide();   // ★빈 화면을 띄운 채로 두지 않습니다.
                return;
            }

            gameObject.SetActive(true);
            isOpen = true;
            if (!playerLocked) { PlayerControlLock.Push(); playerLocked = true; }
            if (profilePanel != null) profilePanel.SetActive(false);

            playerLines.Clear();
            index = 0;
            busy = false;
            if (speakerLabel != null) speakerLabel.text = prompts.speakerName;
            if (statusLabel != null) statusLabel.text = "말을 건네 보시오.  (건너뛰어도 되오)";
            ShowCurrent();
        }

        public void Close()
        {
            if (!isOpen) return;
            isOpen = false;
            if (playerLocked) { playerLocked = false; PlayerControlLock.Pop(); }
            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            isOpen = false;
            if (playerLocked) { playerLocked = false; PlayerControlLock.Pop(); }
        }

        private void ShowCurrent()
        {
            if (index >= prompts.prompts.Length) { Finish(); return; }
            IntroPrompt p = prompts.prompts[index];
            if (npcLine != null) npcLine.text = p.npcLine;
            if (placeholderLabel != null) placeholderLabel.text = p.placeholder;
            if (progressLabel != null) progressLabel.text = (index + 1) + " / " + prompts.prompts.Length;
            if (inputField != null)
            {
                inputField.text = string.Empty;
                inputField.ActivateInputField();
            }
        }

        private void OnFieldSubmit(string value) { Submit(); }

        private void Submit()
        {
            if (busy || inputField == null || index >= prompts.prompts.Length) return;
            string text = inputField.text;
            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0) return;

            playerLines.Add(text.Trim());
            inputField.text = string.Empty;

            // 상대는 짧게만 받습니다. ★단서 없음.
            string reaction = prompts.prompts[index].reaction;
            if (npcLine != null && !string.IsNullOrEmpty(reaction)) npcLine.text = reaction;

            index++;
            if (index >= prompts.prompts.Length) Finish();
            else ShowCurrent();
        }

        /// <summary>★건너뛰기 — 기본 프로필로 넘어갑니다.</summary>
        private void Skip()
        {
            if (busy) return;
            Debug.Log("[서천도입] 건너뜀 → 기본 프로필");
            SeocheonTalkStyleState.UseDefault();
            ShowProfilePanel(SeocheonTalkStyleState.Profile, "건너뜀");
        }

        private void Finish()
        {
            if (playerLines.Count == 0) { Skip(); return; }

            busy = true;
            if (statusLabel != null) statusLabel.text = "…그대가 어떤 사람인지 헤아리는 중";
            if (inputField != null) inputField.interactable = false;
            if (sendButton != null) sendButton.interactable = false;
            if (npcLine != null && prompts != null) npcLine.text = prompts.closingLine;

            responder.BuildProfile(this, playerLines, OnProfileBuilt);
        }

        private void OnProfileBuilt(SeocheonStyleProfile profile, string note)
        {
            busy = false;
            if (inputField != null) inputField.interactable = true;
            if (sendButton != null) sendButton.interactable = true;

            SeocheonTalkStyleState.SetProfile(profile);
            ShowProfilePanel(SeocheonTalkStyleState.Profile, note);
        }

        /// <summary>
        /// ★프로필을 보여 줍니다 — 이름·특징·예시 한 줄까지.
        /// ★disguiseFit 은 ★보여 주지 않습니다. 위장이 들켰는지는 인물의 태도로만 알아채야 합니다.
        /// </summary>
        private void ShowProfilePanel(SeocheonStyleProfile p, string note)
        {
            if (profilePanel == null) { Close(); return; }
            profilePanel.SetActive(true);
            if (profileNameLabel != null) profileNameLabel.text = p.styleName;
            if (profileTraitsLabel != null) profileTraitsLabel.text = p.TraitsJoined;
            if (profileSampleLabel != null) profileSampleLabel.text = "“" + p.sampleTone + "”";
            if (statusLabel != null)
                statusLabel.text = p.fromCard ? "기본 말투로 가겠소." : "그대는 이런 사람이오.";
        }
    }
}
