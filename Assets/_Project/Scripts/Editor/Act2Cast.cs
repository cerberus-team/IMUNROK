using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>2막 사람들을 들인다.</b> 메뉴: [이문록 ▸ 관아 ▸ ⑱ 2막 사람 들이기]
    ///
    /// 새로 받은 다섯(서리·옹덕구·늙은하인·마름·복동)이 <b>다 같은 병</b>을 앓고 있다.
    /// 아내 때와 한 글자도 다르지 않다:
    ///
    ///   · <b>아바타가 NoAvatar</b> — 애니메이터가 뼈를 못 찾아 아무 동작도 안 돈다.
    ///   · 클립 이름에 <b>Armature| 이 붙어 있고</b> 도는 것이 하나도 없다.
    ///   · 키가 <b>1.30 ~ 1.88</b> 로 제각각이다. 그대로 세우면 난쟁이와 거인이 나란히 선다.
    ///   · 재질이 기본 <b>Lit</b> 이라 하얀 사람으로 뜬다.
    ///
    /// 손으로 고치면 사람마다 네 군데씩 스무 군데다. 한 번에 잰다.
    ///
    /// <b>키는 몸피로 재면 안 된다.</b> 바인드 자세는 팔이 벌어져 있어 참값이 아니고,
    /// SkinnedMeshRenderer.bounds 는 갱신이 늦어 옛 값을 문다(아내 때 2.19 라고 나왔는데
    /// 실제로는 1.76 이었다). <b>정수리 뼈와 발 뼈 사이</b>를 재야 한다.
    ///
    /// <b>서서 기다리나 앉아서 기다리나는 클립이 정한다.</b> 새로 받은 것 가운데
    /// 옹덕구와 복동에는 선 자세(Idle)가 없고 {앉기·앉은 채·일어나기·걷기} 만 있다.
    /// 그 넷은 <b>의자에 앉아 심문받는 사람</b>의 몸짓이다. 그래서 Idle 이 있는 사람은
    /// 뜰에 서서 기다리고, 없는 사람은 뜰에 앉아서 기다린다 — 죄인이 뜰에 꿇려 앉아
    /// 기다리는 것이 되레 옳으니 억지가 아니다. 나중에 Idle 을 뽑아 넣으면 이 도구가
    /// 알아서 세운다.
    ///
    /// 마름·복동·서리는 아직 씬에 몸이 없다. <b>이미 선 사람을 통째로 베껴</b> 몸만
    /// 갈아 끼운다 — 심문·부름·발붙임 같은 부품이 스무 칸씩 이어져 있어서, 새로 붙이면
    /// 어느 한 칸을 빠뜨리기 십상이다.
    ///
    /// 두 번 눌러도 두 벌이 안 선다.
    /// </summary>
    public static class Act2Cast
    {
        private const string Art = "Assets/_Project/Onggojip/Art/Characters/";

        /// <summary>
        /// 뜰에서 기다리는 줄이 서는 x. 甲·乙 을 나란히 둔다 — <b>둘은 얼자 형제라</b>
        /// 닮았으되 같은 사람이 아니다. 그래서 몸도 서로 다른 것을 쓴다.
        /// </summary>
        private const float WaitX = 5.60f;

        private class Who
        {
            public string 씬이름;      // 씬에 선 사람 오브젝트
            public string fbx;         // Art 아래 상대 경로
            public string 재질;        // Art 아래 상대 경로. 비면 그림으로 새로 만든다
            public string 그림;        // 재질을 만들 때 쓸 텍스처
            public string 자료;        // 심문 자료(Data 아래). 베껴 세울 때 남의 것을 물려받지 않도록 못 박는다
            public float 키;           // 정수리~발
            public float 대기z;        // 뜰에서 서는 자리. NaN 이면 뜰에 안 선다
            public string 베낄것;      // 씬에 없을 때 베낄 사람
        }

        private static readonly Who[] Cast =
        {
            new Who { 씬이름 = "서리",        fbx = "서리/Seori_Merged.fbx",
                      그림 = "서리/Meshy_AI_Seonbi_in_a_Hanbok_wi_0825101955_texture.png",
                      자료 = "Seori_Interrogation", 키 = 1.66f, 대기z = float.NaN },

            // 甲 은 <b>복동</b>이다 — 스무 해 이 집 문서를 다루던 얼자 출신 종이 한 달 전부터
            // 주인 자리에 앉아 있다. 그러니 甲 이 입는 몸은 복동의 것이라야 한다.
            new Who { 씬이름 = "甲",          fbx = "복동/Bokdong_Act2.fbx", 재질 = "복동/복동_보라_Mat.mat",
                      자료 = "Gap_Interrogation", 키 = 1.64f, 대기z = -4.50f },

            // 乙 이 <b>진짜 옹덕구</b>다. 온 마을이 이 사람을 죽은 종 복동으로 안다.
            new Who { 씬이름 = "乙",          fbx = "옹덕구/Ongdeokgu_Act2.fbx", 재질 = "옹덕구/옹덕구_병합_Mat.mat",
                      자료 = "EulOng_Interrogation", 키 = 1.66f, 대기z = -2.70f },

            new Who { 씬이름 = "아내",        fbx = "아내/Hanbok_Woman_Merged.fbx", 재질 = "아내/M_아내.mat",
                      자료 = "Wife_Interrogation", 키 = 1.60f, 대기z = -0.90f },
            new Who { 씬이름 = "늙은하인",     fbx = "늙은하인/Hain_Act2.fbx", 재질 = "늙은하인/M_늙은하인.mat",
                      자료 = "Servant_Interrogation", 키 = 1.60f, 대기z = 0.90f },
            new Who { 씬이름 = "마름",        fbx = "마름/Mareum_Act2.fbx", 재질 = "마름/마름_Mat.mat",
                      자료 = "Mareum_Interrogation", 키 = 1.68f, 대기z = 2.70f, 베낄것 = "늙은하인" },
        };

        /// <summary>
        /// 뜰에서 걷어낼 사람. <b>복동은 따로 서지 않는다</b> — 甲 이 복동이기 때문이다.
        /// 乙 은 "복동이는 저 안에 앉아 있소"라 외치는데 뜰에 복동이 또 서 있으면
        /// 그 외침이 헛말이 된다. 예전에 몸만 보고 한 사람으로 세워 두었던 것을 지운다.
        /// </summary>
        private static readonly string[] 걷어낼것 = { "복동" };

        /// <summary>돌아야 하는 클립.</summary>
        private static readonly string[] Loops = { "Idle", "Walking", "Running", "Sitting_Idle" };

        [MenuItem("이문록/관아/⑱ 2막 사람 들이기")]
        public static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.name.Contains("Gwana"))
            {
                Debug.LogWarning("[관아] 관아 씬을 열고 누르십시오(지금은 " + scene.name + ").");
                return;
            }

            var log = new System.Text.StringBuilder("[관아] 2막 사람들을 들인다\n");
            // 세우기 전에 먼저 걷어낸다 — 안 그러면 자리만 겹쳐 놓고 끝난다.
            foreach (var 이름 in 걷어낼것)
            {
                var 군더더기 = FindDeep(scene, 이름);
                if (군더더기 != null)
                {
                    Undo.DestroyObjectImmediate(군더더기.gameObject);
                    log.AppendLine("── " + 이름 + " 을 뜰에서 걷어냈다 — 甲 이 곧 복동이다");
                }
                var 자리 = FindDeep(scene, "뜰_대기_" + 이름);
                if (자리 != null) Undo.DestroyObjectImmediate(자리.gameObject);
            }

            foreach (var w in Cast)
            {
                log.AppendLine("── " + w.씬이름);
                var path = Art + w.fbx;
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                { log.AppendLine("   ※ 모델이 없다: " + path); continue; }

                Groom(path, w, log);
                var mat = Skin(w, log);
                var ac = Controller(path, w, log);
                Stand(scene, w, path, mat, ac, log);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(log.ToString());
        }

        // ── ㉠ 다듬기 ─────────────────────────────

        /// <summary>아바타·클립 이름·도는 것·키를 한 번에 바로잡는다.</summary>
        private static void Groom(string path, Who w, System.Text.StringBuilder log)
        {
            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi == null) return;

            bool dirty = false;
            if (mi.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                mi.animationType = ModelImporterAnimationType.Generic;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                dirty = true;
                log.AppendLine("   · 아바타를 만들게 했다 — 없으면 애니메이터가 뼈를 못 찾는다");
            }

            var src = mi.clipAnimations;
            if (src == null || src.Length == 0) src = mi.defaultClipAnimations;
            var outc = new List<ModelImporterClipAnimation>();
            bool renamed = false, looped = false;
            foreach (var c in src)
            {
                string n = c.name;
                int bar = n.LastIndexOf('|');
                if (bar >= 0) { n = n.Substring(bar + 1); renamed = true; }
                bool loop = System.Array.IndexOf(Loops, n) >= 0;
                if (loop && !c.loopTime) looped = true;
                var cc = new ModelImporterClipAnimation
                {
                    name = n, takeName = c.takeName,
                    firstFrame = c.firstFrame, lastFrame = c.lastFrame,
                    loopTime = loop, loopPose = loop,
                    keepOriginalOrientation = c.keepOriginalOrientation,
                    keepOriginalPositionY = c.keepOriginalPositionY,
                    keepOriginalPositionXZ = c.keepOriginalPositionXZ,
                };
                outc.Add(cc);
            }
            // <b>선 자세가 없으면 일어서는 동작의 끝에서 빌린다.</b>
            //
            // 처음엔 Idle 이 없는 사람을 뜰에 <b>앉혀</b> 두었다. 그런데 Sitting_Idle 은
            // <b>의자에 앉는</b> 몸짓이라, 아무것도 없는 맨땅에서 틀면 허공에 걸터앉은
            // 꼴이 된다 — 실제로 넷이 그렇게 떠 있었다.
            //
            // Sit_To_Stand 는 <b>다 일어선 채로 끝난다</b>. 그 마지막 몇 칸을 잘라
            // 돌리면 그것이 곧 선 자세다. 그림을 새로 뽑을 것도 없고, 나중에 진짜
            // Idle 이 들어오면 이 대목은 저절로 안 돈다.
            bool hasIdle = false;
            ModelImporterClipAnimation stand = null;
            foreach (var c in outc)
            {
                if (c.name == "Idle") hasIdle = true;
                if (c.name == "Sit_To_Stand") stand = c;
            }
            if (!hasIdle && stand != null)
            {
                outc.Add(new ModelImporterClipAnimation
                {
                    name = "Idle", takeName = stand.takeName,
                    firstFrame = Mathf.Max(stand.firstFrame, stand.lastFrame - 2f),
                    lastFrame = stand.lastFrame,
                    loopTime = true, loopPose = true,
                });
                dirty = true;
                log.AppendLine("   · 선 자세가 없어 Sit_To_Stand 끝 세 칸을 잘라 만들었다");
            }

            if (renamed || looped || !hasIdle) { mi.clipAnimations = outc.ToArray(); dirty = true; }
            if (renamed) log.AppendLine("   · 클립 이름에서 'Armature|' 를 뗐다");
            if (looped) log.AppendLine("   · 선 채·걷기·앉은 채를 돌게 했다");

            if (dirty) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            // 키를 맞춘다. 몸피가 아니라 <b>뼈</b>로, 그것도 <b>선 자세로</b> 잰다.
            float now = StandingHeight(path);
            if (now > 0.01f && Mathf.Abs(now - w.키) > 0.01f)
            {
                mi.globalScale *= w.키 / now;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                log.AppendLine("   · 키 " + now.ToString("F2") + "m → " + w.키.ToString("F2")
                             + "m (배율 " + mi.globalScale.ToString("F4") + ")");
            }
        }

        /// <summary>머리뼈가 사람 키의 어디쯤에 오는가. 목뼈 위 머리뼈는 대개 키의 0.87 이다.</summary>
        private const float HeadRatio = 0.87f;

        /// <summary>
        /// <b>선 자세에서</b> 잰 키.
        ///
        /// 바인드 자세로 재면 안 된다. 옹덕구와 복동은 <b>바인드 자세가 웅크린 것</b>이라
        /// 1.30 · 1.39 로 나오는데, 그 값에 맞춰 키우면 실제로 서는 순간 2.30 · 2.18 이
        /// 되어 거인이 된다 — 실제로 그렇게 세워 놓고 한 번 웃었다.
        /// 그래서 선 자세(Idle)를 <b>씌우고 나서</b> 잰다. 씬에 잠깐 세웠다가 지운다.
        ///
        /// <b>꼭대기 뼈로 재면 안 된다 — 쓴 것까지 키로 친다.</b>
        /// 복동은 <b>갓</b>을 썼다. 그 rig 의 HeadTop_End 는 정수리가 아니라 <b>갓 꼭대기</b>에
        /// 박혀 있어, 그 값에 키를 맞추면 갓을 뺀 몸이 그만큼 줄어든다 — 뜰에 세워 놓고 보니
        /// 갓 쓴 복동(1.67)이 맨머리 옹덕구(1.74)보다 <b>작았다</b>. 서리도 갓을 썼고,
        /// 마름은 더 심해서 <c>head_end_end</c> 라는 뼈가 <b>머리 위 17cm 허공</b>에 떠 있다.
        ///
        /// 그래서 <b>머리뼈(Head)</b>로 잰다. 무엇을 쓰든 머리뼈는 두개골 밑에 그대로 있으니
        /// 갓도 상투도 허공의 뼈도 이 자를 못 흔든다. rig 이름이 달라도(mixamorig:Head · Head)
        /// 앞머리(<c>headfront</c>) 같은 것에 걸리지 않게 <b>딱 'head' 인 것</b>만 고른다.
        /// </summary>
        private static float StandingHeight(string path)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (fbx == null) return -1f;

            AnimationClip idle = null; Avatar av = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                var c = o as AnimationClip;
                if (c != null && c.name == "Idle") idle = c;
                var a = o as Avatar;
                if (a != null) av = a;
            }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            try
            {
                var an = inst.GetComponent<Animator>();
                if (an == null) an = inst.AddComponent<Animator>();
                if (av != null) an.avatar = av;
                if (idle != null) idle.SampleAnimation(inst, 0f);

                var smr = inst.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (smr == null || smr.bones == null || smr.bones.Length == 0) return -1f;

                // <b>바닥은 뼈가 아니라 살갗으로 잡는다.</b>
                // 제일 낮은 뼈는 발가락 마디라 <b>밑창보다 3cm쯤 위</b>에 있다. 그 차이만큼
                // 자가 짧게 나와, 도구가 그만큼 사람을 더 키웠다 — 1.60 을 시켰는데 1.64 가
                // 서 있었다. 선 자세로 구운 살갗의 맨 밑을 바닥으로 친다.
                //
                // <b>구운 살갗에 TransformPoint 를 쓰면 안 된다.</b> BakeMesh 가 내주는 점은
                // 이미 <b>실제 크기</b>다 — 거기에 또 transform 을 태우면 배율이 두 번 곱해진다.
                // 마름의 fbx 는 안쪽이 100배라 노드 배율이 0.01 인데, 그대로 태웠더니 키가
                // <b>1.86m 가 0.02m</b> 로 줄어 자가 통째로 헛돌았다. 배율 말고 <b>돌림만</b>
                // 태워서 세로를 세운다.
                float floor = 9e9f;
                {
                    var baked = new Mesh();
                    smr.BakeMesh(baked);
                    var vs = baked.vertices;
                    var rot = smr.transform.rotation;
                    float py = smr.transform.position.y;
                    for (int i = 0; i < vs.Length; i++)
                    {
                        float y = (rot * vs[i]).y + py;
                        if (y < floor) floor = y;
                    }
                    Object.DestroyImmediate(baked);
                }

                float lo = 9e9f, hi = -9e9f; Transform head = null;
                foreach (var b in smr.bones)
                {
                    if (b == null) continue;
                    if (b.position.y < lo) lo = b.position.y;
                    if (b.position.y > hi) hi = b.position.y;

                    var n = b.name;
                    int colon = n.LastIndexOf(':');
                    if (colon >= 0) n = n.Substring(colon + 1);
                    if (string.Equals(n, "head", System.StringComparison.OrdinalIgnoreCase)) head = b;
                }
                if (lo > hi) return -1f;
                if (floor > 8e9f) floor = lo;

                // 머리뼈가 있으면 그것으로 잰다. 없으면 옛 자(꼭대기 뼈)로 물러선다 —
                // 쓴 것이 없는 사람은 두 자가 어차피 같은 값을 낸다.
                if (head != null) return (head.position.y - floor) / HeadRatio;
                return hi - floor;
            }
            finally { Object.DestroyImmediate(inst); }
        }

        // ── ㉡ 살갗 ───────────────────────────────

        /// <summary>재질을 찾거나, 없으면 그림 한 장으로 새로 만든다.</summary>
        private static Material Skin(Who w, System.Text.StringBuilder log)
        {
            if (!string.IsNullOrEmpty(w.재질))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(Art + w.재질);
                if (m != null) return m;
                log.AppendLine("   ※ 재질이 없다: " + w.재질);
            }
            if (string.IsNullOrEmpty(w.그림)) return null;

            string made = Art + System.IO.Path.GetDirectoryName(w.그림).Replace('\\', '/') + "/M_" + w.씬이름 + ".mat";
            var have = AssetDatabase.LoadAssetAtPath<Material>(made);
            if (have != null) return have;

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + w.그림);
            if (tex == null) { log.AppendLine("   ※ 그림도 없다: " + w.그림); return null; }
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.12f);
            AssetDatabase.CreateAsset(mat, made);
            log.AppendLine("   · 재질이 없어 그림 한 장으로 만들었다 — " + made);
            return mat;
        }

        // ── ㉢ 몸짓 ───────────────────────────────

        /// <summary>
        /// <b>기다리다 · 걸어가다 · 앉다</b> 세 마디짜리 컨트롤러.
        ///
        /// <see cref="CourtSummon"/> 이 부르는 값 두 개에 맞춘다 — 걸을 때 <c>Walking</c>,
        /// 자리에 닿으면 <c>Sitting</c>. 그래서 이 그림만 그려 두면 부름·심문 쪽 코드는
        /// 한 줄도 안 고쳐도 된다.
        /// </summary>
        private static AnimatorController Controller(string path, Who w, System.Text.StringBuilder log)
        {
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                var c = o as AnimationClip;
                if (c != null && !c.name.StartsWith("__")) clips[c.name] = c;
            }

            // <b>몸짓표는 사람이 아니라 <i>몸</i>을 따라 만든다.</b> 甲 과 乙 은 같은 옹덕구
            // 모델을 쓴다 — 그것이 이 사건의 핵심이다(둘이 똑같이 생겼다). 사람 이름으로
            // 만들면 같은 몸에 똑같은 표가 두 벌 생긴다.
            string dir = Art + System.IO.Path.GetDirectoryName(w.fbx).Replace('\\', '/');
            string acPath = dir + "/" + System.IO.Path.GetFileNameWithoutExtension(w.fbx) + "_AC.controller";
            // <b>남의 씬이 쓰는 표는 절대 안 건드린다.</b>
            //
            // 이 도구의 첫 판은 표를 <b>사람 이름</b>으로 찾았다(마름_AC). 그런데 그것은
            // 1막이 쓰는 표였고, 이 도구는 상태끼리의 전이를 <b>죄 걷어내고</b> 새로
            // 긋는다 — 1막 마름의 얼개를 그렇게 지웠다. 게다가 1막의 Walking 은
            // Trigger 인데 여기서 Bool 로 알고 IfNot 을 걸어, 유니티가
            // "uses parameter 'Walking' which is not compatible with condition type"
            // 이라고 울었다.
            //
            // 지금은 표를 <b>몸(FBX) 이름</b>으로 찾으니 다시는 안 겹친다. 그래도 한 번
            // 겪은 일이라 문을 하나 더 단다 — 만들려는 표를 <b>다른 씬이 이미 쥐고
            // 있으면</b> 손대지 않고 물러난다.
            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(acPath);
            if (ac != null && HeldByAnotherScene(acPath, out string holder))
            {
                log.AppendLine("   ※ " + System.IO.Path.GetFileName(acPath) + " 는 " + holder
                             + " 가 쓰고 있어 손대지 않는다 — 겹치면 그 씬이 망가진다");
                return ac;
            }
            if (ac == null)
            {
                ac = AnimatorController.CreateAnimatorControllerAtPath(acPath);
                log.AppendLine("   · 몸짓표를 새로 만들었다 — " + acPath);
            }
            foreach (var p in new[] { "Walking", "Sitting" })
            {
                bool has = false;
                foreach (var q in ac.parameters) if (q.name == p) has = true;
                if (!has) ac.AddParameter(p, AnimatorControllerParameterType.Bool);
            }

            var sm = ac.layers[0].stateMachine;
            // 서 있는 클립이 있으면 서서 기다리고, 없으면 앉아서 기다린다.
            bool stands = clips.ContainsKey("Idle");
            string rest = stands ? "Idle" : "Sitting_Idle";

            var made = new Dictionary<string, AnimatorState>();
            int col = 0;
            foreach (var name in new[] { rest, "Walking", "Stand_To_Sit", "Sitting_Idle", "Sit_To_Stand" })
            {
                if (!clips.ContainsKey(name) || made.ContainsKey(name)) continue;
                AnimatorState st = null;
                foreach (var s in sm.states) if (s.state.name == name) st = s.state;
                if (st == null) st = sm.AddState(name, new Vector3(260f, 60f + 70f * col, 0f));
                st.motion = clips[name];
                made[name] = st;
                col++;
            }
            if (!made.ContainsKey(rest)) { log.AppendLine("   ※ 쉬는 자세 클립이 없다"); return ac; }
            sm.defaultState = made[rest];

            // 옛 전이는 다 걷어내고 새로 긋는다 — 그래야 두 번 눌러도 겹치지 않는다.
            foreach (var s in sm.states)
                while (s.state.transitions.Length > 0) s.state.RemoveTransition(s.state.transitions[0]);

            Link(made, rest, "Walking", "Walking", true);
            Link(made, "Walking", rest, "Walking", false);
            Link(made, rest, "Stand_To_Sit", "Sitting", true);
            Link(made, "Walking", "Stand_To_Sit", "Sitting", true);
            Exit(made, "Stand_To_Sit", "Sitting_Idle");
            Link(made, "Sitting_Idle", "Sit_To_Stand", "Sitting", false);
            Exit(made, "Sit_To_Stand", rest);

            if (!stands)
                log.AppendLine("   ※ 선 자세를 끝내 못 만들었다 — 뜰에서도 앉아 기다린다");

            EditorUtility.SetDirty(ac);
            AssetDatabase.SaveAssets();
            return ac;
        }

        /// <summary>관아 말고 다른 씬이 이 자산을 쥐고 있나.</summary>
        private static bool HeldByAnotherScene(string assetPath, out string holder)
        {
            holder = null;
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid)) return false;
            foreach (var p in AssetDatabase.GetAllAssetPaths())
            {
                if (!p.EndsWith(".unity") || p.Contains("Gwana")) continue;
                string txt;
                try { txt = System.IO.File.ReadAllText(p); } catch { continue; }
                if (!txt.Contains(guid)) continue;
                holder = System.IO.Path.GetFileNameWithoutExtension(p);
                return true;
            }
            return false;
        }

        private static void Link(Dictionary<string, AnimatorState> s, string from, string to, string flag, bool on)
        {
            if (!s.ContainsKey(from) || !s.ContainsKey(to) || from == to) return;
            var t = s[from].AddTransition(s[to]);
            t.hasExitTime = false; t.duration = 0.16f;
            t.AddCondition(on ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, flag);
        }

        /// <summary>동작이 끝나면 저절로 넘어간다(앉는 짓·일어나는 짓은 한 번만 돈다).</summary>
        private static void Exit(Dictionary<string, AnimatorState> s, string from, string to)
        {
            if (!s.ContainsKey(from) || !s.ContainsKey(to)) return;
            var t = s[from].AddTransition(s[to]);
            t.hasExitTime = true; t.exitTime = 0.92f; t.duration = 0.12f;
        }

        // ── ㉣ 세우기 ─────────────────────────────

        private static void Stand(Scene scene, Who w, string path, Material mat,
                                  AnimatorController ac, System.Text.StringBuilder log)
        {
            var host = FindDeep(scene, w.씬이름);
            if (host == null && !string.IsNullOrEmpty(w.베낄것))
            {
                var seed = FindDeep(scene, w.베낄것);
                if (seed == null) { log.AppendLine("   ※ 베낄 사람(" + w.베낄것 + ")이 없다"); return; }
                var made = Object.Instantiate(seed.gameObject, seed.parent);
                made.name = w.씬이름;
                Undo.RegisterCreatedObjectUndo(made, "2막 사람");
                host = made.transform;
                log.AppendLine("   · 씬에 없어 " + w.베낄것 + " 을 베껴 세웠다 — 부품이 그대로 이어진다");
            }
            if (host == null) { log.AppendLine("   ※ 씬에 없고 베낄 것도 안 정했다"); return; }

            // 뜰에 서는 자리
            if (!float.IsNaN(w.대기z))
            {
                var spot = Spot(scene, "뜰_대기_" + w.씬이름, new Vector3(WaitX, 0f, w.대기z));
                host.SetPositionAndRotation(spot.position, spot.rotation);
                Wire(host, spot);
            }

            // 몸을 갈아 끼운다
            Transform old = null;
            foreach (Transform ch in host)
                if (ch.GetComponentInChildren<SkinnedMeshRenderer>(true) != null) { old = ch; break; }
            var lp = old != null ? old.localPosition : Vector3.zero;
            var lr = old != null ? old.localRotation : Quaternion.identity;
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var body = (GameObject)PrefabUtility.InstantiatePrefab(fbx, host);
            PrefabUtility.UnpackPrefabInstance(body, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            body.name = w.씬이름 + "_모델";
            body.transform.localPosition = lp;
            body.transform.localRotation = lr;
            body.transform.localScale = Vector3.one;

            var an = body.GetComponent<Animator>();
            if (an == null) an = body.AddComponent<Animator>();
            an.runtimeAnimatorController = ac;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            { var av = o as Avatar; if (av != null) an.avatar = av; }
            an.applyRootMotion = false;
            an.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            foreach (var smr in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (mat != null) smr.sharedMaterial = mat;
                smr.updateWhenOffscreen = true;
            }

            // <b>심문 자료를 못 박는다.</b>
            //
            // 남을 베껴 세우면 부품이 통째로 딸려 오는데, 거기엔 <b>베낀 사람의 자료</b>도
            // 들어 있다. 그래서 마름과 복동이 둘 다 늙은하인의 자료를 물고 있었다 —
            // 셋한테 물으면 같은 늙은이가 세 번 대답했다. 이름은 표에 적어 두었으니
            // 세울 때마다 다시 걸어 준다.
            if (!string.IsNullOrEmpty(w.자료))
            {
                var 자료 = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    "Assets/_Project/Onggojip/Data/" + w.자료 + ".asset");
                var 심문 = host.GetComponent<InterrogationController>();
                if (자료 == null) log.AppendLine("   ※ 심문 자료 " + w.자료 + " 을 못 찾았다");
                else if (심문 == null) log.AppendLine("   ※ 심문 부품이 없어 자료를 못 걸었다");
                else Set(심문, so => so.FindProperty("_character").objectReferenceValue = 자료);
            }

            // <b>숨을 쉬게 한다.</b>
            //
            // 선 자세를 Sit_To_Stand 끝 세 칸에서 잘라 만들었으니 그것은 사실상 <b>언 자세</b>다.
            // 그대로 두면 뜰에 인형 넷이 선다. BreathSway 는 애니메이터가 자세를 다 쓴 뒤에
            // <b>등뼈만</b> 아주 조금 돌린다 — 다리와 발은 엉덩이뼈 반대쪽 가지라 안 움직이니
            // 발이 미끄러지지도, 발붙임과 다투지도 않는다.
            //
            // 숨 박자를 사람마다 어긋나게 준다. 같은 박자로 쉬면 넷이 한 몸처럼 보인다.
            var breath = body.GetComponent<BreathSway>();
            if (breath == null) breath = Undo.AddComponent<BreathSway>(body);
            Set(breath, so =>
            {
                so.FindProperty("_animator").objectReferenceValue = an;
                so.FindProperty("_bones").arraySize = 0;          // 등뼈는 이름으로 스스로 찾는다
                so.FindProperty("_facing").objectReferenceValue = host;
                so.FindProperty("_phase").floatValue = Phase(w.씬이름);
            });

            // <b>발붙임에 새 몸을 일러 준다.</b>
            //
            // 이걸 빠뜨려서 아무도 계단을 못 올라갔다. GroundFeet 은 <b>_model 을 세로로
            // 옮겨</b> 발을 땅에 붙이는데, 그 칸이 甲 의 경우 <c>왼팔_안쪽</c> 을 가리키고
            // 있었다 — 손목 심문 때 만든 팔이다. 그러니 계단을 밟을 때마다 <b>팔만</b>
            // 올라가고 몸은 뜰 높이에 남았고, 그대로 기단을 뚫고 걸어가 앞자리에 섰다.
            //
            // 발 뼈도 함께 비운다. 옛 몸의 뼈를 쥔 채로 두면 그 뼈는 이미 지워진 것이라
            // 발을 못 찾는다. 비워 두면 GroundFeet 이 새 몸에서 이름으로 다시 찾는다.
            var feet = host.GetComponent<GroundFeet>();
            if (feet != null)
                Set(feet, so =>
                {
                    so.FindProperty("_model").objectReferenceValue = body.transform;
                    so.FindProperty("_bodyBone").objectReferenceValue = null;
                    so.FindProperty("_footBones").arraySize = 0;
                });

            // 부름 쪽에 앉는 값을 일러 둔다. 이름이 안 맞으면 앉는 짓이 조용히 안 돈다.
            var summon = host.GetComponent<CourtSummon>();
            if (summon != null)
                Set(summon, so =>
                {
                    so.FindProperty("_animator").objectReferenceValue = an;
                    so.FindProperty("_walkBool").stringValue = "Walking";
                    so.FindProperty("_kneelBool").stringValue = "Sitting";
                });

            log.AppendLine("   · 몸을 갈아 끼웠다 (재질 " + (mat != null ? mat.name : "없음") + ")");
        }

        /// <summary>이름에서 숨 박자를 뽑는다 — 사람마다 다르고, 다시 눌러도 같다.</summary>
        private static float Phase(string name)
        {
            int h = 0;
            foreach (var c in name) h = h * 31 + c;
            return Mathf.Abs(h % 1000) / 1000f * 6.2831853f;
        }

        /// <summary>대기 자리 표. 어사 쪽(+x)을 본다.</summary>
        private static Transform Spot(Scene scene, string name, Vector3 pos)
        {
            var t = FindDeep(scene, name);
            if (t == null)
            {
                var go = new GameObject(name);
                SceneManager.MoveGameObjectToScene(go, scene);
                Undo.RegisterCreatedObjectUndo(go, "대기 자리");
                t = go.transform;
            }
            t.SetPositionAndRotation(pos, Quaternion.Euler(0f, 90f, 0f));
            return t;
        }

        /// <summary>부름 쪽에 제 대기 자리를 다시 일러 둔다.</summary>
        private static void Wire(Transform host, Transform spot)
        {
            var summon = host.GetComponent<CourtSummon>();
            if (summon == null) return;
            Set(summon, so => so.FindProperty("_waitSpot").objectReferenceValue = spot);
        }

        private static void Set(Object target, System.Action<SerializedObject> edit)
        {
            var so = new SerializedObject(target);
            edit(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static Transform FindDeep(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t;
            return null;
        }
    }
}
