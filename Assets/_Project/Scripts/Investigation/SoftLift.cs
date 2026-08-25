using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>천처럼 말려 올라간다</b> — 요·이불·보자기를 들출 때의 그 몸짓.
    ///
    /// 여태 보료는 경첩을 축으로 <b>통째로 30° 젖혀졌다</b>. 널빤지라면 그것이 맞지만
    /// 보료는 솜을 둔 요다. 널빤지처럼 각을 세우고 올라가니 "들춘다"가 아니라 "뚜껑이
    /// 열린다"가 되었다.
    ///
    /// 진짜 요는 <b>잡은 데부터</b> 들리고 바닥에 붙은 쪽은 늦게 따라온다. 그래서
    /// 한 각도로 도는 것이 아니라 <b>자리마다 다른 각도로</b> 돈다 — 경첩 가까이는 거의
    /// 그대로 눕고, 멀어질수록 많이 들린다. 그 사이가 곡선이면 천처럼 휜다.
    ///
    /// 그래서 이 부품은 메시의 <b>정점을 하나씩</b> 경첩 축 둘레로 돌리되, 축에서 멀수록
    /// 더 돌린다. 뼈도 옷감 물리도 쓰지 않는다 — 들추는 동안만 도는 일이라 그만한 것을
    /// 붙일 값이 아니고, 이 편이 어떤 메시에나 그대로 붙는다.
    ///
    /// 붙이는 곳: 요 <b>메시</b>(MeshFilter 가 있는 오브젝트). 들추는 장치
    /// (<see cref="AshRake"/>)의 '부드럽게 들리기' 칸에 이것을 걸면, 경첩을 뻣뻣하게
    /// 돌리는 대신 이쪽으로 들어 올린다.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    public class SoftLift : MonoBehaviour
    {
        [Tooltip("들리는 축이 지나는 자리(경첩). 비우면 부모를 쓴다")]
        [SerializeField] private Transform _hinge;

        [Tooltip("경첩의 어느 축을 돌아 들리나. 보료는 한쪽 끝을 잡고 젖히므로 X 다")]
        [SerializeField] private Vector3 _axis = Vector3.right;

        [Tooltip("<b>끝자락</b>이 들리는 각(도). 경첩 쪽은 이보다 훨씬 덜 들린다")]
        [SerializeField] private float _maxAngle = 46f;

        [Tooltip("경첩에서 먼 쪽으로 재는 방향(경첩 기준). 보료는 경첩이 남쪽 끝이라 +Z 다")]
        [SerializeField] private Vector3 _along = Vector3.forward;

        [Tooltip("휘는 결. 1보다 크면 경첩 가까운 쪽이 오래 바닥에 붙어 있다가 늦게 따라온다 — " +
                 "천이 그렇다. 1이면 널빤지처럼 고르게 돈다")]
        [Range(1f, 3f)] [SerializeField] private float _bend = 1.7f;

        [Tooltip("들린 자락이 이만큼 안쪽으로 끌려온다(0~0.3). 요를 들면 끝이 위로만 가는 것이 " +
                 "아니라 잡은 쪽으로 조금 끌려 접힌다")]
        [Range(0f, 0.3f)] [SerializeField] private float _draw = 0.10f;

        private Mesh _mesh;
        private Vector3[] _hingeBase;     // 경첩 기준으로 옮겨 둔 원래 정점
        private Vector3[] _work;          // 그때그때 계산한 것(메시 기준)
        private Matrix4x4 _hingeToLocal;  // 경첩 기준 → 메시 기준
        private float _far = 1f;          // 경첩에서 가장 먼 정점까지
        private float _near;
        private float _lift = -1f;

        private void Awake()
        {
            var mf = GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) { enabled = false; return; }
            if (_hinge == null) _hinge = transform.parent;
            if (_hinge == null) { enabled = false; return; }

            // 메시를 <b>제 것으로 한 벌 뜬다</b>. 원본을 건드리면 같은 메시를 쓰는
            // 다른 물건까지 함께 휜다.
            _mesh = Instantiate(mf.sharedMesh);
            _mesh.name = mf.sharedMesh.name + "_들리는것";
            _mesh.MarkDynamic();
            mf.sharedMesh = _mesh;

            var baseV = _mesh.vertices;
            _hingeBase = new Vector3[baseV.Length];
            _work = new Vector3[baseV.Length];

            Matrix4x4 localToHinge = _hinge.worldToLocalMatrix * transform.localToWorldMatrix;
            _hingeToLocal = localToHinge.inverse;

            Vector3 along = _along.sqrMagnitude > 1e-6f ? _along.normalized : Vector3.forward;
            _near = float.MaxValue; _far = float.MinValue;
            for (int i = 0; i < baseV.Length; i++)
            {
                _hingeBase[i] = localToHinge.MultiplyPoint3x4(baseV[i]);
                float d = Vector3.Dot(_hingeBase[i], along);
                if (d < _near) _near = d;
                if (d > _far) _far = d;
            }
            if (_far - _near < 1e-4f) { enabled = false; return; }
        }

        /// <summary>0(누워 있음) ~ 1(다 들림). 들추는 장치가 매 프레임 넣어 준다.</summary>
        public void SetLift(float k)
        {
            k = Mathf.Clamp01(k);
            if (_mesh == null || Mathf.Abs(k - _lift) < 0.002f) return;
            _lift = k;

            Vector3 axis = _axis.sqrMagnitude > 1e-6f ? _axis.normalized : Vector3.right;
            Vector3 along = _along.sqrMagnitude > 1e-6f ? _along.normalized : Vector3.forward;
            float span = _far - _near;

            for (int i = 0; i < _hingeBase.Length; i++)
            {
                Vector3 h = _hingeBase[i];
                float u = Mathf.Clamp01((Vector3.Dot(h, along) - _near) / span);
                float w = Mathf.Pow(u, _bend);                    // 경첩 쪽은 늦게 따라온다
                var rot = Quaternion.AngleAxis(_maxAngle * k * w, axis);
                Vector3 p = rot * h;

                // 들린 자락이 잡은 쪽으로 조금 끌려온다 — 천은 늘어나지 않는다
                if (_draw > 0f) p -= along * (span * _draw * k * w);

                _work[i] = _hingeToLocal.MultiplyPoint3x4(p);
            }
            _mesh.vertices = _work;
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
        }
    }
}
