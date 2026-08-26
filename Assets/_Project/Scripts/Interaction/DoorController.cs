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

        [Header("소리")]
        [Tooltip("여닫을 때 나는 소리 크기(0~1). 잠행 중에 누가 듣는다(NoiseMeter). " +
                 "0이면 소리 안 난 것으로 친다. 나무 문은 0.55 남짓")]
        [Range(0f, 1f)] [SerializeField] private float _noise = 0.55f;
        [Tooltip("여는 소리. 비우면 소리 없이 크기만 잰다")]
        [SerializeField] private AudioClip _openSound;
        [Tooltip("닫는 소리. 비우면 여는 소리를 그대로 쓴다 — 미닫이는 여닫는 소리가 거의 같다")]
        [SerializeField] private AudioClip _closeSound;
        [Tooltip("소리 높낮이를 이만큼 흔든다. 같은 문을 여닫을 때마다 똑같이 들리면 " +
                 "소리가 아니라 <b>버튼</b>처럼 들린다")]
        [Range(0f, 0.3f)] [SerializeField] private float _pitchJitter = 0.08f;

        [Tooltip("녹음의 이 자리(초)부터 쓴다. 앞머리에 잡음이나 손 대는 소리가 있으면 건너뛴다")]
        [SerializeField] private float _soundStartAt = 0f;
        [Tooltip("녹음을 이만큼(초)만 쓴다. 0이면 끝까지. " +
                 "<b>스르륵 뒤에 쿵이 붙은 녹음</b>에서 앞 토막만 쓰는 값이다 — " +
                 "손으로 살며시 미는 문에 쿵이 붙으면 밀 때마다 문을 걷어차는 소리가 난다")]
        [SerializeField] private float _soundSeconds = 0f;

#if UNITY_EDITOR
        /// <summary>
        /// <b>미닫이 소리를 여닫이에 걸어 두는 실수</b>를 잡는다.
        ///
        /// 실제로 그렇게 되어 있었다 — 바깥 대문(두 짝짜리 <b>여닫이</b>)에 방 안
        /// 미닫이문 녹음이 걸려 있어서, 무거운 판장문을 밀어 여는데 방문이 스르륵
        /// 미끄러지는 소리가 났다. 오류가 아니니 아무 데도 안 뜨고, 소리는 나기는
        /// 나니 귀로 잡기 전에는 모른다.
        ///
        /// 그래서 <b>인스펙터에서 걸어 두는 순간</b> 한 줄 일러 준다. 막지는 않는다 —
        /// 일부러 그렇게 쓸 까닭이 있을 수도 있고, 이름만 보고 판을 뒤집을 일은 아니다.
        /// </summary>
        private void OnValidate()
        {
            if (_motion != Motion.Swing) return;
            Warn(_openSound, "여는");
            Warn(_closeSound, "닫는");
        }

        private void Warn(AudioClip clip, string which)
        {
            if (clip == null) return;
            if (clip.name.IndexOf("미닫이", System.StringComparison.Ordinal) < 0 &&
                clip.name.IndexOf("Slide", System.StringComparison.OrdinalIgnoreCase) < 0) return;
            Debug.LogWarning("[문] " + name + " 은 <b>여닫이</b>인데 " + which +
                             " 소리로 미닫이 녹음(" + clip.name + ")이 걸려 있다. " +
                             "돌쩌귀 우는 소리로 바꾸는 편이 맞다.", this);
        }
