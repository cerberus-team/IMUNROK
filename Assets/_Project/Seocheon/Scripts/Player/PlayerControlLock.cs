// UI 가 열려 있는 동안 플레이어 이동·시점 회전을 잠급니다. 공통 스크립트는 수정하지 않고 enabled 만 토글합니다.
using System.Collections.Generic;
using IMUNROK.Common;
using UnityEngine;

namespace IMUNROK.Seocheon.Player
{
    /// <summary>
    /// 모달 UI 용 조작 잠금. 여러 곳에서 중첩 호출해도 안전하도록 참조 카운트로 관리합니다.
    ///
    /// 공통 파트(TempWalker / MouseInspector / DebugFlyCamera)는 ★수정하지 않고
    /// 컴포넌트의 enabled 만 껐다 켭니다. 끄기 전 상태를 기억해 두었다가 그대로 되돌립니다.
    /// </summary>
    public static class PlayerControlLock
    {
        private static readonly List<Behaviour> Suspended = new List<Behaviour>();
        private static int _depth;
        private static CursorLockMode _prevLock;
        private static bool _prevVisible;

        public static bool IsLocked { get { return _depth > 0; } }

        public static void Push()
        {
            _depth++;
            if (_depth > 1) return;   // 이미 잠겨 있음

            _prevLock = Cursor.lockState;
            _prevVisible = Cursor.visible;

            Suspended.Clear();
            Disable<TempWalker>();
            Disable<MouseInspector>();
            Disable<DebugFlyCamera>();
            Disable<PlayerCrouch>();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public static void Pop()
        {
            if (_depth == 0) return;
            _depth--;
            if (_depth > 0) return;

            for (int i = 0; i < Suspended.Count; i++)
                if (Suspended[i] != null) Suspended[i].enabled = true;
            Suspended.Clear();

            Cursor.lockState = _prevLock;
            Cursor.visible = _prevVisible;
        }

        /// <summary>씬 전환 등으로 균형이 깨졌을 때의 강제 복구.</summary>
        public static void ForceRelease()
        {
            _depth = 1;
            Pop();
        }

        private static void Disable<T>() where T : Behaviour
        {
            // UI 를 여는 순간에만 도는 탐색입니다(매 프레임 아님).
            T[] found = Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] == null || !found[i].enabled) continue;
                found[i].enabled = false;
                Suspended.Add(found[i]);
            }
        }
    }
}
