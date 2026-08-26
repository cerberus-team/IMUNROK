using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>서고에 불을 들인다.</b> 메뉴: [이문록 ▸ 관아 ▸ ⑯ 서고에 불을 들이다]
    ///
    /// 등불과 돋보기가 쓸모없어 보였던 까닭은 설계가 아니라 <b>일감이 없어서</b>였다.
    /// 코드를 읽어 보면 다 갖춰져 있다 — <see cref="LanternReveal"/> 는 단추 없이
    /// 불·종이·눈이 한 줄에 서면 배접을 비추고, <see cref="Firelight"/> 는 방에 놓인
    /// 불에도 붙는다. 그런데 재어 보니:
    ///
    ///   · 관아 씬에 <b>Firelight 가 하나도 없다</b> — 비출 불이 없다.
    ///   · <b>litPage/litPrint 를 채운 문서가 하나도 없다</b> — 비출 것이 없다.
    ///   · 서고가 <b>대낮처럼 밝다</b> — 불을 켤 까닭이 없다.
    ///
    /// 그래서 <c>LanternReveal.Update()</c> 는 늘 첫 두 줄에서 돌아 나갔다.
    /// 이 도구는 그 셋을 채운다. 새 장치를 만드는 것이 아니라 <b>연료를 넣는 일</b>이다.
    ///
    /// ㉠ <b>등경을 세운다</b> — 빈 채로. 플레이어가 들고 온 불을 제 손으로 얹어야
    ///    두 손이 풀린다(<see cref="LanternStand"/>).
    /// ㉡ <b>방을 어둡게 한다</b> — 사람이 안에 있는 동안만(<see cref="RoomDarkness"/>).
    /// ㉢ <b>입안대장에 지운 글을 넣는다</b> — 등불은 종이 <b>속</b>을, 돋보기는 종이
    ///    <b>겉</b>을 읽게 나눈다. 한 종이에서 두 도구가 서로 다른 것을 본다.
    ///
    /// 두 번 눌러도 두 벌이 안 선다.
    /// </summary>
    public static class SeogoLight
    {
        /// <summary>등경이 설 자리. 지도로 내려서는 데(-1.5, 0.82, -6.2) 바로 앞이다.</summary>
        private static readonly Vector3 Stand = new Vector3(-2.00f, 0.82f, -7.60f);

        /// <summary>불꽃이 오는 높이. 마루에서 0.98 — 종이를 눈앞에 들면 그 너머에 온다.</summary>
        private const float Flame = 0.98f;

        /// <summary>등불에 비추면 배어 나오는 면. 배커가 굽는다.</summary>
        private const string LitPage =
            "Assets/_Project/Onggojip/Art/Textures/T_Doc_G03_Ipan_lit.png";

        private const string LanternFbx =
            "Assets/_Project/Art/Tools/Lantern/Meshy_AI_Antique_Korean_Handhe_0811153903_texture.fbx";

        [MenuItem("이문록/관아/⑯ 서고에 불을 들이다")]
        public static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.name.Contains("Gwana"))
            {
                Debug.LogWarning("[관아] 관아 씬을 열고 누르십시오(지금은 " + scene.name + ").");
                return;
            }

            var log = new System.Text.StringBuilder("[관아] 서고에 불을 들인다\n");
            Lamp(scene, log);
            Dark(scene, log);
            Ledger(log);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(log.ToString());
        }

        // ── ㉠ 등경 ──────────────────────────────

        private static void Lamp(Scene scene, System.Text.StringBuilder log)
        {
            var root = Find(scene, "등경");
            if (root == null)
            {
                root = new GameObject("등경");
                SceneManager.MoveGameObjectToScene(root, scene);
                Undo.RegisterCreatedObjectUndo(root, "등경");
                log.AppendLine("  · 등경을 세웠다");
            }
            root.transform.SetPositionAndRotation(Stand, Quaternion.identity);
            root.transform.localScale = Vector3.one;

            var wood = Wood();
            Part(root.transform, "받침", PrimitiveType.Cylinder, new Vector3(0f, 0.025f, 0f), new Vector3(0.30f, 0.025f, 0.30f), wood);
            Part(root.transform, "기둥", PrimitiveType.Cylinder, new Vector3(0f, 0.50f, 0f), new Vector3(0.05f, 0.475f, 0.05f), wood);
            Part(root.transform, "걸이", PrimitiveType.Cube, new Vector3(0f, Flame + 0.13f, 0.05f), new Vector3(0.05f, 0.04f, 0.22f), wood);

            // ── 얹혔을 때 보일 등불 ──
            var propT = root.transform.Find("놓인등불");
            GameObject prop;
            if (propT == null)
            {
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(LanternFbx);
                if (fbx == null) { log.AppendLine("  ※ 등불 모델을 못 찾았다: " + LanternFbx); return; }
                prop = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
                PrefabUtility.UnpackPrefabInstance(prop, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                prop.name = "놓인등불";
                Undo.RegisterCreatedObjectUndo(prop, "놓인 등불");
                log.AppendLine("  · 얹을 등불을 걸어 두었다(처음엔 비어 있다)");
            }
            else prop = propT.gameObject;

            // 모델 크기를 재어 <b>불꽃이 Flame 높이에 오게</b> 앉힌다. 손으로 적어 두면
            // 모델을 바꿀 때마다 어긋난다.
            prop.transform.localRotation = Quaternion.identity;
            prop.transform.localScale = Vector3.one;
            prop.transform.localPosition = Vector3.zero;
            var b = Wrap(prop);
            float want = 0.34f;                                    // 등불 몸통 높이(m)
            if (b.size.y > 0.001f)
            {
                float k = want / b.size.y;
                prop.transform.localScale = Vector3.one * k;
                b = Wrap(prop);
            }
            prop.transform.localPosition += new Vector3(0f, Stand.y + Flame - b.center.y, 0f);

            // 재질을 입힌다. FBX 를 그냥 앉히면 기본 흰 재질이 붙어 어둠 속에서
            // 하얀 상자로 뜬다 — 실제로 한 번 그렇게 나왔다.
            var lampMat = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Art/Tools/Lantern/Lantern_Mat.mat");
            if (lampMat != null)
                foreach (var mr2 in prop.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mats = mr2.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = lampMat;
                    mr2.sharedMaterials = mats;
                }

            // 불빛
            var lightT = prop.transform.Find("불");
            GameObject lightGo = lightT != null ? lightT.gameObject : new GameObject("불");
            if (lightT == null) { lightGo.transform.SetParent(prop.transform, false); Undo.RegisterCreatedObjectUndo(lightGo, "등불 빛"); }
            // <b>불빛은 등롱 <i>밖</i>에 둔다.</b>
            //
            // 처음엔 등롱 한가운데에 두었다. 그랬더니 등불이 시뻘겋게 타 버렸다 —
            // 등롱은 한 뼘(0.34m)짜리 상자라 그 안쪽 면이 불에서 0.15m 다. 거리의
            // 제곱으로 줄어드는 빛을 그만큼 가까이서 맞으면 어떤 세기로도 탄다.
            //
            // 그런데 <b>등불은 제 빛으로 이미 빛난다</b>(Lantern_Mat 이 발광 재질이다).
            // 그러니 이 불빛이 할 일은 등불을 밝히는 것이 아니라 <b>방을 밝히는 것</b>뿐이다.
            // 몸통 위로 빼면 상자는 안 타고 방은 그대로 밝다. 종이를 비추는 자리
            // (Firelight.Where)로도 한 뼘 차이는 아무 상관이 없다.
            lightGo.transform.localPosition = new Vector3(0f, 0.24f, 0f);
            var light = lightGo.GetComponent<Light>();
            if (light == null) light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.80f, 0.52f);
            light.intensity = 1.1f;
            light.range = 7.0f;
            light.shadows = LightShadows.None;                     // 값이 싸다. 서고 안 물건이 많다

            var fire = lightGo.GetComponent<Firelight>();
            if (fire == null) fire = Undo.AddComponent<Firelight>(lightGo);
            var fso = new SerializedObject(fire);
            fso.FindProperty("_reach").floatValue = 2.6f;
            fso.FindProperty("_strength").floatValue = 1f;
            fso.FindProperty("_light").objectReferenceValue = light;
            fso.ApplyModifiedPropertiesWithoutUndo();

            prop.SetActive(false);                                  // 빈 등경으로 시작한다

            // ── 누를 수 있게 ──
            var col = root.GetComponent<BoxCollider>();
            if (col == null) col = Undo.AddComponent<BoxCollider>(root);
            col.center = new Vector3(0f, Flame * 0.6f, 0f);
            col.size = new Vector3(0.34f, Flame * 1.3f, 0.34f);

            var stand = root.GetComponent<LanternStand>();
            if (stand == null) stand = Undo.AddComponent<LanternStand>(root);
            var sso = new SerializedObject(stand);
            sso.FindProperty("_toolId").stringValue = "lantern";
            sso.FindProperty("_prop").objectReferenceValue = prop;
            sso.FindProperty("_maxTouchDistance").floatValue = 2.5f;
            sso.ApplyModifiedPropertiesWithoutUndo();
            log.AppendLine("  · 등경에 불을 얹고 도로 들 수 있다 — 얹으면 두 손이 빈다");
        }

        // ── ㉡ 어둠 ──────────────────────────────

        private static void Dark(Scene scene, System.Text.StringBuilder log)
        {
            var go = Find(scene, "서고_어둠");
            if (go == null)
            {
                go = new GameObject("서고_어둠");
                SceneManager.MoveGameObjectToScene(go, scene);
                Undo.RegisterCreatedObjectUndo(go, "서고 어둠");
                log.AppendLine("  · 서고를 어둡게 했다 — 사람이 안에 있는 동안만");
            }
            // 문서고 안: x -8~4, z -11.3~-5.3, 마루 0.82 ~ 지붕 밑동 3.30
            go.transform.SetPositionAndRotation(new Vector3(-2.00f, 2.05f, -8.30f), Quaternion.identity);
            go.transform.localScale = Vector3.one;

            var room = go.GetComponent<RoomDarkness>();
            if (room == null) room = Undo.AddComponent<RoomDarkness>(go);
            var so = new SerializedObject(room);
            so.FindProperty("_size").vector3Value = new Vector3(11.8f, 2.6f, 5.8f);
            so.FindProperty("_inside").floatValue = 0.16f;
            so.FindProperty("_adapt").floatValue = 2.4f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── ㉢ 입안대장 ──────────────────────────

        /// <summary>
        /// <b>한 종이를 두 도구가 나눠 읽는다.</b>
        ///
        /// 돋보기는 종이의 <b>겉</b>을 본다 — 먹빛이 다르고 획 끝이 뭉개졌고 종이 결이
        /// 일어난 것. 곧 <b>손댔다</b>는 사실이다.
        /// 등불은 종이의 <b>속</b>을 본다 — 긁어낸 자리 밑에 남은 글. 곧 <b>무엇이었나</b>다.
        ///
        /// 하나만으로는 반만 안다. 그리고 두 도구가 각각 제가 잘하는 일을 한다 —
        /// 확대경은 잘 보는 물건이고, 불은 꿰뚫어 보는 물건이다.
        /// </summary>
        private static void Ledger(System.Text.StringBuilder log)
        {
            InspectableNote note = null;
            foreach (var n in Object.FindObjectsByType<InspectableNote>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (n.name == "입안대장") note = n;
            if (note == null) { log.AppendLine("  ※ 입안대장을 못 찾았다"); return; }

            var so = new SerializedObject(note);

            // <b>비친 면</b>. 같은 장부를 한 번 더 굽되 다섯째 줄 밑에 눌려 있던 글을
            // 흐리게 얹은 것이다(docs.json 의 T_Doc_G03_Ipan_lit — hidden/hiddenAt).
            // 등불을 뒤에 대면 이 면이 원래 종이 위로 배어 나온다.
            var lit = AssetDatabase.LoadAssetAtPath<Texture2D>(LitPage);
            if (lit != null) so.FindProperty("_litPage").objectReferenceValue = lit;
            else log.AppendLine("  ※ 비친 면을 못 찾았다 — [이문록 ▸ 에셋: 사건 문서 텍스처 굽기] 를 먼저 누를 것");

            so.FindProperty("_fineText").stringValue =
                "다섯째 줄만 먹빛이 옅다. 획 끝이 뭉개지고 종이 결이 일어나 있다 — *긁어내고 그 위에 덧쓴* 자리다.";
            so.FindProperty("_litGlyphs").stringValue = "免賤";
            so.FindProperty("_litText").stringValue =
                "긁어낸 자리 밑에서 지운 글이 배어 나온다 — *「복동 면천(免賤), 기묘년 시월」*. " +
                "관이 이미 놓아준 것을 누군가 도로 종으로 만들어 놓았다.";
            // 등불로 본 사람만 갖는 열쇠. 장계에서 "복동" 을 쓸 수 있는 자격이 이것이다 —
            // 그냥 읽기만 한 사람은 다섯째 줄이 고쳐진 줄을 모른다.
            so.FindProperty("_litClueKey").stringValue = "G05";
            so.FindProperty("_litClueOwnText").stringValue =
                "입안대장 다섯째 줄은 긁어내고 덧쓴 것이다. 지운 밑글은 「免賤 己卯十月」 — " +
                "복동은 이미 놓여난 몸이었다.";
            so.FindProperty("_litClueText").stringValue =
                "입안대장 다섯째 줄은 긁어내고 덧쓴 것이다. 지운 밑글은 「복동 면천, 기묘년 시월」 — " +
                "복동은 이미 면천된 몸이다. 이 책이 고쳐진 덕에 아직 종으로 남아 있다.";
            so.FindProperty("_clueNeedsLantern").boolValue = false;   // 쥐어 보면 G03 은 그대로 적힌다
            so.FindProperty("_clueNeedsMagnifier").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            log.AppendLine("  · 입안대장 — 돋보기는 겉(덧쓴 자국)을, 등불은 속(지운 밑글)을 읽는다");
        }

        // ── 잔손 ─────────────────────────────────

        private static Material _wood;

        private static Material Wood()
        {
            if (_wood != null) return _wood;
            foreach (var g in AssetDatabase.FindAssets("t:Material MI_KoreanWood"))
            {
                _wood = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
                if (_wood != null) return _wood;
            }
            return null;
        }

        private static void Part(Transform parent, string name, PrimitiveType kind,
                                 Vector3 pos, Vector3 scale, Material mat)
        {
            var t = parent.Find(name);
            GameObject go;
            if (t == null)
            {
                go = GameObject.CreatePrimitive(kind);
                go.name = name;
                go.transform.SetParent(parent, false);
                Undo.RegisterCreatedObjectUndo(go, "등경 조각");
                var c = go.GetComponent<Collider>();
                if (c != null) Object.DestroyImmediate(c);          // 뿌리 상자 하나로 받는다
            }
            else go = t.gameObject;
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = scale;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null && mat != null) mr.sharedMaterial = mat;
        }

        private static Bounds Wrap(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        private static GameObject Find(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == name) return root;
            return null;
        }
    }
}
