using System.Collections;
using UnityEngine;

namespace IMUNROK.Seocheon.Environment
{
    /// <summary>
    /// 성 밖(개천 남쪽) 진입 시에만 안개를 켜는 구역 트리거.
    ///
    /// 다리에 놓인 BoxCollider(isTrigger)의 X/Y 범위 안에서 플레이어가 중심 Z면을 넘는 순간,
    /// 이동 방향(Z 부호)으로 ON(남·성 밖) / OFF(북·마을)를 판정한다.
    /// 판정은 플레이어 트랜스폼의 위치로 하므로 XR 리그에 물리 콜라이더가 없어도 동작한다
    /// (콜라이더는 구역 경계를 정의하는 소스로만 읽는다).
    ///
    /// 안개는 항상 RenderSettings.fog = true로 두고 start/end 거리만 Lerp 한다.
    /// (거리만 조절하면 VR에서 프레임 단위 튐이 없다. OFF는 사실상 안 보이는 원거리.)
    /// fogColor는 고정.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class SeocheonFogZone : MonoBehaviour
    {
        [Header("플레이어 (비우면 Camera.main 사용)")]
        [SerializeField] private Transform player;

        [Header("안개 거리 — ON(성 밖) / OFF(마을)")]
        [SerializeField] private float onStart = 35f;
        [SerializeField] private float onEnd = 80f;
        [SerializeField] private float offStart = 300f;   // 사실상 안 보임
        [SerializeField] private float offEnd = 600f;

        [Header("전환")]
        [SerializeField, Range(0.1f, 5f)] private float transitionSeconds = 1.75f;
        [SerializeField] private Color fogColor = new Color(0.74f, 0.78f, 0.80f);

        private BoxCollider _zone;
        private bool _fogOn;
        private float _prevZ;
        private bool _hasPrev;
        private Coroutine _lerp;

        private void Awake()
        {
            _zone = GetComponent<BoxCollider>();
            _zone.isTrigger = true;                 // 물리적으로 막지 않도록
            RenderSettings.fog = true;              // 런타임엔 항상 true, 거리로만 제어
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fogColor;
        }

        private void OnEnable()
        {
            // [4] 씬 진입 시 초기 상태 판정 — 다리 남쪽에서 시작하면 안개 ON으로 시작
            var p = ResolvePlayer();
            bool south = p != null && p.position.z < ZPlane();
            ApplyInstant(south);
            _hasPrev = p != null;
            _prevZ = p != null ? p.position.z : ZPlane();
        }

        private void Update()
        {
            var p = ResolvePlayer();
            if (p == null) { _hasPrev = false; return; }

            float z = p.position.z;
            if (!_hasPrev) { _prevZ = z; _hasPrev = true; return; }

            float plane = ZPlane();
            bool wasNorth = _prevZ >= plane;
            bool isNorth = z >= plane;
            if (wasNorth != isNorth && WithinZoneXY(p.position))
                SetFog(!isNorth);                   // 남(성 밖)=ON, 북(마을)=OFF

            _prevZ = z;
        }

        private float ZPlane()
        {
            return _zone.transform.TransformPoint(_zone.center).z;
        }

        /// <summary>다리 외 지점에서의 Z 통과를 걸러내기 위해 트리거 박스의 X/Y 범위 안일 때만 유효.</summary>
        private bool WithinZoneXY(Vector3 world)
        {
            Vector3 local = _zone.transform.InverseTransformPoint(world) - _zone.center;
            Vector3 e = _zone.size * 0.5f;
            return Mathf.Abs(local.x) <= e.x && Mathf.Abs(local.y) <= e.y;
        }

        private Transform ResolvePlayer()
        {
            if (player != null) return player;
            var cam = Camera.main;
            return cam != null ? cam.transform : null;
        }

        private void SetFog(bool on)
        {
            if (on == _fogOn) return;                // 이미 같은 상태면 무시
            _fogOn = on;
            if (_lerp != null) StopCoroutine(_lerp); // [4] 코루틴 중복 방지
            _lerp = StartCoroutine(LerpFog(on ? onStart : offStart, on ? onEnd : offEnd));
        }

        private void ApplyInstant(bool on)
        {
            _fogOn = on;
            if (_lerp != null) { StopCoroutine(_lerp); _lerp = null; }
            RenderSettings.fogStartDistance = on ? onStart : offStart;
            RenderSettings.fogEndDistance = on ? onEnd : offEnd;
        }

        private IEnumerator LerpFog(float targetStart, float targetEnd)
        {
            float s0 = RenderSettings.fogStartDistance;
            float e0 = RenderSettings.fogEndDistance;
            float dur = Mathf.Max(0.01f, transitionSeconds);
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;         // 타임스케일 영향 없이
                float k = Mathf.SmoothStep(0f, 1f, t / dur);
                RenderSettings.fogStartDistance = Mathf.Lerp(s0, targetStart, k);
                RenderSettings.fogEndDistance = Mathf.Lerp(e0, targetEnd, k);
                yield return null;
            }
            RenderSettings.fogStartDistance = targetStart;
            RenderSettings.fogEndDistance = targetEnd;
            _lerp = null;
        }

#if UNITY_EDITOR
        // 씬 뷰에서 구역을 보기 쉽게
        private void OnDrawGizmosSelected()
        {
            var bc = GetComponent<BoxCollider>();
            if (bc == null) return;
            Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.25f);
            Gizmos.matrix = bc.transform.localToWorldMatrix;
            Gizmos.DrawCube(bc.center, bc.size);
        }
#endif
    }
}
