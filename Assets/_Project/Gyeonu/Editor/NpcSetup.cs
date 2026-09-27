using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IMUNROK.Gyeonu.EditorTools
{
    /// <summary>
    /// NPC 11인 설치 (2026-08-25). 견우 한 사람으로 구조를 확인한 뒤 나머지 열 사람을 붙였다.
    ///
    /// ■ 하는 일 넷
    ///   ① <see cref="NpcProfile"/> 11장 — 문서 「제5부」의 인물 정의 (<see cref="NpcPersonas"/>)
    ///   ② 애니메이터 11개 — FBX 클립을 평평한 상태 판으로 (<see cref="NpcRig"/>)
    ///   ③ 씬에 <b>이미 서 있는</b> 사람에게 부품 붙이기 — 자리는 건드리지 않는다
    ///   ④ 자리마다 다른 인스턴스 세우기 — 밤의 견우, 주막의 상인, 구출 뒤의 두 사람 …
    ///
    /// ■ ⚠️ 이미 서 있는 사람의 자리를 옮기지 않는다
    ///   NPC 11인은 사람 손으로 눈으로 맞춰 세워 둔 것이다. 그 좌표·각도를 코드가 다시 계산하면
    ///   반드시 어긋난다. <see cref="Slot.pos"/> 는 <b>씬에 없을 때만</b> 쓰는 예비 자리다.
    ///
    /// ■ 멱등하다
    ///   다시 눌러도 같은 결과다. 이미 있는 <b>프로필의 글은 덮어쓰지 않는다</b> —
    ///   인스펙터에서 말투를 고쳐 놓고 메뉴를 다시 누르는 일이 흔하다.
    ///   문서 그대로 되돌리려면 「전원 프로필 문서대로 되돌리기」를 쓴다.
    ///
    /// ■ 원본 에셋을 건드리지 않는다 (CLAUDE.md)
    ///   FBX 임포트 설정도, 원본 폴더도 손대지 않는다. 클립 루프는 자기 전이로 잇는다.
    /// </summary>
    public static class NpcSetup
    {
        const string NpcFolder = "Assets/_Project/Gyeonu/Npc";
        const string SceneFolder = "Assets/_Project/Gyeonu/Scenes/";

        // ═══════════════════════════════════════════════════════
        //  메뉴
        // ═══════════════════════════════════════════════════════

        [MenuItem("Tools/이문록/NPC/① 프로필·애니메이터 전부 만들기")]
        public static void MakeAssets()
        {
            EnsureFolder(NpcFolder);
            var missing = new List<string>();
            foreach (var d in NpcPersonas.All)
            {
                EnsureProfile(d);
                NpcRig.Build(d.model, d.ModelPath, d.ControllerPath, d.wantedMotions, missing);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[NPC] 프로필·애니메이터 11인 준비 완료 — " + NpcFolder);
            ReportMissing(missing);
        }

        [MenuItem("Tools/이문록/NPC/② 이 씬의 NPC 세우기")]
        public static void InstallCurrentScene()
        {
            EnsureFolder(NpcFolder);
            var scene = EditorSceneManager.GetActiveScene();
            string key = Path.GetFileNameWithoutExtension(scene.path);
            if (!Plan.ContainsKey(key)) { Debug.LogWarning("[NPC] 이 씬에는 배치할 NPC가 없다: " + key); return; }

            int n = Install(key);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[NPC] " + key + " — " + n + "명 설치. 씬을 저장하세요.");
        }

        [MenuItem("Tools/이문록/NPC/③ 전 씬 일괄 설치")]
        public static void InstallAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolder(NpcFolder);
            MakeAssets();

            int total = 0;
            foreach (var key in Plan.Keys)
            {
                var scene = EditorSceneManager.OpenScene(SceneFolder + key + ".unity", OpenSceneMode.Single);
                int n = Install(key);
                total += n;
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[NPC] " + key + " — " + n + "명");
            }
            Debug.Log("[NPC] 전 씬 설치 완료 — 모두 " + total + "자리.");
        }

        [MenuItem("Tools/이문록/NPC/전원 프로필 문서대로 되돌리기")]
        public static void ResetAllProfiles()
        {
            if (!EditorUtility.DisplayDialog("프로필 되돌리기",
                "NPC 11인의 프로필 글을 최종기획안 11차 「제5부」 그대로 되돌린다.\n" +
                "인스펙터에서 고친 말투·태도는 사라진다.", "되돌린다", "그만두기")) return;
            WriteAllProfiles();
        }

        /// <summary>물어보지 않고 문서 그대로 다시 쓴다 — 설치 스크립트가 부른다.</summary>
        public static void WriteAllProfiles()
        {
            EnsureFolder(NpcFolder);
            foreach (var d in NpcPersonas.All)
            {
                var p = EnsureProfile(d);
                d.write(p);
                EditorUtility.SetDirty(p);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[NPC] 11인의 프로필을 문서 그대로 되돌렸다.");
        }

        [MenuItem("Tools/이문록/NPC/모션 점검표")]
        public static void MotionReport()
        {
            var sb = new System.Text.StringBuilder("[NPC] 모션 점검 — 문서 「제5부」가 적은 모션이 FBX에 있는가\n");
            var missing = new List<string>();
            foreach (var d in NpcPersonas.All)
            {
                var have = NpcRig.StateNames(d.model, d.ModelPath);
                var lack = new List<string>();
                foreach (var w in d.wantedMotions) if (!have.Contains(w)) { lack.Add(w); missing.Add(d.model + "." + w); }
                sb.Append("  ").Append(d.model.PadRight(22))
                  .Append(lack.Count == 0 ? "OK   " : "없음! ")
                  .Append(string.Join(", ", have.ToArray()));
                if (lack.Count > 0) sb.Append("   ← 문서에 있으나 없는 것: ").Append(string.Join(", ", lack.ToArray()));
                sb.AppendLine();
            }
            Debug.Log(sb.ToString());
            ReportMissing(missing);
        }

        static void ReportMissing(List<string> missing)
        {
            if (missing.Count == 0) { Debug.Log("[NPC] 문서에 적힌 모션이 전부 FBX에 있다. 없는 것 없음."); return; }
            Debug.LogError("[NPC] ⚠️ 문서에 있으나 FBX에 없는 모션 " + missing.Count + "종:\n  " +
                           string.Join("\n  ", missing.ToArray()));
        }

        // ═══════════════════════════════════════════════════════
        //  배치표 — 문서 「제5부」의 위치표
        // ═══════════════════════════════════════════════════════

        /// <summary>한 자리에 선 NPC 하나.</summary>
        class Slot
        {
            public NpcId id;
            /// <summary>씬 오브젝트 이름. 비우면 모델 이름 그대로.</summary>
            public string name;
            /// <summary>씬에 <b>없을 때만</b> 쓰는 예비 자리. 이미 있으면 손대지 않는다.</summary>
            public Vector3 pos;
            public float yaw;
            /// <summary>새로 세울 때 바닥을 짚을지. 마루·평상 위에 앉히는 자리는 끈다.</summary>
            public bool ground = true;

            public TimeOfDay[] at;
            public string[] need;
            public string[] forbid;
            public TimeOfDay[] ignoreFlagsAt;
            public NpcSchedule.Rescue rescue = NpcSchedule.Rescue.상관없음;

            /// <summary>기본 자세를 프로필과 다르게 둘 때 (상인은 주막에서 앉아 마신다).</summary>
            public string baseState;
            /// <summary>기본 자세 클립을 이 지점(0~1)부터 튼다 — 상인의 SitDrinking 은 앞 30%가 '서 있다가 앉는' 구간.</summary>
            public float baseStart = 0f;
            /// <summary>다른 상태의 쓸 구간(0~1). 아이03의 Clap 은 앞뒤가 '앉아 있는' 구간이라 가운데만 쓴다.</summary>
            public (string state, float start, float end)[] starts;
            /// <summary>자리마다 다를 때 (상인 서서/앉아). <c>"-"</c> 는 "이 자리에서는 대화 모션 없음".</summary>
            public string talkState;
            /// <summary>자세가 바뀌면 얼굴 높이도 바뀐다. 음수면 프로필의 값.</summary>
            public float eyeHeight = -1f;
            public float talkDistance = -1f;
            /// <summary>그 자세의 몸이 오브젝트 정면과 어긋난 각도(도).</summary>
            public float faceYaw = 0f;
            public NpcProfile.RandomMotion[] random;       // 자리마다 다를 때 (견우 밤 LookAround)
            public float startDelay = -1f;

            public Action<GameObject> extra;
        }

        static NpcProfile.RandomMotion R(string s, float a, float b, float c = 1f) =>
            new NpcProfile.RandomMotion { state = s, minInterval = a, maxInterval = b, chance = c };

        static readonly TimeOfDay[] 낮 = { TimeOfDay.Day };
        static readonly TimeOfDay[] 낮_초밤 = { TimeOfDay.Day, TimeOfDay.EarlyNight };
        static readonly TimeOfDay[] 밤 = { TimeOfDay.EarlyNight, TimeOfDay.LateNight };
        static readonly TimeOfDay[] 초밤 = { TimeOfDay.EarlyNight };

        /// <summary>상인이 은하담을 떠났는가 — 한 번 만나고 나면 주막 평상으로 옮겨 앉는다.</summary>
        const string 상인만남 = "npc_talked_FestivalMerchant";
        /// <summary>수령을 관아에서 만났는가 — 그 뒤 한 차례 은하담을 둘러본다.</summary>
        const string 수령만남 = "npc_talked_Magistrate";

        static readonly Dictionary<string, Slot[]> Plan = new Dictionary<string, Slot[]>
        {
            // ── ① 성하리 마을 ────────────────────────────────────
            ["Gyeonu"] = new[]
            {
                // 낮 — 견우 집 마당. 이미 서 있다 (44.59, 0, 40.70) / 190°
                //   ② 타공 비밀지도 전달 (신뢰도 70). 나타나는 자리는 <b>담장 사립문 안쪽</b> —
                //      Plot_09_GyeonuHouse/Door01k (7), 통로 가운데 (47.07, 35.96) 에서 마당 쪽으로
                //      한 걸음 (실측 2026-09-11). 플레이어가 방금 지나온 문이라 저절로 '뒤'가 된다.
                new Slot { id = NpcId.Gyeonu, name = "Gyeonu",
                           pos = new Vector3(47.0f, 0f, 41.6f), yaw = 180f,
                           at = 낮, rescue = NpcSchedule.Rescue.구출_전에만,
                           extra = go =>
                           {
                               var k = Add<GyeonuGiveKey>(go);
                               k.kind = GyeonuGiveKey.Kind.Map;
                               k.itemId = "A1";
                               k.needsNight = false;
                               k.triggerAnchor = new Vector3(44.59f, 0f, 40.70f);   // 마당 (제자리)
                               k.triggerRadius = 7f;
                               k.gateSpot = new Vector3(47.07f, 0f, 36.90f);        // 사립문 안쪽 한 걸음
                               k.gateObjectName = "Door01k (7)";
                               k.voiceLine = "…가시기 전에 잠깐. 보여 드릴 것이 있소.";
                           } },

                // 밤 — 선아 집 마당. 문 (-44.97, ·, 60.55) 앞, 마당 한쪽에 서서 기다린다
                //   ① 선아 집 열쇠 전달 (신뢰도 40 · 밤). 나타나는 자리는 <b>담장 일각문 안쪽</b> —
                //      선아집_담장/KMWall_일각문_높임_통로2.2m, 통로 가운데 (−42.99, 51.63) 에서
                //      마당 쪽으로 한 걸음 (실측 2026-09-11). 문짝은 SM_Door01C3·C4 두 짝이다.
                new Slot { id = NpcId.Gyeonu, name = "Gyeonu_밤_선아집마당",
                           pos = new Vector3(-43.2f, 0f, 57.2f), yaw = 332f,
                           at = 밤, rescue = NpcSchedule.Rescue.구출_전에만,
                           random = new[] { R("LookAround", 12f, 20f) },      // 문서 「23」 밤은 12~20초
                           extra = go =>
                           {
                               var k = Add<GyeonuGiveKey>(go);
                               k.kind = GyeonuGiveKey.Kind.Key;
                               k.itemId = "SEONA_HOUSE_KEY";
                               k.needsNight = true;
                               k.triggerAnchor = new Vector3(-43.2f, 0f, 57.2f);    // 마당 (제자리)
                               k.triggerRadius = 7f;
                               k.gateSpot = new Vector3(-42.99f, 0f, 52.55f);       // 일각문 안쪽 한 걸음
                               k.gateObjectName = "KMWall_일각문_높임_통로2.2m";
                               k.keyScale = 0.018f;      // 제 크기(10cm)는 밤에 손바닥 얼룩처럼 보였다
                               k.voiceLine = "…거기 잠깐. 그 문은 그냥 열리지 않소.";
                           } },

                // 구출 뒤 — 선아와 함께 견우 집 마당
                new Slot { id = NpcId.Gyeonu, name = "Gyeonu_구출뒤",
                           pos = new Vector3(45.8f, 0f, 40.2f), yaw = 200f,
                           rescue = NpcSchedule.Rescue.구출_뒤에만 },
                new Slot { id = NpcId.Seona, name = "Seona_견우집",
                           pos = new Vector3(47.3f, 0f, 39.6f), yaw = 205f,
                           rescue = NpcSchedule.Rescue.구출_뒤에만,
                           baseState = "StandingIdle" },

                // 주모 — 주막 마당. 이미 서 있다 (1.64, 0, −20.80). 늦은 밤 퇴장
                new Slot { id = NpcId.Jumo, name = "Jumomo",
                           pos = new Vector3(1.64f, 0f, -20.80f), yaw = 234f, at = 낮_초밤 },

                // 상인 — 주막 마당. 낮 후반(은하담에서 만난 뒤) ~ 초밤
                //
                // 2026-09-11 — <b>앉기를 걷어내고 세웠다.</b> 이 팩에는 걸터앉는 클립이 없어
                //   수령의 앉기(PerchIdle/PerchTalk)를 빌려 왔었는데, 빌린 자세라 무릎에서 가슴까지
                //   크게 도는 데다 술 마시는 동작도 없었다. 자기 클립으로 돌아간다 —
                //   <c>StandingIdle</c> / <c>StandTalk</c> / 랜덤 <c>StandDrinking</c> 은 전부
                //   프로필에 이미 있는 것이라 이 자리에서 덮어쓸 값이 하나도 없다.
                //
                // 자리: 평상(Low_Wooden_Bench (1), 중심 −1.73·−17.74, 228°) 남동변 <b>바깥 땅</b>.
                //   가장자리 중심 −0.63·−18.96, 바깥 법선 (0.669, −0.743) 에서 한 걸음 나온 곳이다.
                //   술상(Small_Dining_Table (2), −1.73·−18.63)이 뒤 왼편 1.5m — 한 걸음이면 닿는다.
                //   쉬는 자리(주막_쉬는자리, −3.40·−16.00)와 평상 위 멍석을 비켜 서고, 캡슐 겹침
                //   검사로 빈 땅임을 확인했다(2026-09-11 실측). 120° 로 서서 마당과 주모 쪽을 본다.
                new Slot { id = NpcId.FestivalMerchant, name = "FestivalMerchant_주막",
                           pos = new Vector3(-0.69f, 0f, -19.78f), yaw = 120f,
                           at = 낮_초밤, need = new[] { 상인만남 }, ignoreFlagsAt = 초밤 },

                // 아이 3인 — 마을에서 은하담으로 가는 길목. 이미 서 있다. 밤 퇴장
                // ⚠️ 문서 「28. 그룹 연출」 — 시작을 4초·6초씩 어긋낸다
                new Slot { id = NpcId.Child01, name = "VillageChild_01",
                           pos = new Vector3(67.26f, 0f, 9.25f), yaw = 0f, at = 낮, startDelay = 0f,
                           // 아이들 노래(V01)는 아이01에 붙은 ChildrenSong 이 세 아이 한가운데서 3D로 튼다 (2026-09-09).
                           // 씬이 열릴 때 스스로 붙기도 하지만, 설치 때 붙여 두면 인스펙터에서 간격·거리를 만질 수 있다.
                           extra = go => { Add<ChildrenSong>(go); } },
                new Slot { id = NpcId.Child03, name = "VillageChild_03",
                           pos = new Vector3(66.58f, 0f, 12.81f), yaw = 128f, at = 낮, startDelay = 4f,
                           // 문서 「28」의 '짧은 Walk' — 1~2m 나갔다가 제자리로 돌아온다 (2026-09-10).
                           // 랜덤 Walk 는 제자리걸음일 뿐이라 프로필에서 뺐다 (NpcPersonas.Child03).
                           // ⚠️ Clap 클립은 "양반다리로 앉음 → 서서 손뼉 → 도로 앉음"이다. 앉은 몸이 루트를
                           //    두고 허공에 떠서 어느 쪽으로 붙여도 어색했다 (2026-09-10 에 바위를 놓고
                           //    루트를 밀어 걸터앉히는 시도를 했다가 2026-09-11 에 되돌렸다).
                           //    이제 <b>서 있는 구간만</b> 쓴다 — 앞의 앉은 구간은 건너뛰고(startAt)
                           //    뒤에 다시 주저앉기 전에 기본 자세로 돌아온다(endAt).
                           //    구간은 실측으로 잡았다 (2026-09-11, 머리 높이로 앉음/섬을 갈랐다):
                           //      0.00~0.14 앉음(머리 0.99) · 0.17~0.71 섬(머리 1.36) · 0.76~1.00 도로 앉음
                           //    ⚠️ 이 클립에는 <b>서서 손뼉 치는 동작이 없다</b>. 서 있는 구간은 한 손을
                           //       가슴께에 든 채 거의 정지해 있다 — 손이 움직이는 곳은 앉은 구간뿐이다.
                           starts = new[] { ("Clap", 0.170f, 0.710f) },
                           extra = go =>
                           {
                               var pt = Add<NpcPatrol>(go);
                               pt.points = new[] { new Vector3(-1.5f, 0f, 0.6f) };   // 아이01·02 반대쪽으로
                               pt.walkState = "Walk"; pt.standIdle = "Idle";
                               pt.sitIdle = pt.sitToStand = pt.standToSit = "";
                               pt.roamForever = false; pt.pointsPerTrip = 1;
                               pt.speed = 0.6f;                                  // 아이 걸음
                               pt.restRange = new Vector2(2f, 4f);
                               pt.idleRange = new Vector2(22f, 34f);             // 문서 — 15~25초 언저리
                               pt.onlyAt = new[] { TimeOfDay.Day };
                           } },
                new Slot { id = NpcId.Child02, name = "VillageChild_02",
                           pos = new Vector3(70.46f, 0f, 12.17f), yaw = 243f, at = 낮, startDelay = 10f,
                           extra = go =>
                           {
                               var ev = Add<NpcSecretEvent>(go);
                               ev.secretState = "Secret";
                               ev.companionState = "Talk";
                               var one = GameObject.Find("VillageChild_01");
                               ev.companion = one != null ? one.GetComponent<NpcActor>() : null;
                           } },

                // 어머니 — 대나무 길 끝 집 앞. 고정. 이미 앉아 있다
                new Slot { id = NpcId.Mother, name = "FirstJiknyeo_Mother",
                           pos = new Vector3(-58.14f, 0.08f, -48.32f), yaw = 93f, ground = false },
            },

            // ── ② 은하담 ────────────────────────────────────────
            ["Gyeonu_EunhaDam"] = new[]
            {
                // 상인 — 낮 초반. 한 번 만나고 나면 주막으로 옮겨 앉는다
                // ⚠️ ground = false 를 유지할 것. 풍영정의 보행 콜라이더(풍영정_보행콜라이더, y 1.155)는
                //    눈에 보이는 마루(≈1.00)보다 0.15m 위에 있어 광선으로 짚으면 마루 위에 떠 선다 (2026-09-10 실측).
                //    지금 자리(y 0.98)는 눈으로 맞춘 것이고 화면에서 정상이다.
                new Slot { id = NpcId.FestivalMerchant, name = "FestivalMerchant",
                           pos = new Vector3(-9.65f, 0.98f, 37.68f), yaw = 66f, ground = false,
                           at = 낮, forbid = new[] { 상인만남 },
                           baseState = "StandingIdle", talkState = "StandTalk",
                           random = new[] { R("StandDrinking", 15f, 30f) } },

                // 수령 — 낮 특수. 한 차례 현장을 둘러본다. 앉지 않는다 (문서 「25」)
                new Slot { id = NpcId.Magistrate, name = "Magistrate_은하담시찰",
                           pos = new Vector3(62f, 6.56f, 1f), yaw = 270f,
                           at = 낮, need = new[] { 수령만남 },
                           baseState = "StandIdle", talkState = "StandIdle",
                           eyeHeight = 1.62f, talkDistance = 1.70f,   // 여기서는 서 있다
                           random = Array.Empty<NpcProfile.RandomMotion>(),
                           extra = go =>
                           {
                               var pt = Add<NpcPatrol>(go);
                               pt.points = new[] { new Vector3(-7f, 0f, -2f), new Vector3(-13f, 0f, 4f) };
                               pt.standIdle = "StandIdle";
                               pt.sitIdle = pt.sitToStand = pt.standToSit = "";
                               pt.roamForever = true;
                               pt.onlyAt = new[] { TimeOfDay.Day };
                               pt.idleRange = new Vector2(6f, 12f);
                           } },

                // 견우 — 밤. 은하담 주변을 헤맨다
                // ⚠️ 풍영정 앞 흙길은 z ≈ −2 ~ +2 띠다 (2026-09-10 위에서 본 실측). 예전 자리 (76, ·, −3)와
                //    지점 (72, −9)는 길 남쪽 풀밭·덤불이었다. 길 위로 되돌렸다 — 자리 (74, ·, 0.3),
                //    지점 (68, 0.8)·(63, 0). 수령의 낮 시찰과 같은 길이지만 시간대가 달라 마주치지 않는다.
                new Slot { id = NpcId.Gyeonu, name = "Gyeonu_밤_은하담",
                           pos = new Vector3(74f, 5.9f, 0.3f), yaw = 250f,
                           at = 밤, rescue = NpcSchedule.Rescue.구출_전에만,
                           random = new[] { R("LookAround", 12f, 20f) },
                           extra = go =>
                           {
                               var pt = Add<NpcPatrol>(go);
                               pt.points = new[] { new Vector3(-6f, 0f, 0.5f), new Vector3(-11f, 0f, -0.3f) };
                               pt.walkState = "Walk"; pt.standIdle = "Idle";
                               pt.sitIdle = pt.sitToStand = pt.standToSit = "";
                               pt.roamForever = true;
                               pt.onlyAt = new[] { TimeOfDay.EarlyNight, TimeOfDay.LateNight };
                               pt.speed = 0.95f;
                               pt.idleRange = new Vector2(10f, 18f);
                           } },
            },

            // ── ③ 관아 동헌 ──────────────────────────────────────
            ["Gyeonu_Gwana"] = new[]
            {
                // 수령 — 낮. 대청 수령 자리에 앉아 있다. 밤에는 거처로 돌아가 사라진다
                new Slot { id = NpcId.Magistrate, name = "Magistrate",
                           pos = new Vector3(0.17f, 3.68f, 15.07f), yaw = 191f, ground = false,
                           at = 낮,
                           extra = go =>
                           {
                               var pt = Add<NpcPatrol>(go);
                               // ⚠️ 문서의 '앞마당 루틴'을 <b>대청마루 위로 줄였다</b> (2026-08-25).
                               //    동헌에서 앞마당(월대)까지는 대청 4.00 → 기단 3.45 → 정면계단 →
                               //    월대 1.80 으로 2.2m를 내려가는 <b>기념비적 층계</b>다. 길찾기 없는
                               //    직선 걸음은 한 번 아래로 떨어지면 기단 속으로 들어가 다시 못 올라온다
                               //    (실측: 돌아온 수령이 마루 밑 1.80m에 서 있었다). 층계를 제대로 타려면
                               //    발 높이 제한이 있는 걸음(CharacterController급)이 필요한데, 그것은
                               //    이 사건의 다른 NPC 누구에게도 쓰이지 않는다.
                               //    문서가 말한 "장시간 순찰이 아니라 잠깐 나와 주변을 보는 정도"는
                               //    대청 위 두 지점으로도 그대로 산다. 층계 걸음이 생기면 여기만 늘리면 된다.
                               pt.points = new[] { new Vector3(0f, 0f, -3.0f), new Vector3(-2.6f, 0f, -2.5f) };
                               pt.sitIdle = "SittingIdle";
                               pt.sitToStand = "SitToStand";
                               pt.standToSit = "StandToSit";
                               pt.standIdle = "StandIdle";
                               pt.pointsPerTrip = 2;                    // 문서 — 약 2세트
                               pt.speed = 0.8f;                         // 느리고 절제되게
                               pt.onlyAt = new[] { TimeOfDay.Day };
                               pt.idleRange = new Vector2(60f, 110f);
                           } },

                // 구출 뒤 — 견우가 관아에 먼저 와서 기다린다 (문서 「31」)
                new Slot { id = NpcId.Gyeonu, name = "Gyeonu_관아_재회",
                           pos = new Vector3(1.8f, 0f, -4.5f), yaw = 0f,
                           rescue = NpcSchedule.Rescue.구출_뒤에만 },

                // 선아 — 집무실에서 따라 나온다
                new Slot { id = NpcId.Seona, name = "Seona_관아",
                           pos = new Vector3(-3.4f, 4.0f, 13.2f), yaw = 90f,
                           rescue = NpcSchedule.Rescue.구출_뒤에만,
                           baseState = "StandingIdle",
                           extra = go => Add<NpcFollow>(go) },
            },

            // ── 관아 집무실 ─────────────────────────────────────
            ["Gyeonu_GwanaOffice"] = new[]
            {
                new Slot { id = NpcId.Seona, name = "Seona_집무실",
                           pos = new Vector3(0f, 0.95f, 9.6f), yaw = 180f, ground = false,
                           rescue = NpcSchedule.Rescue.구출_뒤에만,
                           baseState = "StandingIdle",
                           extra = go => Add<NpcFollow>(go) },
            },

            // ── ④ 관측실·서고 ───────────────────────────────────
            ["Gyeonu_Observatory"] = new[]
            {
                // 선아 — 서고 안쪽. 이미 쓰러져 있다 (15.54, −7.12, 32.29)
                // ⚠️ 서고 바닥은 −6.60 이고 FallIdle 의 몸 최저점은 루트보다 0.52 위다. 루트를 −7.30 에 두었더니
                //    누운 몸이 바닥 아래로 0.19m 들어갔다 (2026-09-10 실측) — 루트 y = −6.60 − 0.52 = −7.12.
                //    일어선 뒤의 높이는 SeonaRescue 가 StandingUp 동안 바닥까지 끌어올린다.
                new Slot { id = NpcId.Seona, name = "Seona",
                           pos = new Vector3(15.54f, -7.12f, 32.29f), yaw = 44f, ground = false,
                           baseState = "FallIdle",
                           // 쓰러져 있다 — 겨눌 높이는 SeonaRescue 가 자세에 맞춰 갈아 끼운다
                           eyeHeight = 0.55f, talkDistance = 1.40f,
                           extra = go => { Add<SeonaRescue>(go); Add<NpcFollow>(go); } },
            },

            // ── ⑥ 견우마을 ──────────────────────────────────────
            ["Gyeonu_GyeonuVillage"] = new[]
            {
                // 지도 해독 후 첫 등장 (문서 「30」). 첫 방문에는 처음부터 서 있지 않고 플레이어가 집 앞에
                // 다가섰을 때 <b>뒤에서 나타나</b> 걸어온다 — FirstCoupleEntrance (SceneExtras) 가
                // F_최초두사람_등장 을 세울 때까지 NpcSchedule 이 감춘다 (2026-09-10).
                new Slot { id = NpcId.FirstJiknyeo, name = "FirstJiknyeo",
                           pos = new Vector3(-0.58f, 1.69f, 32.90f), yaw = 229f, ground = false,
                           need = new[] { GyeonuWorld.F_타공지도_길밝힘, GyeonuWorld.F_최초두사람_등장 } },
                new Slot { id = NpcId.FirstGyeonu, name = "FirstGyeonu",
                           pos = new Vector3(-0.35f, 1.70f, 32.31f), yaw = 254f, ground = false,
                           need = new[] { GyeonuWorld.F_타공지도_길밝힘, GyeonuWorld.F_최초두사람_등장 } },
            },
        };

        // ═══════════════════════════════════════════════════════
        //  설치
        // ═══════════════════════════════════════════════════════
        static int Install(string sceneKey)
        {
            int n = 0;
            foreach (var s in Plan[sceneKey])
            {
                var def = NpcPersonas.Find(s.id);
                if (def == null) { Debug.LogError("[NPC] 정의가 없다: " + s.id); continue; }

                var profile = EnsureProfile(def);
                var controller = NpcRig.Build(def.model, def.ModelPath, def.ControllerPath, null, null);
                string objName = string.IsNullOrEmpty(s.name) ? def.model : s.name;

                var go = Find(objName, def.ModelPath, s.name == def.model);
                if (go == null)
                {
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(def.ModelPath);
                    if (model == null) { Debug.LogError("[NPC] 모델이 없다: " + def.ModelPath); continue; }
                    go = (GameObject)PrefabUtility.InstantiatePrefab(model);
                    go.name = objName;
                    // 처음 세울 때는 높이를 모른다 — 하늘에서 넉넉히 내려다본다.
                    // (걸을 때 쓰는 짧은 탐침과 달리 여기서는 지형이 머리 위에 있을 수도 있다)
                    var p = s.ground ? NpcPatrol.Ground(s.pos, 60f, 120f) : s.pos;
                    go.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, s.yaw, 0f));
                    Debug.Log("[NPC] 새로 세웠다 — " + objName + " @" + p.ToString("F2"));
                }

                Attach(go, def, profile, controller, s);
                n++;
            }
            SceneExtras(sceneKey);
            return n;
        }

        /// <summary>사람이 아닌 것 — 씬마다 하나씩 있는 특수 장치.</summary>
        static void SceneExtras(string sceneKey)
        {
            // 상인 정리 (2026-09-11) — 손에 쥐여 준 것을 거두고, 주막의 상인을 세운다.
            // 없는 이름은 그냥 지나가므로 어느 씬에서 돌려도 안전하다.
            DropHandProp("FestivalMerchant");
            DropHandProp("FestivalMerchant_주막");
            StandMerchantAtTavern();

            if (sceneKey == "Gyeonu")
            {
                // 2026-09-11 — 아이03을 걸터앉히려고 앞에 놓았던 바위(2026-09-10)를 치운다.
                //   앉는 모션 자체가 어색해 Clap 의 <b>서서 손뼉 치는 구간만</b> 쓰기로 했고,
                //   그러면 앉을 것이 필요 없다. 예전에 세워 둔 씬에도 남아 있으므로 여기서 지운다.
                var oldRock = GameObject.Find("아이_앉는바위");
                if (oldRock != null)
                {
                    UnityEngine.Object.DestroyImmediate(oldRock);
                    Debug.Log("[NPC] 아이03 앞의 앉는 바위를 치웠다 (앉는 연출을 되돌림).");
                }

                return;
            }

            if (sceneKey == "Gyeonu_GyeonuVillage")
            {
                // 최초의 두 사람 등장 연출 (문서 「30」 — 플레이어 뒤에서 등장). 2026-09-10.
                const string Name = "최초의두사람_등장";
                var host = GameObject.Find(Name);
                if (host == null) host = new GameObject(Name);
                var ent = Add<FirstCoupleEntrance>(host);
                var g = GameObject.Find("FirstGyeonu"); var j = GameObject.Find("FirstJiknyeo");
                ent.gyeonu = g != null ? g.GetComponent<NpcActor>() : null;
                ent.jiknyeo = j != null ? j.GetComponent<NpcActor>() : null;
                if (ent.gyeonu == null || ent.jiknyeo == null) Debug.LogWarning("[NPC] 견우마을에 두 사람이 없어 등장 연출을 잇지 못했다.");
                return;
            }

            if (sceneKey != "Gyeonu_Gwana") return;

            // ⚠️ 문서 「25」 — 낮에 집무실에 들어가려 하면 제지당하고 쫓겨난다.
            //    문 자체에 붙인다(수령 오브젝트가 아니라). 낮에는 수령이 안에 있고,
            //    밤에는 거처로 돌아가 관아에서 사라지므로 그때 문이 열린다.
            var door = GameObject.Find("출구_집무실");
            if (door == null) { Debug.LogWarning("[NPC] 관아 씬에 '출구_집무실' 이 없다 — 낮 잠금을 붙이지 못했다."); return; }
            var exit = door.GetComponent<SceneExit>();
            if (exit == null) { Debug.LogWarning("[NPC] '출구_집무실' 에 SceneExit 이 없다."); return; }
            if (door.GetComponent<OfficeDayGate>() == null)
            {
                var gate = door.AddComponent<OfficeDayGate>();
                gate.nightPrompt = exit.promptText;
                Debug.Log("[NPC] 집무실 문에 낮 잠금을 붙였다 (OfficeDayGate).");
            }
        }

        // ═══════════════════════════════════════════════════════
        //  상인 — 손을 비우고 세운다 (2026-09-11)
        // ═══════════════════════════════════════════════════════

        /// <summary>예전에 손에 매달았던 것의 이름. 남아 있으면 걷어낸다.</summary>
        const string HandPropName = "손_술그릇";

        /// <summary>
        /// 상인의 손에 쥐여 주었던 술병·잔을 거둔다 (2026-09-11 철회).
        /// 하루 만에 되돌린 까닭: 걸터앉기를 버리면서 "무엇을 들고 무엇을 하는 자세인가"가
        /// 통째로 바뀌었다. 손은 비우고 <c>StandDrinking</c> 모션만 남긴다.
        /// </summary>
        static void DropHandProp(string npcName)
        {
            var npc = GameObject.Find(npcName);
            if (npc == null) return;
            int n = 0;
            foreach (var t in npc.GetComponentsInChildren<Transform>(true))
                if (t != null && t.name == HandPropName) { UnityEngine.Object.DestroyImmediate(t.gameObject); n++; }
            if (n > 0) Debug.Log("[NPC] " + npcName + " 의 손에서 " + n + "개를 거두었다.");
        }

        /// <summary>
        /// 주막의 상인을 <b>평상 밖 땅에 세운다</b> (2026-09-11).
        ///
        /// ⚠️ <see cref="Slot.pos"/> 는 "씬에 없을 때만" 쓰는 예비 자리라, 이미 서 있는 사람은
        ///    옮기지 않는 것이 이 설치 스크립트의 원칙이다. 여기서만 그 원칙을 <b>일부러</b> 어긴다 —
        ///    예전 자리는 평상 위에 걸터앉는 자세를 전제로 공중(y −0.02)에 띄워 둔 것이라,
        ///    자세를 세우면 그 좌표가 그대로 <b>땅에 박힌 사람</b>이 된다. 한 번은 옮겨야 한다.
        ///    옮기고 나면 그 뒤로는 값이 같아 아무 일도 하지 않는다(멱등).
        /// </summary>
        static void StandMerchantAtTavern()
        {
            var go = GameObject.Find("FestivalMerchant_주막");
            if (go == null) return;

            Vector3 want = new Vector3(-0.69f, 0f, -19.78f);
            want = NpcPatrol.Ground(want, 20f, 60f);
            var rot = Quaternion.Euler(0f, 120f, 0f);
            if ((go.transform.position - want).sqrMagnitude < 0.0004f
                && Quaternion.Angle(go.transform.rotation, rot) < 0.5f) return;

            go.transform.SetPositionAndRotation(want, rot);
            Debug.Log("[NPC] 주막 상인을 평상 밖 땅에 세웠다 @" + want.ToString("F2") + " / 120°");
        }

        /// <summary>
        /// 부품을 얹는다. <b>자리·각도는 손대지 않는다.</b> 프리팹 연결도 끊지 않는다 —
        /// 컴포넌트 추가는 인스턴스 덧붙임(override)으로 남고 원본 FBX는 그대로다.
        /// </summary>
        static void Attach(GameObject go, NpcPersonas.Def def, NpcProfile profile,
                           AnimatorController controller, Slot s)
        {
            var anim = go.GetComponent<Animator>();
            if (anim == null) anim = go.AddComponent<Animator>();
            if (anim.runtimeAnimatorController == null) anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            // 몸통 — 조준(레이캐스트)도 받고 통과도 막는다.
            // ⚠️ 정수리까지 덮어야 한다. 1.76m로 잡았더니 **플레이어 눈높이(1.78m)에서 쏜
            //    수평 레이가 머리 위로 지나가** 정면에 두고도 조준이 되지 않았다(2026-08-25 실측).
            //    아이는 키가 절반이라 어른 치수를 그대로 쓰면 허공이 조준된다.
            var cap = go.GetComponent<CapsuleCollider>();
            if (cap == null) cap = go.AddComponent<CapsuleCollider>();
            Capsule(profile, s, out float h, out float r);
            cap.center = new Vector3(0f, h * 0.49f, 0f);
            cap.height = h;
            cap.radius = r;

            var talk = go.GetComponent<NpcDialogue>();
            if (talk == null) talk = go.AddComponent<NpcDialogue>();
            talk.profile = profile;
            talk.displayName = profile.displayName;
            talk.focusDistance = profile.talkDistance;
            talk.transitionTime = 0.55f;
            talk.layout = DialogueLayout.하단바_확정;   // 하단 가로 바 확정 (2026-08-27)
            talk.dimStrength = 0.35f;
            talk.faceYawOffset = s.faceYaw;

            var actor = go.GetComponent<NpcActor>();
            if (actor == null) actor = go.AddComponent<NpcActor>();
            actor.profile = profile;
            actor.animator = anim;
            actor.motions = NpcRig.MotionTable(def.model, def.ModelPath);
            // 기본 자세를 클립 중간부터 트는 자리 (상인 주막의 SitDrinking — 앉은 구간만)
            if (s.baseStart > 0f && !string.IsNullOrEmpty(s.baseState))
                foreach (var m in actor.motions) if (m.state == s.baseState) m.startAt = s.baseStart;
            if (s.starts != null)
                foreach (var (state, start, end) in s.starts)
                    foreach (var m in actor.motions)
                        if (m.state == state) { m.startAt = start; m.endAt = end; }
            actor.overrideRandom = s.random ?? Array.Empty<NpcProfile.RandomMotion>();
            actor.overrideStartDelay = s.startDelay;

            // 자리마다 기본 자세가 다를 수 있다 — 상인은 은하담에서 서서 마시고 주막에서 앉아 마신다.
            // ⚠️ 값이 <b>실제로 다를 때만</b> 곁가지를 만든다. 같은 값인데도 만들면
            //    자리 이름이 인물 이름과 같은 경우(FestivalMerchant) 원본 에셋을 제 위에 덮어쓴다.
            bool needLocal = (!string.IsNullOrEmpty(s.baseState) && s.baseState != profile.idleState)
                          || (!string.IsNullOrEmpty(s.talkState) && s.talkState != profile.talkState)
                          || s.eyeHeight >= 0f || s.talkDistance >= 0f;
            if (needLocal)
            {
                var local = LocalProfile(profile, s);
                actor.profile = local;
                talk.profile = local;
                talk.focusDistance = local.talkDistance;
            }

            var sched = go.GetComponent<NpcSchedule>();
            bool needSched = (s.at != null && s.at.Length > 0) || (s.need != null && s.need.Length > 0)
                          || (s.forbid != null && s.forbid.Length > 0) || s.rescue != NpcSchedule.Rescue.상관없음;
            if (needSched)
            {
                if (sched == null) sched = go.AddComponent<NpcSchedule>();
                sched.activeAt = s.at ?? Array.Empty<TimeOfDay>();
                sched.requireFlags = s.need ?? Array.Empty<string>();
                sched.forbidFlags = s.forbid ?? Array.Empty<string>();
                sched.ignoreFlagsAt = s.ignoreFlagsAt ?? Array.Empty<TimeOfDay>();
                sched.rescueState = s.rescue;
            }
            else if (sched != null) UnityEngine.Object.DestroyImmediate(sched);

            s.extra?.Invoke(go);

            EditorUtility.SetDirty(go);
        }

        /// <summary>
        /// 자리마다 기본 자세·대화 모션이 다를 때 쓰는 <b>곁가지 프로필</b>.
        /// 원본을 복제해 두 값만 갈아 끼운다 — 인물 정의는 한 장뿐이어야 하므로
        /// 말투·금지·게이트는 원본 그대로 따라간다. 파일 이름에 자리 이름이 붙는다.
        /// </summary>
        static NpcProfile LocalProfile(NpcProfile src, Slot s)
        {
            string path = NpcFolder + "/자리_" + Sanitize(s.name) + ".asset";
            var p = AssetDatabase.LoadAssetAtPath<NpcProfile>(path);
            if (p == null)
            {
                p = UnityEngine.Object.Instantiate(src);
                AssetDatabase.CreateAsset(p, path);
            }
            else EditorUtility.CopySerialized(src, p);

            // ⚠️ CopySerialized 는 이름까지 베껴 온다. 에셋 파일 이름과 어긋나면 Unity가
            //    "Main Object Name does not match filename" 을 띄우므로 되돌려 준다.
            p.name = Path.GetFileNameWithoutExtension(path);

            if (!string.IsNullOrEmpty(s.baseState)) p.idleState = s.baseState;
            if (s.talkState == "-") p.talkState = "";                       // 이 자리에서는 대화 모션 없음
            else if (!string.IsNullOrEmpty(s.talkState)) p.talkState = s.talkState;
            if (s.eyeHeight >= 0f) p.eyeHeight = s.eyeHeight;
            if (s.talkDistance >= 0f) p.talkDistance = s.talkDistance;
            // ⚠️ 원본 프로필의 랜덤 표까지 베껴 온다. 자리의 random 이 비어 있으면 NpcActor 는 프로필의 표로
            //    되돌아가므로, 앉은 상인 자리에 은하담용 StandDrinking 이 살아나 앉은 사람이 일어서서 마셨다
            //    (2026-09-10 실측). 자리에 random 을 적었으면(빈 배열 포함) 그것이 이 자리의 표다.
            if (s.random != null) p.randomMotions = s.random;
            EditorUtility.SetDirty(p);
            return p;
        }

        static string Sanitize(string s) => s.Replace('/', '_').Replace('\\', '_');

        /// <summary>
        /// 몸통 치수 — 조준(레이캐스트)을 받는 크기다.
        ///
        /// ⚠️ 아이 3인을 따로 재지 않는다. 임포트된 아이 모델이 <b>어른 키</b>이기 때문이다
        ///    (실측 1.86·1.91·1.89m, 2026-08-25). 아이 치수(1.28m)를 그대로 붙였더니
        ///    수평으로 쏜 조준선이 머리 위로 지나가 정면에 두고도 말을 걸 수 없었다.
        ///    모델을 줄이게 되면 여기도 함께 줄여야 한다.
        /// </summary>
        static void Capsule(NpcProfile p, Slot s, out float height, out float radius)
        {
            bool seated = !string.IsNullOrEmpty(s.baseState)
                            ? s.baseState.Contains("Sit") || s.baseState.Contains("Fall")
                            : p.idleState.Contains("Sit") || p.idleState.Contains("Fall");

            if (seated) { height = 1.35f; radius = 0.40f; return; }   // 앉은 사람은 낮고 넓다
            height = 1.95f; radius = 0.32f;
        }

        /// <summary>이름으로 찾고, 못 찾으면 원본 FBX 경로로 찾는다 (이름을 바꿔 두었을 수도 있다).</summary>
        static GameObject Find(string name, string modelPath, bool alsoByModel)
        {
            var byName = GameObject.Find(name);
            if (byName != null) return byName;
            if (!alsoByModel) return null;
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.parent != null) continue;
                if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) == modelPath)
                    return t.gameObject;
            }
            return null;
        }

        static T Add<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        // ═══════════════════════════════════════════════════════
        static NpcProfile EnsureProfile(NpcPersonas.Def d)
        {
            var p = AssetDatabase.LoadAssetAtPath<NpcProfile>(d.ProfilePath);
            if (p != null) return p;               // 이미 있으면 글을 덮어쓰지 않는다
            p = ScriptableObject.CreateInstance<NpcProfile>();
            d.write(p);
            AssetDatabase.CreateAsset(p, d.ProfilePath);
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
