using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>열린 문짝 하나가 들이는 볕 한 줄기.</b>
    ///
    /// 문서고는 어두워야 등불이 뜻을 가진다. 그런데 칸이 통째로 열리면 한낮의 볕이
    /// 죄 쏟아져 들어와 어둠도 등불도 없어진다 — 그래서 판문을 <b>네 짝으로 갈랐다</b>
    /// (<c>SesalDoorSplit</c>). 한 짝만 열면 들어오는 것은 한 줄기고, 그 한 줄기가
    /// 이 방의 놀이가 된다.
    ///
    /// <b>이 부품은 자리만 안다.</b> 볕이 어느 쪽에서 드는지는 <see cref="LightBeam"/> 가
    /// 하나로 쥔다 — 해는 멀리 있어 <b>어느 문으로 들어오든 나란히</b> 든다. 문짝마다
    /// 딴 각을 적어 두면 해가 문마다 하나씩 있는 꼴이 된다.
    ///
    /// <b>쉴 때의 자리를 적어 둔다.</b> 문이 열리면 문짝은 비켜 서므로, 볕이 드는
    /// 자리는 문짝이 <b>있던</b> 자리다.
    ///
    /// 붙이는 곳: 문짝(<see cref="SwingDoor"/> 이 달린 것).
    /// </summary>
    [RequireComponent(typeof(SwingDoor))]
    public class SunShaft : MonoBehaviour
    {
        [Tooltip("문틀 한가운데에서 얼마나 옮겨 볕이 드나(m). 위로 올리면 높이 든다")]
        [SerializeField] private Vector3 _offset = new Vector3(0f, 0.30f, 0f);

        [Tooltip("이 문짝이 볕을 들이나. 꺼 두면 열어도 어둡다 — 처마에 가린 칸에 쓴다")]
        [SerializeField] private bool _letsLightIn = true;

        private SwingDoor _door;
        private Vector3 _rest;

        /// <summary>지금 이 짝으로 볕이 드나.</summary>
        public bool Open { get { return _letsLightIn && _door != null && _door.IsOpen; } }

        /// <summary>볕이 드는 자리(문짝이 있던 데).</summary>
        public Vector3 Origin { get { return _rest + _offset; } }

        private void Awake()
        {
            _door = GetComponent<SwingDoor>();
            var r = GetComponent<Renderer>();
            _rest = r != null ? r.bounds.center : transform.position;
        }

        private void OnDrawGizmosSelected()
        {
            var r = GetComponent<Renderer>();
            Vector3 p = (Application.isPlaying ? _rest : (r != null ? r.bounds.center : transform.position)) + _offset;
            Gizmos.color = new Color(1f, 0.9f, 0.55f, 0.9f);
            Gizmos.DrawWireSphere(p, 0.12f);
        }
    }
}
