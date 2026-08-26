using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 문 앞에서 시작되는 퍼즐의 규약 (2026-08-23).
    ///
    /// <see cref="SecretStoneDoor"/> 는 조건(단서 → 밤)을 다 갖췄을 때 **문을 여는 대신**
    /// 이 인터페이스를 부른다. 퍼즐 쪽은 다 풀면 <see cref="SecretStoneDoor.SolvePuzzle"/> 로
    /// 플래그를 세우고 <see cref="SecretStoneDoor.Open"/> 을 부르면 된다 —
    /// 문 코드는 퍼즐이 무엇인지 전혀 모른다.
    /// </summary>
    public interface IDoorPuzzle
    {
        /// <summary>이미 풀렸는가. 참이면 문은 퍼즐을 건너뛰고 곧장 열린다.</summary>
        bool Solved { get; }

        /// <summary>퍼즐 시작 — 보통 포커스 모드 진입.</summary>
        void BeginPuzzle(GameObject actor);
    }
}
