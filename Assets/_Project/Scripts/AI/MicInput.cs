using System;
using Oculus.Voice.Dictation;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IMUNROK.Common
{
    /// <summary>
    /// 마이크로 말해서 심문하기 — 눌러서 말하기(push-to-talk).
    ///
    /// 음성 인식은 Meta Voice SDK(Wit.ai)가 한다. 이 프로젝트에는 이미 들어와 있고
    /// 한국어 내장 모델(voiceSDK_ko)이 토큰까지 포함돼 있어 Wit.ai 계정이 필요 없다.
    ///
    /// 왜 Gemini에 음성을 직접 보내지 않는가:
    ///  · 말하는 동안 실시간으로 자막이 찍히는 편이 VR에서 훨씬 자연스럽다("듣고 있다"는 신호).
    ///  · 결과가 텍스트라서 기존 대화 경로(NpcRequest.playerInput)를 그대로 쓴다 —
    ///    INpcResponder·Gemini 쪽은 한 줄도 바뀌지 않는다.
    /// (참고: gemini-3.5-flash-lite 는 오디오 입력도 받는다. Wit.ai 한국어가 사극 어휘에
    ///  약하면 INpcResponder 구현만 갈아끼워 그쪽으로 옮길 수 있다.)
    ///
    /// ── 씬 준비(한 번만) ──
    ///  1) 상단 메뉴 [Meta ▸ Voice SDK ▸ Get Started] 에서 Built-In Models 의 Korean 선택
    ///     → WitConfiguration 에셋이 만들어진다(계정·토큰 입력 불필요).
    ///  2) 빈 GameObject에 AppDictationExperience 컴포넌트를 추가하고 그 설정을 연결.
    ///  3) 이 컴포넌트(MicInput)를 아무 상시 오브젝트에 붙인다. 끝.
    /// 설정이 없으면 아래 Start에서 무엇이 빠졌는지 로그로 알려준다.
    /// </summary>
    public class MicInput : MonoBehaviour
    {
        [Tooltip("비우면 씬에서 AppDictationExperience를 자동으로 찾는다")]
        [SerializeField] private AppDictationExperience _dictation;

        // 견우팀 꾸러미와 같은 글쇠다(2026-08-27). 저쪽 대화창이 왼쪽 Ctrl 로 말하고,
        // 우리는 여태 T 였다 — 판을 똑같이 맞춰 놓고 손만 다른 데를 누르게 둘 수는 없다.
        // Ctrl 이어야 하는 까닭이 하나 더 있다: 글쇠 칸에 한글을 치는 중에도
        // <b>Ctrl 은 글자를 먹지 않는다</b>. T 는 먹는다.
        [Tooltip("이 키를 누르고 있는 동안 듣는다(VR에선 그립). 견우팀 꾸러미와 같은 자리다")]
        [SerializeField] private Key _pushToTalkKey = Key.LeftCtrl;

        [Tooltip("이 시간(초)이 지나면 자동으로 듣기를 끝낸다. 0이면 제한 없음")]
        [SerializeField] private float _maxListenSeconds = 12f;

        public static MicInput Instance { get; private set; }

        /// <summary>지금 듣고 있는가.</summary>
        public bool IsListening { get; private set; }

        /// <summary>말하는 도중의 중간 결과(계속 갱신됨). 자막에 실시간으로 뿌리는 용도.</summary>
        public event Action<string> OnPartial;

        /// <summary>다 말한 뒤의 최종 문장. 이걸 NPC에게 보낸다.</summary>
        public event Action<string> OnFinal;

        private float _listenTimer;
        private bool _wired;

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            Unwire();
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (_dictation == null) _dictation = FindFirstObjectByType<AppDictationExperience>();

            if (_dictation == null)
            {
                Debug.LogWarning("[MicInput] 씬에 AppDictationExperience가 없어 음성 입력이 꺼집니다.\n" +
                                 "  [Meta ▸ Voice SDK ▸ Get Started] 에서 Built-In Models ▸ Korean 을 고른 뒤,\n" +
                                 "  빈 오브젝트에 AppDictationExperience를 붙이고 그 설정을 연결하세요.");
                enabled = false;
                return;
            }
            if (_dictation.Configuration == null)
                Debug.LogWarning("[MicInput] AppDictationExperience에 Wit 설정이 비어 있습니다. " +
                                 "Built-In Models ▸ Korean 설정을 연결하세요.");

            Wire();
        }

        private void Wire()
        {
            if (_wired || _dictation == null) return;
            _dictation.DictationEvents.OnPartialTranscription.AddListener(HandlePartial);
            _dictation.DictationEvents.OnFullTranscription.AddListener(HandleFinal);
            _wired = true;
        }

        private void Unwire()
        {
            if (!_wired || _dictation == null) return;
            _dictation.DictationEvents.OnPartialTranscription.RemoveListener(HandlePartial);
            _dictation.DictationEvents.OnFullTranscription.RemoveListener(HandleFinal);
            _wired = false;
        }

        // ─────────────────────────────────────────────
        //  조작 — 마이크 버튼·컨트롤러가 호출(UnityEvent에 그대로 연결 가능)
        // ─────────────────────────────────────────────

        /// <summary>듣기 시작. 이미 듣는 중이면 아무 일도 하지 않는다.</summary>
        public void StartListening()
        {
            if (IsListening || _dictation == null) return;

            IsListening = true;
            _listenTimer = 0f;
            _dictation.Activate();
            if (_logDevice) Debug.Log("[MicInput] 듣기 시작 — " + CurrentDeviceName());
        }

        [Tooltip("들을 때마다 어느 마이크를 잡았는지 콘솔에 적는다. 잘 되면 꺼도 된다")]
        [SerializeField] private bool _logDevice = true;

        /// <summary>지금 잡고 있는 마이크 이름. 안 잡혔으면 그렇다고 말한다.</summary>
        private string CurrentDeviceName()
        {
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (mb != null && mb.GetType().FullName == "Meta.WitAi.Lib.Mic")
                    return (string)mb.GetType().GetProperty("CurrentDeviceName").GetValue(mb, null);
            return "아직 마이크 부품이 없다";
        }

        /// <summary>듣기 종료 → Wit.ai가 최종 문장을 만들어 OnFinal로 돌려준다.</summary>
        public void StopListening()
        {
            if (!IsListening || _dictation == null) return;
            IsListening = false;
            _dictation.Deactivate();
        }

        /// <summary>한 번 누르면 시작/종료가 번갈아 — VR 버튼 하나로 쓰고 싶을 때.</summary>
        public void Toggle()
        {
            if (IsListening) StopListening();
            else StartListening();
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                var key = kb[_pushToTalkKey];
                if (key.wasPressedThisFrame) StartListening();
                else if (key.wasReleasedThisFrame) StopListening();
            }
#endif
            // 손을 떼는 걸 놓쳐도(컨트롤러 이벤트 유실 등) 영원히 듣고 있지 않게 한다.
            if (IsListening && _maxListenSeconds > 0f)
            {
                _listenTimer += Time.deltaTime;
                if (_listenTimer >= _maxListenSeconds) StopListening();
            }
        }

        // <b>들린 말을 콘솔에도 적는다.</b>
        //
        // 단추가 빨개지는 것만으로는 <b>알아듣고 있는지</b>를 알 수가 없다. 마이크가
        // 안 잡혔는지, 잡혔는데 소리가 안 들어오는지, 들어왔는데 못 알아들었는지가
        // 화면에서는 다 똑같이 "아무 일 없음"으로 보인다. 한 마디라도 돌아오면
        // 여기에 찍히므로, 안 찍히면 <b>들리기 전 단계</b>가 막힌 것이다.
        private void HandlePartial(string text)
        {
            if (_logDevice && !string.IsNullOrWhiteSpace(text)) Debug.Log("[MicInput] …" + text);
            OnPartial?.Invoke(text);
        }

        private void HandleFinal(string text)
        {
            IsListening = false;
            if (_logDevice) Debug.Log("[MicInput] 들은 말 = " +
                (string.IsNullOrWhiteSpace(text) ? "(빔 — 소리가 안 들어왔거나 못 알아들었다)" : text.Trim()));
            if (!string.IsNullOrWhiteSpace(text)) OnFinal?.Invoke(text.Trim());
        }
    }
}
