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

        [TextArea(2, 5)]
        public string dayMessage =
            "관노가 앞을 막아선다. 「수령 어른의 집무실이오. 나그네가 들 곳이 아니외다.」 떠밀리듯 물러났다.";

        // 2026-09-10 — 초밤에는 잠입이 아직 이르다. 수령은 거처로 갔지만 관노들이 아직 오간다.
        [TextArea(2, 5)]
        public string eveningMessage =
            "집무실에 아직 불이 밝고 관노들이 마루를 오간다. 관아가 잠들려면 밤이 더 깊어야 한다.";

        [Tooltip("원래 문구 — 늦은 밤에 되돌려 놓는다")]
        public string nightPrompt = "들어가기";

        SceneExit exit;
        TimeOfDay _appliedTime;
        bool _appliedHint;
        bool _first = true;

        void Awake() { exit = GetComponent<SceneExit>(); }

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
            else if (time == TimeOfDay.Day) exit.lockedMessage = hint ? dayMessage + "\n" + EntryHint : dayMessage;
            else exit.lockedMessage = eveningMessage;
        }
    }
}
