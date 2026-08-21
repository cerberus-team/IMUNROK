using UnityEngine;
using UnityEngine.Events;

namespace IMUNROK.Common
{
    /// <summary>
    /// 문 열림/닫힘 컨트롤러 — 여닫이(회전)·미닫이(슬라이드) 둘 다 지원, 문짝 여러 장(대문=2짝) 가능.
    ///
    /// 붙이는 법:
    ///  · 빈 오브젝트 "대문"을 만들고 그 밑에 문짝들(SM_Door01D_L, _R 등)을 넣는다.
    ///  · "대문"에 이 컴포넌트를 붙이고, _leaves에 각 문짝(또는 경첩 빈오브젝트)을 드래그.
    ///  · 문짝에 Collider가 있어야 마우스/VR 레이로 집힌다(대개 Mesh Collider 있음).
    ///
    /// 상호작용(ISelectable): 클릭/VR레이 →
    ///  · 잠금(_locked)이면 열리지 않고 OnKnock 이벤트만 발생(대문 두드리기 시퀀스 훅).
    ///  · 잠금 아니면 열림/닫힘 토글.
    ///
    /// 스크립트에서: Open()/Close()/SetLocked(false) 호출(시퀀스가 문을 여는 용도).
    /// </summary>
    public class DoorController : MonoBehaviour, ISelectable
    {
        private enum Motion { Swing, Slide }

        [System.Serializable]
        private class Leaf
        {
            [Tooltip("회전/이동시킬 문짝(또는 경첩 자리에 둔 빈 오브젝트)")]
            public Transform pivot;
            [Tooltip("여닫이: 열렸을 때 로컬 Y 회전각(부호로 방향). 두 짝은 반대 부호로.")]
            public float swingAngle = 90f;
            [Tooltip("미닫이: 열렸을 때 로컬 이동량(m).")]
            public Vector3 slideOffset = new Vector3(0.9f, 0f, 0f);
        }

        [Header("문 방식")]
        [SerializeField] private Motion _motion = Motion.Swing;
        [SerializeField] private Leaf[] _leaves;

        [Header("동작")]
        [Tooltip("여는 데 걸리는 시간(초)")]
        [SerializeField] private float _openDuration = 1.2f;
        [SerializeField] private bool _startOpen = false;
        [Tooltip("끄면 플레이어가 손대도 여닫히지 않는다(마름·복동이 여는 대문·중문). " +
                 "실수로 닫아 못 들어가는 일을 막는다. 잠금이 풀린 뒤에도 유효하다")]
        [SerializeField] private bool _playerCanToggle = true;

        [Tooltip("이 거리(m) 안에서만 눌린다. 문짝까지의 거리로 잰다. 0이면 거리를 안 본다")]
        [SerializeField] private float _maxTouchDistance = 3f;

        [Tooltip("잠기면 클릭해도 안 열리고 OnKnock만 발생(대문 시퀀스용)")]
        [SerializeField] private bool _locked = false;

        [Header("이벤트")]
        [Tooltip("잠긴 문을 클릭(두드림)했을 때 — 대문 두드리기 시퀀스 연결")]
        public UnityEvent OnKnock;
        [Tooltip("문이 다 열렸을 때")]
        public UnityEvent OnOpened;

        private bool _open;
        private int _onlyLeaf = -1;    // -1 = 다 여닫는다. 0 이상이면 그 짝 하나만
        private float _t;              // 0 = 닫힘, 1 = 열림
        private int _dir;              // +1 열리는 중, -1 닫히는 중, 0 정지
        private Quaternion[] _closedRot;
        private Vector3[] _closedPos;
        private bool _firedOpened;

        private void Start()
        {
            int n = _leaves != null ? _leaves.Length : 0;
            _closedRot = new Quaternion[n];
            _closedPos = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                if (_leaves[i].pivot == null) continue;
                _closedRot[i] = _leaves[i].pivot.localRotation;
                _closedPos[i] = _leaves[i].pivot.localPosition;
            }

            _open = _startOpen;
            _t = _open ? 1f : 0f;
            _dir = 0;
            Apply();
        }

        private void Update()
        {
            if (_dir == 0) return;

            _t += _dir * Time.deltaTime / Mathf.Max(0.01f, _openDuration);
            if (_t >= 1f) { _t = 1f; _dir = 0; }
            else if (_t <= 0f) { _t = 0f; _dir = 0; }
            Apply();

            if (_open && _t >= 1f && !_firedOpened)
            {
                _firedOpened = true;
                OnOpened?.Invoke();
            }
        }

