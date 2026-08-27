using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 인물의 몸을 마커 자리에, 발을 바닥에 붙여 둔다.
    ///
    /// 왜 필요한가: 이 프로젝트의 캐릭터 fbx들은 <b>바인드 자세와 애니메이션 자세가
    /// 서로 다른 자리</b>에 있다. 옹덕구는 그 차이가 수평으로 2.63m, 높이로 수십 cm다.
    /// 그래서 오브젝트에 고정 오프셋을 넣어 맞추면 씬뷰에서 맞을 때 플레이에서 어긋나고,
    /// 플레이에서 맞추면 씬뷰가 어긋난다 — 어느 한쪽을 포기해야 한다.
    ///
    /// 고정 오프셋 대신 <b>지금 자세의 실제 뼈 위치</b>를 재서 맞춘다. 그러면 어떤
    /// 클립이 돌든, 에디터든 플레이든 몸이 마커 위에 서 있고 발이 바닥에 있다.
    ///
    /// 붙이는 법: 인물 마커(모델의 부모)에 붙이면 끝. 뼈는 스스로 찾는다.
    /// 걸어다니는 인물(복동)도 그대로 쓸 수 있다 — 마커를 스크립트가 옮기면
    /// 몸이 마커를 따라간다.
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

        [Tooltip("발 뼈는 발등 안쪽에 있어 발바닥보다 위다. 켜면 몸을 한 번 구워 " +
                 "뼈에서 발바닥까지가 몇 cm인지 재서 그만큼 올린다 — 발이 땅에 묻히지 않는다")]
        [SerializeField] private bool _soleOnGround = true;

        [Tooltip("몸(엉덩이뼈)을 마커 자리에 수평으로 맞출지. fbx 의 바인드 자세가 원점에서 " +
                 "멀리 떨어져 있어도 인물이 마커 위에 선다. 끄면 높이만 맞춘다")]
        [SerializeField] private bool _pinHorizontally = true;

        [Tooltip("몸의 기준으로 삼을 뼈. 비우면 스킨메시의 루트 뼈(보통 Hips)")]
        [SerializeField] private Transform _bodyBone;

        [Tooltip("바닥 높이가 갑자기 바뀔 때 따라가는 속도(m/s). 0이면 그 자리에서 즉시 — " +
                 "중문 문지방처럼 턱이 있으면 한 프레임에 훌쩍 올라가 점프하는 것처럼 보인다")]
        [SerializeField] private float _followSpeed = 1.5f;

        [Tooltip("발보다 이만큼 위까지만 바닥으로 친다(m). 그보다 높은 것은 문짝·처마·서까래라 딛을 수 없다")]
        [SerializeField] private float _maxStepUp = 0.5f;

        /// <summary>
        /// 높이 맞추기만 잠시 끈다. 몸을 마커 자리에 붙들어 두는 일은 계속한다.
        ///
        /// 앉은 자세에는 이 보정이 안 맞는다 — 발바닥까지의 거리를 선 자세로 재 뒀기 때문에
        /// 그대로 쓰면 몸이 마루 밑으로 꺼진다. 그렇다고 이 부품을 통째로 끄면 수평 고정까지
        /// 같이 꺼져서, 앉기 클립의 원점 어긋남 때문에 몸이 2m 넘게 밀려나 버린다
        /// (실제로 머리가 x 6.05 → 3.35 로 날아갔다).
        /// </summary>
        public bool PinHeight { get; set; } = true;

        private float _shownGroundY;      // 지금 몸이 딛고 있는 것으로 치는 높이
        private bool _hasGround;

        private void Reset() => CacheBones();
        private void OnEnable() => CacheBones();

        private void CacheBones()
        {
            if (_model == null && transform.childCount > 0) _model = transform.GetChild(0);
            if (_model == null) return;
            if (_bodyBone == null)
            {
                var sk = _model.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (sk != null) _bodyBone = sk.rootBone;
            }
            // 이 모델은 스크립트가 매 프레임 옮긴다. 스킨메시의 경계는 미리 계산된 것을 쓰므로
            // 옮겨 다니는 동안 실제 몸과 어긋나, 가까이 있는데도 화면 밖으로 판정돼 통째로
            // 사라져 버린다(복동이 걷다가 없어진 원인). 매 프레임 다시 재게 한다.
            foreach (var sk2 in _model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                sk2.updateWhenOffscreen = true;

            if (_footBones != null && _footBones.Length > 0) return;

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

            // 먼저 몸을 마커 자리로. fbx 의 바인드 자세와 애니메이션 자세가 서로 다른
            // 자리에 있어도, 지금 뼈가 있는 곳을 재서 밀면 늘 마커 위에 선다.
            if (_pinHorizontally && _bodyBone != null)
            {
                Vector3 d = transform.position - _bodyBone.position;
                d.y = 0f;
                if (d.sqrMagnitude > 0.0000005f) _model.position += d;
            }

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

                // 발보다 한참 위에 있는 것은 바닥이 아니다.
                // 중문 문짝 콜라이더가 마당보다 1.71m 위에 떠 있어서, 위에서 쏜 광선이
                // 그것을 먼저 맞고 바닥으로 쳤다 — 복동이 문간에서 1.7m 솟구친 원인이다.
                // 디딤돌(10cm)·문지방은 넘고 문짝·처마는 거르도록 딛을 수 있는 높이로 자른다.
                if (h.point.y > lowest + _maxStepUp) continue;

                if (h.distance < best) { best = h.distance; groundY = h.point.y; }
            }

            // 문지방을 넘을 때 바닥이 한 번에 몇 십 cm 뛴다. 그대로 따라가면 몸이 튀어오르므로
            // 정해진 속도로만 쫓아간다. 편집 모드에서는 즉시 맞춘다(씬뷰가 흔들리면 안 된다).
            if (!PinHeight) return;      // 수평 고정만 하고 높이는 부르는 쪽이 알아서 한다

            if (!Application.isPlaying || !_hasGround || _followSpeed <= 0f) _shownGroundY = groundY;
            else _shownGroundY = Mathf.MoveTowards(_shownGroundY, groundY, _followSpeed * Time.deltaTime);
            _hasGround = true;

            float delta = (_shownGroundY - _sink + SoleOffset(lowest)) - lowest;
            if (Mathf.Abs(delta) < 0.0005f) return;
            _model.position += new Vector3(0f, delta, 0f);
        }

        private float _soleOffset = -1f;   // 아직 안 재봄

        /// <summary>
        /// <b>자세가 바뀌었다고 이른다.</b> 다음 칸에 발바닥 거리를 다시 잰다.
        ///
        /// 앉으면 이 거리가 통째로 달라진다 — 서 있을 땐 신발 두께(2~3cm)뿐이지만,
        /// 앉으면 <b>치맛단이 발보다 아래로 처져</b> 아내의 경우 13cm 가 된다. 선 자세에서
        /// 잰 값을 그대로 쓰면 그 13cm 만큼 치마가 마루를 뚫는다.
        /// 매 칸 재기에는 몸을 굽는 일이 비싸니, 앉고 서는 그때만 이렇게 일러 준다.
        /// </summary>
        public void Repose() { _soleOffset = -1f; _settleUntil = Time.time + ReposeSettle; }

        /// <summary>자세가 바뀌었다고 이른 뒤, 이만큼(초) 은 매 칸 다시 잰다.</summary>
        private const float ReposeSettle = 1.6f;
        private float _settleUntil;

        /// <summary>
        /// 발 뼈에서 <b>몸의 가장 낮은 곳</b>까지의 거리. 몸을 한 번 구워 가장 낮은 정점과
        /// 발 뼈를 비교해 잰다. 인물마다 신발 두께가 다르고 뼈가 발등 어디에 박혔는지도
        /// 달라서, 숫자를 손으로 적어 넣으면 인물을 바꿀 때마다 다시 틀린다.
        ///
        /// 선 자세에서는 이것이 곧 신발 밑창이고, 앉은 자세에서는 <b>치맛단</b>이 된다.
        /// 어느 쪽이든 "몸의 맨 아래가 바닥에 닿는다"가 되어 옳다.
        /// </summary>
        private float SoleOffset(float lowestBoneY)
        {
            if (!_soleOnGround) return 0f;

            // <b>이르자마자 재면 아직 안 앉아 있다.</b> Stand_To_Sit 이 한 칸에 끝나지
            // 않으므로, 앉으라 이른 그 프레임에 재면 <b>선 자세</b>가 잡힌다. 자세가
            // 바뀐다고 들은 뒤 잠깐은 매 칸 다시 재서, 다 앉고 난 값이 남게 한다.
            if (_soleOffset >= 0f && Time.time >= _settleUntil) return _soleOffset;

            var sk = _model.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (sk == null || sk.sharedMesh == null) { _soleOffset = 0f; return 0f; }

            // <b>구운 살갗에 localToWorldMatrix 를 태우면 안 된다.</b> BakeMesh 가 내주는
            // 점은 이미 실제 크기라, 거기에 또 행렬을 태우면 배율이 두 번 곱해진다.
            // 마름의 fbx 는 안쪽이 100배(노드 배율 0.01)라 키가 1.86m 에서 0.02m 로
            // 줄어 자가 통째로 헛돈다. 배율 말고 <b>돌림과 자리만</b> 태운다.
            var baked = new Mesh();
            sk.BakeMesh(baked);
            var rot = sk.transform.rotation;
            float py = sk.transform.position.y;
            float meshLow = float.MaxValue;
            foreach (var v in baked.vertices)
            {
                float y = (rot * v).y + py;
                if (y < meshLow) meshLow = y;
            }
            if (Application.isPlaying) Destroy(baked); else DestroyImmediate(baked);

            // 앉으면 치맛단이 13cm 를 넘기도 하니 0.25 로는 모자란다.
            _soleOffset = meshLow == float.MaxValue ? 0f : Mathf.Clamp(lowestBoneY - meshLow, 0f, 0.40f);
            return _soleOffset;
        }
    }
}
