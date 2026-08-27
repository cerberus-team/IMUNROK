using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace IMUNROK.Gyeonu.EditorTools
{
    /// <summary>
    /// NPC 애니메이터 굽기 (2026-08-25). FBX 안의 클립을 <b>평평한 상태 판</b>으로 늘어놓는다.
    ///
    /// ■ 전이 조건을 하나도 만들지 않는 까닭
    ///   기획의 모션 규칙(문서 「23」)은 <b>우선순위</b>다. 그것을 전이 조건으로 옮기면
    ///   11개의 그래프에 같은 규칙이 열한 벌 복사되고, 한 인물만 어긋났을 때 그래프를 열어
    ///   눈으로 찾아야 한다. 그래서 그래프에는 상태만 두고 고르는 일은 <see cref="NpcActor"/> 가
    ///   <c>CrossFadeInFixedTime</c> 로 직접 한다. 규칙은 코드 한 곳에만 있다.
    ///
    /// ■ 원본 에셋을 건드리지 않는다 (CLAUDE.md 규칙)
    ///   클립은 전부 FBX 안에 있고 루프가 꺼져 있다. 임포트 설정을 고치는 대신
    ///   <b>자기 자신으로 가는 전이</b>로 잇는다 — 원본 폴더는 그대로 둔다.
    ///   되도는 것은 서 있기·걷기·말하기뿐이고, 한 번만 도는 것(GiveKey·Secret·Thank·Clap…)은
    ///   전이를 달지 않는다. <see cref="NpcActor"/> 가 길이를 보고 스스로 되돌아간다.
    ///
    /// ■ 클립 이름 → 상태 이름
    ///   <c>FirstJiknyeo_Mother_Meshy_SmartRig|FirstJiknyeo_Mother_SitIdle_Hybrid</c> → <c>SitIdle</c>
    ///   리그 이름·인물 이름·<c>_Final</c>/<c>_Hybrid</c> 꼬리를 떼면 문서에 적힌 모션 이름이 남는다.
    /// </summary>
    public static class NpcRig
    {
        /// <summary>되도는 상태인가 — 이름에 이것들이 들어 있으면 자기 전이를 단다.</summary>
        static readonly string[] LoopKeys = { "Idle", "Walk", "Talk" };

        /// <summary>
        /// 그 인물의 애니메이터를 만든다(있으면 그대로 쓴다). 없는 클립이 있으면 <paramref name="missing"/> 에 남긴다.
        /// </summary>
        public static AnimatorController Build(string npcName, string modelPath, string controllerPath,
                                               IEnumerable<string> wanted, List<string> missing)
        {
            var clips = CollectClips(modelPath, npcName);
            if (clips.Count == 0)
            {
                Debug.LogError("[NPC] 클립을 찾지 못했다: " + modelPath);
                return null;
            }

            if (wanted != null && missing != null)
                foreach (var w in wanted)
                    if (!string.IsNullOrEmpty(w) && !clips.ContainsKey(w)) missing.Add(npcName + "." + w);

            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (ac != null)
            {
                // 멱등 — 이미 있으면 다시 굽지 않는다.
                // ⚠️ 다만 <b>상태 수가 모자라면</b> 다시 굽는다. 견우의 애니메이터는 구조 확인 때
                //    Idle 하나만 두고 만들었다. 그 판을 그대로 두면 GiveKey·LookAround·Walk 가
                //    "상태가 없다"로 조용히 버려진다 (2026-08-25).
                if (ac.layers[0].stateMachine.states.Length >= clips.Count) return ac;
                Debug.Log("[NPC] " + npcName + " 애니메이터가 " + ac.layers[0].stateMachine.states.Length +
                          "상태뿐이라 " + clips.Count + "상태로 다시 굽는다.");
                AssetDatabase.DeleteAsset(controllerPath);
            }

            ac = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var sm = ac.layers[0].stateMachine;

            bool first = true;
            foreach (var kv in clips)
            {
                var st = sm.AddState(kv.Key);
                st.motion = kv.Value;
                st.writeDefaultValues = true;
                if (first) { sm.defaultState = st; first = false; }

                if (!IsLoop(kv.Key)) continue;
                // 끝에서 살짝 겹쳐 자기 자신으로 — 이음매가 튀지 않는다.
                var loop = st.AddTransition(st);
                loop.hasExitTime = true;
                loop.exitTime = 0.97f;
                loop.duration = 0.06f;
                loop.hasFixedDuration = true;
            }

            EditorUtility.SetDirty(ac);
            return ac;
        }

        /// <summary><see cref="NpcActor.motions"/> 에 구울 상태 표.</summary>
        public static NpcActor.Motion[] MotionTable(string npcName, string modelPath)
        {
            var clips = CollectClips(modelPath, npcName);
            var list = new List<NpcActor.Motion>();
            foreach (var kv in clips)
                list.Add(new NpcActor.Motion { state = kv.Key, length = kv.Value.length, loops = IsLoop(kv.Key) });
            return list.ToArray();
        }

        /// <summary>그 인물이 실제로 가진 모션 이름 전부 — 검증 보고에 쓴다.</summary>
        public static List<string> StateNames(string npcName, string modelPath)
        {
            var list = new List<string>(CollectClips(modelPath, npcName).Keys);
            list.Sort();
            return list;
        }

        static bool IsLoop(string state)
        {
            foreach (var k in LoopKeys) if (state.Contains(k)) return true;
            return false;
        }

        static SortedDictionary<string, AnimationClip> CollectClips(string modelPath, string npcName)
        {
            var map = new SortedDictionary<string, AnimationClip>();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(modelPath))
            {
                var clip = o as AnimationClip;
                if (clip == null || clip.name.StartsWith("__preview__")) continue;
                string s = StateNameOf(clip.name, npcName);
                if (string.IsNullOrEmpty(s) || map.ContainsKey(s)) continue;
                map[s] = clip;
            }
            return map;
        }

        static string StateNameOf(string clipName, string npcName)
        {
            int bar = clipName.LastIndexOf('|');
            string s = bar >= 0 ? clipName.Substring(bar + 1) : clipName;
            if (s.StartsWith(npcName + "_")) s = s.Substring(npcName.Length + 1);
            if (s.EndsWith("_Final")) s = s.Substring(0, s.Length - 6);
            else if (s.EndsWith("_Hybrid")) s = s.Substring(0, s.Length - 7);
            return s;
        }
    }
}
