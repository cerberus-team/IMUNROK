using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>알아본 뒤에야 열린다</b> — 단서 하나를 얻으면 풀리는 자물쇠.
    ///
    /// <b>왜 필요한가.</b> 문갑에는 자물쇠가 달려 있다. 그런데 여태 그것은 <b>그림</b>이었다 —
    /// 서랍은 처음부터 그냥 열렸고(<c>_lockedAtStart = false</c>), 자물쇠는 옆에 붙어
    /// 읽히기만 하는 쪽지였다. 잠긴 것을 열었다는 느낌이 없으니 그 안에서 나온 문서도
    /// <b>그냥 거기 있던 것</b>이 된다.
    ///
    /// <b>이 집의 자물쇠는 이미 부서져 있다.</b> 그것이 이 사건의 요점이다 —
    /// 고리가 <b>안쪽으로</b> 휘었으니 밖에서 딴 것이 아니라 안에 있던 사람이 열쇠 없이
    /// 비틀어 뜯은 것이고(J15), 그러므로 이 집에서 열쇠를 가진 사람은 지금 주인 행세를
    /// 하는 자가 <b>아니다</b>. 부서졌어도 고리에는 그대로 물려 있다. 그것을 알아보고
    /// 벗겨 내야 서랍이 빠진다.
    ///
    /// 그래서 순서가 이렇게 선다:
    ///   ① 서랍을 잡아당긴다 → 안 빠진다. 「자물쇠가 고리에 물려 있다」
    ///   ② 자물쇠를 살핀다 → J15. 부서진 것을 알아본다.
    ///   ③ 그제야 서랍이 빠진다.
    /// 단서를 <b>안 얻으면 못 여는</b> 것이 아니라, <b>알아보는 것이 곧 여는 것</b>이다.
    ///
    /// <b>왜 이벤트로 안 잇는가</b>: <see cref="UnlockWhenGone"/> 과 같은 까닭이다.
    /// 잠긴 쪽이 스스로 <b>수첩을 본다</b> — 씬을 건너뛰는 참조를 저장하지 않으므로
    /// 끊길 자리가 없다.
    ///
    /// 붙이는 곳: 잠가 둘 <see cref="AshRake"/> 와 같은 오브젝트.
    /// 그쪽 <c>_lockedAtStart</c> 를 켜 두어야 뜻이 있다.
    /// </summary>
    [RequireComponent(typeof(AshRake))]
    public class UnlockWhenClued : MonoBehaviour
    {
        [Tooltip("이 단서를 얻으면 풀린다")]
        [SerializeField] private string _clueKey = "J15";
        [Tooltip("어느 사건의 단서인가")]
        [SerializeField] private CaseId _case = CaseId.Case1_Onggojip;

        private AshRake _rake;
        private bool _done;

        private void Start() { _rake = GetComponent<AshRake>(); }

        private void Update()
        {
            if (_done || _rake == null || string.IsNullOrEmpty(_clueKey)) return;

            // 수첩은 늦게 설 수 있다. 설 때까지 매 칸 다시 본다 — 한 번 못 찾았다고
            // 영영 잠겨 있으면, 자물쇠가 아니라 <b>막아 둔 것</b>이 된다.
            var j = Journal.Instance;
            if (j == null || !j.HasClue(_case, _clueKey)) return;

            _rake.Unlock();
            _done = true;
        }
    }
}
