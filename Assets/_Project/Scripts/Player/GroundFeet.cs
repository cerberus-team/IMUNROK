using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 인물의 발을 바닥에 붙여 둔다.
    ///
    /// 왜 필요한가: 이 프로젝트의 캐릭터 fbx들은 클립마다 몸 높이가 다르다.
    /// 바인드 자세와 애니메이션 자세가 다르고, 애니메이션끼리도 다르다. 그래서
    /// 오브젝트에 고정 오프셋을 넣어 맞추면 씬뷰에서 맞을 때 플레이에서 뜨고,
    /// 플레이에서 맞추면 씬뷰에서 파묻힌다 — 어느 한쪽을 포기해야 한다.
    ///
    /// 고정 오프셋 대신 <b>지금 자세의 실제 발 높이</b>를 재서 맞춘다. 그러면
    /// 어떤 클립이 돌든, 에디터든 플레이든 발이 바닥에 있다.
    ///
    /// 붙이는 법: 인물 마커(모델의 부모)에 붙이면 끝. 발 뼈는 스스로 찾는다.
    /// 고택에 콜라이더가 거의 없으므로 바닥을 못 찾으면 _fallbackY 를 쓴다.
    /// </summary>
    [ExecuteAlways]
    public class GroundFeet : MonoBehaviour
    {
        [Tooltip("높이를 조절할 모델. 비우면 첫 번째 자식")]
        [SerializeField] private Transform _model;

        [Tooltip("발로 칠 뼈. 비우면 이름으로 찾고, 그래도 없으면 가장 낮은 뼈를 쓴다")]
        [SerializeField] private Transform[] _footBones;

        [Tooltip("바닥으로 칠 레이어")]
        [SerializeField] private LayerMask _groundMask = ~0;

        [Tooltip("이 높이에서 아래로 바닥을 찾는다(발 기준 상대)")]
        [SerializeField] private float _rayUp = 2.5f;

        [Tooltip("바닥을 못 찾았을 때 쓸 높이. 마당은 -1.67, 사랑채 마루는 -0.845")]
        [SerializeField] private float _fallbackY = -1.67f;

        [Tooltip("발이 바닥보다 살짝 눌리게(신발 두께). 파묻히면 줄인다")]
        [SerializeField] private float _sink = 0.01f;

        private void Reset() => CacheBones();
        private void OnEnable() => CacheBones();

        private void CacheBones()
        {
            if (_model == null && transform.childCount > 0) _model = transform.GetChild(0);
            if (_footBones != null && _footBones.Length > 0) return;
            if (_model == null) return;

            // 발 뼈를 이름으로 찾는다. 못 찾으면 LateUpdate 에서 가장 낮은 것을 쓴다.
            var found = new System.Collections.Generic.List<Transform>();
            foreach (var t in _model.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("toebase") || n.EndsWith("foot")) found.Add(t);
            }
            _footBones = found.ToArray();
        }

        private void LateUpdate()
        {
            if (_model == null) return;

            float lowest = float.MaxValue;
            if (_footBones != null && _footBones.Length > 0)
            {
                foreach (var b in _footBones)
                    if (b != null && b.position.y < lowest) lowest = b.position.y;
            }
            else
            {
                foreach (var t in _model.GetComponentsInChildren<Transform>(true))
                    if (t.position.y < lowest) lowest = t.position.y;
            }
            if (lowest == float.MaxValue) return;

            // 발 바로 밑의 바닥. 자기 콜라이더는 세지 않는다.
            float groundY = _fallbackY;
            Vector3 origin = new Vector3(transform.position.x, lowest + _rayUp, transform.position.z);
            var hits = Physics.RaycastAll(origin, Vector3.down, _rayUp + 6f, _groundMask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(transform)) continue;   // 나 자신 제외
                if (h.distance < best) { best = h.distance; groundY = h.point.y; }
            }

            float delta = (groundY - _sink) - lowest;
            if (Mathf.Abs(delta) < 0.0005f) return;
            _model.position += new Vector3(0f, delta, 0f);
        }
    }
}
