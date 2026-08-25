using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>묻는 것이 아니라 시키는 것.</b> 심문 중에 어사가 내리는 명령 하나.
    ///
    /// 여태 심문에서 할 수 있는 일은 셋뿐이었다 — 말하기 · 추천 질문 · 증거 제시.
    /// <b>셋 다 묻는 일이다.</b> 그런데 마루에 앉아 하는 일 가운데는 묻지 않는 것도 있다.
    /// "소매를 걷으시오" 는 물음이 아니라 명령이고, 돌아오는 것은 대답이 아니라 <b>몸</b>이다.
    /// 이것을 추천 질문 틀에 우겨 넣으면 <b>답이 돌아와야 하는 자리에 돌아올 답이 없다</b> —
    /// AI가 없는 말을 지어내거나, 목업이 빈 줄을 내놓는다.
    ///
    /// 그래서 조작판에 줄을 하나 더 둔다. 이 부품을 인물에게 붙여 두면 그 인물을 심문하는
    /// 동안 <b>붉은 명령 단추</b>로 뜬다.
    ///
    /// <b>근거가 없으면 아예 안 뜬다</b>(<see cref="Available"/>). 어사라도 까닭 없이 남의
    /// 몸을 뒤지지는 못한다 — 대장에 적힌 한 줄을 <b>먼저 읽어야</b> 소매를 걷으라 할 수 있다.
    /// 단추가 안 보이는 것이 곧 "아직 근거가 없다"는 말이다.
    /// </summary>
    public abstract class InterrogationOrder : MonoBehaviour
    {
        /// <summary>단추에 뜨는 말.</summary>
        public abstract string Label { get; }

        /// <summary>지금 이 명령을 내릴 수 있나. 거짓이면 단추가 아예 안 뜬다.</summary>
        public abstract bool Available { get; }

        /// <summary>눌렸다. 자막이든 연출이든 각자 알아서 한다.</summary>
        public abstract void Run(InterrogationController who);
    }
}
