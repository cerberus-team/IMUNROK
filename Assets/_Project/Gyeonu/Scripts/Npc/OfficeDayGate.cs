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
    /// </summary>
    [RequireComponent(typeof(SceneExit))]
    public class OfficeDayGate : MonoBehaviour
    {
        [TextArea(2, 5)]
        public string dayMessage =
            "관노가 앞을 막아선다. 「수령 어른의 집무실이오. 나그네가 들 곳이 아니외다.」\n" +
            "떠밀리듯 마당으로 물러났다.";

        [Tooltip("원래 문구 — 밤에 되돌려 놓는다")]
        public string nightPrompt = "들어가기";

        SceneExit exit;
        bool _appliedDay;
        bool _first = true;

        void Awake() { exit = GetComponent<SceneExit>(); }

        void Update()
        {
            bool day = !GyeonuCase.Night;
            if (!_first && day == _appliedDay) return;
            _first = false;
            _appliedDay = day;

            exit.locked = day;
            if (day) exit.lockedMessage = dayMessage;
            else exit.promptText = nightPrompt;
        }
    }
}
