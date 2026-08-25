using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using IMUNROK.Common;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>관아에 사람을 세운다.</b> 메뉴: [이문록 ▸ 관아 ▸ ⑨ 사람 세우기]
    ///
    /// 관아는 집도 서고도 다 섰는데 <b>사람이 하나도 없었다</b>. 출도해서 들어가 봐야
    /// 빈 마당이라, 2막이 열렸는지 아닌지도 알 수가 없다.
    ///
    /// <b>다섯을 세운다</b>
    ///   · <b>서리</b>  — 대문 앞. 2막의 첫 사람이고, 어디가 어디인지 일러 준다
    ///   · <b>甲</b>·<b>乙</b> — 동헌 뜰. 둘 다 제가 옹덕구라 한다
    ///   · <b>아내</b>·<b>늙은하인</b> — 그 뒤. 불려 나오면 앞으로 선다
    ///
    /// <b>몸은 1막에서 데려온다.</b> 새로 붙이는 것이 아니라 Onggojip.unity 에 서 있는
    /// 그 사람을 그대로 복제한다 — 키(1.70)도 살빛도 애니메이터도 1막에서 맞춰 둔 그대로다.
    /// 여기서 다시 맞추면 <b>같은 사람이 두 씬에서 다른 키</b>가 되고, 그러면 J04(둘이
    /// 닮았다)가 무너진다. 대신 1막에만 쓰던 부품(복동 걷기·하인 순찰·소리 듣기)은 떼어 낸다 —
    /// 관아 뜰에서 순찰을 돌 까닭이 없다.
    ///
    /// <b>서리와 아내는 몸 없이 선다.</b> 모델이 아직 없다. 말·심문·부르기는 다 되고
    /// 몸만 없는 상태라, 나중에 모델이 오면 그 빈 오브젝트 밑에 넣기만 하면 된다.
    ///
    /// <b>甲·乙에게는 소매 걷기 명령</b>(<see cref="SleeveOrder"/>)과 <b>왼팔</b>
    /// (<see cref="Measurable"/>)을 붙인다. 손목 심문이 여기서 성립한다 —
    /// 서고에서 대장을 읽고(G01) → 소매를 걷으라 이르고 → 유척으로 재면(G07) 끝난다.
    ///
    /// 두 번 눌러도 두 벌이 안 선다 — 있는 것은 자리만 고쳐 앉힌다.
    /// </summary>
    public static class GwanaPeople
    {
        private const string Act1 = "Assets/_Project/Onggojip/Scenes/Onggojip.unity";
        private const string DataDir = "Assets/_Project/Onggojip/Data";
        private const string SeoriAsset = DataDir + "/Seori_Interrogation.asset";
        private const string WifeAsset = DataDir + "/Wife_Interrogation.asset";

        // 뜰(y=0)에서 동헌 마루(y=2.21)를 올려다보는 자리.
        //
        // 앞줄 둘이 甲·乙이다 — 둘을 나란히 세워 두는 것 자체가 이 사건의 그림이라,
        // 뒷줄(아내·하인)과 한 걸음 벌려 놓는다. 동헌 마루 앞턱이 x=11.5 이고 기단이
        // x=8~10 이므로, 뜰에 발이 닿는 마지막 자리가 x≈7 이다.
        private static readonly Vector3 자리_甲 = new Vector3(7.00f, 0f, -1.20f);
        private static readonly Vector3 자리_乙 = new Vector3(7.00f, 0f, 1.20f);
        private static readonly Vector3 자리_아내 = new Vector3(6.10f, 0f, -3.00f);
        private static readonly Vector3 자리_하인 = new Vector3(6.10f, 0f, 3.00f);
        private static readonly Vector3 자리_서리 = new Vector3(-10.50f, 0f, 0.80f);

        [MenuItem("이문록/관아/⑨ 사람 세우기")]
        public static void Place()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.name.Contains("Gwana"))
            {
                Debug.LogWarning("[관아] 관아 씬을 열고 누르십시오(지금은 " + scene.name + ").");
                return;
            }

            var log = new System.Text.StringBuilder("[관아] 사람을 세웠다\n");

            ClearOldSpots(scene, log);

            // 1막을 곁들여 열어 몸을 빌려 온다. 다 쓰고 곧바로 닫는다.
            var act1 = EditorSceneManager.OpenScene(Act1, OpenSceneMode.Additive);

            var 甲 = Bring(scene, act1, "甲_가짜", "甲", 자리_甲, log);
            var 乙 = Bring(scene, act1, "乙_진짜옹덕구", "乙", 자리_乙, log);
            var 하인 = Bring(scene, act1, "늙은하인", "늙은하인", 자리_하인, log);

            EditorSceneManager.CloseScene(act1, true);

            // 몸이 아직 없는 둘 — 말과 심문은 다 되고 몸만 없다
            var 아내 = Bodiless(scene, "아내", 자리_아내, WifeAsset, log);
            var 서리 = Bodiless(scene, "서리", 자리_서리, EnsureSeori(log), log);
            if (서리 != null)
            {
                // 서리만 반대로 선다 — 들어오는 사람을 마주 봐야 맞이가 된다
                서리.transform.rotation = Quaternion.Euler(0f, 270f, 0f);
                var usher = 서리.GetComponent<GwanaUsher>();
                if (usher == null)
                {
                    usher = Undo.AddComponent<GwanaUsher>(서리);
                    log.AppendLine("  · 서리에게 맞이하는 말을 붙였다");
                }
                // 씬이 열리면서 유척이 먼저 나온다("품에서 유척을 꺼낸다"). 서리가 1.2초에
                // 입을 열면 그 말이 도구 알림에 덮여 <b>첫마디가 통째로 사라진다</b>.
                // 도구가 다 나오고 나서 말을 걸게 한 박자 물린다.
                Set(usher, so => so.FindProperty("_delay").floatValue = 3.2f);
                // 인사가 끝나야 붙잡고 물을 수 있다
                Set(서리.GetComponent<InterrogationController>(), so => so.FindProperty("_lockedAtStart").boolValue = true);
            }

            // 소매 걷기 + 왼팔 — 甲과 乙 <b>둘 다</b>. 한쪽에만 대면 심문이 아니라 덫이다.
            Sleeve(甲, true, log);
            Sleeve(乙, false, log);

            Bench(scene, 甲, 乙, 아내, 하인, log);
            Seat(scene, log);
            Entrance(scene, log);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(log.ToString());
        }

        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 옛 자리 표식을 걷어 낸다.
        ///
        /// Zone_2 밑에 甲·乙·아내·하인의 <b>자리 표식 넷</b>이 있었다. 그런데 그 자리는
        /// 동헌 마루보다 <b>반 미터 위</b>(y 2.70, 마루는 2.21)라 사람을 그 위에 세우면
        /// 허공에 뜬다. 게다가 넷이 다 마루 위였다 — 어사와 죄인이 같은 마루에 서면
        /// 동헌이 아니라 그냥 마루방이다.
        ///
        /// 이제 사람이 직접 뜰에 서므로 표식은 쓸 데가 없다. 걷어 낸다.
        /// <b>Zone_ 밑에 있는 것만</b> 지운다 — 같은 이름의 진짜 사람을 지우면 안 된다.
        /// </summary>
        private static void ClearOldSpots(Scene scene, System.Text.StringBuilder log)
        {
            string[] old = { "甲_자리", "乙_자리", "아내", "늙은하인_소환" };
            int n = 0;
            foreach (var name in old)
            {
                var go = Find(scene, name);
                if (go == null) continue;
                var p = go.transform.parent;
                if (p == null || !p.name.StartsWith("Zone_")) continue;   // 표식이 아니다 — 놔둔다
                Undo.DestroyObjectImmediate(go);
                n++;
            }
            if (n > 0) log.AppendLine("  · 마루 위에 떠 있던 옛 자리 표식 " + n + "개를 걷었다 (y 2.70 — 마루는 2.21)");
        }

        /// <summary>1막에서 사람을 복제해 온다. 이미 있으면 자리만 고쳐 앉힌다.</summary>
        private static GameObject Bring(Scene scene, Scene act1, string 원본, string 이름, Vector3 pos,
                                        System.Text.StringBuilder log)
        {
            var had = Find(scene, 이름);
            if (had != null)
            {
                had.transform.position = pos;
                had.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                Tune(had);
                log.AppendLine("  · " + 이름 + " — 이미 서 있어 자리만 고쳤다");
                return had;
            }

            var src = Find(act1, 원본);
            if (src == null) { log.AppendLine("  ※ 1막에서 " + 원본 + " 을 못 찾았다"); return null; }

            var go = (GameObject)Object.Instantiate(src);
            go.name = 이름;
            go.transform.SetParent(null);                 // 옮기기 전에 뿌리로 — 뿌리가 아니면 씬을 못 옮긴다
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, 90f, 0f);   // 동헌 쪽(+x)을 본다

            // 1막에서만 쓰던 부품은 떼어 낸다. 관아 뜰에서 순찰을 돌거나
            // 앞장서 걷거나 발소리를 세고 있을 까닭이 없다.
            string[] strip = { "BokdongController", "ServantCheck", "NoiseListener", "SnapTo", "MareumController", "HidePlace" };
            foreach (var c in go.GetComponents<MonoBehaviour>())
                if (c != null && System.Array.IndexOf(strip, c.GetType().Name) >= 0)
                    Object.DestroyImmediate(c);

            Tune(go);
            Undo.RegisterCreatedObjectUndo(go, "관아 사람");
            log.AppendLine("  · " + 이름 + " 을 1막에서 데려왔다 (" + 원본 + ")");
            return go;
        }

        /// <summary>몸 없이 세운다 — 모델이 아직 없는 사람. 말과 심문은 다 된다.</summary>
        private static GameObject Bodiless(Scene scene, string 이름, Vector3 pos, string 심문에셋,
                                           System.Text.StringBuilder log)
        {
            var go = Find(scene, 이름);
            if (go == null)
            {
                go = new GameObject(이름);
                SceneManager.MoveGameObjectToScene(go, scene);
                Undo.RegisterCreatedObjectUndo(go, "관아 사람");

                // 몸이 없어도 <b>눌릴 자리</b>는 있어야 한다. 모델이 오면 이 안에 들어온다.
                var col = go.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, 0.85f, 0f);
                col.size = new Vector3(0.55f, 1.70f, 0.40f);
                col.isTrigger = true;

                var mark = new GameObject("여기에_모델을_넣는다");
                mark.transform.SetParent(go.transform, false);

                log.AppendLine("  · " + 이름 + " 을 몸 없이 세웠다 — 모델이 오면 '여기에_모델을_넣는다' 자리에");
            }
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            var talk = go.GetComponent<InterrogationController>();
            if (talk == null) talk = Undo.AddComponent<InterrogationController>(go);
            if (!string.IsNullOrEmpty(심문에셋))
            {
                var ch = AssetDatabase.LoadAssetAtPath<InterrogationCharacter>(심문에셋);
                if (ch != null) Set(talk, so => so.FindProperty("_character").objectReferenceValue = ch);
                else log.AppendLine("  ※ 심문 데이터를 못 찾았다: " + 심문에셋);
            }
            Tune(go);
            return go;
        }

        /// <summary>
        /// 동헌에 선 사람의 심문 설정.
        ///
        /// 1막 설정 그대로 두면 <b>마루에서는 아무도 못 부른다</b> — 말을 걸 수 있는 거리가
        /// 3m 인데 마루에서 뜰까지는 6.5m 다. 그리고 물러섰다고 창이 닫히면 안 된다.
        /// 여기서 창을 닫고 여는 것은 거리가 아니라 <b>부르기 판</b>이 한다.
        /// </summary>
        private static void Tune(GameObject go)
        {
            var talk = go != null ? go.GetComponent<InterrogationController>() : null;
            if (talk == null) return;
            Set(talk, so =>
            {
                so.FindProperty("_beginOnStart").boolValue = false;      // 켜져 있으면 닫는 순간 조사청으로 나간다
                so.FindProperty("_maxTalkDistance").floatValue = 12f;    // 마루에서 뜰까지
                so.FindProperty("_walkAwayDistance").floatValue = 0f;    // 저절로 안 끊긴다
                so.FindProperty("_rememberBetweenTalks").boolValue = true;
                so.FindProperty("_topicsAtOnce").intValue = 3;
                // 1막에서 잠가 둔 채로 따라온다(甲은 물러간 뒤 잠긴다). 여기서는 풀어 둔다 —
                // 부르기 판은 잠금을 무시하지만, 뜰로 내려가 직접 눌렀을 때도 열려야 한다.
                so.FindProperty("_lockedAtStart").boolValue = false;
                var seed = so.FindProperty("_seedGateCluesForTest");
                if (seed != null) seed.boolValue = false;
            });
            var face = go.GetComponent<FaceThePlayer>();
            if (face == null) go.AddComponent<FaceThePlayer>();

            // 발을 땅에 붙인다. 1막 마당은 y=-1.67 이었고 관아 뜰은 0 이라,
            // 그대로 데려오면 발목이 십몇 cm 묻힌다.
            if (go.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
            {
                var feet = go.GetComponent<GroundFeet>();
                if (feet == null) feet = Undo.AddComponent<GroundFeet>(go);
                Set(feet, so => so.FindProperty("_fallbackY").floatValue = 0f);
            }
        }

        /// <summary>
        /// 소매 걷기 명령과 그 밑에 있는 왼팔.
        ///
        /// <paramref name="hasScar"/> 가 참이면 대장에 적힌 그 흉터가 있다(甲).
        /// 거짓이면 팔에 아무것도 없다(乙) — <b>없다는 것도 잰 결과다</b>.
        /// </summary>
        private static void Sleeve(GameObject go, bool hasScar, System.Text.StringBuilder log)
        {
            if (go == null) return;

            var arm = go.transform.Find("왼팔_안쪽");
            if (arm == null)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "왼팔_안쪽";
                Object.DestroyImmediate(cube.GetComponent<MeshRenderer>());
                Object.DestroyImmediate(cube.GetComponent<MeshFilter>());
                cube.transform.SetParent(go.transform, false);
                // 사람의 왼쪽은 제 몸 기준 -x 다. 팔뚝 높이(≈0.95m)에 소매 속으로 한 뼘.
                cube.transform.localPosition = new Vector3(-0.22f, 0.95f, 0.06f);
                cube.transform.localScale = new Vector3(0.18f, 0.34f, 0.18f);
                var bc = cube.GetComponent<BoxCollider>();
                // 트리거로 둔다 — 겨누는 레이는 짚지만, 바닥을 찾는 레이는 지나간다.
                // 아니면 팔뚝이 '바닥'으로 잡혀 그 위에 올라서게 된다.
                if (bc != null) bc.isTrigger = true;
                arm = cube.transform;
            }

            var m = arm.GetComponent<Measurable>();
            if (m == null) m = arm.gameObject.AddComponent<Measurable>();
            Set(m, so =>
            {
                so.FindProperty("_case").enumValueIndex = 0;             // Case1_Onggojip
                so.FindProperty("_what").stringValue = hasScar ? "왼팔 안쪽 흉터" : "왼팔 안쪽";
                // 2.1 은 "두 치"로 읽힌다(소수 0.1 은 '남짓'에 못 미친다). 대장에 적힌 말이
                // "두 치 <b>남짓</b>"이므로 잰 값도 그렇게 읽혀야 한 문장으로 겹친다.
                so.FindProperty("_chi").floatValue = hasScar ? 2.25f : 0f;
                so.FindProperty("_expected").floatValue = hasScar ? 2.25f : 0f;
                so.FindProperty("_tolerance").floatValue = 0.35f;
                so.FindProperty("_needsClueKey").stringValue = hasScar ? "G01" : "";
                so.FindProperty("_clueKey").stringValue = hasScar ? "G07" : "";
                // ※ 이 글은 OnggojipClues.All 의 G07 과 한 벌이어야 한다.
                so.FindProperty("_clueText").stringValue = hasScar
                    ? "유척으로 잰 왼팔 안쪽 흉터 — 두 치 남짓, 대장과 같다" : "";
                so.FindProperty("_nothingLine").stringValue = hasScar
                    ? "" : "왼팔 안쪽 — *아무것도 없다*. 데인 자국도, 아문 자리도.";
            });

            var order = go.GetComponent<SleeveOrder>();
            if (order == null) order = Undo.AddComponent<SleeveOrder>(go);
            Set(order, so =>
            {
                so.FindProperty("_case").enumValueIndex = 0;
                so.FindProperty("_needsClueKey").stringValue = "G01";
                so.FindProperty("_label").stringValue = "왼 소매를 걷으시오";
                so.FindProperty("_arm").objectReferenceValue = arm.gameObject;
                so.FindProperty("_bareHint").stringValue = "(유척을 들고 저 팔을 겨눈다)";

                var refuse = so.FindProperty("_refuseLines");
                refuse.arraySize = hasScar ? 1 : 0;
                if (hasScar)
                    refuse.GetArrayElementAtIndex(0).stringValue =
                        "어사또, 이 무슨…! 사대부의 몸에 함부로 손을 대시는 법이 어디 있소.";
                so.FindProperty("_bareLine").stringValue = hasScar
                    ? "……소매를 걷는다. 왼팔 안쪽에 오래된 데인 자국이 있다."
                    : "예. …보시다시피 아무것도 없소이다.";
            });

            arm.gameObject.SetActive(false);   // 소매 속은 못 잰다
            log.AppendLine("  · " + go.name + " 에 소매 걷기 명령과 왼팔을 붙였다" + (hasScar ? " (흉터 있음)" : " (흉터 없음)"));
        }

        /// <summary>부르기 판 — 마루에서 뜰의 사람을 갈아 부른다.</summary>
        private static void Bench(Scene scene, GameObject 甲, GameObject 乙, GameObject 아내, GameObject 하인,
                                  System.Text.StringBuilder log)
        {
            var go = Find(scene, "_부르기판");
            if (go == null)
            {
                go = new GameObject("_부르기판");
                SceneManager.MoveGameObjectToScene(go, scene);
                Undo.RegisterCreatedObjectUndo(go, "관아 사람");
            }
            go.transform.position = new Vector3(12.00f, 2.21f, 0f);   // 어사가 앉는 자리에서 잰다

            var bench = go.GetComponent<InterrogationBench>();
            if (bench == null) bench = Undo.AddComponent<InterrogationBench>(go);

            var order = new[] { 甲, 乙, 아내, 하인 };
            var names = new[] { "甲", "乙", "아내", "늙은하인" };

            Set(bench, so =>
            {
                so.FindProperty("_radius").floatValue = 7.5f;
                so.FindProperty("_enterSpeaker").stringValue = "";
                so.FindProperty("_enterLine").stringValue = "동헌이다. 뜰에 넷이 서 있다 — *이름을 부르면* 하나가 앞으로 선다.";
                var list = so.FindProperty("_seats");
                list.arraySize = order.Length;
                for (int i = 0; i < order.Length; i++)
                {
                    var e = list.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("이름").stringValue = names[i];
                    e.FindPropertyRelative("사람").objectReferenceValue =
                        order[i] != null ? order[i].GetComponent<InterrogationController>() : null;
                    e.FindPropertyRelative("아직").stringValue = "아직 오지 않았다.";
                }
            });
            log.AppendLine("  · 부르기 판을 놓았다 (甲·乙·아내·늙은하인)");
        }

        /// <summary>어사가 앉는 자리 표식. 앉히는 것은 나중 일이라 지금은 자리만 잡아 둔다.</summary>
        private static void Seat(Scene scene, System.Text.StringBuilder log)
        {
            var go = Find(scene, "어사_자리");
            if (go == null)
            {
                go = new GameObject("어사_자리");
                SceneManager.MoveGameObjectToScene(go, scene);
                Undo.RegisterCreatedObjectUndo(go, "관아 사람");
                log.AppendLine("  · 어사 자리를 잡았다 (동헌 마루 위, 뜰을 내려다보는 각)");
            }
            go.transform.position = new Vector3(12.00f, 2.21f, 0f);   // 마루 앞턱(11.5)에서 반 걸음 안
            go.transform.rotation = Quaternion.Euler(0f, 270f, 0f);   // 뜰(-x)을 본다
        }

        /// <summary>
        /// 들어서는 자리를 <b>대문 앞</b>으로 옮긴다.
        ///
        /// 여태 시작 자리가 (0, 1.6, -6) 이었는데 그 자리는 <b>문서고 건물 안</b>이다.
        /// 출도해서 관아에 닿는 순간 벽 속에서 눈을 뜨는 셈이라, 서리가 맞이할 자리도
        /// 없고 어디가 어디인지 일러 줄 것도 없다. 대문(서쪽 x=-15.5, z=0)으로 옮긴다.
        /// </summary>
        private static void Entrance(Scene scene, System.Text.StringBuilder log)
        {
            var cam = Camera.main;
            if (cam == null) { log.AppendLine("  ※ 카메라를 못 찾아 들어서는 자리는 못 옮겼다"); return; }
            var t = cam.transform.root;
            t.position = new Vector3(-13.50f, 1.60f, 0f);
            t.rotation = Quaternion.Euler(0f, 90f, 0f);               // 마당 쪽(+x)
            log.AppendLine("  · 들어서는 자리를 대문 앞으로 옮겼다 (-13.5, 0) — 여태 문서고 <b>안</b>이었다");

            // <b>하늘을 도로 켠다.</b> 카메라가 배경을 검정(0.06,0.06,0.08)으로 칠하고
            // 있었다 — 1막이 한밤중이라 그렇게 두었던 것이 그대로 따라왔다. 2막은 아침이고
            // 관아는 마당이 훤한 데인데, 담 너머가 죄 먹빛이라 낮인지 밤인지 알 수가 없었다.
            if (cam.clearFlags == CameraClearFlags.SolidColor)
            {
                cam.clearFlags = CameraClearFlags.Skybox;
                log.AppendLine("  · 배경을 하늘로 되돌렸다 — 검정으로 칠하고 있어 담 너머가 먹빛이었다");
            }
        }

        /// <summary>
        /// 서리의 심문 데이터. 없으면 만든다.
        ///
        /// 서리는 사건의 인물이 아니라 <b>이 관아의 사람</b>이다. 그래서 아는 것도
        /// 문서가 어디 있는지, 누구를 어떻게 부르는지 같은 것뿐이다. 옹씨 집안 일은
        /// 소문으로만 알고, 아전이 할 소리가 아니라며 끝을 흐린다.
        /// </summary>
        private static string EnsureSeori(System.Text.StringBuilder log)
        {
            if (AssetDatabase.LoadAssetAtPath<InterrogationCharacter>(SeoriAsset) != null) return SeoriAsset;

            var ch = ScriptableObject.CreateInstance<InterrogationCharacter>();
            ch.characterName = "서리";
            ch.caseId = CaseId.Case1_Onggojip;
            ch.persona =
                "관아 서리(胥吏). 이 고을 문서를 맡아 보는 아전이다. 나이는 쉰 언저리, 여기서만 스무 해를 있었다.\n\n" +
                "[말투] 어사 앞이라 허리가 굽는다. '올시다', '합지요', '아이고' 를 붙인다. " +
                "다만 굽실거릴 뿐 겁을 먹지는 않는다 — 제 잘못이 아닌 일이니까.\n\n" +
                "[아는 것] 문서고 어디에 무엇이 있는지는 훤하다. 호적대장·호구단자·입안대장·환상대장이 " +
                "어느 서가 어느 궤에 들었는지 다 안다. 요 몇 해 종이 더미가 어수선해진 것도 안다.\n\n" +
                "[모르는 것] 옹씨 집안 속사정은 모른다. 소문으로 '그 댁 어른이 요새 좀 달라지셨다' 는 말은 " +
                "들었으나, 아전이 할 소리가 아니라며 끝을 흐린다. 캐물으면 '소인이 어찌 알겠습니까' 로 물러선다.\n\n" +
                "[하지 않는 것] 사건에 대한 판단을 먼저 내놓지 않는다. 어사가 물으면 문서가 어디 있는지, " +
                "사람을 어떻게 부르는지만 대답한다.";
            ch.openingLine = "예, 어사또. 무엇을 찾아 드릴깝쇼.";
            ch.recallLine = "예예, 또 무엇을 찾으십니까.";
            ch.closingLine = "문서고는 저 아래올시다. 종이 더미가 좀 어수선합니다만…";
            ch.topics = new System.Collections.Generic.List<TopicQuestion>
            {
                new TopicQuestion {
                    question = "이 고을 호적은 어디 있소?",
                    mockAnswer = "문서고 안쪽 서가올시다. 식년마다 한 벌씩 넣어 두지요.\n" +
                                 "다만 근래 종이 더미에 묻힌 것이 있어… 좀 헤집으셔야 할 겝니다." },
                new TopicQuestion {
                    question = "옹덕구의 집을 아시오?",
                    mockAnswer = "알다마다요. 이 고을에서 제일 너른 집 아닙니까.\n" +
                                 "…그 댁 어른이 요새 좀 달라지셨다는 말은 있습니다만, 아전이 할 소리는 아니지요." },
                new TopicQuestion {
                    question = "사람은 어찌 부르오?",
                    mockAnswer = "동헌 마루에 오르시면 됩니다. 뜰에 대령시키지요.\n" +
                                 "부르시는 대로 하나씩 세우겠습니다." },
            };
            ch.evidenceGates = new System.Collections.Generic.List<EvidenceGate>();

            if (!AssetDatabase.IsValidFolder(DataDir))
                Debug.LogWarning("[관아] " + DataDir + " 가 없다 — 서리 데이터를 못 만든다");
            AssetDatabase.CreateAsset(ch, SeoriAsset);
            AssetDatabase.SaveAssets();
            log.AppendLine("  · 서리의 심문 데이터를 만들었다 (" + SeoriAsset + ")");
            return SeoriAsset;
        }

        // ── 잔손 ──

        private static GameObject Find(Scene scene, string name)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t.gameObject;
            return null;
        }

        private static void Set(Object target, System.Action<SerializedObject> edit)
        {
            if (target == null) return;
            var so = new SerializedObject(target);
            edit(so);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
