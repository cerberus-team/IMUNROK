using System.Collections.Generic;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>방에 들어서면 눈이 어둠에 익는다.</b>
    ///
    /// 등불이 여태 장식이었던 까닭 가운데 하나는 <b>서고가 대낮처럼 밝다</b>는 것이다.
    /// 문을 다 닫아도 밝다 — 해가 드는 것이 아니라 <b>하늘빛(환경광)</b>이 방 안까지
    /// 고르게 들어차기 때문이다. 그러니 불을 켤 까닭이 없고, 켜도 달라지는 것이 없다.
    ///
    /// URP 에서 환경광은 <b>온 세상에 하나</b>다. 방 하나만 어둡게 하려면 빛을 구워야
    /// 하는데, 그것은 배치를 조금 고칠 때마다 다시 구워야 하는 일이다. 그래서 여기서는
    /// 방 안에 <b>사람이 있는 동안만</b> 환경광을 낮춘다.
    ///
    /// <b>그것이 되레 맞다.</b> 밝은 마당에서 어두운 서고로 들어서면 사람 눈도 한동안
    /// 아무것도 못 본다. 천천히 낮추고 천천히 올리면 그 익음이 그대로 그려진다 —
    /// 밖으로 나올 때 잠깐 눈이 부신 것까지.
    ///
    /// 붙이는 곳: 방 한가운데 빈 것 하나. 콜라이더도 트리거도 필요 없다 —
    /// 눈이 어디 있는지만 보면 되는 일이라 카메라 자리를 직접 잰다.
    ///
    /// 실행 중에 바꾼 환경광은 재생을 멈추면 씬 값으로 되돌아간다.
    /// </summary>
    public class RoomDarkness : MonoBehaviour
    {
        [Tooltip("이 방의 크기(이 오브젝트를 한가운데로 삼는다)")]
        [SerializeField] private Vector3 _size = new Vector3(11.8f, 2.6f, 5.8f);

        [Tooltip("방 안에서의 하늘빛 세기. 0 이면 칠흑이라 등불 없이는 한 걸음도 못 뗀다")]
        [Range(0f, 1f)] [SerializeField] private float _inside = 0.16f;

        [Tooltip("눈이 익는 데 걸리는 시간(초). 짧으면 조명 스위치를 누른 것처럼 보인다")]
        [Range(0.2f, 8f)] [SerializeField] private float _adapt = 2.4f;

        [Tooltip("켜면 씬 뷰에 방 테두리를 그린다")]
        [SerializeField] private bool _drawBounds = true;

        /// <summary>지금 씬에 선 어두운 방 전부.</summary>
        private static readonly List<RoomDarkness> All = new List<RoomDarkness>();

        /// <summary>바깥의 하늘빛 — 처음 값을 그대로 지킨다.</summary>
        private static float _outdoor = -1f;

        private void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
            if (_outdoor < 0f) _outdoor = RenderSettings.ambientIntensity;
        }

        private void OnDisable()
        {
            All.Remove(this);
            // 마지막 방이 사라지면 하늘빛을 도로 올려 둔다. 안 그러면 방을 지운 채로
            // 온 마당이 어두운 채 남는다.
            if (All.Count == 0 && _outdoor >= 0f) RenderSettings.ambientIntensity = _outdoor;
        }

        private void Update()
        {
            // 방이 여럿이면 <b>맨 앞의 하나만</b> 몬다. 저마다 환경광을 잡아당기면
            // 방 둘이 겹친 데서 값이 떨린다.
            if (All.Count == 0 || All[0] != this) return;

            var cam = Camera.main;
            if (cam == null) return;

            float want = _outdoor;
            var eye = cam.transform.position;
            foreach (var r in All)
            {
                if (r == null || !r.Contains(eye)) continue;
                if (r._inside < want) want = r._inside;      // 가장 어두운 방을 따른다
            }

            float now = RenderSettings.ambientIntensity;
            if (Mathf.Approximately(now, want)) return;

            // 오르고 내리는 폭을 시간으로 나눈다 — 어디서 시작하든 익는 데 같은 시간이 든다.
            float span = Mathf.Max(0.01f, Mathf.Abs(_outdoor - _inside));
            RenderSettings.ambientIntensity =
                Mathf.MoveTowards(now, want, span / Mathf.Max(0.05f, _adapt) * Time.deltaTime);
        }

        private bool Contains(Vector3 p)
            => new Bounds(transform.position, _size).Contains(p);

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!_drawBounds) return;
            Gizmos.color = new Color(0.2f, 0.3f, 0.6f, 0.35f);
            Gizmos.DrawWireCube(transform.position, _size);
        }
#endif
    }
}
