using UnityEditor;
using UnityEngine;

namespace IMUNROK.Gyeonu.EditorTools
{
    /// <summary>
    /// NPC 손검증 도구 (2026-08-25). Play 중에 부른다.
    ///
    /// ■ 왜 있는가
    ///   NPC 11인은 여섯 씬에 흩어져 있고, 어떤 사람은 밤에만·어떤 사람은 구출 뒤에만 나타난다.
    ///   그 조건을 손으로 맞춰 가며 마을 끝에서 걸어오면 검증이 아니라 노동이 된다.
    ///   여기 모아 둔 것은 전부 <b>검증 전용</b>이고 씬에 아무것도 남기지 않는다.
    ///
    /// ■ <see cref="DialogueTestKit"/> 와 다른 점
    ///   그쪽은 배치안 A·B·C를 견주기 위한 것이라 견우 한 사람에 붙박여 있다.
    ///   여기는 <b>이름으로</b> 아무나 앞에 세운다.
    /// </summary>
    public static class NpcTestKit
    {
        /// <summary>그 이름의 NPC 앞에 서서 말을 건다.</summary>
        public static string Stand(string npcName, bool openTalk = true)
        {
            if (!Application.isPlaying) return "Play 중이 아니다";
            EditorApplication.isPaused = false;
            Application.runInBackground = true;

            var go = GameObject.Find(npcName);
            if (go == null) return "없는 이름: " + npcName;
            var npc = go.GetComponent<NpcDialogue>();
            if (npc == null) return npcName + " 에 NpcDialogue 가 없다";

            var walk = Object.FindFirstObjectByType<DebugWalkController>();
            if (walk == null) return "워커가 없다";

            // 얼굴 앞 2.4m. 몸을 튼 방향(정면)에서 다가선다 — 등 뒤로 돌아 들어가지 않는다.
            Vector3 face = npc.FocusPoint;
            Vector3 dir = go.transform.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.back;
            Vector3 stand = new Vector3(face.x, 0f, face.z) + dir.normalized * 2.4f;
            stand = NpcPatrol.Ground(stand, 40f, 80f);

            var cc = walk.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            walk.transform.position = stand + Vector3.up * 0.05f;
            Vector3 look = new Vector3(face.x, stand.y, face.z) - stand;
            walk.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
            if (cc != null) cc.enabled = true;
            walk.Pitch = 0f;

            if (!openTalk) return "섰다 @" + stand.ToString("F2");

            npc.Interact(walk.eye.gameObject);
            return "대화 시작 — " + npcName + " (" + npc.profile.displayName + ") @" + stand.ToString("F2");
        }

        /// <summary>지금 열린 대화에 한 마디 던진다.</summary>
        public static string Say(string text)
        {
            foreach (var d in Object.FindObjectsByType<NpcDialogue>(FindObjectsSortMode.None))
                if (d.Session != null) { d.Session.Ask(text); return "보냈다: " + text; }
            return "열린 대화가 없다";
        }

        /// <summary>지금 열린 대화의 마지막 줄.</summary>
        public static string Last()
        {
            foreach (var d in Object.FindObjectsByType<NpcDialogue>(FindObjectsSortMode.None))
                if (d.Session != null)
                    return (d.Session.Busy ? "(기다리는 중) " : "") + d.Session.CurrentNpcLine;
            return "열린 대화가 없다";
        }

        /// <summary>대화를 닫는다.</summary>
        public static string Close()
        {
            var walk = Object.FindFirstObjectByType<DebugWalkController>();
            var rig = walk != null && walk.eye != null ? walk.eye.GetComponent<DebugFocusRig>() : null;
            if (rig == null) return "리그가 없다";
            rig.ExitFocus();
            return "닫았다";
        }
    }
}
