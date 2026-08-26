using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using IMUNROK.Common;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>어전 한 칸을 짓는다.</b> 메뉴: [이문록 ▸ 엔딩 ▸ 어전 짓기]
    ///
    /// 복명(復命) 씬에는 <b>카메라 하나와 대사 부품밖에</b> 없었다. 세상이 통째로
    /// 비어 있어서, 세 사건을 다 끝내고 들어서도 캄캄한 데 자막만 떴다.
    ///
    /// <b>왕은 안 보인다.</b> 조선에서 신하는 임금을 마주 보지 않았다 — 발(簾)을
    /// 드리우고 목소리만 오간다. 그것이 예이기도 하고, 이 게임에는 <b>고맙게도</b>
    /// 그 편이 낫다. 곤룡포 입은 모델도 앉는 동작도 필요 없고, 무엇보다
    /// <b>보이지 않는 임금이 더 무섭다</b>. 발 너머는 캄캄한 채로 둔다.
    ///
    /// 새 에셋을 안 쓴다. 관아 문서고를 지을 때 쓴 그 재질과 그 수법(기단·기둥·마루·
    /// 벽·인방)을 그대로 가져온다 — 같은 나라의 같은 목수가 지은 집이라야 한다.
    ///
    /// <code>
    ///        ┌──────────────────────┐  뒤벽
    ///        │      [어좌 단]        │  ← 발 너머, 캄캄하다
    ///        │  ═══════발═══════     │  z +0.6, 아래로 2.4m 드리운다
    ///        │                       │
    ///        │   ● 촛대    촛대 ●    │
    ///        │        ▲ 어사         │  z −2.2, 꿇어앉는다
    ///        └───────  틘 앞  ───────┘
    /// </code>
    ///
    /// <b>맺음</b>: 왕의 마지막 물음이 끝나면 어전이 통째로 꺼지고, 그 어둠 위에
    /// "플레이해 주셔서 감사합니다" 와 만든 사람이 뜬다. 물음에 답을 안 다는 까닭은
    /// 답할 사람이 화면 밖에 있기 때문이다.
    ///
    /// 두 번 눌러도 두 벌이 안 선다.
    /// </summary>
    public static class EndingHall
    {
        private const string ScenePath = "Assets/_Project/Scenes/Core/EndingScene.unity";
        private const string MatDir = "Assets/_Project/_Common/Sets/Gwana/Donheon/Materials/";

        /// <summary>어전 한 칸의 크기(m). 넓힐 까닭이 없다 — 어사는 한자리에 꿇는다.</summary>
        private const float HalfX = 4.5f, HalfZ = 3.5f, Tall = 3.4f;

        /// <summary>발이 드리운 줄. 어사(-2.2)와 어좌(+2.2) 사이다.</summary>
        private const float VeilZ = 1.30f;

        private static Material _wood, _wall, _stone, _floor, _roof;

        [MenuItem("이문록/엔딩/어전 짓기")]
        public static void Run()
        {
            if (!LoadMaterials()) return;

            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var log = new System.Text.StringBuilder("[엔딩] 어전 한 칸을 짓는다\n");

            var hall = Root(scene, "어전");
            Floor(hall, log);
            Walls(hall, log);
            Posts(hall, log);
            Ceiling(hall, log);
            Veil(hall, log);
            Throne(hall, log);
            Candles(hall, log);
            Sky(log);
            Place(scene, hall, log);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(log.ToString());
        }

        // ── 바닥·벽·기둥·천장 ─────────────────────

        private static void Floor(Transform hall, System.Text.StringBuilder log)
        {
            Box(hall, "마루", new Vector3(0f, -0.10f, 0f),
                new Vector3(HalfX * 2f, 0.20f, HalfZ * 2f), _floor);
            Box(hall, "기단", new Vector3(0f, -0.35f, 0f),
                new Vector3(HalfX * 2f + 0.8f, 0.30f, HalfZ * 2f + 0.8f), _stone);
            log.AppendLine("── 마루 " + (HalfX * 2f) + " × " + (HalfZ * 2f) + "m");
        }

        private static void Walls(Transform hall, System.Text.StringBuilder log)
        {
            float y = Tall * 0.5f;
            Box(hall, "벽_뒤", new Vector3(0f, y, HalfZ), new Vector3(HalfX * 2f, Tall, 0.20f), _wall);
            Box(hall, "벽_서", new Vector3(-HalfX, y, 0f), new Vector3(0.20f, Tall, HalfZ * 2f), _wall);
            Box(hall, "벽_동", new Vector3(HalfX, y, 0f), new Vector3(0.20f, Tall, HalfZ * 2f), _wall);

            // 앞은 트여 있다. 다만 <b>인방 아래로만</b> 트인다 — 위가 트여 있으면
            // 방이 아니라 정자가 된다.
            Box(hall, "인방_앞", new Vector3(0f, Tall - 0.25f, -HalfZ),
                new Vector3(HalfX * 2f, 0.50f, 0.24f), _wood);
            log.AppendLine("── 벽 셋과 앞 인방 (앞은 트여 있다 — 어사가 든 자리다)");
        }

        private static void Posts(Transform hall, System.Text.StringBuilder log)
        {
            float x = HalfX - 0.45f, z = HalfZ - 0.45f;
            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) == 0 ? -x : x;
                float sz = (i & 2) == 0 ? -z : z;
                Box(hall, "기둥_" + i, new Vector3(sx, Tall * 0.5f, sz),
                    new Vector3(0.34f, Tall, 0.34f), _wood);
            }
            log.AppendLine("── 기둥 넷");
        }

        private static void Ceiling(Transform hall, System.Text.StringBuilder log)
        {
            Box(hall, "천장", new Vector3(0f, Tall + 0.10f, 0f),
                new Vector3(HalfX * 2f + 0.8f, 0.20f, HalfZ * 2f + 0.8f), _wood);
            Box(hall, "지붕", new Vector3(0f, Tall + 0.55f, 0f),
                new Vector3(HalfX * 2f + 1.6f, 0.70f, HalfZ * 2f + 1.6f), _roof);
            log.AppendLine("── 천장과 지붕 (하늘이 안 보여야 어전이다)");
        }

        // ── 발 ────────────────────────────────────

        /// <summary>
        /// 발은 <b>내려온 채로</b> 있다. 어사가 들기 전에 이미 드리워져 있는 것이
        /// 이 자리의 규칙이므로, 내리고 올리는 장치(<see cref="CourtVeil"/>)를 달지 않는다.
        /// </summary>
        private static void Veil(Transform hall, System.Text.StringBuilder log)
        {
            // <b>널 한 장으로는 발이 안 된다.</b> 처음에 통판을 하나 세웠더니 그건
            // 발이 아니라 <b>나무 벽</b>이었다 — 어사 앞이 통째로 막히고, 너머에 무엇이
            // 있는지가 아니라 무엇도 없다는 것만 보였다.
            //
            // 발은 <b>가는 대를 엮은 것</b>이라 틈이 있다. 틈이 있어야 저쪽의 어둠이
            // 조금씩 새어 나오고, 그 어둠이 "저 너머에 누가 있다"를 말해 준다.
            // 대를 하나씩 놓는다 — 값은 상자 스물몇 개, 얻는 것은 자리의 뜻이다.
            var veil = Child(hall, "발");
            foreach (Transform old in veil) UnityEngine.Object.DestroyImmediate(old.gameObject);

            var mat = new Material(_wood) { name = "M_어전_발" };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.20f, 0.15f, 0.10f));

            float top = Tall - 0.12f, drop = 3.00f, w = HalfX * 2f - 1.2f;
            float pitch = 0.088f, slat = 0.055f;
            int n = Mathf.FloorToInt(drop / pitch);
            for (int i = 0; i < n; i++)
                Box(veil, "대_" + i, new Vector3(0f, top - 0.10f - i * pitch, VeilZ),
                    new Vector3(w, slat, 0.025f), mat);

            Box(hall, "발장대", new Vector3(0f, top, VeilZ), new Vector3(w + 0.24f, 0.12f, 0.10f), _wood);
            log.AppendLine("── 발을 드리웠다 — 대 " + n + " 개, z " + VeilZ.ToString("F2") + " (틈으로 너머의 어둠이 샌다)");
        }

        /// <summary>
        /// 어좌는 <b>있되 안 보인다</b>. 발 너머에 단만 하나 두고 빛을 안 준다 —
        /// 형체가 어렴풋해야 "저 너머에 누가 있다"가 되고, 또렷하면 "아무도 없다"가 된다.
        /// </summary>
        private static void Throne(Transform hall, System.Text.StringBuilder log)
        {
            Box(hall, "어좌_단", new Vector3(0f, 0.18f, 2.60f), new Vector3(2.60f, 0.36f, 1.60f), _stone);
            Box(hall, "어좌", new Vector3(0f, 0.72f, 2.75f), new Vector3(0.90f, 0.72f, 0.70f), _wood);
            Box(hall, "어좌_등", new Vector3(0f, 1.35f, 3.05f), new Vector3(0.90f, 1.30f, 0.10f), _wood);
            log.AppendLine("── 어좌를 발 너머에 두었다 (빛을 안 준다 — 어렴풋해야 한다)");
        }

        private static void Candles(Transform hall, System.Text.StringBuilder log)
        {
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -1.70f : 1.70f;
                Box(hall, "촛대_" + i, new Vector3(x, 0.50f, -1.30f), new Vector3(0.10f, 1.00f, 0.10f), _wood);
                var go = new GameObject("촛불_" + i);
                go.transform.SetParent(hall, false);
                go.transform.localPosition = new Vector3(x, 1.08f, -1.30f);
                var lt = go.AddComponent<Light>();
                lt.type = LightType.Point;
                lt.range = 3.4f;
                lt.intensity = 2.6f;
                lt.color = new Color(1f, 0.76f, 0.46f);
                lt.shadows = LightShadows.None;
            }
            log.AppendLine("── 촛불 둘 (어사 앞에만. 발 너머로는 거의 안 간다)");
        }

        /// <summary>
        /// 하늘을 지운다. 어전은 지붕 밑이라 하늘빛이 들 데가 없는데, 하늘 재질이
        /// 남아 있으면 <b>온 방이 고르게 밝아</b> 촛불이 하는 일이 없어진다.
        /// </summary>
        private static void Sky(System.Text.StringBuilder log)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.06f, 0.05f, 0.06f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = false;
            log.AppendLine("── 하늘을 지우고 방을 어둡게 (촛불이 하는 일이 있어야 한다)");
        }

        // ── 어사가 드는 자리 · 부품 잇기 ──────────

        private static void Place(Scene scene, Transform hall, System.Text.StringBuilder log)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                SceneManager.MoveGameObjectToScene(go, scene);
            }
            // 꿇어앉은 눈높이. 헤드셋을 쓰면 그쪽 키가 이깁니다(VRRig 이 바닥을 재서 앉힌다).
            cam.transform.SetPositionAndRotation(new Vector3(0f, 1.05f, -2.30f), Quaternion.identity);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.03f;

            var ctrl = Object.FindFirstObjectByType<EndingController>(FindObjectsInactive.Include);
            if (ctrl == null)
            {
                var go = new GameObject("_EndingController");
                SceneManager.MoveGameObjectToScene(go, scene);
                ctrl = go.AddComponent<EndingController>();
                log.AppendLine("── 복명 부품을 세웠다");
            }
            var so = new SerializedObject(ctrl);
            var slot = so.FindProperty("_worldToHide");
            if (slot != null) { slot.objectReferenceValue = hall.gameObject; so.ApplyModifiedPropertiesWithoutUndo(); }
            EditorUtility.SetDirty(ctrl);

            log.AppendLine("── 어사는 z -2.20 에 꿇는다 · 맺음말이 뜨면 어전이 꺼진다");
        }

        // ── 잔심부름 ──────────────────────────────

        private static Transform Root(Scene scene, string name)
        {
            foreach (var r in scene.GetRootGameObjects())
                if (r.name == name) return r.transform;
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go.transform;
        }

        /// <summary>
        /// 빈 자리 하나. 전에 여기에 통판(큐브)이 서 있었다면 그 껍질을 벗긴다 —
        /// 두 번째로 누를 때 옛 널이 대 사이에 끼어 있으면 도로 벽이 된다.
        /// </summary>
        private static Transform Child(Transform parent, string name)
        {
            Transform t = null;
            foreach (Transform c in parent) if (c.name == name) { t = c; break; }
            if (t == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                t = go.transform;
            }
            var mr = t.GetComponent<MeshRenderer>();
            if (mr != null) UnityEngine.Object.DestroyImmediate(mr);
            var mf = t.GetComponent<MeshFilter>();
            if (mf != null) UnityEngine.Object.DestroyImmediate(mf);
            var col = t.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            return t;
        }

        private static Transform Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            Transform t = null;
            foreach (Transform c in parent) if (c.name == name) { t = c; break; }
            if (t == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                go.transform.SetParent(parent, false);
                t = go.transform;
            }
            t.localPosition = pos;
            t.localRotation = Quaternion.identity;
            t.localScale = size;
            var mr = t.GetComponent<MeshRenderer>();
            if (mr != null && mat != null) mr.sharedMaterial = mat;
            return t;
        }

        private static bool LoadMaterials()
        {
            _wood = Load("MI_KoreanWood_1.001.mat");
            _floor = Load("M_Wood_Tile.mat");
            _wall = Load("MI_R_BrickConcrete1.mat");
            _stone = Load("M_Stone_Granite.mat");
            _roof = Load("MI_R_Roof1.mat");
            if (_wood == null || _wall == null)
            {
                Debug.LogError("[엔딩] 재질을 못 찾았습니다: " + MatDir);
                return false;
            }
            if (_floor == null) _floor = _wood;
            if (_stone == null) _stone = _wall;
            if (_roof == null) _roof = _stone;
            return true;
        }

        private static Material Load(string file)
            => AssetDatabase.LoadAssetAtPath<Material>(MatDir + file);
    }
}
