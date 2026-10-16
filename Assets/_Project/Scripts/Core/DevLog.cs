using System.Diagnostics;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>콘솔에 흘리는 혼잣말을 한 군데에서 잠근다.</b>
    ///
    /// 어전에서 조사청까지 한 번 걸어 나오면 콘솔이 <b>수십 줄</b>로 찬다 —
    /// 「단서 기록」, 「사건 진입」, 「봉서함 활성화」… 하나하나는 지을 때 필요했던 말이다.
    /// 그런데 그 말들이 남아 있으면 <b>정작 봐야 할 것이 묻힌다</b>. 경고 한 줄이
    /// 스무 줄의 혼잣말 사이에 끼면 없는 것과 같다.
    ///
    /// 그렇다고 지울 것은 아니다. 어디까지 갔는지 짚어 볼 때 이만한 것이 없다.
    /// 그래서 <b>잠그되 열 수 있게</b> 둔다 —
    /// <c>[Conditional]</c> 이라 <c>IMUNROK_LOG</c> 가 없으면 부르는 자리까지 컴파일에서
    /// 통째로 빠진다. 문자열을 만드는 값조차 치르지 않는다.
    ///
    /// <b>켜는 법</b>: Edit ▸ Project Settings ▸ Player ▸ Scripting Define Symbols 에
    /// <c>IMUNROK_LOG</c> 를 더한다.
    ///
    /// <b>경고와 오류는 여기 없다.</b> 그것은 「내가 여기까지 왔다」가 아니라
    /// 「무엇이 잘못됐다」이므로 늘 보여야 한다 — <c>Debug.LogWarning</c> 을 그대로 쓴다.
    /// </summary>
    public static class DevLog
    {
        [Conditional("IMUNROK_LOG")]
        public static void Note(string message) => UnityEngine.Debug.Log(message);

        [Conditional("IMUNROK_LOG")]
        public static void Note(string message, Object context) => UnityEngine.Debug.Log(message, context);
    }
}
