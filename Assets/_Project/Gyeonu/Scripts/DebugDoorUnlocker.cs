using UnityEngine;
using UnityEngine.InputSystem;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// ⚠️ **임시 디버그 장치 — 진짜 퍼즐·열쇠가 붙으면 통째로 지운다.**
    ///
    /// 비밀문(<see cref="LockedDoor"/>)에 아직 해제 조건이 없어 걸어 내려가 볼 수가 없어서 만든
    /// 우회 수단이다. **기존 잠금 로직은 손대지 않는다** — LockedDoor가 이미 공개해 둔
    /// <see cref="LockedDoor.Unlock"/> 를 부를 뿐이라, 나중에 퍼즐이 같은 함수를 부르면 된다.
    ///
    /// 【제거 방법 — 셋만 지우면 흔적이 남지 않는다】
    ///   ① 씬의 루트 오브젝트 `디버그_비밀문해제`
    ///   ② 이 파일 (DebugDoorUnlocker.cs)
    ///   ③ GwanaOfficeBuilder 의 `BuildDebugUnlocker()` 호출 한 줄과 그 메서드
    /// LockedDoor·FoldingScreen 쪽은 고칠 것이 없다.
    ///
    /// 【쓰는 법】 셋 중 아무거나
    ///   · Play 중 <b>F1</b> — 즉시 해제 (F2 = 다시 잠금)
    ///   · 인스펙터에서 LockedDoor 의 `locked` 체크 해제
    ///   · 메뉴 `Tools ▸ 이문록 ▸ 관아 집무실 ▸ 비밀문 임시 해제 / 다시 잠그기`
    /// </summary>
    public class DebugDoorUnlocker : MonoBehaviour
    {
        [Tooltip("이 키를 누르면 씬의 모든 잠긴 문이 풀린다")]
        public Key unlockKey = Key.F1;
        [Tooltip("이 키를 누르면 다시 잠근다 (열려 있던 문은 닫지 않는다)")]
        public Key relockKey = Key.F2;
        [Tooltip("Play 시작과 동시에 풀어 둔다 — 반복 검증할 때 편하다")]
        public bool unlockOnStart = false;

        void Start()
        {
            if (unlockOnStart) SetLocked(false);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb[unlockKey].wasPressedThisFrame) SetLocked(false);
            else if (kb[relockKey].wasPressedThisFrame) SetLocked(true);
        }

        /// <summary>
        /// 씬의 모든 LockedDoor 잠금 상태를 바꾼다. 에디터 메뉴도 이걸 부른다.
        /// 은하담 암문(<see cref="SecretStoneDoor"/>)의 **퍼즐 조건**도 같은 키로 함께 푼다 —
        /// 지금은 puzzleFlag가 비어 있어 아무 일도 하지 않지만, 퍼즐이 붙는 순간
        /// F1 하나로 두 문이 다 열리게 된다 (2026-08-20).
        /// </summary>
        public static int SetLocked(bool locked)
        {
            var doors = Object.FindObjectsByType<LockedDoor>(FindObjectsInactive.Include,
                                                            FindObjectsSortMode.None);
            foreach (var d in doors)
            {
                if (locked) d.locked = true;
                else d.Unlock();          // 퍼즐이 쓸 진입점을 그대로 사용한다
            }

            var stones = Object.FindObjectsByType<SecretStoneDoor>(FindObjectsInactive.Include,
                                                                   FindObjectsSortMode.None);
            foreach (var s in stones)
            {
                if (string.IsNullOrEmpty(s.puzzleFlag)) continue;
                if (locked) GyeonuWorld.Set(s.puzzleFlag, false);
                else s.SolvePuzzle();     // 퍼즐이 쓸 진입점을 그대로 사용한다
            }

            int n = doors.Length + stones.Length;
            if (Application.isPlaying && n > 0)
                DebugToast.Show(locked ? "[디버그] 비밀문을 다시 잠갔다"
                                       : "[디버그] 비밀문 잠금 해제 — 문을 클릭해 열 것", 2.5f);
            return n;
        }
    }
}
