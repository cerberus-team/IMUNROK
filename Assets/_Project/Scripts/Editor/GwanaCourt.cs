using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using IMUNROK.Common;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>동헌을 재판하는 자리로 만든다.</b> 메뉴: [이문록 ▸ 관아 ▸ ⑫ 부르면 나오게 + 어사 자리]
    ///
    /// 여태 넷은 뜰에 <b>한 줄로 서 있기만</b> 했다. 이름표를 갈아 눌러도 몸은 그대로라,
    /// 부르는 것이 아니라 말할 상대를 고르는 것에 가까웠다 — 지금 누구에게 묻고 있는지를
    /// 화면 아래 글씨로만 알 수 있었다.
    ///
    /// <b>㉠ 나오고 물러간다.</b> 뜰 뒤에 대기 자리 넷을 두고, 어사 앞에 나와 설 자리를
    /// 하나 둔다. 부르면 그 사람이 <b>대여섯 걸음 걸어 나와</b> 마루를 올려다보고 서고,
    /// 다른 이를 부르면 등을 보이고 물러난다. 그 등이 "이 사람 이야기는 여기서 끊긴다"는
    /// 말이 된다. 걷는 클립이 없는 인물은 클립 없이 걷는다 — 들어오면 이름만 적어 주면 된다.
    ///
    /// <b>㉡ 어사가 앉는다.</b> 마루 앞턱에 방석을 놓고, 눌러 앉게 한다. 저절로 앉히지
    /// 않는 까닭은 사랑방과 같다 — 시야가 저 혼자 내려가면 앉은 것이 아니라 가라앉은 것이다.
    ///
    /// 다만 <b>일어설 길을 함께 준다</b>. 사랑방에서는 상대가 물러가면서 일으켜 주었는데,
    /// 동헌에는 물러가 줄 사람이 없다. 부를 사람을 다 부르고 나면 일어나 서고로 가야 하는데
    /// 앉은 채로 갇힌다. Space 를 준다.
    ///
    /// 두 번 눌러도 두 벌이 안 놓인다.
    /// </summary>
    public static class GwanaCourt
    {
        private const string MatPath = "Assets/_Project/_Common/Sets/Gwana/Donheon/Materials/M_어사_방석.mat";

        /// <summary>어사 앞에 나와 서는 자리. 마루 앞턱(11.5)에서 네 걸음 앞이다.</summary>
        private static readonly Vector3 Front = new Vector3(7.30f, 0f, 0f);

        /// <summary>어사가 앉는 자리 — 마루 앞턱에서 반 걸음 안. ⑨ 가 잡아 둔 자리와 같다.</summary>
        private static readonly Vector3 Seat = new Vector3(12.00f, 2.21f, 0f);

        // 뒤에서 기다리는 줄. 甲·乙 을 나란히 두는 것은 <b>둘이 닮았다</b>(J04)를
        // 눈으로 한 번 더 말하기 위해서다. 이름표 차례와 같은 순서로 선다.
        private static readonly (string 사람, float z)[] Wait =
        {
            ("甲", -3.00f), ("乙", -1.00f), ("아내", 1.00f), ("늙은하인", 3.00f),
        };

        private const float WaitX = 3.80f;

        [MenuItem("이문록/관아/⑫ 부르면 나오게 + 어사 자리")]
        public static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.name.Contains("Gwana"))
            {
                Debug.LogWarning("[관아] 관아 씬을 열고 누르십시오(지금은 " + scene.name + ").");
                return;
            }

            var log = new System.Text.StringBuilder("[관아] 동헌을 재판하는 자리로\n");

            var front = Spot(scene, "뜰_앞자리", Front, 90f, log);      // 마루(+x)를 본다
            var seatSpot = Find(scene, "어사_자리") ?? Spot(scene, "어사_자리", Seat, 270f, log).gameObject;
            seatSpot.transform.position = Seat;
            seatSpot.transform.rotation = Quaternion.Euler(0f, 270f, 0f);

            Cushion(scene, log);
            Sit(scene, seatSpot.transform, front.transform, log);

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
                });
                log.AppendLine("  · " + w.사람 + " — 대기 자리 (" + WaitX.ToString("F1") + ", " + w.z.ToString("F1") + ") 에서 앞자리까지 "
                             + Vector3.Distance(new Vector3(WaitX, 0f, w.z), Front).ToString("F1") + "m");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(log.ToString());
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
                log.AppendLine("  · " + name + " 을 잡았다");
            }
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }

        /// <summary>
        /// 어사가 앉을 방석. 누를 것이 있어야 앉는다.
        ///
        /// <b>권해 주는 사람이 없는 자리</b>라 처음부터 앉을 수 있게 열어 둔다.
        /// 사랑방 방석은 주인이 권해야 앉는 자리지만, 동헌 마루는 어사가 제 발로
        /// 올라가 제 자리에 앉는 데다 — 거기서 누가 권하기를 기다리면 영영 못 앉는다.
        /// </summary>
        private static void Cushion(Scene scene, System.Text.StringBuilder log)
        {
            var go = Find(scene, "어사_방석");
            if (go == null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "어사_방석";
                SceneManager.MoveGameObjectToScene(go, scene);
                Undo.RegisterCreatedObjectUndo(go, "동헌 자리");
                log.AppendLine("  · 마루 앞턱에 방석을 놓았다 — 눌러 앉는다");
            }
            go.transform.position = new Vector3(Seat.x, Seat.y + 0.035f, Seat.z);
            go.transform.rotation = Quaternion.Euler(0f, 270f, 0f);
            go.transform.localScale = new Vector3(0.62f, 0.07f, 0.62f);

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = EnsureMaterial(log);

            var col = go.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;   // 밟고 올라서는 물건이 아니다

            var cush = go.GetComponent<SeatCushion>();
            if (cush == null) cush = Undo.AddComponent<SeatCushion>(go);
            Set(cush, so =>
            {
                so.FindProperty("_title").stringValue = "어사 자리";
                so.FindProperty("_idleBody").stringValue = "동헌 마루. 뜰이 내려다보인다.";
                so.FindProperty("_offeredBody").stringValue = "동헌 마루. 뜰이 내려다보인다.";
                so.FindProperty("_hint").stringValue = "(눌러 앉기)";
                so.FindProperty("_offeredAtStart").boolValue = true;
                so.FindProperty("_maxTouchDistance").floatValue = 3f;
            });
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
                so.FindProperty("_lookAt").objectReferenceValue = look;      // 앉으면서 뜰을 본다
                so.FindProperty("_seatedEyeHeight").floatValue = 1.05f;      // 마루에 앉은 눈높이
                // <b>동헌에서는 제 발로 일어선다.</b> 물러가 줄 사람이 없다.
                //
                // Key 는 새 입력 꾸러미의 열거인데 이 편집기 어셈블리는 그것을 참조하지
                // 않는다. 형을 직접 적으면 컴파일이 안 되므로 <b>이름으로</b> 찾아 넣는다.
                var rise = so.FindProperty("_riseKey");
                int idx = System.Array.IndexOf(rise.enumNames, "Space");
                if (idx >= 0) rise.enumValueIndex = idx;
                so.FindProperty("_seatedLine").stringValue = "마루에 앉았다. 뜰이 내려다보인다.   (*Space* 일어서기)";
            });
            log.AppendLine("  · 앉으면 눈높이 " + (2.21f + 1.05f).ToString("F2") + " — 뜰에 선 사람 머리(1.9)보다 위다");
        }

        private static Material EnsureMaterial(System.Text.StringBuilder log)
        {
            var had = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (had != null) return had;
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_어사_방석" };
            // 관아의 붉은 전(氈). 마루 나무빛과 갈려야 앉을 자리로 읽힌다.
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.33f, 0.08f, 0.08f, 1f));
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.12f);
            AssetDatabase.CreateAsset(m, MatPath);
            AssetDatabase.SaveAssets();
            log.AppendLine("  · 방석 감을 만들었다 (" + MatPath + ")");
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
