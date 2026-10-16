using UnityEngine;
using UnityEngine.Events;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>유척(鍮尺)</b> — 놋쇠 자. 대면 <b>치수가 나온다</b>.
    ///
    /// <b>왜 이것이 어사의 물건인가</b>: 사람의 말은 흔들린다. 甲은 형제가 없다 하고,
    /// 마름은 취해서 말을 바꾸고, 하인은 호칭을 얼버무린다. 그러나 <b>치수는 안 흔들린다</b>.
    /// 관아 호적대장에 적힌 "왼팔 안쪽 데인 자국 <b>두 치 남짓</b>"은 스무 해 전에 적힌
    /// 숫자이고, 지금 저 팔에 있는 흉터도 숫자다. 둘을 견주는 데는 말이 필요 없다.
    ///
    /// 조선의 어사가 유척을 지닌 본뜻도 같다 — 고을 수령이 말과 되를 속이지 않는지
    /// <b>재어서</b> 밝히라는 것이다. 따져 묻는 것이 아니라 대어 보는 것이다.
    ///
    /// <b>쓰는 법</b>: 손에 들고 <see cref="Measurable"/> 이 달린 것을 겨눈다.
    /// 재는 데 <see cref="_measureSeconds"/> 초가 든다 — 자를 대고 눈금을 읽는 시간이다.
    /// 다 재면 치수가 뜨고, 견줄 것이 있으면 <b>맞는지 어긋나는지</b>까지 말해 준다.
    ///
    /// 붙이는 곳: 아무 데나 하나.
    /// </summary>
    public class BrassRuler : MonoBehaviour
    {
        [Header("무엇을 들었을 때")]
        [SerializeField] private string _toolId = "yucheok";

        [Header("재기")]
        [Tooltip("이 거리(m) 안의 것만 잰다. 자를 대야 하므로 팔이 닿아야 한다")]
        [SerializeField] private float _reach = 2.2f;
        [Tooltip("눈금을 읽는 데 드는 시간(초). 대자마자 나오면 잰 것이 아니라 본 것이다")]
        [SerializeField] private float _measureSeconds = 1.8f;

        [Header("말")]
        [SerializeField] private string _speaker = "";
        [TextArea(2, 3)]
        [Tooltip("들었는데 잴 것을 안 겨눴을 때")]
        [SerializeField] private string _idleLine = "놋쇠 자다. 잴 것에 대어 본다.";

        private float _dwell;
        private Measurable _at;

        private void Update()
        {
            if (ToolbeltHud.SelectedToolId != _toolId) { _at = null; _dwell = 0f; return; }

            // 판(물증 보는 자리)이 떠 있으면 거기서 잰다(ArmMeasure). 판 뒤의 세상을 겨누면
            // 같은 팔을 두 군데서 재는 말이 겹친다.
            if (DocumentView.IsOpen) { _at = null; _dwell = 0f; return; }
            var cam = Camera.main;
            if (cam == null) return;

            Measurable hit = null;
            if (Physics.Raycast(cam.transform.position, cam.transform.forward, out var info, _reach))
                hit = info.collider.GetComponentInParent<Measurable>();

            if (hit != _at) { _at = hit; _dwell = 0f; }

            // <b>마주 선 사람의 말을 덮지 않는다.</b> 관아에서는 심문 중에도 유척이 손에
            // 남는데(드러난 팔을 재야 하므로), 그동안 겨눈 것이 없으면 이 자가 매 프레임
            // 「놋쇠 자다. 잴 것에 대어 본다」로 인물의 대사를 지웠다. 자막은 한 사람 몫이다.
            bool talking = InterrogationController.AnyOpen;

            // <b>겨눈 것이 없으면 아무 말도 안 한다.</b>
            //
            // 여태는 빈 데를 보고 있어도 「놋쇠 자다. 잴 것에 대어 본다」를 띄웠다.
            // 자를 손에 든 내내 자막판이 서 있는 셈이라, 정작 잴 것을 만났을 때
            // 뜨는 말과 구별이 안 되고 화면 아래가 늘 가려졌다. 손에 무엇을 들었는지는
            // 도구띠가 이미 말하고 있다 — 자가 제 이름을 다시 댈 까닭이 없다.
            if (_at == null) { _last = null; return; }
            if (_at.Measured) { if (!talking) Say(_at.ResultLine(), true); return; }

            _dwell += Time.deltaTime;
            if (_dwell < _measureSeconds)
            {
                Say($"{_at.What} 을(를) 재는 중… {Mathf.RoundToInt(_dwell / _measureSeconds * 100f)}%", false);
                return;
            }
            _at.Measure();
            // 재는 동안은 눈금이 차오르는 것을 보여야 하지만, 잰 <b>결과</b>는 다르다.
            // 마주 선 자리에서는 그 사람이 반응하는 것이 결과다(Measurable 의 잰 뒤 자리).
            // 여기서 한 줄을 더 띄우면 그 반응을 덮는다.
            if (!talking) Say(_at.ResultLine(), true);
            else _last = null;
        }

        private string _last;
        private void Say(string line, bool key)
        {
            if (line == _last) return;
            _last = line;
            SubtitleView.Show(_speaker, line, "", key);
        }

        private void OnDisable() { _last = null; }
    }

}
