using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>마주 보는 자리와 발.</b> 메뉴: [이문록 ▸ 관아 ▸ ⑲ 마주 보는 자리와 발]
    ///
    /// 여태 심문받는 사람은 대청 앞자리에 <b>선 채로</b> 있었다. 그런데 새로 받은
    /// 몸짓은 죄 {앉기 · 앉은 채 · 일어나기 · 걷기} 다 — <b>의자에 앉아 신문받는
    /// 사람</b>의 몸짓이다. 그러니 앉을 것을 놓아야 그 몸짓이 산다.
    ///
    /// <b>같은 의자를 두 개 놓지 않는다.</b> 어사는 교의(交椅)에 앉는다 — 등받이와
    /// 팔걸이가 있는 높은 의자다. 신문받는 이는 <b>걸상</b>에 앉는다 — 등도 팔도 없는
    /// 맨 널이다. 마주 앉되 같은 자리가 아니라는 것을 가구가 먼저 말한다.
    ///
    /// <b>높이는 재서 놓는다.</b> 앉은 자세(Sitting_Idle)의 발은 0, 엉덩이는 0.55 다.
    /// 엉덩이뼈는 골반 속이라 앉은 널보다 한 뼘쯤 위에 있으므로 좌판은 <b>0.46</b> 이
    /// 맞다 — 어사 교의의 좌판과 같은 높이다. 손으로 어림잡으면 허공에 앉거나
    /// 널에 파묻힌다.
    ///
    /// <b>발(簾).</b> 아녀자를 신문할 때는 사이에 발을 드리웠다. 얼굴을 마주하고
    /// 묻지 않는 것이 예의이자 보호였다. 자세한 뜻은 <see cref="CourtVeil"/> 에 적었다.
    /// 여기서는 <b>발을 어디에 얼마만 하게</b> 다는지만 정한다:
    ///
    ///   · 매다는 데 — 어사와 걸상 사이, 걸상 쪽에 가깝게(x 13.60).
    ///     어사 쪽에 가까우면 어사가 발 안에 갇힌 꼴이 된다.
    ///   · 위 끝 — 문 위 인방 밑(y 4.05). 아래 끝 — 마루(2.21). 곧 길이 1.84m.
    ///     앉은 사람의 정수리가 3.41 이니 넉넉히 덮는다.
    ///   · 너비 3.60m — 앉은 자리를 가운데 두고 좌우로 트인 데를 다 가린다.
    ///
    /// 발 그림은 <b>그 자리에서 짠다</b>. 대오리가 가로로 촘촘히 늘어선 무늬라
    /// 손으로 그릴 것이 없고, 굵기를 고치고 싶으면 아래 수치만 바꾸면 된다.
    ///
    /// 두 번 눌러도 두 벌이 안 선다.
    /// </summary>
    public static class CourtSeat
    {
        /// <summary>대청 마루.</summary>
        private const float Maru = 2.21f;

        /// <summary>신문받는 이가 앉는 자리(어사는 15.30 에서 이쪽을 본다).</summary>
        private static readonly Vector3 Front = new Vector3(12.40f, Maru, 0f);

        /// <summary>좌판 윗면까지. 앉은 자세의 엉덩이(0.55)에서 한 뼘 내린 값이다.</summary>
        private const float SeatH = 0.46f;

        private const float StoolW = 0.46f, StoolD = 0.40f, Plank = 0.05f, Leg = 0.05f;

        // ── 발 ──
        private const float VeilX = 13.60f;
        private const float VeilTop = 4.05f;
        private const float VeilLen = VeilTop - Maru;      // 1.84
        private const float VeilWide = 3.60f;
        private const float VeilThick = 0.03f;

        /// <summary>발을 내리는 사람.</summary>
        private const string Veiled = "아내";

        private const string VeilTexPath = "Assets/_Project/_Common/Sets/Gwana/Donheon/T_발.png";
        private const string VeilMatPath = "Assets/_Project/_Common/Sets/Gwana/Donheon/M_발.mat";

        [MenuItem("이문록/관아/⑲ 마주 보는 자리와 발")]
        public static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.name.Contains("Gwana"))
            {
                Debug.LogWarning("[관아] 관아 씬을 열고 누르십시오(지금은 " + scene.name + ").");
                return;
            }
            var log = new System.Text.StringBuilder("[관아] 마주 보는 자리와 발\n");
            Stool(scene, log);
            Veil(scene, log);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(log.ToString());
        }

        // ── ㉠ 걸상 ───────────────────────────────

        private static void Stool(Scene scene, System.Text.StringBuilder log)
        {
            var root = Find(scene, "죄인_걸상");
            if (root == null)
            {
                root = new GameObject("죄인_걸상");
                SceneManager.MoveGameObjectToScene(root, scene);
                Undo.RegisterCreatedObjectUndo(root, "죄인 걸상");
                log.AppendLine("  · 걸상을 놓았다 — 어사는 교의, 신문받는 이는 등도 팔도 없는 걸상이다");
            }
            root.transform.SetPositionAndRotation(Front, Quaternion.Euler(0f, 90f, 0f));  // 어사(+x)를 본다
            root.transform.localScale = Vector3.one;

            var wood = Wood();
            float top = SeatH;                       // 좌판 윗면(자리 기준)
            Part(root.transform, "좌판", new Vector3(0f, top - Plank * 0.5f, 0f),
                 new Vector3(StoolW, Plank, StoolD), wood);
            float lh = top - Plank;                  // 다리 길이
            float ax = StoolW * 0.5f - Leg * 0.7f, az = StoolD * 0.5f - Leg * 0.7f;
            Part(root.transform, "다리_1", new Vector3(-ax, lh * 0.5f, -az), new Vector3(Leg, lh, Leg), wood);
            Part(root.transform, "다리_2", new Vector3(ax, lh * 0.5f, -az), new Vector3(Leg, lh, Leg), wood);
            Part(root.transform, "다리_3", new Vector3(-ax, lh * 0.5f, az), new Vector3(Leg, lh, Leg), wood);
            Part(root.transform, "다리_4", new Vector3(ax, lh * 0.5f, az), new Vector3(Leg, lh, Leg), wood);
            Part(root.transform, "가로대", new Vector3(0f, lh * 0.32f, 0f),
                 new Vector3(StoolW - Leg, 0.035f, 0.035f), wood);

            log.AppendLine("  · 좌판 " + (Maru + SeatH).ToString("F2") + "m — 앉은 자세의 엉덩이(마루+0.55)에서 한 뼘 내린 자리");
        }

        // ── ㉡ 발 ─────────────────────────────────

        private static void Veil(Scene scene, System.Text.StringBuilder log)
        {
            var root = Find(scene, "발");
            if (root == null)
            {
                root = new GameObject("발");
                SceneManager.MoveGameObjectToScene(root, scene);
                Undo.RegisterCreatedObjectUndo(root, "발");
                log.AppendLine("  · 발을 매달았다 — 아녀자를 신문할 때만 내려온다");
            }
            // <b>90° 돌려 건다.</b> 어사와 걸상은 x 축으로 마주 앉는다 — 그 사이를 막으려면
            // 발이 <b>z 축으로</b> 펼쳐져야 한다. 안 돌리고 걸었더니 발이 시선과 나란히 서서
            // 두께 3cm 짜리 널 옆면만 보였고, 어사 자리에서는 아무것도 안 가려졌다.
            root.transform.SetPositionAndRotation(new Vector3(VeilX, VeilTop, 0f), Quaternion.Euler(0f, 90f, 0f));
            root.transform.localScale = Vector3.one;

            // 매단 자리에 걸린 <b>대나무 장대</b> — 걷혀 있어도 이건 남아 있어야
            // "저기 발이 걸려 있다"가 보인다.
            Part(root.transform, "발장대", new Vector3(0f, 0.04f, 0f),
                 new Vector3(VeilWide + 0.16f, 0.07f, 0.07f), Wood());

            var blindT = root.transform.Find("발널");
            GameObject blind;
            if (blindT == null)
            {
                blind = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blind.name = "발널";
                blind.transform.SetParent(root.transform, false);
                Undo.RegisterCreatedObjectUndo(blind, "발널");
                var c = blind.GetComponent<Collider>();
                if (c != null) Object.DestroyImmediate(c);   // 지나다닐 수 있어야 한다. 눈만 가린다
            }
            else blind = blindT.gameObject;
            blind.transform.localRotation = Quaternion.identity;

            var mr = blind.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = VeilMat(log);
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            var veil = root.GetComponent<CourtVeil>();
            if (veil == null) veil = Undo.AddComponent<CourtVeil>(root);

            CourtSummon who = null;
            foreach (var s in Object.FindObjectsByType<CourtSummon>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (s.name == Veiled) who = s;
            if (who == null) log.AppendLine("  ※ " + Veiled + " 를 못 찾았다 — 발이 누구에게도 안 내려온다");

            veil.Setup(who, blind.transform, VeilLen, VeilWide, VeilThick);
            EditorUtility.SetDirty(veil);

            log.AppendLine("  · x " + VeilX.ToString("F2") + " 에 너비 " + VeilWide.ToString("F2")
                         + "m, 길이 " + VeilLen.ToString("F2") + "m — 앉은 정수리(" + (Maru + 1.20f).ToString("F2") + ")를 덮는다");
        }

        /// <summary>
        /// 발 그림을 그 자리에서 짠다 — 대오리가 가로로 늘어서고 사이가 성기다.
        ///
        /// 반투명이라야 하는 까닭: 다 가리면 <b>거기 사람이 있다는 것</b>까지 사라진다.
        /// 그림자만 비쳐야 얼굴은 못 읽어도 앉아 있는 것은 안다.
        /// </summary>
        private static Material VeilMat(System.Text.StringBuilder log)
        {
            // <b>이미 있어도 다시 짠다.</b> 굵기와 성긴 정도는 눈으로 보고 고치는 값이라
            // "한 번 만들면 끝"으로 두면 아래 수치를 고쳐도 안 먹는다.
            const int W = 8, H = 256;
            const int Slat = 7, Gap = 5;                 // 대오리 굵기와 사이(칸)
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
            {
                int m = y % (Slat + Gap);
                bool slat = m < Slat;
                // 대오리는 둥글어 가운데가 밝고 가장자리가 어둡다
                float round = slat ? 1f - Mathf.Abs(m - (Slat - 1) * 0.5f) / (Slat * 0.5f) : 0f;
                byte r = (byte)Mathf.RoundToInt(Mathf.Lerp(96f, 176f, round));
                byte g = (byte)Mathf.RoundToInt(Mathf.Lerp(74f, 146f, round));
                byte b = (byte)Mathf.RoundToInt(Mathf.Lerp(46f, 96f, round));
                byte a = slat ? (byte)236 : (byte)28;        // 사이는 훤히 틔워야 발로 읽힌다
                for (int x = 0; x < W; x++) px[y * W + x] = new Color32(r, g, b, a);
            }
            tex.SetPixels32(px); tex.Apply();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(VeilTexPath));
            System.IO.File.WriteAllBytes(VeilTexPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(VeilTexPath, ImportAssetOptions.ForceUpdate);

            var ti = AssetImporter.GetAtPath(VeilTexPath) as TextureImporter;
            if (ti != null)
            {
                ti.alphaIsTransparency = true;
                ti.wrapModeU = TextureWrapMode.Repeat;
                ti.wrapModeV = TextureWrapMode.Repeat;
                ti.filterMode = FilterMode.Bilinear;
                ti.SaveAndReimport();
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(VeilMatPath);
            bool fresh = mat == null;
            if (fresh) mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(VeilTexPath));
            // URP 에서 반투명은 값 하나로 안 된다 — 표면 갈래·섞는 법·깊이 쓰기·줄까지 다 바꿔야 한다.
            mat.SetFloat("_Surface", 1f);                 // Transparent
            mat.SetFloat("_Blend", 0f);                   // Alpha
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Smoothness", 0.06f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            // 되풀이하는 횟수가 곧 대오리 굵기다. 처음엔 12 로 두었더니 1.84m 에
            // 대오리가 256 줄이 서서, 발이 아니라 <b>화면 주사선</b>처럼 보였다.
            // 5 면 한 줄이 1.7cm 라 발로 읽힌다.
            mat.SetTextureScale("_BaseMap", new Vector2(1f, 5f));
            EditorUtility.SetDirty(mat);
            if (fresh) AssetDatabase.CreateAsset(mat, VeilMatPath);
            AssetDatabase.SaveAssets();
            log.AppendLine("  · 발 그림과 재질을 짰다 — 대오리 " + Slat + "칸, 사이 " + Gap + "칸, 세로 5번 되풀이");
            return mat;
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

        private static void Part(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var t = parent.Find(name);
            GameObject go;
            if (t == null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                go.transform.SetParent(parent, false);
                Undo.RegisterCreatedObjectUndo(go, "가구 조각");
                var c = go.GetComponent<Collider>();
                if (c != null) Object.DestroyImmediate(c);
            }
            else go = t.gameObject;
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = scale;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null && mat != null) mr.sharedMaterial = mat;
        }

        private static GameObject Find(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == name) return root;
            return null;
        }
    }
}
