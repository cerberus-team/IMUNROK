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

        private void Build()
        {
            var go = new GameObject("닫힘가리개");
            go.transform.SetParent(transform, true);

            Bounds b;
            if (_size != Vector3.zero) b = new Bounds(_center, _size);
            else if (!ModelBounds.TryGet(transform, out b) || b.size.sqrMagnitude < 0.0001f)
            {
                Object.Destroy(go);
                return;
            }

            go.transform.position = b.center;
            go.transform.rotation = transform.rotation;
            _shell = go.AddComponent<BoxCollider>();
            // 크기는 <b>제 자로</b> 적어야 한다. 받아온 세간은 뿌리에 1/100 짜리 배율이 걸려
            // 있어서, 월드에서 잰 0.41m 를 그대로 적으면 4mm 짜리가 선다(실제로 0이 나왔다).
            Vector3 ls = go.transform.lossyScale;
            _shell.size = new Vector3(
                Mathf.Max(0.02f, b.size.x - _shrink) / Mathf.Max(0.0001f, Mathf.Abs(ls.x)),
                Mathf.Max(0.02f, b.size.y - _shrink) / Mathf.Max(0.0001f, Mathf.Abs(ls.y)),
                Mathf.Max(0.02f, b.size.z - _shrink) / Mathf.Max(0.0001f, Mathf.Abs(ls.z)));
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
