using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>마패(馬牌)</b> — 1막과 2막을 잇는 <b>단 하나의 물건</b>.
    ///
    /// 다른 도구는 무엇을 <b>알아내는</b> 데 쓴다. 등불은 비추고 돋보기는 키우고 유척은
    /// 잰다. 마패만은 아무것도 알아내지 못한다 — 마패가 하는 일은 <b>내가 누구인지를
    /// 밝히는 것</b> 하나뿐이고, 그 한 번으로 잠행이 끝난다.
    ///
    /// <b>그래서 도구가 아니라 문(門)이다.</b>
    ///
    /// ⚠️ <b>이것이 막는 것은 출도뿐이다.</b> 등불도 돋보기도 유척도 처음부터 언제든 쓴다 —
    /// 조사를 막는 자물쇠가 아니다. 그런데 「아직 이르다, 품에 든 것을 꺼내기에는」이라고만
    /// 적어 두었더니 <b>도구가 잠긴 줄로 읽혔다</b>. 말이 무엇을 막는지 밝히지 않으면,
    /// 사람은 자기가 지금 못 하는 일 가운데 <b>가장 답답한 것</b>을 그 말에 갖다 붙인다.
    ///
    /// <b>왜 도구벨트에서 뺐나</b>: 한동안 마패가 벨트 네 번째 칸에 걸려 있었다.
    /// 그러면 두 가지가 한꺼번에 망가진다.
    ///
    /// 하나, 어사가 제 표식을 <b>허리에 차고</b> 남의 집 마당을 걸어 들어가는 꼴이 된다.
    /// 조선의 어사는 마패와 유척을 봉서와 함께 <b>품에 넣고</b> 다녔고 출도 전에는
    /// 아무에게도 보이지 않았다. 그것이 잠행이다.
    ///
    /// 둘, 마패가 등불·돋보기와 <b>같은 급의 물건</b>이 된다. Q 를 몇 번 돌리면 나오는
    /// 것은 문이 아니라 도구다.
    ///
    /// 그래서 이것은 끝까지 <b>도구 목록에 없다</b>. 있다는 것은 아는데 꺼낼 수가 없다 —
    /// 그 답답함이 1막 내내 깔려 있다가 출도에서 한 번에 풀린다.
    ///
    /// <b>맨손일 때만 나온다.</b> F 는 「들어 올린다」는 하나의 손짓이라 돋보기·등불과
    /// 나눠 쓰는데, 손에 무언가 들려 있으면 그 F 는 <b>그 도구의 것</b>이다.
    /// 마패는 품에서 나오는 것이므로 손이 비어 있어야 한다.
    ///
    /// <b>꺼내는 느낌</b>: 톡 눌러 되돌릴 수 없는 일이 벌어지면 안 되므로 <b>꾹</b>
    /// 눌러야 한다. 누르고 있는 동안 패가 품에서 <b>천천히 올라온다</b>. 손을 떼면
    /// 도로 들어간다 — 그 되돌아가는 동작이 있어야 "아직 안 늦었다"가 몸으로 읽힌다.
    ///
    /// <b>낼 것이 없으면 안 나간다</b>: 증거 없이 출도한 어사는 무고한 이를 잡아들인
    /// 것이 된다. 아직 못 챙긴 것이 있으면 패는 <b>품에서 나오지도 않고</b>, 무엇이
    /// 모자란지만 말해 준다.
    ///
    /// 붙이는 곳: 카메라 밑의 마패 자리(<see cref="_model"/> 을 자식으로 둔 빈 오브젝트).
    /// </summary>
    public class RoyalWarrant : MonoBehaviour
    {
        /// <summary>
        /// <b>2막에서는 이 패가 도구벨트에 오른다.</b> 출도가 이미 끝났으므로 품에 감출
        /// 까닭이 없고, 서리 앞에 내보이는 물건이 되어 있다.
        ///
        /// 그런데 벨트에 오르는 순간 <b>같은 모형을 두 부품이 다툰다</b> —
        /// <see cref="HeldToolModel"/> 이 켜 놓으면 이쪽이 그 다음 칸에 도로 끈다.
        /// 그래서 벨트에서 마패를 골라도 손에 아무것도 안 들렸다. 오류도 안 났다.
        ///
        /// 벨트가 들고 있는 동안에는 <b>손을 뗀다</b>. 품에서 꺼내는 일(1막의 출도)은
        /// 벨트에 마패가 없을 때의 일이다.
        /// </summary>
        private static bool OnTheBelt { get { return ToolbeltHud.SelectedToolId == "mapae"; } }

        [Header("무엇을 꺼내나")]
        [Tooltip("품에서 꺼낼 패의 모형. 평소에는 꺼져 있다")]
        [SerializeField] private GameObject _model;
        [Tooltip("다 꺼냈을 때 모형의 기울기(도). 새겨진 말이 이쪽을 보게 돌려 둔 값")]
        [SerializeField] private Vector3 _faceEuler = new Vector3(90f, 0f, 0f);

        [Header("품속 — 아직 안 꺼낸 자리")]
        [Tooltip("품에 있을 때 모형이 놓이는 자리(다 꺼낸 자리에서 얼마나 떨어져 있나, m). " +
                 "아래로 내려가 있어야 <b>품에서 올라오는</b> 것으로 보인다")]
        [SerializeField] private Vector3 _tuckedOffset = new Vector3(-0.03f, -0.30f, -0.06f);
        [Tooltip("품에 있을 때 더 얹히는 기울기(도). 눕혀져 있다가 서면서 낯짝이 드러난다")]
        [SerializeField] private Vector3 _tuckedTilt = new Vector3(-62f, 0f, 34f);

        [Header("꺼내기")]
#if ENABLE_INPUT_SYSTEM
        [Tooltip("이 키를 <b>꾹 눌러</b> 꺼낸다")]
        [SerializeField] private UnityEngine.InputSystem.Key _raiseKey = UnityEngine.InputSystem.Key.F;
#endif
        [Tooltip("이만큼(초) 잡고 있어야 다 꺼낸 것으로 친다. 팔을 품에 넣었다 빼는 시간이다")]
        [SerializeField] private float _holdSeconds = 1.6f;
        [Tooltip("손을 떼면 이 배로 빨리 도로 들어간다. 넣는 것은 꺼내는 것보다 급하다")]
        [Range(1f, 5f)] [SerializeField] private float _tuckBackFaster = 2.2f;

        [Header("낼 것이 있어야 한다")]
        [Tooltip("이 사건. 필수 단서를 다 챙겼는지 여기에 물어본다")]
        [SerializeField] private CaseId _case = CaseId.Case1_Onggojip;
        [Tooltip("이 단서들을 다 가져야 출도할 수 있다. <b>비우면 언제든 나간다</b> — " +
                 "비워 두면 잠행이 시작하자마자 끝날 수 있으니 반드시 채운다")]
        [SerializeField] private string[] _required;

        [Header("말")]
        [SerializeField] private string _speaker = "";
        [TextArea(2, 3)]
        [Tooltip("낼 것이 모자랄 때 눌렀을 때. {0} 자리에 몇 개 모자란지가 들어간다. " +
                 "<b>무엇을 막는 말인지가 드러나야 한다</b> — 「품에 든 것을 꺼내기에는」이라고만 " +
                 "적어 두었더니 도구를 못 쓴다는 말로 읽혔다. 도구는 언제든 쓸 수 있고, " +
                 "막히는 것은 출도 하나뿐이다")]
        [SerializeField] private string _notYetLine = "아직 이르다 — *출도(出道)*하려면 밝혀 둘 것이 *{0}가지* 남았다.";
        [TextArea(2, 3)]
        [Tooltip("다 채운 <b>그 순간</b> 한 번. 이것이 곧 '이제 나가도 된다'는 신호다")]
        [SerializeField] private string _readyLine = "품 속의 것이 *무거워진다*. ({0} 꾹 눌러 내보이기)";
        [TextArea(2, 3)]
        [Tooltip("꺼내다가 손을 뗐을 때 한 번. 되돌릴 수 있다는 것을 알려 준다")]
        [SerializeField] private string _tuckLine = "패를 도로 품에 넣는다.";
        [TextArea(2, 3)]
        [SerializeField] private string _cryLine = "암행어사 *출도(出道)*야!";

        [Header("내보인 뒤")]
        [Tooltip("외치고 나서 이만큼(초) 뒤에 넘어간다")]
        [SerializeField] private float _beforeLeave = 2.2f;
        [Tooltip("넘어갈 씬. 비우면 씬은 그대로 두고 이벤트만 부른다")]
        [SerializeField] private string _nextScene = "";
        [Tooltip("내보인 순간 한 번. 씬을 안 넘기고 같은 씬에서 이어갈 때 쓴다")]
        [SerializeField] private UnityEvent _onRevealed;

        /// <summary>낼 것을 다 챙겼나.</summary>
        public bool Ready => Missing() == 0;

        /// <summary>지금 패가 얼마나 나와 있나(0 = 품속, 1 = 다 꺼냄).</summary>
        public float Drawn { get; private set; }

        private bool _done;
        private bool _toldReady;
        private bool _wasDrawing;
        private Vector3 _outPos;

        /// <summary>아직 모자란 필수 단서 수.</summary>
        public int Missing()
        {
            if (_required == null || _required.Length == 0) return 0;
            var j = Journal.Instance;
            if (j == null) return _required.Length;
            int n = 0;
            foreach (var k in _required)
                if (!string.IsNullOrEmpty(k) && !j.HasClue(_case, k)) n++;
            return n;
        }

        private void Awake()
        {
            // 다 꺼냈을 때의 자리는 <b>공통</b>에서 받는다(HeldRig). 도구벨트에는 없지만
            // 손에 드는 자리는 다른 도구와 한 군데에서 정해져야 몸이 어긋나지 않는다.
            HeldRig.Apply(transform, "mapae", _model != null ? _model.transform : null);
            _outPos = _model != null ? _model.transform.localPosition : Vector3.zero;
            Place(0f);
            if (_model != null) _model.SetActive(false);
        }

        private void Update()
        {
            if (_done) return;

            // 벨트가 들고 있으면 <b>다 꺼낸 자리에 그대로 둔다</b>.
            // 그냥 손만 떼면 모형이 품속 자리(-30cm)에 남아 눈 밑으로 내려가 버린다 —
            // 켜지긴 했는데 화면 밖에 있어, 골라도 안 보이는 것은 매한가지가 된다.
            if (OnTheBelt)
            {
                if (_model != null)
                {
                    if (!_model.activeSelf) _model.SetActive(true);
                    Place(1f);
                }
                return;
            }

            bool ready = Missing() == 0;

            // 다 채운 <b>그 순간</b> 한 번만 이른다. 이것이 유일한 신호다 —
            // 벨트에 칸이 늘지도 않고 화면에 아이콘이 뜨지도 않으므로.
            if (ready && !_toldReady)
            {
                _toldReady = true;
                Say(string.Format(_readyLine, KeyName()));
            }

            // <b>손이 비어 있어야 듣는다.</b>
            //
            // F 는 세 곳이 함께 쓴다 — 돋보기를 눈에 대고(MagnifierLens), 손에 든 것을
            // 들어 올리고(ToolRaise), 마패를 품에서 꺼낸다. 앞의 둘은 <b>손에 무언가
            // 들려 있을 때</b>의 손짓이고 마패는 <b>품</b>에서 나오는 것이므로, 셋이 한
            // 키를 나눠 쓰는 것 자체는 옳다 — 「들어 올린다」는 하나의 손짓이다.
            //
            // 다만 마패가 <b>남의 차례에 말을 얹고 있었다</b>. 돋보기를 눈에 대려고
            // F 를 누르면 마패도 그 F 를 듣고 「아직 이르다」를 뱉었다. 도구를 쓰는데
            // 난데없이 출도 이야기가 뜨니, 도구가 잠긴 줄로 읽힌다.
            //
            // 그러니 <b>맨손일 때만</b> 듣는다. 어사가 돋보기를 눈에 대고 있는 채로
            // 품에서 마패가 올라오는 그림도 말이 안 된다.
            bool emptyHanded = string.IsNullOrEmpty(ToolbeltHud.SelectedToolId);

#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var key = emptyHanded && kb != null ? kb[_raiseKey] : null;
            bool pressing = key != null && key.isPressed;
            bool tapped = key != null && key.wasPressedThisFrame;
#else
            bool pressing = false, tapped = false;
#endif

            if (!ready)
            {
                // <b>품에서 나오지도 않는다.</b> 다만 왜 안 나오는지는 말해 준다 —
                // 아무 일도 안 일어나면 고장인지 아직인지 알 수가 없다.
                if (tapped) Say(string.Format(_notYetLine, Missing()));
                Move(false);
                return;
            }

            Move(pressing);
            if (Drawn >= 1f) StartCoroutine(Cry());
        }

        /// <summary>꺼내거나 도로 넣는다. 누르는 동안 올라오고, 떼면 내려간다.</summary>
        private void Move(bool pressing)
        {
            float span = Mathf.Max(0.05f, _holdSeconds);
            float step = Time.deltaTime / span * (pressing ? 1f : -_tuckBackFaster);
            float before = Drawn;
            Drawn = Mathf.Clamp01(Drawn + step);

            if (_model != null && _model.activeSelf != (Drawn > 0.001f))
                _model.SetActive(Drawn > 0.001f);
            Place(Drawn);

            // 꺼내다 만 것을 한 번 알려 준다 — 되돌릴 수 있다는 것이 손끝으로 읽혀야 한다
            bool drawing = pressing && Drawn > 0.02f;
            if (_wasDrawing && !drawing && before > 0.25f) Say(_tuckLine);
            _wasDrawing = drawing;

            if (drawing) Say("패가 품에서 올라온다… " + Mathf.RoundToInt(Drawn * 100f) + "%");
        }

        /// <summary>얼마나 나왔는지에 따라 모형을 앉힌다.</summary>
        private void Place(float k)
        {
            if (_model == null) return;
            float t = Mathf.SmoothStep(0f, 1f, k);
            _model.transform.localPosition = _outPos + _tuckedOffset * (1f - t);
            _model.transform.localRotation =
                Quaternion.Euler(_faceEuler) * Quaternion.Euler(_tuckedTilt * (1f - t));
        }

        /// <summary>
        /// 내보인다 — <b>여기부터 되돌릴 수 없다</b>.
        ///
        /// 외치고, 잠깐 두고, 넘어간다. 곧바로 씬을 갈면 제가 무슨 짓을 했는지 볼 새가 없다.
        /// </summary>
        private IEnumerator Cry()
        {
            _done = true;
            Drawn = 1f;
            Place(1f);
            Say(_cryLine);

            // 이제 소리를 조심할 일이 없다. 어사가 왔는데 발소리를 죽일 까닭이 없다.
            NoiseMeter.Disarm();

            var gs = GameState.Instance;
            if (gs != null) gs.RevealIdentity(true);   // 이제 '나그네'가 아니라 '어사'다

            _onRevealed?.Invoke();
            yield return new WaitForSeconds(_beforeLeave);

            if (!string.IsNullOrEmpty(_nextScene) &&
                Application.CanStreamedLevelBeLoaded(_nextScene))
            {
                ScreenFade.Blink(0.6f, 0.9f,
                    () => UnityEngine.SceneManagement.SceneManager.LoadScene(_nextScene));
            }
            else
            {
                Debug.LogWarning("[마패] 다음 씬 '" + _nextScene + "' 이 비었거나 빌드 목록에 없다 — 씬은 그대로 두고 이벤트만 불렀다.");
            }
        }

        private string _lastSaid;
        private void Say(string line)
        {
            if (string.IsNullOrEmpty(line) || line == _lastSaid) return;
            _lastSaid = line;
            SubtitleView.Show(_speaker, line, "", Ready && !_done);
        }

        private string KeyName()
        {
#if ENABLE_INPUT_SYSTEM
            return _raiseKey.ToString();
#else
            return "F";
#endif
        }

        private void OnDisable() { _lastSaid = null; }
    }
}