#endif

        [Header("문간 마개")]
        [Tooltip("닫혔을 때만 길을 막는 콜라이더. 비워 두면 이 오브젝트에 붙은 것을 쓴다. " +
                 "문짝에 붙은 것이 아니라 문 뿌리에 붙은 것을 넣는다")]
        [SerializeField] private Collider _blocker;
        [Tooltip("끄면 마개를 건드리지 않는다(문짝 콜라이더만으로 막는 문)")]
        [SerializeField] private bool _blockerFollowsDoor = true;

        [Header("이벤트")]
        [Tooltip("잠긴 문을 클릭(두드림)했을 때 — 대문 두드리기 시퀀스 연결")]
        public UnityEvent OnKnock;
        [Tooltip("문이 다 열렸을 때")]
        public UnityEvent OnOpened;

        // ── 짝마다 제 상태를 가진다 ──
        //
        // 여태 문 하나에 열림/닫힘이 하나뿐이었다. 그래서 왼짝을 밀어 열어 둔 채
        // 오른짝을 밀면 <b>열려 있던 왼짝이 닫혔다</b> — 손이 닿지도 않은 문짝이
        // 저 혼자 움직인 것이다. 사람이 미는 문은 민 짝만 움직이고, 두 짝을 차례로
        // 밀면 두 짝이 다 열린 채로 있는다.
        private float[] _t;            // 짝마다 0 = 닫힘, 1 = 열림
        private int[] _dir;            // 짝마다 +1 열리는 중, -1 닫히는 중, 0 정지
        private bool[] _leafOpen;      // 짝마다 열어 두기로 한 상태인가
        private Quaternion[] _closedRot;
        private Vector3[] _closedPos;
        private bool _firedOpened;

        private void Start()
        {
            int n = _leaves != null ? _leaves.Length : 0;
            _closedRot = new Quaternion[n];
            _closedPos = new Vector3[n];
            _t = new float[n];
            _dir = new int[n];
            _leafOpen = new bool[n];
            for (int i = 0; i < n; i++)
            {
                if (_leaves[i].pivot == null) continue;
                _closedRot[i] = _leaves[i].pivot.localRotation;
                _closedPos[i] = _leaves[i].pivot.localPosition;
            }

            // 문간 마개 — 안 넣었으면 이 오브젝트에 붙은 것을 쓴다. 문짝에 붙은 것은
            // 짝을 따라 비켜나지만, 문 뿌리에 붙은 것은 제자리에 남아 길을 막는다.
            if (_blocker == null) _blocker = GetComponent<Collider>();

            for (int i = 0; i < n; i++)
            {
                _leafOpen[i] = _startOpen;
                _t[i] = _startOpen ? 1f : 0f;
                _dir[i] = 0;
            }
            Apply();
        }

        private void Update()
        {
            if (_dir == null) return;
            bool moved = false;
            bool anyJustOpened = false;

            for (int i = 0; i < _dir.Length; i++)
            {
                if (_dir[i] == 0) continue;
                moved = true;
                _t[i] += _dir[i] * Time.deltaTime / Mathf.Max(0.01f, _openDuration);
                if (_t[i] >= 1f) { _t[i] = 1f; _dir[i] = 0; anyJustOpened = true; }
                else if (_t[i] <= 0f) { _t[i] = 0f; _dir[i] = 0; }
            }
            if (!moved) return;
            Apply();

            if (anyJustOpened && !_firedOpened)
            {
                _firedOpened = true;
                OnOpened?.Invoke();
            }
        }

        /// <summary>
        /// <b>문간 마개를 여닫는다.</b>
        ///
        /// 문짝은 경첩을 돌아 비켜나지만, 문 뿌리에 붙은 콜라이더는 제자리에 남는다.
        /// 두드릴 과녁이 있어야 하니 지울 수는 없다 — 레이가 집을 것이 없으면 문을
        /// 두드리지도 못한다. 그래서 <b>다 닫혔을 때만</b> 막고, 조금이라도 열리면 비킨다.
        ///
        /// 닫는 중에도 비켜 둔다. 문간에 선 채로 문이 닫히면 벽에 갇히기 때문이다.
        /// </summary>
        private void ApplyBlocker()
        {
            if (!_blockerFollowsDoor || _blocker == null || _t == null) return;
            // 한 짝이라도 열려 있으면 길이 트인 것이다
            bool shut = true;
            for (int i = 0; i < _t.Length; i++) if (_t[i] > 0.001f) { shut = false; break; }
            if (_blocker.enabled != shut) _blocker.enabled = shut;
        }

        /// <summary>현재 _t(0~1)를 문짝들에 반영.</summary>
        private void Apply()
        {
            ApplyBlocker();
            if (_leaves == null || _t == null) return;
            for (int i = 0; i < _leaves.Length; i++)
            {
                var leaf = _leaves[i];
                if (leaf.pivot == null) continue;

                float k = Mathf.SmoothStep(0f, 1f, _t[i]);   // 짝마다 제 몫으로 부드럽게

                if (_motion == Motion.Swing)
                    leaf.pivot.localRotation = _closedRot[i] * Quaternion.Euler(0f, leaf.swingAngle * k, 0f);
                else
                    leaf.pivot.localPosition = _closedPos[i] + leaf.slideOffset * k;
            }
        }

        // ── 외부(스크립트/시퀀스)에서 ──

        /// <summary>한 짝이라도 열려 있나.</summary>
        public bool IsOpen
        {
            get
            {
                if (_leafOpen == null) return _startOpen;
                for (int i = 0; i < _leafOpen.Length; i++) if (_leafOpen[i]) return true;
                return false;
            }
        }

        /// <summary>
        /// <b>다 연다</b>. 사람이 양손으로 미는 문에 쓴다 — 복동의 문 여는 동작이
        /// 두 손으로 두 짝을 미는 것(Door_OpenBoth)이라, 그가 여는 문은 두 짝이 함께 열려야
        /// 손과 문짝이 맞는다. 플레이어가 제 손으로 미는 것은 <see cref="ToggleLeaf"/> 다.
        /// </summary>
        public void Open()
        {
            if (_leafOpen == null) { _startOpen = true; return; }
            bool any = false;
            for (int i = 0; i < _leafOpen.Length; i++)
            {
                if (_leafOpen[i]) continue;
                _leafOpen[i] = true; _dir[i] = +1; any = true;
            }
            if (!any) return;
            _firedOpened = false;
            Creak("문 여는 소리");
        }

        /// <summary>
        /// <b>그 짝만</b> 여닫는다 — 민 짝만 움직이고 나머지는 지금 그대로 둔다.
        ///
        /// 한 짝을 열어 둔 채 다른 짝을 밀면 <b>그 짝도 열린다</b>. 두 짝이 다 열린 채로
        /// 있을 수 있고, 열린 짝을 다시 밀면 그 짝만 닫힌다. 손이 닿지 않은 문짝은
        /// 무슨 일이 있어도 저 혼자 움직이지 않는다.
        /// </summary>
        public void ToggleLeaf(int i)
        {
            if (_leafOpen == null || i < 0 || i >= _leafOpen.Length) return;
            bool open = !_leafOpen[i];
            _leafOpen[i] = open;
            _dir[i] = open ? +1 : -1;
            if (open) _firedOpened = false;
            Creak(open ? "문 여는 소리" : "문 닫는 소리", !open);
        }

        /// <summary>여닫는 소리를 낸다 — 잠행 중이면 누가 듣는다.</summary>
        private void Creak(string what, bool closing = false)
        {
            if (_noise <= 0f) return;
            Vector3 at = ModelBounds.TryGet(transform, out var b) ? b.center : transform.position;
            var clip = closing ? (_closeSound != null ? _closeSound : _openSound) : _openSound;
            float pitch = 1f + Random.Range(-_pitchJitter, _pitchJitter);
            NoiseMeter.Play(at, clip, _noise, what, pitch, _soundStartAt, _soundSeconds);
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
            if (_leafOpen == null) return;
            if (leafIndex < 0 || leafIndex >= _leafOpen.Length) { Open(); return; }
            if (_leafOpen[leafIndex]) return;
            _leafOpen[leafIndex] = true; _dir[leafIndex] = +1; _firedOpened = false;
            Creak("문 여는 소리");
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

        /// <summary><b>그 짝만</b> 닫는다. 나머지는 지금 그대로 둔다.</summary>
        public void CloseOnly(int leafIndex)
        {
            if (_leafOpen == null) return;
            if (leafIndex < 0 || leafIndex >= _leafOpen.Length) { Close(); return; }
            if (!_leafOpen[leafIndex]) return;
            _leafOpen[leafIndex] = false; _dir[leafIndex] = -1;
            Creak("문 닫는 소리", true);
        }

        /// <summary>다 닫는다.</summary>
        public void Close()
        {
            if (_leafOpen == null) { _startOpen = false; return; }
            bool any = false;
            for (int i = 0; i < _leafOpen.Length; i++)
            {
                if (!_leafOpen[i]) continue;
                _leafOpen[i] = false; _dir[i] = -1; any = true;
            }
            if (!any) return;
            Creak("문 닫는 소리", true);
        }

        public void Toggle() { if (IsOpen) Close(); else Open(); }

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
                // 두드리는 것은 소리보다 손에 먼저 온다. 소리(문_두드림_셋)와 같은
                // 박자로 쿵·쿵·쿵 세 번 울려, 문 소리를 튼 것이 아니라 제가 친 것이
                // 되게 한다. 헤드셋이 없으면 저절로 아무 일도 안 한다.
                Haptics.Knock();
                OnKnock?.Invoke();   // 두드리기 → 시퀀스가 받아 처리
                return;
            }
            if (!_playerCanToggle) return;   // 시퀀스가 여닫는 문 — 손대도 안 움직인다

            // <b>손이 하나면 문짝도 하나다.</b>
            //
            // 민 짝만 움직인다. 짚은 자리는 광선이 이미 알고 있으므로(Pointing) 그 점으로
            // 짝을 고른다 — 카메라 자리로 어림잡으면 문 한가운데 섰을 때 어느 짝인지
            // 뒤집힌다. 짚은 점이 없으면(다른 손이 부른 경우) 카메라 자리로 어림잡는다.
            Vector3 at = Pointing.Fresh ? Pointing.LastPoint
                       : (cam != null ? cam.transform.position : transform.position);
            ToggleLeaf(NearestLeaf(at));
        }
    }
}
