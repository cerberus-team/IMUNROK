using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 낮의 집무실 문 (2026-08-25). 문서 「25. 수령」의
    /// <b>"⚠️ 낮에 집무실 진입 시 제지당하고 쫓겨난다"</b> 한 줄이 이 부품이다.
    ///
    /// ■ 왜 진행 조건(requiredFlags)이 아니라 잠금(locked)인가
    ///   진행 조건은 "무엇이 모자란가"를 알려 주는 장치다. 여기서 모자란 것은 물건이 아니라
    ///   <b>때</b>다 — 낮에는 무엇을 들고 와도 들어갈 수 없고, 밤이 되면 아무것도 없어도 들어간다.
    ///   그래서 <see cref="SceneExit.locked"/> 를 시간대에 따라 켜고 끈다.
    ///   조준했을 때 문구도 '들어가기'가 아니라 '살펴보기'로 바뀌어, 지금은 못 들어간다는 것이
    ///   누르기 전에 드러난다.
    ///
    /// ■ 밤에는 열린다
    ///   수령은 밤이면 거처로 돌아가 관아에서 사라진다(문서 「25. 위치」). 잠입이 가능해지는
    ///   그 시각이 곧 이 문이 열리는 시각이다.
    ///
    /// ■ 낮에 쫓겨나는 것은 <b>연출</b>이다 (2026-09-11)
    ///   예전에는 한 줄 문구였다. 그 한 줄 안에 <b>관노가 하는 말</b>과 <b>플레이어의 독백</b>이
    ///   같이 들어 있어, 누가 하는 말인지 읽어야 알 수 있었다. 둘로 쪼개고 사이에 암전을 넣는다.
    /// <code>
    ///   화면이 어두워진다
    ///   → ① 관노의 말            (guardLine)
    ///   → 화면이 밝아진다 — 관아 진입 스폰(ejectSpawn)에 서 있다
    ///   → ② 플레이어의 독백      (afterLine, 개구멍을 모르면 EntryHint 한 줄이 더 붙는다)
    /// </code>
    ///   암전은 씬 전환이 쓰는 것을 그대로 쓴다(<see cref="SceneTransition.Blink"/>) — 어두워지는
    ///   모양이 게임 안에서 하나여야 한다. 안내 판은 암전보다 <b>위에</b> 그려지므로(ToastPanel)
    ///   캄캄한 화면에 관노의 말만 남는다.
    ///
    /// ■ 초밤은 그냥 한 줄이다
    ///   초밤에 막히는 것은 누가 떠미는 것이 아니라 "아직 이르다"는 형편이다. 연출 없이
    ///   <see cref="eveningMessage"/> 한 줄만 띄운다.
    ///
    /// ■ 다른 길이 있다는 안내 (2026-09-10)
    ///   쫓겨난 뒤에 <see cref="EntryHint"/> 를 덧붙인다 — 개구멍 이야기를 아직 못 들었을 때만.
    ///   아이들이라고는 말하지 않는다. 밤의 외삼문(<see cref="GateDayNight"/>)도 같은 줄을 쓴다.
    /// </summary>
    [RequireComponent(typeof(SceneExit))]
    public class OfficeDayGate : MonoBehaviour
    {
        /// <summary>정문이 막혔을 때 붙는 한 줄. 개구멍을 이미 알면 붙지 않는다.</summary>
        public const string EntryHint =
            "정문으로는 안 되겠다. 마을 사람 가운데 다른 길을 아는 이가 있을지도 모른다.";

        [Header("낮 — 쫓겨나는 연출")]
        [TextArea(2, 4)]
        [Tooltip("① 관노가 하는 말. 캄캄한 화면에 이것만 뜬다")]
        public string guardLine =
            "관노가 앞을 막아선다. 「수령 어른의 집무실이오. 나그네가 들 곳이 아니외다.」";

        [TextArea(2, 4)]
        [Tooltip("② 밝아진 뒤 플레이어가 속으로 하는 말")]
        public string afterLine =
            "떠밀리듯 마당 밖까지 물러났다.";

        [Tooltip("밝아졌을 때 서 있을 자리 — 관아 진입 스폰 마커")]
        public string ejectSpawn = "SpawnPoint_FromVillage";

        [Tooltip("캄캄한 채로 관노의 말을 읽는 시간(초)")]
        public float guardSeconds = 2.6f;

        [Tooltip("밝아지고 이만큼(초) 뒤에 독백이 뜬다")]
        public float afterDelay = 0.35f;

        // 2026-09-10 — 초밤에는 잠입이 아직 이르다. 수령은 거처로 갔지만 관노들이 아직 오간다.
        [Header("초밤 — 한 줄 안내")]
        [TextArea(2, 5)]
        public string eveningMessage =
            "집무실에 아직 불이 밝고 관노들이 마루를 오간다. 관아가 잠들려면 밤이 더 깊어야 한다.";

        [Tooltip("원래 문구 — 늦은 밤에 되돌려 놓는다")]
        public string nightPrompt = "들어가기";

        SceneExit exit;
        TimeOfDay _appliedTime;
        bool _appliedHint;
        bool _first = true;
        bool _ejecting;

        void Awake() { exit = GetComponent<SceneExit>(); }

        void OnEnable() { if (exit != null) exit.lockedHandler = Eject; }
        void OnDisable() { if (exit != null && exit.lockedHandler == (System.Func<bool>)Eject) exit.lockedHandler = null; }

        void Update()
        {
            // 늦은 밤에만 열린다 (2026-09-10). 초밤에는 수령만 없을 뿐 관아가 아직 깨어 있다.
            var time = GyeonuCase.Time;
            bool hint = !GyeonuWorld.Has(GyeonuWorld.F_개구멍이야기);
            if (!_first && time == _appliedTime && hint == _appliedHint) return;
            _first = false;
            _appliedTime = time;
            _appliedHint = hint;

            bool open = time == TimeOfDay.LateNight;
            exit.locked = !open;
            if (open) exit.promptText = nightPrompt;
            // 낮에는 Eject 가 맡으므로 lockedMessage 는 쓰이지 않는다. 그래도 채워 둔다 —
            // 연출이 어떤 이유로 돌지 못했을 때(전환 중 등) 이 줄이 최소한의 설명이 된다.
            else if (time == TimeOfDay.Day) exit.lockedMessage = hint ? guardLine + "\n" + EntryHint : guardLine;
            else exit.lockedMessage = eveningMessage;
        }

        /// <summary>낮에 문을 열려 했을 때. <c>true</c> 면 기본 안내 문구를 띄우지 않는다.</summary>
        bool Eject()
        {
            if (GyeonuCase.Time != TimeOfDay.Day) return false;   // 초밤 — 한 줄 안내로 충분하다
            if (_ejecting || SceneTransition.IsTransitioning) return true;

            _ejecting = true;
            SceneTransition.Blink(
                atBlack: () =>
                {
                    DebugToast.Show(guardLine, guardSeconds + 0.9f);   // 밝아지는 동안 한 박자 더 남는다
                    SceneTransition.MoveToSpawn(ejectSpawn);
                },
                fadeOut: 0.35f, hold: guardSeconds, fadeIn: 0.8f);

            Invoke(nameof(SayAfter), 0.35f + guardSeconds + 0.8f + afterDelay);
            return true;
        }

        void SayAfter()
        {
            _ejecting = false;
            string line = afterLine;
            if (!GyeonuWorld.Has(GyeonuWorld.F_개구멍이야기)) line += "\n" + EntryHint;
            DebugToast.ShowPinned(line);
        }
    }
}
