using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IMUNROK.Gyeonu.EditorTools
{
    /// <summary>
    /// NPC 설치 (2026-08-25). 지금은 <b>올해의 견우 한 사람</b>만 붙인다 — 구조 확인이 목적이다.
    ///
    /// ■ 하는 일 세 가지
    ///   ① <see cref="NpcProfile"/> 에셋 — 성격·프롬프트·신뢰도 구간별 태도 (문서 「24. 올해의 견우」)
    ///   ② 애니메이터 — 서 있는 모습(Idle)만. 모션 운용 원칙(문서 「23」)은 따로 붙일 일이다
    ///   ③ 마을 씬(<c>Gyeonu.unity</c>)에 <b>이미 서 있는</b> 견우에게 대화를 붙인다
    ///
    /// ■ ⚠️ 자리는 우리가 정하지 않는다
    ///   NPC 11인은 이미 사람 손으로 마을에 세워져 있다(견우는 집 마당 (44.59, 0, 40.70),
    ///   몸을 190° 로 틀어 대문을 본다). 그 좌표·각도는 눈으로 맞춘 것이라 코드가 다시 계산하면
    ///   반드시 어긋난다. 그래서 <b>찾아서 붙이기만</b> 한다 — 옮기지도, 돌리지도 않는다.
    ///   씬에 없을 때만 마당 한가운데에 새로 세운다.
    ///
    /// ■ 멱등하다
    ///   다시 눌러도 같은 결과다. 이미 있는 <b>프로필의 글은 덮어쓰지 않는다</b> —
    ///   인스펙터에서 말투를 고쳐 놓고 메뉴를 다시 누르는 일이 흔하다.
    ///   프롬프트를 문서 그대로 되돌리고 싶으면 「견우 프로필 문서대로 되돌리기」를 쓴다.
    ///
    /// ■ 원본 에셋을 건드리지 않는다
    ///   Idle 클립은 FBX 안에 있고 루프가 꺼져 있다. 클립을 복제하거나 임포트 설정을 고치는
    ///   대신 <b>상태에서 자기 자신으로 가는 전이</b>로 잇는다 — 원본 폴더는 그대로 둔다.
    /// </summary>
    public static class NpcSetup
    {
        const string NpcFolder = "Assets/_Project/Gyeonu/Npc";
        const string ProfilePath = NpcFolder + "/Npc_Gyeonu.asset";
        const string ControllerPath = NpcFolder + "/Npc_Gyeonu.controller";
        const string ModelPath = "Assets/Unity_Final_Characters_Textured/Gyeonu/Gyeonu.fbx";
        const string VillageScene = "Assets/_Project/Gyeonu/Scenes/Gyeonu.unity";

        /// <summary>씬에 이미 서 있는 오브젝트 이름 (FBX 이름 그대로).</summary>
        const string NpcName = "Gyeonu";

        /// <summary>씬에 아직 없을 때만 쓰는 예비 자리 — 견우 집 마당 한가운데.
        /// 담장 x 37.6~56.9 · z 36.1~50.5, 대문은 남쪽 x=47.1.</summary>
        static readonly Vector3 YardPos = new Vector3(47.0f, 0f, 41.6f);
        /// <summary>대문(남쪽)을 바라보고 선다 — 들어오는 사람과 눈이 마주친다.</summary>
        const float YardYaw = 180f;

        [MenuItem("Tools/이문록/NPC/① 견우 프로필·애니메이터 만들기")]
        public static void MakeAssets()
        {
            EnsureFolder(NpcFolder);
            var profile = EnsureProfile();
            EnsureController();
            AssetDatabase.SaveAssets();
            Selection.activeObject = profile;
            Debug.Log("[NPC] 프로필·애니메이터 준비 완료 — " + ProfilePath);
        }

        [MenuItem("Tools/이문록/NPC/② 마을 견우에게 대화 붙이기")]
        public static void AttachGyeonuInVillage()
        {
            EnsureFolder(NpcFolder);
            var profile = EnsureProfile();
            var controller = EnsureController();

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != VillageScene)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(VillageScene);
            }

            var go = FindPlaced(NpcName, ModelPath);
            bool made = false;
            if (go == null)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
                if (model == null) { Debug.LogError("[NPC] 견우 모델이 없다: " + ModelPath); return; }
                go = (GameObject)PrefabUtility.InstantiatePrefab(model);
                go.name = NpcName;
                go.transform.SetPositionAndRotation(Ground(YardPos), Quaternion.Euler(0f, YardYaw, 0f));
                made = true;
            }

            Attach(go, profile, controller);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = go;
            Debug.Log("[NPC] 견우에게 대화를 붙였다 — " + go.transform.position + " / " + go.transform.eulerAngles.y.ToString("F0") + "°"
                      + (made ? " (씬에 없어 새로 세웠다)" : " (이미 서 있던 자리 그대로)") + ". 씬을 저장하세요.");
        }

        /// <summary>
        /// 이미 세워 둔 NPC에 대화 부품만 얹는다. <b>자리·각도는 손대지 않는다.</b>
        /// 프리팹 연결도 끊지 않는다 — 컴포넌트 추가는 인스턴스 덧붙임(override)으로 남고
        /// 원본 FBX는 그대로다.
        /// </summary>
        static void Attach(GameObject go, NpcProfile profile, AnimatorController controller)
        {
            var anim = go.GetComponent<Animator>();
            if (anim == null) anim = Undo.AddComponent<Animator>(go);
            if (anim.runtimeAnimatorController == null) anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            // 몸통 — 조준(레이캐스트)도 받고 통과도 막는다.
            // ⚠️ 정수리(1.83m)까지 덮어야 한다. 1.76m로 잡았더니 **플레이어 눈높이(1.78m)에서 쏜
            //    수평 레이가 머리 위로 지나가** 정면에 두고도 조준이 되지 않았다(2026-08-25 실측).
            var cap = go.GetComponent<CapsuleCollider>();
            if (cap == null) cap = Undo.AddComponent<CapsuleCollider>(go);
            cap.center = new Vector3(0f, 0.94f, 0f);
            cap.height = 1.92f;
            cap.radius = 0.32f;

            var talk = go.GetComponent<NpcDialogue>();
            if (talk == null) talk = Undo.AddComponent<NpcDialogue>(go);
            talk.profile = profile;
            talk.displayName = profile.displayName;
            talk.focusDistance = profile.talkDistance;
            talk.transitionTime = 0.55f;
            // 배치안 A 확정 (2026-08-25) — 판은 왼쪽 아래, NPC는 오른쪽.
            // 다시 저울질할 일이 생기면 Tools ▸ 이문록 ▸ 대화 검증 에서 갈아 끼워 볼 수 있다.
            talk.layout = DialogueLayout.A_좌측판;
            // 대화창은 화면 아래를 덮는다 — 비네트까지 짙으면 판의 아랫단이 어두워 읽기 어렵다
            talk.dimStrength = 0.35f;
            EditorUtility.SetDirty(go);
        }

        /// <summary>이름 또는 원본 FBX 경로로 씬에 이미 있는 인물을 찾는다 (이름을 바꿔 두었을 수도 있다).</summary>
        static GameObject FindPlaced(string name, string modelPath)
        {
            var byName = GameObject.Find(name);
            if (byName != null) return byName;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.parent != null) continue;
                if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) == modelPath)
                    return t.gameObject;
            }
            return null;
        }

        [MenuItem("Tools/이문록/NPC/견우 프로필 문서대로 되돌리기")]
        public static void ResetProfileText()
        {
            var profile = EnsureProfile();
            WriteGyeonuText(profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log("[NPC] 견우 프로필을 최종기획안 11차 「24」 그대로 되돌렸다.");
        }

        // ─────────────────────────────────────────────────────────
        static NpcProfile EnsureProfile()
        {
            var p = AssetDatabase.LoadAssetAtPath<NpcProfile>(ProfilePath);
            if (p != null) return p;               // 이미 있으면 글을 덮어쓰지 않는다
            p = ScriptableObject.CreateInstance<NpcProfile>();
            WriteGyeonuText(p);
            AssetDatabase.CreateAsset(p, ProfilePath);
            return p;
        }

        /// <summary>문서 「24. 올해의 견우」를 그대로 옮긴다. 숫자·금지 항목을 임의로 늘리지 않는다.</summary>
        static void WriteGyeonuText(NpcProfile p)
        {
            p.npcId = NpcId.Gyeonu;
            p.displayName = "견우";
            p.talkVerb = "말 걸기";
            p.eyeHeight = 1.62f;
            p.talkDistance = 1.65f;
            p.useTrustBands = true;
            p.gradeTalk = true;

            p.openingLine = "…예. 무슨 일이십니까.";

            p.persona =
"너는 조선 성하리 마을의 스무 살 평민 사내다. 이름 대신 '올해의 견우'로 불린다.\n" +
"말을 거는 상대는 한양에서 내려온 나그네다. 관인이 아니다 — 관아 사람으로 대하지 마라.\n" +
"\n" +
"[말투]\n" +
"- 1인칭은 '저'. 누구에게나 공손하다.\n" +
"- 문장이 매우 짧다. 한 번에 두 문장을 넘기지 않는다.\n" +
"- 침묵과 망설임이 많다. 말끝을 흐리고 '…'을 자주 쓴다.\n" +
"- 조선 말투를 쓰되 알아듣기 어려운 고어는 쓰지 않는다.\n" +
"\n" +
"[성격과 처지]\n" +
"- 성실하고 우직하다. 변명하지 않아 오히려 의심을 산다.\n" +
"- 마을 대부분이 너를 선아 실종의 범인으로 여긴다. 관아에도 불려 갔다 왔다.\n" +
"- 억울하지만 그 억울함을 길게 늘어놓지 않는다.\n" +
"\n" +
"[네가 아는 것]\n" +
"- 선아와 함께 마을을 떠나기로 한 계획.\n" +
"- 구멍이 뚫린 종이 한 장(타공 지도)이 있다는 것. 네가 지니고 있다.\n" +
"- 칠석날 밤 옛길 초입까지 갔다가 혼자 돌아온 일.\n" +
"\n" +
"[네가 모르는 것 — 아는 척하지 마라]\n" +
"- 타공 지도의 해독법과 목적지. 선아가 알아냈고, 선아 집에 실마리가 있으리라 짐작만 한다.\n" +
"- 선아가 지금 어디 있는지. 전혀 모른다.\n" +
"- 누가 선아를 데려갔는지. 짐작조차 없다.\n" +
"\n" +
"[절대 금지 — 가장 중요하다]\n" +
"- 선아가 어디 있는지 안다고 말하지 마라.\n" +
"- 수령이나 관아 사람을 먼저 의심하지 마라. 범인을 지목하지 마라.\n" +
"- 아래 [지금의 태도]가 허락하지 않은 것은 절대 먼저 꺼내지 마라. 특히 타공 지도는\n" +
"  70 이상 구간의 태도가 오기 전에는 있다는 사실조차 말하지 않는다.\n" +
"- \"말하면 선아가 죄인이 된다\"는 이유는 쓰지 마라.\n" +
"- 모르는 것은 지어내지 말고 모른다고 하라.\n" +
"\n" +
"[대답 형식 — 반드시 지켜라]\n" +
"첫 줄에 상대가 방금 한 말의 등급을 네 가지 중 하나로 적는다. 다른 말은 붙이지 않는다.\n" +
"  [등급:호의]  선아를 걱정하거나, 너를 믿겠다는 태도이거나, 너를 범인 취급하지 않는 말\n" +
"  [등급:중립]  사실 확인, 장소·시간·사람을 묻는 말\n" +
"  [등급:압박]  왜 말하지 않았느냐, 그날 밤 어디 있었느냐 하고 몰아붙이는 말\n" +
"  [등급:모욕]  네가 범인이다, 네가 선아를 해쳤다, 비웃거나 욕하는 말\n" +
"그 다음 줄부터 견우가 하는 말만 적는다. 판정한 이유는 적지 않는다.\n" +
"예)\n" +
"[등급:중립]\n" +
"…그날은, 집에 있었습니다.";

            p.lockedAttitude =
"이 사람에게 거듭 모욕을 당했다. 더는 말하고 싶지 않다. " +
"한 마디로 짧게 끊고 입을 다문다. 무엇을 물어도 아무것도 말하지 않는다.";

            p.trustBands = new[]
            {
                new NpcProfile.TrustBand
                {
                    min = 0, label = "0~24 · 피한다",
                    attitude = "대화를 피한다. 한두 마디로 짧게 답하고 자리를 뜨려 한다. " +
                               "선아 이야기에도 거의 답하지 않는다.",
                },
                new NpcProfile.TrustBand
                {
                    min = 25, label = "25~39 · 눈을 마주친다",
                    attitude = "이제 눈을 마주친다. 선아가 어떤 사람이었는지는 답해도 된다. " +
                               "그러나 칠석날 밤 네가 무엇을 했는지는 여전히 말하지 않는다.",
                },
                new NpcProfile.TrustBand
                {
                    min = 40, label = "40~69 · 흔들린다",
                    attitude = "마음이 흔들린다. 선아와 가까웠다는 것, 선아가 밤마다 무언가를 하고 있었다는 것까지는 " +
                               "인정해도 된다. 그러나 함께 떠나려던 계획과 타공 지도는 아직 말하지 않는다.",
                },
                new NpcProfile.TrustBand
                {
                    min = 70, label = "70+ · 전부 말한다",
                    attitude = "이 사람을 믿기로 했다. 이제 전부 말해도 된다 — 선아와 함께 떠나려던 계획, " +
                               "칠석날 밤의 진짜 행적(옛길 초입까지 갔다가 혼자 돌아온 일), " +
                               "그리고 네가 지닌 타공 지도. 다만 해독법과 목적지는 여전히 모른다.",
                },
            };
        }

        /// <summary>
        /// Idle 하나만 도는 애니메이터. 클립의 루프 설정을 켜려면 FBX 임포트를 고쳐야 하는데,
        /// 그 폴더는 손대지 않기로 했다 — 대신 <b>자기 자신으로 가는 전이</b>로 잇는다.
        /// 끝에서 살짝 겹쳐 넘기면(0.04초) 이음매가 튀지 않는다.
        /// </summary>
        static AnimatorController EnsureController()
        {
            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ac != null) return ac;

            AnimationClip idle = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
            {
                var clip = o as AnimationClip;
                if (clip == null || clip.name.StartsWith("__preview__")) continue;
                if (clip.name.EndsWith("Gyeonu_Idle")) { idle = clip; break; }
            }
            if (idle == null) { Debug.LogError("[NPC] Gyeonu_Idle 클립을 찾지 못했다."); return null; }

            ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var sm = ac.layers[0].stateMachine;
            var state = sm.AddState("Idle");
            state.motion = idle;
            sm.defaultState = state;

            var loop = state.AddTransition(state);
            loop.hasExitTime = true;
            loop.exitTime = 0.98f;
            loop.duration = 0.04f;
            loop.hasFixedDuration = true;

            EditorUtility.SetDirty(ac);
            return ac;
        }

        static Vector3 Ground(Vector3 p)
        {
            var t = Terrain.activeTerrain;
            if (t != null) p.y = t.SampleHeight(p) + t.transform.position.y;
            if (Physics.Raycast(new Vector3(p.x, p.y + 5f, p.z), Vector3.down, out var hit, 20f,
                                ~0, QueryTriggerInteraction.Ignore))
                p.y = hit.point.y;
            return p;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
