using System.Collections;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 문갑에 놓인 도구 하나 — <b>집어 가는 것이 아니라 익히는 것</b>이다.
    ///
    /// 왜 이렇게 하나: 도구를 그냥 집게 하면 벨트에 이름 하나가 늘 뿐이고, 무엇에 쓰는
    /// 물건인지는 사건 한복판에서 헤매며 알아내야 한다. 조사청은 그걸 배우라고 있는 방이다.
    /// 그래서 누르면 물건이 눈앞으로 떠오르고, 쓰는 법을 한 마디씩 짚어 준 다음,
    /// 마지막에 손에 쥐여 준다 — 읽고 끝나는 안내가 아니라 그 자리에서 한 번 겪는 것.
    ///
    /// 흐름:
    ///   놓임 → (누름) 떠오름 → 한 마디씩 → 마지막에 벨트로 들어가고 손에 잡힘 → 제자리로
    /// 자막의 <b>닫기</b>를 누르면 도중에 그만두고 도구는 문갑으로 돌아간다.
    /// 그만둔 도구는 다시 눌러 처음부터 익힐 수 있다.
    ///
    /// 붙이는 법: 문갑 위에 놓은 도구 모델에 콜라이더와 함께 붙이고, 도구 정의와
    /// 익힘 글줄을 채운다. 자리는 켜질 때의 자리를 제집으로 삼는다.
    /// </summary>
    public class ToolTutorial : MonoBehaviour, ISelectable
    {
        private enum Phase { 놓임, 떠오르는중, 익히는중, 내려가는중 }

        [Tooltip("다 익히면 벨트에 들어갈 도구")]
        [SerializeField] private ToolDef _tool;

        [Tooltip("한 마디씩 짚어 줄 글. 마지막 줄을 넘기면 손에 쥐어 준다")]
        [TextArea(2, 4)]
        [SerializeField] private string[] _steps;

        [Tooltip("다 익힌 뒤 할 말. {0} 자리에 도구 이름이 들어간다")]
        [SerializeField] private string _endWord = "{0}을 손에 익혔다. 이제 언제든 꺼내 쓸 수 있다.";

        [Header("해 보기 — 읽고 끝나지 않게")]
        [Tooltip("과제를 낼 때 손에 쥐여 줄 예시 증거. 비워 두면 말만 하고 만다.\n" +
                 "'저기 문갑 위의 무엇을 가져다 해 보시오' 는 심부름이지 배움이 아니다 — " +
                 "종이는 이쪽에서 쥐여 주고, 도구 쓰는 일만 하게 한다")]
        [SerializeField] private InspectableNote _example;
        [Tooltip("말이 끝나면 손에 쥐여 주고 이 과제를 낸다. 실제로 해내야 다 익힌 것이 된다.\n" +
                 "비워 두면 예전처럼 말만 하고 끝난다")]
        [TextArea(2, 3)]
        [SerializeField] private string _practice = "";
        [Tooltip("과제를 해냈을 때 할 말. {0} 자리에 도구 이름이 들어간다")]
        [SerializeField] private string _practiceDone = "됐다. 이만하면 {0}은 손에 익었다.";

        [Header("들어 올리기")]
        [Tooltip("눈에서 이만큼 앞에 들어 올린다(m)")]
        [SerializeField] private float _readDistance = 0.42f;
        [Tooltip("눈높이에서 이만큼 내려 잡는다(m)")]
        [SerializeField] private float _readDrop = 0.08f;
        [SerializeField] private float _liftSeconds = 0.5f;
        [Tooltip("들고 있는 동안 천천히 돈다 — 어느 쪽에서 봐도 무엇인지 알게")]
        [SerializeField] private float _spinSpeed = 25f;

        [Range(0f, 1f)]
        [SerializeField] private float _hoverBrighten = 0.3f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Renderer[] _renderers;
        private MaterialPropertyBlock _mpb;
        private Vector3 _homePos;
        private Quaternion _homeRot;
        private Phase _phase = Phase.놓임;
        private int _step;
        private Coroutine _moving;
        private bool _learned;
        private bool _awaiting;   // 낸 과제를 기다리는 중

        /// <summary>생성기가 씬을 짤 때 채운다.</summary>
        public ToolDef Tool { get => _tool; set => _tool = value; }
        public string[] Steps { get => _steps; set => _steps = value; }

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _mpb = new MaterialPropertyBlock();
            _homePos = transform.position;
            _homeRot = transform.rotation;
        }

        private void OnEnable() => SubtitleView.OnClosed += OnNoticeClosed;

        private void OnDisable()
        {
            SubtitleView.OnClosed -= OnNoticeClosed;
            StopAwaiting();   // 씬을 떠나며 남긴 구독은 다음 씬에서 유령이 된다
        }

        private void Start()
        {
            // 이미 익힌 도구라면 문갑에 그대로 두되 다시 익힐 수는 있게 둔다.
            if (_tool != null && ToolbeltHud.Instance != null && ToolbeltHud.Instance.Has(_tool))
                _learned = true;
        }

        /// <summary>지금 누군가 도구를 익히는 중인가. 방을 짚는 손이 이것을 보고 물러난다.</summary>
        public static bool Learning { get; private set; }

        private void Update()
        {
            if (_phase != Phase.익히는중) return;

            if (_spinSpeed != 0f)
                transform.Rotate(Vector3.up, _spinSpeed * Time.deltaTime, Space.World);

            // ── 다음 한 마디로 넘기기 ──
            //
            // 여태는 <b>물건을 다시 눌러</b> 넘겼다. 그런데 눌러지지가 않았다.
            // 이 물건의 콜라이더는 반지름이 0.7m 인데(문갑 모델이 2.9배로 커져 있어
            // 그 밑에 달린 것도 같이 커졌다) 눈앞 0.43m 에 들어 올리므로,
            // <b>콜라이더가 카메라를 통째로 삼킨다</b>. 유니티는 콜라이더 <b>안에서</b>
            // 출발한 레이를 그 콜라이더에 맞은 것으로 치지 않는다 — 레이가 돋보기를
            // 그냥 지나쳐 뒤의 문짝을 맞히고 있었다. 익히기가 첫 마디에서 멎던 것이 이것이다.
            //
            // 물건을 더 멀리 들면 콜라이더는 비껴가지만 물건이 작아져 무엇인지 안 보인다.
            // 애초에 안내도 "물건을 눌러서" 가 아니라 <b>"눌러서 다음"</b> 이었다 —
            // 어디를 눌러도 넘어가는 것이 맞다.
#if ENABLE_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !PointerOnCloseTab())
                NextStep();
#endif
        }

        /// <summary>
        /// 상태를 바꾸면서 <see cref="Learning"/> 을 같이 적는다.
        ///
        /// 익히는 동안에는 방을 짚는 손이 물러나야 한다. 어디를 눌러도 다음으로
        /// 넘어가게 해 두었으니, 그 누름이 돋보기를 뚫고 뒤의 문짝까지 닿으면
        /// 한 마디 넘길 때마다 문이 여닫힌다.
        /// </summary>
        private void SetPhase(Phase p)
        {
            _phase = p;
            Learning = (p == Phase.익히는중);
        }

        /// <summary>
        /// 지금 가리키는 것이 자막의 <b>닫기 표</b>인가.
        ///
        /// 어디를 눌러도 넘어가게 해 두면 그만두는 길이 막힌다. 닫기 표만은
        /// 넘기기로 세지 않고 제 일을 하게 둔다.
        /// </summary>
        private static bool PointerOnCloseTab()
        {
#if ENABLE_INPUT_SYSTEM
            var cam = Camera.main;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (cam == null || mouse == null) return false;
            RaycastHit h;
            if (!Physics.Raycast(cam.ScreenPointToRay(mouse.position.ReadValue()), out h, 8f)) return false;
            return h.collider.GetComponentInParent<NoticeCloseTab>() != null;
#else
            return false;
#endif
        }

        // ── 손대기 ────────────────────────────────────

        public void OnHoverEnter() { if (_phase == Phase.놓임) Tint(_hoverBrighten); }
        public void OnHoverExit() { if (_phase == Phase.놓임) Tint(0f); }

        public void OnSelect()
        {
            switch (_phase)
            {
                case Phase.놓임:
                    Begin();
                    break;
                case Phase.익히는중:
                    NextStep();
                    break;
            }
        }

        // ── 익히기 ────────────────────────────────────

        private void Begin()
        {
            if (_tool == null) return;

            // 과제를 기다리던 중에 다시 눌렀다면 그 기다림은 접는다 — 처음부터
            // 다시 익히는 것이고, 구독이 겹치면 한 번 해내고 두 번 축하받는다.
            StopAwaiting();

            // 익히는 동안은 도구벨트를 허리 아래로 내린다.
            //
            // 벨트는 눈에서 0.6m, 자막은 1.3m 다. 벨트가 앞이므로 <b>고른 칸의 붉은
            // 바탕이 자막 글씨를 덮는다</b> — "돋보기다. 작은 것을 크게 본다" 의 첫
            // 두 글자가 붉은 판에 가려 안 읽혔다. 익히는 중에 읽어야 할 것은 자막이지
            // 벨트가 아니다. 수첩을 펼 때 쓰던 것과 같은 장치다.
            WorldHudAnchor.StowAll = true;

            Tint(0f);
            _step = -1;
            if (_moving != null) StopCoroutine(_moving);
            _moving = StartCoroutine(LiftRoutine());
        }

        private IEnumerator LiftRoutine()
        {
            SetPhase(Phase.떠오르는중);
            var cam = Camera.main;
            if (cam == null) { SetPhase(Phase.놓임); yield break; }

            Vector3 from = transform.position;
            Quaternion fromRot = transform.rotation;
            Vector3 to = ReadPosition(cam);

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, _liftSeconds);
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                transform.position = Vector3.Lerp(from, to, e);
                transform.rotation = Quaternion.Slerp(fromRot, cam.transform.rotation, e);
                yield return null;
            }

            SetPhase(Phase.익히는중);
            _moving = null;
            NextStep();
        }

        private void NextStep()
        {
            _step++;
            if (_steps == null || _step >= _steps.Length)
            {
                Finish();
                return;
            }

            string name = _tool != null ? _tool.displayName : "";
            string hint = (_step == _steps.Length - 1) ? "(눌러서 손에 쥔다)" : "(눌러서 다음)";
            SubtitleView.Show(name, _steps[_step], hint);
        }

        private void Finish()
        {
            var belt = ToolbeltHud.Instance;
            if (belt != null && _tool != null)
            {
                belt.Grant(_tool);
                // 손에 쥐여 준다 — 배운 것을 곧바로 들고 있어야 익힌 것이 된다.
                for (int i = 0; i < belt.Tools.Count; i++)
                    if (belt.Tools[i] == _tool) { belt.Select(i + 1); break; }
            }

            // 마지막 한 마디까지 읽고 나면 벨트를 도로 올린다 — 방금 배운 것이
            // 벨트에 들어가 앉는 것을 보아야 "손에 익혔다"가 눈으로 확인된다.
            WorldHudAnchor.StowAll = false;

            // ── 해 보기 ──
            // 말을 다 읽었다고 익힌 것이 아니다. 도구는 <b>손에 든 채로 무언가에 대 보아야</b>
            // 무엇에 쓰는 물건인지 알아진다. 그래서 여기서 끝내지 않고 과제를 하나 낸다 —
            // 물건은 문갑으로 돌려보내고(이제 손에 든 것으로 해야 하니까), 해냈다는
            // 소식이 <see cref="ToolPractice"/> 로 올 때까지 기다린다.
            if (!string.IsNullOrEmpty(_practice) && !_learned)
            {
                _awaiting = true;
                ToolPractice.OnUsed += OnPracticed;
                SubtitleView.Show(_tool != null ? _tool.displayName : "", _practice, "(직접 해 보면 된다)");

                // 예시 증거를 <b>쥐여 준다</b>.
                //
                // "문갑 위의 사목을 가져다 해 보시오" 는 심부름이다 — 물건을 찾아
                // 방을 헤매다 보면 정작 배우려던 도구는 뒷전이 된다. 종이는 이쪽에서
                // 펴 주고, 배우는 사람은 <b>도구 쓰는 일 하나만</b> 하면 된다.
                // 종이가 펴진 뒤에 물건이 내려가야 한다 — 순서가 뒤집히면 내려가는
                // 물건에 눈이 따라가 종이가 펴진 것을 못 본다.
                if (_example != null) _example.OpenNow();

                GoHome();
                return;
            }

            _learned = true;
            if (!string.IsNullOrEmpty(_endWord) && _tool != null)
                SubtitleView.Show(_tool.displayName, string.Format(_endWord, _tool.displayName), "(닫기)");

            GoHome();
        }

        /// <summary>낸 과제를 해냈다. 어느 도구를 썼는지로 가른다 — 남의 과제에 끼어들지 않게.</summary>
        private void OnPracticed(string toolId)
        {
            if (!_awaiting || _tool == null || toolId != _tool.id) return;
            StopAwaiting();
            _learned = true;
            SubtitleView.Show(_tool.displayName,
                              string.Format(_practiceDone, _tool.displayName), "(닫기)");
        }

        private void StopAwaiting()
        {
            if (!_awaiting) return;
            _awaiting = false;
            ToolPractice.OnUsed -= OnPracticed;
        }

        /// <summary>자막을 닫으면 익히기도 접는다 — 눈앞의 물건만 남아 있으면 갇힌 꼴이 된다.</summary>
        private void OnNoticeClosed()
        {
            if (_phase == Phase.익히는중) GoHome();
        }

        private void GoHome()
        {
            // 도중에 그만두었을 수도 있다. 어느 길로 끝나든 벨트는 도로 올린다 —
            // 안 그러면 익히기를 접은 뒤로 벨트가 영영 내려가 있다.
            WorldHudAnchor.StowAll = false;
            if (_moving != null) StopCoroutine(_moving);
            _moving = StartCoroutine(HomeRoutine());
        }

        private IEnumerator HomeRoutine()
        {
            SetPhase(Phase.내려가는중);
            Vector3 from = transform.position;
            Quaternion fromRot = transform.rotation;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, _liftSeconds);
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                transform.position = Vector3.Lerp(from, _homePos, e);
                transform.rotation = Quaternion.Slerp(fromRot, _homeRot, e);
                yield return null;
            }

            transform.position = _homePos;
            transform.rotation = _homeRot;
            SetPhase(Phase.놓임);
            _moving = null;
        }

        private Vector3 ReadPosition(Camera cam)
            => cam.transform.position + cam.transform.forward * _readDistance
               - cam.transform.up * _readDrop;

        private void Tint(float brighten)
        {
            if (_renderers == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            foreach (var r in _renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, Color.white * (1f + brighten));
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
