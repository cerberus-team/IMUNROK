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
        private void OnDisable() => SubtitleView.OnClosed -= OnNoticeClosed;

        private void Start()
        {
            // 이미 익힌 도구라면 문갑에 그대로 두되 다시 익힐 수는 있게 둔다.
            if (_tool != null && ToolbeltHud.Instance != null && ToolbeltHud.Instance.Has(_tool))
                _learned = true;
        }

        private void Update()
        {
            if (_phase == Phase.익히는중 && _spinSpeed != 0f)
                transform.Rotate(Vector3.up, _spinSpeed * Time.deltaTime, Space.World);
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
            Tint(0f);
            _step = -1;
            if (_moving != null) StopCoroutine(_moving);
            _moving = StartCoroutine(LiftRoutine());
        }

        private IEnumerator LiftRoutine()
        {
            _phase = Phase.떠오르는중;
            var cam = Camera.main;
            if (cam == null) { _phase = Phase.놓임; yield break; }

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

            _phase = Phase.익히는중;
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
            _learned = true;

            if (!string.IsNullOrEmpty(_endWord) && _tool != null)
                SubtitleView.Show(_tool.displayName, string.Format(_endWord, _tool.displayName), "(닫기)");

            GoHome();
        }

        /// <summary>자막을 닫으면 익히기도 접는다 — 눈앞의 물건만 남아 있으면 갇힌 꼴이 된다.</summary>
        private void OnNoticeClosed()
        {
            if (_phase == Phase.익히는중) GoHome();
        }

        private void GoHome()
        {
            if (_moving != null) StopCoroutine(_moving);
            _moving = StartCoroutine(HomeRoutine());
        }

        private IEnumerator HomeRoutine()
        {
            _phase = Phase.내려가는중;
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
            _phase = Phase.놓임;
            _moving = null;
        }

        private Vector3 ReadPosition(Camera cam)
            => cam.transform.position + cam.transform.forward * _readDistance
               - cam.transform.up * _readDrop;

        private void Tint(float brighten)
        {
            if (_renderers == null) return;
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
