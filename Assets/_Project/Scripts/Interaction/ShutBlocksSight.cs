using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 닫힌 세간은 <b>속을 못 짚게</b> 막는다.
    ///
    /// 장·문갑 같은 것은 문짝에만 콜라이더가 달려 있고 몸통에는 없다. 그래서 문짝을
    /// 살짝 비껴 겨누면 광선이 나무를 그대로 통과해 <b>닫힌 장 속의 수표</b>가 짚혔다.
    /// 문을 열어 본 적도 없는데 증거가 손에 들어오는 것이다.
    ///
    /// 여기서는 닫혀 있는 동안만 통짜 상자 하나를 세워 둔다. 그 상자는 이 세간 자신에
    /// 붙으므로, 눌러도 속이 아니라 <b>문</b>이 짚인다(같은 오브젝트의 DoorController).
    /// 문이 열리면 상자를 치운다 — 그제야 속에 손이 닿는다.
    ///
    /// 몸통 메시에 콜라이더를 다는 편이 간단해 보이지만 그러면 문을 열어도 계속 막는다.
    /// 막는 것은 <b>닫혀 있다는 사실</b>이지 나무가 아니다.
    /// </summary>
    [RequireComponent(typeof(DoorController))]
    public class ShutBlocksSight : MonoBehaviour
    {
        [Tooltip("가릴 크기(m). 비워 두면 이 세간의 겉을 재서 쓴다")]
        [SerializeField] private Vector3 _size = Vector3.zero;
        [Tooltip("가릴 자리(월드). 비워 두면 이 세간의 한가운데")]
        [SerializeField] private Vector3 _center = Vector3.zero;
        [Tooltip("겉보다 이만큼 줄여 세운다 — 문짝보다 안쪽에 서야 문이 먼저 짚힌다")]
        [SerializeField] private float _shrink = 0.01f;

        private DoorController _door;
        private BoxCollider _shell;
        private bool _wasOpen;

        private void Start() { _door = GetComponent<DoorController>(); }

        /// <summary>
        /// 이 세간의 겉을 <b>제 좌표로</b> 잰다.
        ///
        /// 월드에서 잰 상자(축에 나란하다)를 기울인 물건에 씌우면 가로세로가 뒤바뀐다.
        /// 메시 정점을 이 트랜스폼의 좌표로 옮겨 담으면 기울기와 무관하게 맞는다.
        /// </summary>
        private static bool LocalBounds(Transform root, out Bounds local)
        {
            local = new Bounds();
            bool first = true;
            var w2l = root.worldToLocalMatrix;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(false))
            {
                var m = mf.sharedMesh;
                if (m == null) continue;
                var mb = m.bounds;
                var mtx = w2l * mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = mtx.MultiplyPoint3x4(corner);
                    if (first) { local = new Bounds(p, Vector3.zero); first = false; }
                    else local.Encapsulate(p);
                }
            }
            return !first;
        }

        private void Build()
        {
            var go = new GameObject("닫힘가리개");
            go.transform.SetParent(transform, true);

            // 세간의 <b>제 좌표</b>로 잰 겉. 손으로 적어 준 값이 있으면 그것을 쓴다.
            Bounds b;
            if (_size != Vector3.zero) b = new Bounds(transform.InverseTransformPoint(_center), _size);
            else if (!LocalBounds(transform, out b) || b.size.sqrMagnitude < 0.0001f)
            {
                Object.Destroy(go);
                return;
            }

            // <b>세워 놓고 재지 말고, 재 놓고 세운다.</b>
            //
            // 여태 월드에서 잰 겉치수(축에 나란한 상자)를 <b>세간과 같이 기울인</b> 상자에
            // 그대로 적었다. 세간이 90도 돌아 있으면 가로와 세로가 뒤바뀐다 — 장롱은
            // 폭 0.55·높이 1.92 인데 가리개는 <b>폭 1.90·높이 1.00</b> 으로 서서, 옆으로
            // 드러누운 채 통로를 1.9m 나 가로막았다. 장롱을 지나 문갑으로 못 가던 것이 이것이다.
            // 세간의 <b>제 좌표</b>로 재면 기울여도 어긋날 것이 없다.
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            _shell = go.AddComponent<BoxCollider>();
            _shell.center = b.center;
            _shell.size = new Vector3(Mathf.Max(0.02f, b.size.x - _shrink),
                                      Mathf.Max(0.02f, b.size.y - _shrink),
                                      Mathf.Max(0.02f, b.size.z - _shrink));

            // <b>막는 것은 눈이지 몸이 아니다.</b>
            //
            // 이 상자가 하려는 일은 "닫힌 장 속을 광선으로 짚지 못하게" 하는 것뿐이다.
            // 그런데 통짜 콜라이더로 두니 사람의 <b>걸음</b>까지 막았다. 트리거로 세우면
            // 광선은 그대로 걸리고(Physics.queriesHitTriggers 가 켜져 있다) 몸은 지나간다 —
            // 걸음 판정은 트리거를 무시한다(DebugFlyCamera.Slide).
            _shell.isTrigger = true;

            _wasOpen = _door.IsOpen;
            _shell.enabled = !_wasOpen;
        }

        /// <summary>
        /// 가리개는 <b>늦게</b> 세운다.
        ///
        /// 세간은 마당에 서 있는 동안 통째로 꺼져 있다(InteriorSceneSwap 이 실내 소품을
        /// 그때 끈다). 꺼진 것에서는 겉을 잴 수 없으므로, 시작할 때 세우려 들면 크기를 0으로
        /// 얻고 아무것도 못 막는다. 그래서 이 세간이 처음 켜진 뒤에 한 번 세운다.
        /// </summary>
        private void Update()
        {
            if (_door == null) return;

            if (_shell == null)
            {
                Build();
                if (_shell == null) return;      // 아직 꺼져 있다 — 다음에 다시
            }

            if (_door.IsOpen == _wasOpen) return;
            _wasOpen = _door.IsOpen;
            _shell.enabled = !_wasOpen;
        }
    }
}
