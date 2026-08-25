using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using IMUNROK.Common;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>심문을 동헌 안으로 들인다.</b> 메뉴: [이문록 ▸ 관아 ▸ ⑫ 동헌 안에서 심문하게]
    ///
    /// <b>왜 안으로 들이나.</b> 처음에는 죄인을 뜰(0.00)에 세우고 어사가 마루(2.21)에서
    /// 내려다보게 두었다. 실제 동헌 재판이 그렇게 생겼고, 다섯 걸음에 두 길 높이라는
    /// 것이 곧 위계였다.
    ///
    /// 그런데 이 게임에서 심문은 <b>얼굴을 읽는 일</b>이다. 甲과 乙은 닮았고, 무너지는
    /// 것도 얼굴에서 먼저 무너진다. 여섯 걸음 밖 두 길 아래에 선 사람은 <b>얼굴이 안 보인다</b> —
    /// 위계는 서는데 심문이 안 된다. 그러면 그 자리는 재판이 아니라 그림이다.
    ///
    /// 그래서 안으로 들인다. 대청은 <b>5.5m 깊이 × 9m 폭</b>에 천장이 3.4~4.3m 고,
    /// 앞이 통째로 트여 있어 안에 앉아도 뜰이 내다보인다. 재어 보니 사람 둘이 마주 앉고도
    /// 남는다 — 어사(15.3)와 불려 온 사람(12.4) 사이가 <b>2.9m</b> 다.
    ///
    /// <b>위계는 단으로 되찾는다.</b> 마주 앉는 순간 눈높이가 같아지면 심문이 아니라
    /// 면담이 된다. 어사 자리 밑에 한 단(0.30m)을 놓아 눈이 3.56 에 오게 한다 —
    /// 마주 선 사람 눈(3.81)보다 낮지만, 꿇는 동작이 들어오면 그때 확실히 위가 된다.
    ///
    /// <b>기다리는 사람은 뜰에 둔다.</b> 넷을 다 안에 들이면 대청이 대기실이 되고,
    /// 하나씩 불러 세우는 뜻이 없어진다. 뜰에 서 있다가 이름이 불리면 <b>계단을 올라</b>
    /// 들어온다. 그 계단이 곧 "불려 들어간다"는 말이다.
    ///
    /// <b>계단은 한 군데뿐이다.</b> 기단 앞면은 죄 1.65m 벽이고 오를 수 있는 데는
    /// 한가운데(z 폭 2.2m) 하나다. 그래서 뜰에서 마루로 곧장 직선을 그으면 벽에 코를
    /// 박고 선다 — 계단 아래를 거쳐 가게 경유점을 하나 둔다.
    ///
    /// 두 번 눌러도 두 벌이 안 놓인다.
    /// </summary>
    public static class GwanaCourt
    {
        private const string CushionMat = "Assets/_Project/_Common/Sets/Gwana/Donheon/Materials/M_어사_방석.mat";
        private const string StoneMat = "Assets/_Project/_Common/Sets/Gwana/Donheon/Materials/M_Wood_Tile.mat";

        /// <summary>대청 바닥 높이.</summary>
        private const float Maru = 2.21f;

        /// <summary>어사가 앉는 자리 — 대청 안쪽. 뒷벽(16.93)에서 한 걸음 반 앞.</summary>
        private static readonly Vector3 Seat = new Vector3(15.30f, Maru, 0f);

        /// <summary>불려 온 사람이 서는 자리 — 대청 앞턱(11.5) 안쪽. 어사와 2.9m.</summary>
        private static readonly Vector3 Front = new Vector3(12.40f, Maru, 0f);

        /// <summary>계단 아래. 뜰에서 마루로 오르는 <b>유일한</b> 길목이다.</summary>
        private static readonly Vector3 Stairs = new Vector3(7.40f, 0f, 0f);

        /// <summary>어사 자리 단의 높이. 마주 앉아 눈높이가 같아지지 않게.</summary>
        private const float Dais = 0.30f;

        // 뜰에서 기다리는 줄. 甲·乙 을 나란히 두는 것은 <b>둘이 닮았다</b>(J04)를
        // 눈으로 한 번 더 말하기 위해서다. 이름표 차례와 같은 순서로 선다.
        private static readonly (string 사람, float z)[] Wait =
        {
            ("甲", -3.00f), ("乙", -1.00f), ("아내", 1.00f), ("늙은하인", 3.00f),
        };

        private const float WaitX = 5.60f;

        [MenuItem("이문록/관아/⑫ 동헌 안에서 심문하게")]
        public static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.name.Contains("Gwana"))
            {
                Debug.LogWarning("[관아] 관아 씬을 열고 누르십시오(지금은 " + scene.name + ").");
                return;
            }

            var log = new System.Text.StringBuilder("[관아] 심문을 동헌 안으로\n");

            var front = Spot(scene, "대청_앞자리", Front, 90f, log);      // 어사(+x)를 본다
            var stairs = Spot(scene, "뜰_계단아래", Stairs, 90f, log);
            var seatSpot = Spot(scene, "어사_자리", Seat, 270f, log);      // 뜰(-x)을 본다

            Daises(scene, log);
            Chair(scene, log);
            Sit(scene, seatSpot.transform, front.transform, log);
            Bench(scene, seatSpot.transform, log);

            foreach (var w in Wait)
            {
                var person = Find(scene, w.사람);
                if (person == null) { log.AppendLine("  ※ " + w.사람 + " 이 없다 — ⑨ 를 먼저 누르십시오"); continue; }

                var wait = Spot(scene, "뜰_대기_" + w.사람, new Vector3(WaitX, 0f, w.z), 90f, log);
                person.transform.position = wait.transform.position;
                person.transform.rotation = wait.transform.rotation;

                var summon = person.GetComponent<CourtSummon>();
                if (summon == null) summon = Undo.AddComponent<CourtSummon>(person);
                Set(summon, so =>
                {
                    so.FindProperty("_waitSpot").objectReferenceValue = wait.transform;
                    so.FindProperty("_frontSpot").objectReferenceValue = front.transform;
                    so.FindProperty("_faceTarget").objectReferenceValue = seatSpot.transform;
                    so.FindProperty("_speed").floatValue = 1.25f;
                    // 계단 아래를 거쳐 오른다. 곧장 걸으면 기단 벽에 붙어 선다.
                    var via = so.FindProperty("_via");
                    via.arraySize = 1;
                    via.GetArrayElementAtIndex(0).objectReferenceValue = stairs.transform;
                });

                float walk = Vector3.Distance(new Vector3(WaitX, 0f, w.z), Stairs)
                           + Vector3.Distance(Stairs, new Vector3(Front.x, 0f, Front.z));
                log.AppendLine("  · " + w.사람 + " — 뜰(" + WaitX.ToString("F1") + ", " + w.z.ToString("F1")
                             + ") → 계단 → 대청 앞자리, 걸어서 " + walk.ToString("F1") + "m");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine("  어사 눈 " + (Maru + Dais + 1.24f).ToString("F2") + "  ·  마주 선 사람과 "
                         + Vector3.Distance(Seat, Front).ToString("F1") + "m");
            Debug.Log(log.ToString());
        }

        // ── 어사 자리 ──

        /// <summary>
        /// 어사가 앉는 단(壇).
        ///
        /// 마루에 그냥 앉으면 마주 선 사람과 <b>눈높이가 거의 같아진다</b>. 그러면
        /// 심문이 아니라 면담이다. 한 단 올려 앉는 것은 실제 동헌도 그렇다 —
        /// 수령은 교의에 앉거나 한 단 높은 자리에 앉는다.
        /// </summary>
        private static void Daises(Scene scene, System.Text.StringBuilder log)
        {
            var go = Find(scene, "어사_단");
            if (go == null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "어사_단";
                SceneManager.MoveGameObjectToScene(go, scene);
                Undo.RegisterCreatedObjectUndo(go, "동헌 자리");
                log.AppendLine("  · 어사 자리에 한 단(" + Dais.ToString("F2") + "m)을 놓았다 — 마주 앉아 눈높이가 같아지지 않게");
            }
            go.transform.position = new Vector3(Seat.x, Seat.y + Dais * 0.5f, Seat.z);
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = new Vector3(1.70f, Dais, 1.70f);
            var mr = go.GetComponent<MeshRenderer>();
            var wood = AssetDatabase.LoadAssetAtPath<Material>(StoneMat);
            if (mr != null && wood != null) mr.sharedMaterial = wood;
        }

        /// <summary>
        /// <b>교의(交椅)</b> — 어사가 앉는 의자.
        ///
        /// 여태 마루에 방석 하나였다. 그런데 방석에 앉으면 <b>눈이 바닥에서 한 자</b>
        /// 남짓이라, 마주 선 사람을 한참 올려다보게 된다. 그리고 마루에 퍼져 앉은
        /// 사람은 재판을 하는 것이 아니라 <b>손님으로 앉은 것</b>으로 보인다.
        ///
        /// 조선의 수령이 동헌에서 앉는 것도 방석이 아니라 교의다 — 등받이와 팔걸이가
        /// 있는 높은 의자. 앉는 자리가 높으면 그것만으로 자리가 갈린다.
        ///
        /// <b>널조각을 짜 맞춰 만든다.</b> 의자 모델이 아직 없고, 이 물건은 다리 넷과
        /// 좌판·등받이·팔걸이로 다 되는 모양이라 상자 열 개면 선다. 나중에 진짜 모델이
        /// 오면 이 밑에 넣고 이것만 끄면 된다.
        ///
        /// <b>누를 것은 좌판</b>이다. 좌판에만 트리거를 두고 나머지 조각은 콜라이더를
        /// 뗀다 — 안 그러면 일어서서 걷다가 의자 다리에 걸린다.
        /// </summary>
        private static void Chair(Scene scene, System.Text.StringBuilder log)
        {
            // 예전에 놓았던 방석은 걷는다 — 이제 좌판이 그 일을 한다
            var oldCushion = Find(scene, "어사_방석");
            if (oldCushion != null && oldCushion.transform.parent == null)
            {
                Undo.DestroyObjectImmediate(oldCushion);
                log.AppendLine("  · 마루에 놓았던 방석을 걷었다 — 교의가 대신한다");
            }

            var root = Find(scene, "어사_교의");
            bool made = root == null;
            if (made)
            {
                root = new GameObject("어사_교의");
                SceneManager.MoveGameObjectToScene(root, scene);
                Undo.RegisterCreatedObjectUndo(root, "어사 교의");
            }
            root.transform.position = new Vector3(Seat.x, Seat.y + Dais, Seat.z);
            root.transform.rotation = Quaternion.Euler(0f, 270f, 0f);   // 뜰(-x)을 본다

            var wood = AssetDatabase.LoadAssetAtPath<Material>(StoneMat);
            var red = EnsureCushionMat(log);

            // 이 의자는 제 앞(local +z)이 뜰 쪽이다. 등받이는 뒤(-z)에 선다.
            const float SeatH = 0.46f;   // 좌판 밑까지
            const float SeatW = 0.54f, SeatD = 0.50f, Plank = 0.06f;
            const float Leg = 0.06f;

            Part(root, "다리_앞왼", new Vector3(-SeatW * 0.5f + Leg * 0.5f, SeatH * 0.5f,  SeatD * 0.5f - Leg * 0.5f), new Vector3(Leg, SeatH, Leg), wood);
            Part(root, "다리_앞오", new Vector3( SeatW * 0.5f - Leg * 0.5f, SeatH * 0.5f,  SeatD * 0.5f - Leg * 0.5f), new Vector3(Leg, SeatH, Leg), wood);
            Part(root, "다리_뒤왼", new Vector3(-SeatW * 0.5f + Leg * 0.5f, SeatH * 0.5f, -SeatD * 0.5f + Leg * 0.5f), new Vector3(Leg, SeatH, Leg), wood);
            Part(root, "다리_뒤오", new Vector3( SeatW * 0.5f - Leg * 0.5f, SeatH * 0.5f, -SeatD * 0.5f + Leg * 0.5f), new Vector3(Leg, SeatH, Leg), wood);

            Part(root, "가로대", new Vector3(0f, SeatH * 0.45f, -SeatD * 0.5f + Leg * 0.5f), new Vector3(SeatW - Leg, 0.045f, 0.035f), wood);

            // 등받이 — 뒤에 서서 어깨 높이까지
            Part(root, "등받이", new Vector3(0f, SeatH + 0.30f, -SeatD * 0.5f + 0.03f), new Vector3(SeatW, 0.60f, 0.05f), wood);
            Part(root, "등마루", new Vector3(0f, SeatH + 0.62f, -SeatD * 0.5f + 0.03f), new Vector3(SeatW + 0.08f, 0.07f, 0.09f), wood);

            // 팔걸이 — 앞 기둥에 얹는다
            Part(root, "팔기둥_왼", new Vector3(-SeatW * 0.5f + 0.03f, SeatH + 0.11f,  SeatD * 0.5f - 0.06f), new Vector3(0.05f, 0.22f, 0.05f), wood);
            Part(root, "팔기둥_오", new Vector3( SeatW * 0.5f - 0.03f, SeatH + 0.11f,  SeatD * 0.5f - 0.06f), new Vector3(0.05f, 0.22f, 0.05f), wood);
            Part(root, "팔걸이_왼", new Vector3(-SeatW * 0.5f + 0.03f, SeatH + 0.24f, -0.02f), new Vector3(0.055f, 0.05f, SeatD - 0.04f), wood);
            Part(root, "팔걸이_오", new Vector3( SeatW * 0.5f - 0.03f, SeatH + 0.24f, -0.02f), new Vector3(0.055f, 0.05f, SeatD - 0.04f), wood);

            // <b>좌판이 곧 앉는 자리다.</b> 여기만 눌린다.
            var seat = Part(root, "좌판", new Vector3(0f, SeatH + Plank * 0.5f, 0f), new Vector3(SeatW, Plank, SeatD), red);
            var col = seat.GetComponent<Collider>();
            if (col == null) col = seat.AddComponent<BoxCollider>();
            col.isTrigger = true;      // 걸어 다니다 걸리지 않게. 클릭 레이는 트리거도 짚는다
            col.enabled = true;

            var cush = seat.GetComponent<SeatCushion>();
            if (cush == null) cush = Undo.AddComponent<SeatCushion>(seat);
            Set(cush, so =>
            {
                so.FindProperty("_title").stringValue = "어사 교의";
                so.FindProperty("_idleBody").stringValue = "동헌 대청. 앞이 트여 뜰이 내다보인다.";
                so.FindProperty("_offeredBody").stringValue = "동헌 대청. 앞이 트여 뜰이 내다보인다.";
                so.FindProperty("_hint").stringValue = "(눌러 앉기)";
                so.FindProperty("_offeredAtStart").boolValue = true;
                so.FindProperty("_maxTouchDistance").floatValue = 3f;
                so.FindProperty("_hoverRise").floatValue = 0f;   // 좌판이 들썩이면 의자가 부서진 것처럼 보인다
            });

            if (made) log.AppendLine("  · 어사 교의를 짜 넣었다 — 좌판 " + (Seat.y + Dais + SeatH + Plank).ToString("F2")
                                   + ", 앉으면 눈 " + (Seat.y + Dais + 1.24f).ToString("F2"));
        }

        /// <summary>의자 조각 하나. 콜라이더는 뗀다 — 좌판만 남긴다.</summary>
        private static GameObject Part(GameObject root, string name, Vector3 lp, Vector3 size, Material mat)
        {
            var t = root.transform.Find(name);
            GameObject go;
            if (t == null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                go.transform.SetParent(root.transform, false);
                var c = go.GetComponent<Collider>();
                if (c != null) Object.DestroyImmediate(c);   // 좌판만 나중에 도로 붙인다
            }
            else go = t.gameObject;

            go.transform.localPosition = lp;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = size;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null && mat != null) mr.sharedMaterial = mat;
            return go;
        }

        /// <summary>어사를 앉힐 채비. 카메라에 붙는다.</summary>
        private static void Sit(Scene scene, Transform seat, Transform look, System.Text.StringBuilder log)
        {
            var cam = Camera.main;
            if (cam == null) { log.AppendLine("  ※ 카메라를 못 찾아 앉는 채비는 못 했다"); return; }

            var ps = cam.GetComponent<PlayerSeat>();
            if (ps == null) { ps = Undo.AddComponent<PlayerSeat>(cam.gameObject); log.AppendLine("  · 카메라에 앉는 채비를 붙였다"); }

            Set(ps, so =>
            {
                so.FindProperty("_seatSpot").objectReferenceValue = seat;
                so.FindProperty("_lookAt").objectReferenceValue = look;      // 앉으면서 앞자리를 본다
                so.FindProperty("_seatedEyeHeight").floatValue = 1.24f;   // 교의에 앉은 눈(단 위에서)
                // <b>동헌에서는 제 발로 일어선다.</b> 물러가 줄 사람이 없다.
                //
                // Key 는 새 입력 꾸러미의 열거인데 이 편집기 어셈블리는 그것을 참조하지
                // 않는다. 형을 직접 적으면 컴파일이 안 되므로 <b>이름으로</b> 찾아 넣는다.
                var rise = so.FindProperty("_riseKey");
                int idx = System.Array.IndexOf(rise.enumNames, "Space");
                if (idx >= 0) rise.enumValueIndex = idx;
                so.FindProperty("_seatedLine").stringValue = "교의에 앉았다.   (*Space* 일어서기)";
            });
        }

        /// <summary>부르기 판은 어사 자리에서 잰다 — 대청 안에 들어서야 이름표가 뜬다.</summary>
        private static void Bench(Scene scene, Transform seat, System.Text.StringBuilder log)
        {
            var go = Find(scene, "_부르기판");
            if (go == null) { log.AppendLine("  ※ 부르기 판이 없다 — ⑨ 를 먼저"); return; }
            go.transform.position = seat.position;
            var bench = go.GetComponent<InterrogationBench>();
            if (bench == null) return;
            Set(bench, so =>
            {
                // 대청(11.5~17.0)을 덮고 계단 위쪽까지. 뜰 한복판에서는 안 뜬다 —
                // 마당에 서서 사람을 부르는 것은 어사가 하는 일이 아니다.
                so.FindProperty("_radius").floatValue = 6.5f;
                so.FindProperty("_enterLine").stringValue =
                    "동헌 대청이다. 뜰에 넷이 서 있다 — *이름을 부르면* 하나가 올라온다.";
            });
            log.AppendLine("  · 부르기 판을 어사 자리로 옮겼다 (반경 6.5 — 대청 안에서만 뜬다)");
        }

        // ── 잔손 ──

        private static GameObject Spot(Scene scene, string name, Vector3 pos, float yaw, System.Text.StringBuilder log)
        {
            var go = Find(scene, name);
            if (go == null)
            {
                go = new GameObject(name);
                SceneManager.MoveGameObjectToScene(go, scene);
                Undo.RegisterCreatedObjectUndo(go, "동헌 자리");
                log.AppendLine("  · " + name + " 을 잡았다 " + pos.ToString("F2"));
            }
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }

        private static Material EnsureCushionMat(System.Text.StringBuilder log)
        {
            var had = AssetDatabase.LoadAssetAtPath<Material>(CushionMat);
            if (had != null) return had;
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_어사_방석" };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.33f, 0.08f, 0.08f, 1f));
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.12f);
            AssetDatabase.CreateAsset(m, CushionMat);
            AssetDatabase.SaveAssets();
            log.AppendLine("  · 방석 감을 만들었다");
            return m;
        }

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
