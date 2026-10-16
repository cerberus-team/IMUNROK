using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>동경(銅鏡)</b> — 눌러 돌리면 볕을 꺾는 거울.
    ///
    /// <b>왜 끌지 않고 누르나.</b> 거울을 마우스로 잡아 끌게 하면 그 동안 고개가 같이
    /// 돌아간다 — 둘러보는 마우스와 돌리는 마우스가 한 마리라서 그렇다. 입력을 빼앗아
    /// 잠그는 길도 있지만, 그러면 이 방에만 있는 <b>새 조작</b>이 하나 더 는다.
    ///
    /// 그래서 <b>한 번 누르면 한 뼘 돈다</b>. 스무남은 번 누르면 한 바퀴고, 누를 때마다
    /// 빛줄기가 벽을 훑고 지나간다 — 답을 맞히는 것이 아니라 <b>이리저리 돌려 보다가</b>
    /// 글자에 앉는다. 등불을 등경마다 걸어 보는 <see cref="LightAngleReveal"/> 과 같은 손짓이다.
    ///
    /// <b>거울 면은 앞쪽(+Z)이다.</b> 딴 축으로 세운 모델이면 <see cref="_face"/> 에
    /// 그 면을 가리키는 빈 것을 하나 세워 물려 준다.
    ///
    /// 붙이는 곳: 거울 받침(콜라이더가 있어야 눌린다).
    /// </summary>
    public class BronzeMirror : MonoBehaviour, IInspectable, ISelectable
    {
        [Tooltip("거울 면. 이것의 <b>앞쪽(+Z)</b>이 비추는 쪽이다. 비우면 이 오브젝트")]
        [SerializeField] private Transform _face;

        [Tooltip("<b>한쪽만 비춘다.</b> 동경은 한 면만 갈아 놓은 물건이다 — 뒷면은 " +
                 "무늬를 새긴 놋판이거나 나무 등판이라 볕을 꺾지 못한다.\n\n" +
                 "꺼 두면 앞뒤 어느 쪽으로 맞아도 꺾인다. 그러면 <b>나무 등판이 볕을 " +
                 "튕기는</b> 꼴이 보이고, 맞는 각도 한 바퀴에 둘이 되어 돌리는 일이 헐거워진다")]
        [SerializeField] private bool _oneSided = true;

        /// <summary>
        /// <b>이 방향에서 온 볕을 꺾을 수 있나.</b>
        ///
        /// <paramref name="incoming"/> 은 볕이 <b>나아가는</b> 쪽이다. 거울 면의 앞쪽과
        /// 마주 보아야(내적이 음수) 갈아 놓은 면에 든 것이다.
        /// </summary>
        public bool Catches(Vector3 incoming)
        {
            if (!_oneSided) return true;
            return Vector3.Dot(Normal, incoming) < 0f;
        }

        /// <summary>
        /// <b>지금 볕이 여기 닿았다.</b> <see cref="LightBeam"/> 이 줄기를 따라가다
        /// 이 거울에 맞으면 매 프레임 알려 준다 — 앞면이든 등판이든.
        ///
        /// 이것을 받아 두는 까닭은 <b>살펴본 글에 지금 형편을 적기</b> 위해서다.
        /// 「돌려 보세요」만 적어 두면 스물네 눈금을 다 눌러 보는 수밖에 없는데,
        /// 등판에 맞고 멎는 중인지 갈아 놓은 면에 든 것인지만 알려 줘도
        /// <b>반 바퀴</b>가 줄어든다.
        /// </summary>
        public void NoteHit(Vector3 incoming)
        {
            _hitDir = incoming;
            _hitAt = Time.time;
        }

        private Vector3 _hitDir;
        private float _hitAt = -99f;

        /// <summary>바로 지금 볕이 닿고 있나(한 프레임 늦어도 참).</summary>
        private bool Lit { get { return Time.time - _hitAt < 0.25f; } }

        [Tooltip("한 번 누를 때 도는 각(도)")]
        [SerializeField] private float _step = 15f;

        [Tooltip("이 거리(m) 안에서만 돌릴 수 있다")]
        [SerializeField] private float _maxTouchDistance = 2.4f;

        [Tooltip("돌아가는 빠르기(초당 도). 탁 끊기지 않게 굴러가듯 돈다")]
        [SerializeField] private float _turnSpeed = 220f;

        [SerializeField] private string _title = "동경(銅鏡)";
        [TextArea(2, 3)] [SerializeField] private string _body = "놋쇠를 갈아 만든 거울. 받침째 돌아간다.";
        [SerializeField] private string _hint = "(눌러 돌리기)";

        private Transform Face { get { return _face != null ? _face : transform; } }

        /// <summary>거울 면이 보는 쪽.</summary>
        public Vector3 Normal { get { return Face.forward; } }

        /// <summary>거울 면의 한가운데.</summary>
        public Vector3 Here { get { return Face.position; } }

        private float _want;      // 가려는 각(도)
        private bool _init;

        private void Awake()
        {
            _want = Face.eulerAngles.y;
            _init = true;
        }

        private void Update()
        {
            if (!_init) return;
            var e = Face.eulerAngles;
            float now = Mathf.MoveTowardsAngle(e.y, _want, _turnSpeed * Time.deltaTime);
            if (Mathf.Abs(Mathf.DeltaAngle(now, e.y)) < 0.0001f) return;
            Face.rotation = Quaternion.Euler(e.x, now, e.z);
        }

        // ── 가리키기 ──
        public string GetInspectTitle() { return _title; }

        public string GetInspectBody()
        {
            string s = _body;
            if (_oneSided && Lit)
                s += Catches(_hitDir)
                     ? "\n<b>갈아 놓은 면</b>에 볕이 든다."
                     : "\n볕이 <b>등판</b>에 맞아 여기서 멎는다 — 반 바퀴 돌려야 한다.";
            return string.IsNullOrEmpty(_hint) ? s : s + "\n" + _hint;
        }

        public void OnInspected() { }

        // ── 누르기 ──
        public void OnHoverEnter() { }
        public void OnHoverExit() { }

        public void OnSelect()
        {
            var cam = Camera.main;
            if (cam != null && _maxTouchDistance > 0f &&
                ModelBounds.DistanceTo(transform, cam.transform.position) > _maxTouchDistance)
                return;
            _want = Mathf.Repeat(_want + _step, 360f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.45f, 0.9f);
            Gizmos.DrawLine(Face.position, Face.position + Face.forward * 0.6f);
        }
    }
}