        /// <summary>현재 _t(0~1)를 문짝들에 반영.</summary>
        private void Apply()
        {
            if (_leaves == null) return;
            float e = Mathf.SmoothStep(0f, 1f, _t); // 부드럽게
            for (int i = 0; i < _leaves.Length; i++)
            {
                var leaf = _leaves[i];
                if (leaf.pivot == null) continue;

                // 한 짝만 밀기로 했으면 나머지는 닫힌 채로 둔다
                float k = (_onlyLeaf >= 0 && i != _onlyLeaf) ? 0f : e;

                if (_motion == Motion.Swing)
                    leaf.pivot.localRotation = _closedRot[i] * Quaternion.Euler(0f, leaf.swingAngle * k, 0f);
                else
                    leaf.pivot.localPosition = _closedPos[i] + leaf.slideOffset * k;
            }
        }

        // ── 외부(스크립트/시퀀스)에서 ──
        public bool IsOpen => _open;

        public void Open()
        {
            _onlyLeaf = -1;
            if (_open) return;
            _open = true; _dir = +1; _firedOpened = false;
        }

        /// <summary>
        /// <b>한 짝만</b> 연다. 나머지는 닫힌 채로 둔다.
        ///
        /// 사람이 미는 문은 민 쪽만 열린다. 마름이 한쪽 문짝에 손을 대는데 두 짝이 함께
        /// 활짝 열리면, 미는 것이 아니라 문이 알아서 열리고 그 앞에서 손짓을 하는 꼴이 된다.
        /// 손님 하나 들이는 데 대문을 양쪽 다 여는 법도 없다 — 그건 가마가 들어올 때 일이다.
        /// </summary>
        public void OpenOnly(int leafIndex)
        {
            _onlyLeaf = (_leaves != null && leafIndex >= 0 && leafIndex < _leaves.Length) ? leafIndex : -1;
            if (_open) return;
            _open = true; _dir = +1; _firedOpened = false;
        }

        /// <summary>이 자리에서 가장 가까운 문짝. 미는 사람이 어느 짝을 밀지 고를 때 쓴다.</summary>
        public int NearestLeaf(Vector3 worldPos)
        {
            if (_leaves == null || _leaves.Length == 0) return -1;
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < _leaves.Length; i++)
            {
                var p = _leaves[i].pivot;
                if (p == null) continue;
                var r = p.GetComponentInChildren<Renderer>();
                Vector3 at = r != null ? r.bounds.center : p.position;
                float d = (at - worldPos).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        public void Close()
        {
            if (!_open) return;
            _open = false; _dir = -1;
        }

        public void Toggle() { if (_open) Close(); else Open(); }

        public void SetLocked(bool locked) => _locked = locked;

        /// <summary>
        /// 여는 데 걸리는 시간을 밖에서 정한다.
        ///
        /// 사람이 미는 문은 <b>미는 팔만큼</b> 열려야 한다. 문짝의 속도를 인스펙터에
        /// 손으로 적어 두면 동작을 조금만 손봐도 어긋나 — 팔은 다 폈는데 문은 아직
        /// 열리는 중이거나, 문이 먼저 열리고 팔이 뒤따라간다. 그래서 미는 쪽이
        /// 제 동작에 남은 시간을 재서 여기로 넘긴다.
        /// </summary>
        public void SetOpenDuration(float seconds)
        {
            _openDuration = Mathf.Max(0.05f, seconds);
        }

        /// <summary>잠금 해제 후 즉시 열기(시퀀스 마무리용).</summary>
        public void Unlock() { _locked = false; }

        // ── ISelectable(클릭/VR 레이) ──
        public void OnHoverEnter() { }   // 나중에 하이라이트 붙일 자리
        public void OnHoverExit() { }

        public void OnSelect()
        {
            // 손이 닿는 거리에서만 눌린다. 문짝 자체까지의 거리로 재야 한다 —
            // 피벗은 문틀 구석에 박혀 있어서, 그것으로 재면 코앞에 서 있어도 멀다고 막힌다.
            var cam = Camera.main;
            if (cam != null && _maxTouchDistance > 0f &&
                ModelBounds.DistanceTo(transform, cam.transform.position) > _maxTouchDistance)
                return;

            if (_locked)
            {
                OnKnock?.Invoke();   // 두드리기 → 시퀀스가 받아 처리
                return;
            }
            if (!_playerCanToggle) return;   // 시퀀스가 여닫는 문 — 손대도 안 움직인다
            Toggle();
        }
    }
}
