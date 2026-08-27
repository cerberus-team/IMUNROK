using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// 주인이 방을 나가야 손이 간다 — 그때까지는 잠가 둔다.
    ///
    /// 보료는 甲이 그 위에 앉아 있는 동안에는 들출 수 없다. 앉은 사람 밑을 뒤지는 그림도
    /// 우습거니와, 조사 순서 자체가 그가 나간 뒤부터다. 그래서 그가 나가면 풀린다.
    ///
    /// <b>왜 이벤트로 하지 않는가</b>: 예전에는 甲 쪽 배선(_onLeft)이 이 자물쇠를 풀었다.
    /// 그런데 甲은 마당 씬에, 보료는 실내 씬에 있다. 유니티는 씬을 건너뛰는 참조를 저장하지
    /// 못하므로 그 배선은 저장하는 순간 <b>조용히 끊겼다</b> — 보료가 영영 잠겨 있었다.
    /// 그래서 가리키는 방향을 뒤집었다. 잠긴 쪽이 스스로 <b>주인이 나갔는지</b>를 본다.
    /// 이름이나 참조를 저장하지 않으므로 끊길 자리가 없다.
    /// </summary>
    [RequireComponent(typeof(AshRake))]
    public class UnlockWhenGone : MonoBehaviour
    {
        private AshRake _rake;
        private BokdongController _owner;
        private bool _done;

        private void Start() { _rake = GetComponent<AshRake>(); }

        private void Update()
        {
            if (_done || _rake == null) return;

            // 주인은 늦게 나타날 수 있다(씬을 갈아 끼우는 중). 찾을 때까지 매번 다시 본다.
            if (_owner == null)
            {
                _owner = FindFirstObjectByType<BokdongController>();
                if (_owner == null) return;
            }

            if (!_owner.HasLeft) return;
            _rake.Unlock();
            _done = true;
        }
    }
}
