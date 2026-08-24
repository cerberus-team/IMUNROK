using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Gyeonu.EditorTools
{
    /// <summary>
    /// 관아 집무실 — 문갑 상판 「쌍학월도」 음각판 + 볼록렌즈 구슬 3 + 서랍 속 열쇠 (2026-08-24).
    /// **멱등**하다. 몇 번을 눌러도 결과가 같다.
    ///
    /// 만드는 것
    ///   ① 상판 음각판  — 첨부 그림(쌍학월도.png)을 굽는다. 테두리를 문갑 상판의 진짜 나뭇결로
    ///      **이어 붙여** 판이 따로 올려진 것으로 보이지 않게 한다. 노멀·금속/광택 마스크도 굽는다.
    ///   ② 볼록렌즈 구슬 3 — 구면 캡(돔) 메시. 처음에는 서안 위에 놓인다.
    ///   ③ 문갑 잠금  — FurnitureParts.unlockFlag = lens_puzzle_solved
    ///   ④ 서랍 속 수령의 비밀 열쇠 (모델 프리팹 + 소지품 SO + ItemPickup)
    ///   ⑤ 비밀문(LockedDoor)에 열쇠 조건 물리기
    ///   ⑥ 자리 지정용 큐브 삭제
    ///
    /// ⚠️ 원본 에셋 폴더는 건드리지 않는다. 문갑 나뭇결은 **임포터를 고치지 않고**
    ///    PNG 파일을 직접 읽어 쓴다(isReadable을 켜면 gitignore된 원본이 더럽혀진다).
    /// </summary>
    public static class LensPuzzleBuilder
    {
        // ── 자리·치수 (전부 실측) ────────────────────────
        const string RootName = "집무실_렌즈퍼즐";
        const string ChestPath = "집무실_소품/기물/문갑";
        const string DeskPath = "집무실_소품/기물/서안";

        /// <summary>문갑 상판 평면 (Rectangle096 메시 실측).</summary>
        const float TopY = 0.44273f;
        /// <summary>사용자가 놓아 둔 큐브 자리 — 음각판 중심.
        /// 큐브: pos (2.601, 0.444, 0.133) · scale (0.25, 0.01, 0.25) · 회전 없음.</summary>
        static readonly Vector2 PanelCenterXZ = new Vector2(2.601f, 0.133f);
        /// <summary>
        /// 판 크기 (m). 큐브 한 변 0.25를 **가로**(문갑 z축)로 삼고, 그림 비 4:3(1448×1086)을
        /// 지켜 세로(문갑 x축)를 0.1875로 잡은 것이 원래 값이고, 2026-08-24 2차에서
        /// **그림을 1.5배**로 키우라는 지시에 따라 그 1.5배로 늘렸다.
        ///
        /// 상판이 x 2.4185~2.8180 · z −0.4998~0.6998 이므로 0.375×0.28125는
        /// x 2.4604~2.7416 · z −0.0545~0.3205 — 넉넉히 들어간다(백자 접시·화병과도 안 겹친다).
        /// ⚠️ 렌즈 반지름·편심·정답 반경도 **같이 1.5배**해야 퍼즐이 그대로다.
        ///    그림 대비 배치가 안 변해야 정답 위치가 바뀌지 않는다.
        /// </summary>
        const float ArtScale = 1.5f;
        static readonly Vector2 PanelSize = new Vector2(0.25f * ArtScale, 0.1875f * ArtScale);
        /// <summary>판을 상판 위로 띄우는 양 — z-파이팅 회피.</summary>
        const float PanelLift = 0.0006f;

        /// <summary>
        /// 판 알베도 보정값 (선형). 「같은 알베도인데 판이 상판보다 밝게 뜨는」 차이를 없앤다.
        ///
        /// 원인은 상판 재질의 **노멀맵**이다 — BumpScale을 0으로 두면 상판이 9배 밝아진다.
        /// 빛이 상판을 스치듯 들어와 결의 잔 요철마다 N·L이 0으로 잘리기 때문인데, 그 결을
        /// 원본 텍셀 밀도(163×122)로 새 판에 옮겨도 같은 어두움이 재현되지 않았다
        /// (BumpScale 40까지 올려도 0.187→0.175가 전부). 곱하기 데칼로 갈아타 보니
        /// 이번엔 상판의 새까만 결 줄무늬가 그림을 갈기갈기 찢어 놓아 그림이 안 읽혔다.
        /// → 그림 가독성을 택하고, **테두리 밝기만 상수로 맞춘다.**
        ///
        /// 재측정 절차: 판을 켠 장면과 끈 장면을 **같은 카메라 자리**에서 찍어, 판 테두리 띠의
        /// 픽셀을 선형으로 바꿔 평균낸 뒤 (상판 ÷ 판) 을 구해 여기 적는다.
        /// 조명 배치를 크게 바꾸면 다시 재야 한다 — 이 값은 "스치는 빛"이 만든 차이라서 그렇다.
        ///
        /// ⚠️ **금 상감에는 이 보정을 걸지 않는다.** 판 전체를 0.153배로 눌렀더니 이음매는
        ///    완벽해졌지만 그림이 통째로 안 보였다. 이음매는 **나무↔나무**에서 생기는 것이므로
        ///    나무만 상판에 맞추고, 금은 밝게 둔다 — 상감이 빛을 받아 도드라지는 것은 정상이다.
        /// </summary>
        /// ⚠️⚠️ **반드시 무채색(회색)이어야 한다.** 2026-08-24 3차에 채널별로 다른 값
        ///    (0.537, 0.796, 0.942)을 쓴 적이 있는데, 그건 *렌더된 픽셀의 색조*를 맞추려다
        ///    **조명의 색조를 알베도로 흉내 낸 것**이었다. 앰비언트가 푸른 밤 화면에서는
        ///    맞아 보였지만, 따뜻한 조사등이 지배하는 포커스 화면에서는 판만 푸르뎅뎅하게 떴다
        ///    (실측: 상판 R/G=2.00인데 판은 1.31).
        ///    색조는 **알베도끼리** 맞춰야 어떤 빛 아래서도 같다 — 그 일은 위의 톤 이득 g가 한다.
        ///    여기는 밝기만 담당한다.
        static readonly Color CalibWood = new Color(0.70f, 0.70f, 0.70f, 1f);
        const float CalibGold = 0.95f;

        /// <summary>포커스 조사등 세기 (밤 기준 실측).</summary>
        const float InspectIntensity = 4.4f;

        const float LensR = 0.018f * ArtScale;      // 렌즈 반지름
        const float LensH = 0.011f * ArtScale;      // 돔 높이
        const float Mag = 2.0f;                     // 확대 배율 (그림 대비라 안 키운다)
        const float Tolerance = 0.010f * ArtScale;  // 정답 반경

        /// <summary>
        /// 구슬이 **처음부터 놓여 있는 자리** (판 로컬 m). 2026-08-24 3차 지시로 서안에서 판 위로 옮겼다.
        ///
        /// 고르는 규칙 — 정답을 암시하지 않아야 한다:
        ///   · 정답(달·두 학 머리)에서 8cm 이상 떨어뜨린다 (정답 반경은 1.5cm)
        ///   · 셋을 **가지런히 늘어놓지 않는다.** 예전 대기줄처럼 아래 한 줄로 두면
        ///     "여기서 위로 올리는 것"이라는 힌트가 되고, 좌·중·우 대칭으로 두면
        ///     학–달–학 배치를 그대로 흉내 내 정답이 읽힌다.
        ///   · 서로 겹치지 않게 (지름 5.4cm)
        /// </summary>
        static readonly Vector2[] BeadStart =
        {
            new Vector2(-0.118f, -0.082f),   // 왼쪽 아래
            new Vector2(-0.052f,  0.092f),   // 가운데 위 (달에서 6cm 옆)
            new Vector2( 0.121f, -0.070f),   // 오른쪽 아래
        };

        // ── 그림 위 기준점 (원본 1448×1086 픽셀, y는 위에서 아래) ──
        //    나중에 그림이 바뀌어도 이 네 값만 다시 재면 된다.
        const int ImgW = 1448, ImgH = 1086;
        static readonly Vector2 PxMoon = new Vector2(727f, 325f);
        static readonly Vector2 PxCraneL = new Vector2(498f, 572f);   // 왼쪽 학 머리·눈
        static readonly Vector2 PxCraneR = new Vector2(947f, 572f);   // 오른쪽 학 머리·눈
        /// <summary>학 머리에서 **달의 반대쪽**으로 렌즈를 얼마나 물릴 것인가(m).
        /// 상 = C + M(F−C) 이므로 머리는 이만큼 × (M−1) 만큼 달 쪽으로 떠오른다.</summary>
        const float Eccentric = 0.007f * ArtScale;

        // ── 경로 ─────────────────────────────────────────
        const string TexDir = "Assets/_Project/Gyeonu/Art/Textures/GwanaOffice";
        const string MatDir = "Assets/_Project/Gyeonu/Art/Materials/GwanaOffice";
        const string SrcArt = "Assets/_Project/Gyeonu/Art/Textures/Items/쌍학월도.png";
        const string KeyFbx = "Assets/_Project/Gyeonu/Art/Models/Items/Magistrate_Secret_Key/Meshy_AI_Ornate_Antique_Key_0824040338_texture.fbx";
        const string KeyTex = "Assets/_Project/Gyeonu/Art/Models/Items/Magistrate_Secret_Key/Meshy_AI_Ornate_Antique_Key_0824040338_texture.png";
        const string KeyPrefab = "Assets/_Project/Gyeonu/Prefabs/Items/수령열쇠.prefab";
        const string KeyItem = "Assets/_Project/Gyeonu/Resources/GyeonuItems/Item_SURYEONG_KEY_수령의비밀열쇠.asset";

        // ══════════════════════════════════════════════════

        [MenuItem("Tools/이문록/관아 집무실 ▸ 쌍학월도 렌즈 퍼즐 만들기", priority = 40)]
        public static void Build()
        {
            var chest = GameObject.Find(ChestPath);
            var desk = GameObject.Find(DeskPath);
            if (chest == null || desk == null)
            {
                EditorUtility.DisplayDialog("쌍학월도 렌즈 퍼즐",
                    "관아 집무실 씬(Gyeonu_GwanaOffice)을 열고 실행할 것.\n문갑 또는 서안을 찾지 못했다.", "확인");
                return;
            }

            EnsureFolder(TexDir);
            EnsureFolder(MatDir);

            var panelMat = BakePanelMaterial(chest);
            var lensMats = EnsureLensMaterials(panelMat);
            var looseMat = EnsureLooseGlassMaterial();

            // 이전 결과 제거 (멱등)
            var old = Find(RootName);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "쌍학월도 렌즈 퍼즐");

            // ── ① 음각판 ──
            var panel = new GameObject("쌍학월도_음각판");
            panel.transform.SetParent(root.transform, false);
            panel.transform.SetPositionAndRotation(
                new Vector3(PanelCenterXZ.x, TopY + PanelLift, PanelCenterXZ.y),
                Quaternion.LookRotation(Vector3.right, Vector3.up));   // +X(로컬)=그림 가로, +Z(로컬)=그림 위
            panel.AddComponent<MeshFilter>().sharedMesh = PanelMesh();
            var pr = panel.AddComponent<MeshRenderer>();
            pr.sharedMaterial = panelMat;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var pc = panel.AddComponent<BoxCollider>();
            pc.size = new Vector3(PanelSize.x, 0.0025f, PanelSize.y);
            pc.center = new Vector3(0f, 0.0004f, 0f);

            var puzzle = panel.AddComponent<LensPuzzle>();
            puzzle.displayName = "";
            puzzle.panel = panel.transform;
            puzzle.panelSize = PanelSize;
            puzzle.magnification = Mag;
            puzzle.tolerance = Tolerance;
            // 판 **바로 위**에서 수직으로 내려다본다(사다리꼴 왜곡 없음).
            // 0.58m·화각 32° → 판 세로가 화면의 약 85%, 가로가 64%.
            puzzle.focusDistance = 0.58f;
            puzzle.focusFov = 32f;
            puzzle.transitionTime = 0.6f;
            puzzle.dimStrength = 0.72f;
            puzzle.solvedFlag = GyeonuWorld.F_렌즈퍼즐;
            // 괄호 없이, 쉼표 자리에서 줄을 바꾼다
            puzzle.hintText = "달이 차오르고 두 학이 함께 그 빛을 우러를 때\n감춘 것이 모습을 드러내리라.";

            // 조사등 — 포커스 중에만 켜진다. 밤 상판은 그냥 두면 거의 검다
            var lampGo = new GameObject("조사등");
            lampGo.transform.SetParent(root.transform, false);
            // 스포트로 **판만** 비춘다. 점광원으로 하면 주변 상판까지 환해져 판이 도로 도드라진다.
            lampGo.transform.position = new Vector3(PanelCenterXZ.x, TopY + 0.60f, PanelCenterXZ.y);
            lampGo.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            var lamp = lampGo.AddComponent<Light>();
            lamp.type = LightType.Spot;
            lamp.spotAngle = 46f;        // 0.60m 높이에서 지름 ≈ 0.51m — 판(0.375×0.28)을 조금 넘는다
            lamp.innerSpotAngle = 10f;   // 가장자리를 넉넉히 풀어 웅덩이 테두리가 안 보이게
            lamp.color = new Color(1f, 0.93f, 0.80f);
            lamp.range = 1.2f;           // ⚠️ 짧게 — 방의 큰 메시에 닿으면 창빛 하나를 밀어낸다
            lamp.intensity = 0f;
            lamp.shadows = LightShadows.None;
            lamp.enabled = false;
            puzzle.inspectLight = lamp;
            puzzle.inspectIntensity = InspectIntensity;

            // ── ② 렌즈 구슬 3 ──
            var domeMesh = DomeMesh(LensR, LensH, 40, 12);
            var beads = new LensBead[3];
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject($"렌즈구슬_{i + 1}");
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = domeMesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = lensMats[i];
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var sc = go.AddComponent<SphereCollider>();
                sc.radius = LensR;
                sc.center = new Vector3(0f, 0.002f, 0f);
                var b = go.AddComponent<LensBead>();
                b.puzzle = puzzle;
                b.radius = LensR;
                b.panelMaterial = lensMats[i];
                b.looseMaterial = looseMat;
                b.displayName = "";
                beads[i] = b;
            }
            puzzle.lenses = beads;
            puzzle.answers = new[] { AnswerMoon(), AnswerCrane(PxCraneL), AnswerCrane(PxCraneR) };
            // 구슬은 **처음부터 판 위**에 흩어져 있다 (SetPanelPos가 자리·재질·레이어를 함께 맞춘다).
            // ⚠️ puzzle.lenses / answers 를 채운 뒤에 놓아야 한다 — 자리 계산이 판 좌표계를 쓴다.
            for (int i = 0; i < 3; i++) beads[i].SetPanelPos(BeadStart[i]);
            WarnIfStartHintsAnswer(puzzle);

            // ── ③ 문갑 잠금 ──
            var fp = chest.GetComponent<FurnitureParts>();
            if (fp != null)
            {
                fp.unlockFlag = GyeonuWorld.F_렌즈퍼즐;
                fp.lockedMessage = "서랍이 꿈쩍도 하지 않는다. 상판에 새겨진 학 그림이 눈에 밟힌다.";
                puzzle.chest = fp;
                EditorUtility.SetDirty(fp);
            }

            // ── ④ 열쇠 ──
            BuildKey(root, fp);

            // ── ⑤ 비밀문에 열쇠 조건 ──
            WireSecretDoor();

            // ── ⑥ 자리 지정용 큐브 제거 ──
            var cube = Find("Cube");
            if (cube != null)
            {
                Debug.Log($"[렌즈퍼즐] 자리 지정 큐브 제거 — pos={cube.transform.position:F4} " +
                          $"scale={cube.transform.lossyScale:F4} rotY={cube.transform.eulerAngles.y:F3}");
                Object.DestroyImmediate(cube);
            }

            EditorUtility.SetDirty(root);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Selection.activeGameObject = panel;
            Debug.Log($"[렌즈퍼즐] 완료. 정답(판 로컬 m) = 달 {puzzle.answers[0]:F4} / " +
                      $"왼쪽 학 {puzzle.answers[1]:F4} / 오른쪽 학 {puzzle.answers[2]:F4}, 반경 {Tolerance:F3}m");
        }

        [MenuItem("Tools/이문록/관아 집무실 ▸ 쌍학월도 렌즈 퍼즐 제거", priority = 41)]
        public static void Remove()
        {
            var old = Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            var chest = GameObject.Find(ChestPath);
            var fp = chest != null ? chest.GetComponent<FurnitureParts>() : null;
            if (fp != null) { fp.unlockFlag = ""; EditorUtility.SetDirty(fp); }
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[렌즈퍼즐] 제거함 (문갑 잠금도 풀었다).");
        }

        // ══════════════ 정답 좌표 ══════════════

        /// <summary>시작 자리가 정답을 암시하지 않는지 자동 점검 — 손으로 좌표를 만질 때 안전망.</summary>
        static void WarnIfStartHintsAnswer(LensPuzzle pz)
        {
            for (int i = 0; i < BeadStart.Length; i++)
            {
                for (int j = 0; j < pz.answers.Length; j++)
                {
                    float d = (BeadStart[i] - pz.answers[j]).magnitude;
                    if (d < 0.05f)
                        Debug.LogWarning($"[렌즈퍼즐] 시작 자리 {i + 1}이 정답 {j + 1}에서 {d * 100f:F1}cm 뿐이다 — 정답이 읽힌다.");
                }
                for (int j = i + 1; j < BeadStart.Length; j++)
                    if ((BeadStart[i] - BeadStart[j]).magnitude < LensR * 2.2f)
                        Debug.LogWarning($"[렌즈퍼즐] 시작 자리 {i + 1}·{j + 1}이 서로 겹친다.");
            }
        }

        /// <summary>원본 그림 픽셀 → 판 로컬(m). y는 위에서 아래로 재므로 뒤집는다.</summary>
        static Vector2 PxToPanel(Vector2 px) => new Vector2(
            (px.x / ImgW - 0.5f) * PanelSize.x,
            (0.5f - px.y / ImgH) * PanelSize.y);

        /// <summary>달은 정확히 그 위 — 단순 확대.</summary>
        static Vector2 AnswerMoon() => PxToPanel(PxMoon);

        /// <summary>학은 **달의 반대쪽**으로 물린 자리. 그래야 상이 달 쪽으로 떠오른다.</summary>
        static Vector2 AnswerCrane(Vector2 headPx)
        {
            Vector2 head = PxToPanel(headPx), moon = PxToPanel(PxMoon);
            Vector2 dir = (moon - head).normalized;
            return head - dir * Eccentric;
        }

        // ══════════════ 메시 ══════════════

        /// <summary>음각판 쿼드 — 로컬 XZ 평면, 법선 +Y, uv.x는 +X, uv.y는 +Z.</summary>
        static Mesh PanelMesh()
        {
            float hx = PanelSize.x * 0.5f, hz = PanelSize.y * 0.5f;
            var m = new Mesh { name = "쌍학월도_판" };
            m.vertices = new[]
            {
                new Vector3(-hx, 0f, -hz), new Vector3(hx, 0f, -hz),
                new Vector3(hx, 0f, hz),   new Vector3(-hx, 0f, hz),
            };
            m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            // 접선 w = −1 이라야 종법선이 +Z(=uv.y 방향)로 선다: cross(up, right)*(−1) = +Z
            var tan = new Vector4(1f, 0f, 0f, -1f);
            m.tangents = new[] { tan, tan, tan, tan };
            m.triangles = new[] { 0, 3, 2, 0, 2, 1 };
            m.RecalculateBounds();
            return m;
        }

        /// <summary>구면 캡(돔) — 밑면 반지름 r, 높이 h. 밑판까지 붙여 속이 비지 않게 한다.</summary>
        static Mesh DomeMesh(float r, float h, int seg, int rings)
        {
            float S = (r * r + h * h) / (2f * h);       // 구 반지름
            float cy = h - S;                            // 구 중심 y
            float phiMax = Mathf.Asin(Mathf.Clamp01(r / S));

            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var tri = new List<int>();

            v.Add(new Vector3(0f, h, 0f)); n.Add(Vector3.up);          // 꼭대기
            for (int i = 1; i <= rings; i++)
            {
                float phi = phiMax * i / rings;
                float y = cy + S * Mathf.Cos(phi), rad = S * Mathf.Sin(phi);
                for (int s = 0; s < seg; s++)
                {
                    float a = 2f * Mathf.PI * s / seg;
                    var p = new Vector3(rad * Mathf.Cos(a), y, rad * Mathf.Sin(a));
                    v.Add(p);
                    n.Add(new Vector3(p.x, y - cy, p.z).normalized);
                }
            }
            for (int s = 0; s < seg; s++)                              // 꼭대기 팬
                tri.AddRange(new[] { 0, 1 + (s + 1) % seg, 1 + s });
            for (int i = 0; i < rings - 1; i++)
            {
                int a0 = 1 + i * seg, b0 = 1 + (i + 1) * seg;
                for (int s = 0; s < seg; s++)
                {
                    int s1 = (s + 1) % seg;
                    tri.AddRange(new[] { a0 + s, b0 + s1, b0 + s });
                    tri.AddRange(new[] { a0 + s, a0 + s1, b0 + s1 });
                }
            }
            int baseStart = v.Count;                                    // 밑판
            v.Add(new Vector3(0f, 0f, 0f)); n.Add(Vector3.down);
            for (int s = 0; s < seg; s++)
            {
                float a = 2f * Mathf.PI * s / seg;
                v.Add(new Vector3(r * Mathf.Cos(a), 0f, r * Mathf.Sin(a)));
                n.Add(Vector3.down);
            }
            for (int s = 0; s < seg; s++)
                tri.AddRange(new[] { baseStart, baseStart + 1 + s, baseStart + 1 + (s + 1) % seg });

            // UV = **렌즈 반지름으로 정규화한 판 위 오프셋** (−1~1).
            // 이러면 확대·자리 이동을 전부 재질의 타일/오프셋(_BaseMap_ST)으로 표현할 수 있다:
            //   그림UV = (R /(배율·판크기)) · uv + (렌즈중심/판크기 + 0.5)
            // 셰이더를 따로 쓰지 않고 **URP Lit 그대로** 쓸 수 있어 판과 조명이 저절로 같아진다.
            var uvs = new List<Vector2>(v.Count);
            foreach (var p in v) uvs.Add(new Vector2(p.x / r, p.z / r));

            var m = new Mesh { name = "렌즈구슬_돔" };
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uvs);
            m.SetTriangles(tri, 0);
            m.RecalculateBounds();
            return m;
        }

        // ══════════════ 재질·텍스처 굽기 ══════════════

        /// <summary>
        /// 판 위 렌즈 재질 — **URP Lit 그대로**다. 커스텀 셰이더를 버린 까닭:
        ///
        /// 1차엔 화면에 칠해진 판 픽셀에 비율을 곱하는 데칼이었고, 2차엔 판과 같은 램버트식을
        /// 손으로 재현했다. 둘 다 **판과 조명이 미묘하게 어긋났고**(2차는 4배 어두웠다),
        /// 무엇보다 고장 났을 때 원인을 좁히는 데만 한나절이 갔다.
        /// 지금은 확대·자리를 전부 **텍스처 타일/오프셋**으로 표현한다(돔 메시 UV가 −1~1이라
        /// 아핀 사상 하나로 떨어진다). 조명은 URP가 판에 하는 것과 **같은 코드**로 하므로
        /// 어긋날 여지가 없고, 배율 1로 두면 렌즈 속이 주변 판과 똑같아지는 자체 검증도 된다.
        ///
        /// 렌즈마다 타일/오프셋이 달라야 하므로 **구슬 하나에 재질 하나**를 만든다.
        /// </summary>
        static Material[] EnsureLensMaterials(Material panelMat)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var art = panelMat != null ? panelMat.GetTexture("_BaseMap") : null;
            var outMats = new Material[3];
            for (int i = 0; i < 3; i++)
            {
                string path = MatDir + $"/M_집무실_볼록렌즈_{i + 1}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, path); }
                m.shader = lit;
                m.SetTexture("_BaseMap", art);
                m.SetColor("_BaseColor", new Color(0.96f, 0.98f, 1f, 1f));   // 유리의 옅은 색기
                m.SetFloat("_Metallic", 0f);
                m.SetFloat("_Smoothness", 0f);
                // ⚠️ 판과 **똑같이** 정반사·환경반사를 끈다. 켜 두면 바로 위의 조사등이
                //    돔 한가운데에 큰 하이라이트를 만들어 렌즈 속이 하얗게 날아간다(실측).
                //    구슬다운 입체감은 돔 법선의 확산광만으로도 충분히 읽힌다.
                m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                m.SetFloat("_SpecularHighlights", 0f);
                m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                m.SetFloat("_EnvironmentReflections", 0f);
                EditorUtility.SetDirty(m);
                outMats[i] = m;
            }
            AssetDatabase.SaveAssets();
            return outMats;
        }

        /// <summary>서안 위에 놓여 있을 때 쓰는 그냥 유리 구슬 재질 —
        /// 곱하기 데칼은 밑에 그림이 없으면 아무것도 못 하므로 판 밖에서는 이쪽을 쓴다.</summary>
        static Material EnsureLooseGlassMaterial()
        {
            string path = MatDir + "/M_집무실_유리구슬.mat";
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, path); }
            m.shader = lit;
            m.SetFloat("_Surface", 1f);                       // Transparent
            m.SetFloat("_Blend", 0f);                         // Alpha
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 1f);
            m.SetShaderPassEnabled("ShadowCaster", false);
            m.renderQueue = 3000;
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetColor("_BaseColor", new Color(0.72f, 0.80f, 0.84f, 0.26f));
            m.SetFloat("_Metallic", 0.02f);
            m.SetFloat("_Smoothness", 0.97f);
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
            return m;
        }

        /// <summary>
        /// 음각판 재질을 굽는다. 핵심은 두 가지다.
        ///   ① 테두리 **색**을 문갑 상판의 진짜 나뭇결로 이어 붙인다.
        ///   ② 상판의 **노멀맵(나뭇결)을 원본 해상도 그대로** 물려 온다.
        ///
        /// ⚠️ ②가 없으면 판이 주변보다 2~4배 밝게 뜬다 (2026-08-24 실측).
        ///    상판을 어둡게 만드는 것은 알베도가 아니라 **노멀맵**이다 —
        ///    `M_JL_문갑목`의 BumpScale을 0으로 두면 상판이 0.027→0.247로 9배 밝아진다.
        ///    빛이 상판을 스치듯 들어와서, 결의 잔 요철마다 N·L이 0으로 잘려 평균이 확 내려간다.
        ///
        /// ⚠️⚠️ **결 노멀은 원본 텍셀 밀도로 구워야 한다.** 처음엔 1448×1086 판 텍스처에
        ///    같이 그려 넣었는데, 상판이 쓰는 영역은 2048 아틀라스의 163×122 텍셀뿐이라
        ///    9배로 늘어나며 결이 매끄러워졌고 — 진폭은 남았는데도 — 어두워지는 효과가 사라졌다.
        ///    BumpScale을 40까지 올려도 0.187→0.175가 전부였다. 그래서 결은 **163×122짜리
        ///    별도 노멀맵**으로 굽고(=원본과 같은 밀도), 조각 릴리프만 고해상도 디테일 노멀로 얹는다.
        ///    (알베도·UV·광원 목록·그림자·반사·앰비언트·밉맵·바운즈를 하나씩 배제해 좁힌 결과다)
        /// </summary>
        static Material BakePanelMaterial(GameObject chest)
        {
            var src = LoadPng(SrcArt);
            if (src == null) { Debug.LogError("[렌즈퍼즐] 쌍학월도.png 를 읽지 못했다."); return null; }
            int W = src.width, H = src.height;
            var sp = src.GetPixels();

            // ① 상판 나뭇결 — 임포터를 고치지 않고 PNG를 직접 읽는다 (원본 폴더 불가침)
            var rect = chest.transform.Find("Rectangle096");
            var mat0 = rect.GetComponent<Renderer>().sharedMaterial;
            var woodTexAsset = mat0.GetTexture("_BaseMap");
            var wood = LoadPng(AssetDatabase.GetAssetPath(woodTexAsset));
            var woodNrm = LoadPng(AssetDatabase.GetAssetPath(mat0.GetTexture("_BumpMap")));
            var topUv = new TopFaceUv(rect.GetComponent<MeshFilter>().sharedMesh, rect.transform, TopY);
            var affine = TopFaceAffine(topUv);   // 상판 UV는 (z, −x)에 비례하는 아핀 사상이다 — 한 번만 풀면 된다

            // ② 톤 — 그림의 **안쪽 바탕**(금선·학·달을 뺀 빈 나무 면) 평균을 상판 평균에 맞춘다.
            //    하이라이트를 태우지 않는 소프트 곡선 c·g/(1+c(g−1)) 으로 먹인다.
            //
            // ⚠️ 예전엔 **바깥 띠**를 기준으로 맞췄는데, 그건 이미 페더로 진짜 나뭇결과 섞이는
            //    자리라 늘 맞는 것처럼 보였다. 정작 눈에 들어오는 넓은 안쪽 바탕은 어긋나 있었다.
            //    이 이득이 **알베도의 색조**를 정하므로, 여기서 맞으면 어떤 빛 아래서도 색조가 맞는다.
            Color field = MeanField(sp, W, H, 0.16f);
            Color woodMean = topUv != null && wood != null ? MeanOfWood(wood, topUv) : field;
            var g = new Vector3(
                Mathf.Clamp(woodMean.r / Mathf.Max(1e-4f, field.r), 0.4f, 3.0f),
                Mathf.Clamp(woodMean.g / Mathf.Max(1e-4f, field.g), 0.4f, 3.0f),
                Mathf.Clamp(woodMean.b / Mathf.Max(1e-4f, field.b), 0.4f, 3.0f));

            var baseCol = new Color[W * H];
            var lum = new float[W * H];
            var feather = new float[W * H];
            var goldMap = new float[W * H];
            const float FeatherM = 0.012f;                 // 가장자리 12mm를 나뭇결로 녹인다
            for (int y = 0; y < H; y++)
            {
                float v = (y + 0.5f) / H;                   // 0=아래 … 1=위 (메시 uv.y와 같은 방향)
                float lz = (v - 0.5f) * PanelSize.y;
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W;
                    float lx = (u - 0.5f) * PanelSize.x;
                    var c = sp[y * W + x];
                    var art = new Color(Soft(c.r, g.x), Soft(c.g, g.y), Soft(c.b, g.z), 1f);

                    float dx = PanelSize.x * 0.5f - Mathf.Abs(lx);
                    float dz = PanelSize.y * 0.5f - Mathf.Abs(lz);
                    float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(dx, dz) / FeatherM));

                    // 판 로컬 → 월드 (로컬 +X = 월드 −Z, 로컬 +Z = 월드 +X) → 상판 UV
                    Vector2 tuv = affine(new Vector3(PanelCenterXZ.x + lz, TopY, PanelCenterXZ.y - lx));

                    Color woodC = wood != null ? wood.GetPixelBilinear(tuv.x, tuv.y) : art;
                    var outC = Color.Lerp(woodC, art, k);
                    // 금다움 — 붉은기·채도·밝기가 모두 있어야 금으로 친다. 보정·마스크가 같이 쓴다
                    float mxc = Mathf.Max(outC.r, Mathf.Max(outC.g, outC.b));
                    float mnc = Mathf.Min(outC.r, Mathf.Min(outC.g, outC.b));
                    float sat = mxc > 1e-4f ? (mxc - mnc) / mxc : 0f;
                    float gold = Mathf.Clamp01((outC.g - outC.b) * 3.2f) * Mathf.Clamp01(sat * 1.6f)
                                 * Mathf.Clamp01(mxc * 2.2f) * k;
                    goldMap[y * W + x] = gold;

                    outC = new Color(outC.r * Mathf.Lerp(CalibWood.r, CalibGold, gold),
                                     outC.g * Mathf.Lerp(CalibWood.g, CalibGold, gold),
                                     outC.b * Mathf.Lerp(CalibWood.b, CalibGold, gold), k);
                    baseCol[y * W + x] = outC;
                    lum[y * W + x] = outC.r * 0.35f + outC.g * 0.48f + outC.b * 0.17f;
                    feather[y * W + x] = k;
                }
            }

            // ③-a 결 노멀 — **원본 텍셀 밀도 그대로**. 이것이 판을 주변과 같은 밝기로 앉히는 열쇠다.
            //     ⚠️ 접선 기준이 다르다: 상판 UV는 (+z, −x) 방향으로 늘어나고 판 로컬은
            //        (+x=−z, +z=+x)이라 x·y 부호를 뒤집어야 결이 같은 쪽으로 눕는다.
            float tpm = WoodTexelsPerMeter(affine, woodNrm != null ? woodNrm.width : 2048);
            int GW = Mathf.Max(8, Mathf.RoundToInt(tpm * PanelSize.x));
            int GH = Mathf.Max(8, Mathf.RoundToInt(tpm * PanelSize.y));
            var grain = new Color[GW * GH];
            for (int y = 0; y < GH; y++)
                for (int x = 0; x < GW; x++)
                {
                    float lx = ((x + 0.5f) / GW - 0.5f) * PanelSize.x;
                    float lz = ((y + 0.5f) / GH - 0.5f) * PanelSize.y;
                    Vector2 tuv = affine(new Vector3(PanelCenterXZ.x + lz, TopY, PanelCenterXZ.y - lx));
                    Color c = woodNrm != null
                        ? woodNrm.GetPixel(Mathf.Clamp(Mathf.RoundToInt(tuv.x * woodNrm.width), 0, woodNrm.width - 1),
                                           Mathf.Clamp(Mathf.RoundToInt(tuv.y * woodNrm.height), 0, woodNrm.height - 1))
                        : new Color(0.5f, 0.5f, 1f, 1f);
                    grain[y * GW + x] = new Color(1f - c.r, 1f - c.g, c.b, 1f);   // x·y 부호 반전
                }

            // ③-b 조각 릴리프 — 그림의 밝기를 높이로 본다. 디테일 노멀로 얹는다(고해상도).
            var nrm = new Color[W * H];
            const float NrmStrength = 4.0f;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int xm = Mathf.Max(0, x - 1), xp = Mathf.Min(W - 1, x + 1);
                    int ym = Mathf.Max(0, y - 1), yp = Mathf.Min(H - 1, y + 1);
                    float gx = lum[y * W + xp] - lum[y * W + xm];
                    float gy = lum[yp * W + x] - lum[ym * W + x];
                    float k = feather[y * W + x];
                    var n = new Vector3(-gx * NrmStrength * k, -gy * NrmStrength * k, 1f).normalized;
                    nrm[y * W + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }

            // ④ 금속·광택 마스크 — 금빛(붉은기·채도)일수록 금속답게.
            //    ⚠️ 나무 부분의 광택은 상판 재질과 **똑같이 0.4** 여야 테두리가 안 튄다.
            var mask = new Color[W * H];
            for (int i = 0; i < baseCol.Length; i++)
            {
                float gold = goldMap[i];
                // ⚠️ 나무 부분 광택을 0.4(상판과 같은 값)로 두면 알베도를 낮춘 뒤 **반사만 남아**
                //    판이 푸르스름한 판때기로 뜬다. 알베도를 낮춘 만큼 광택도 낮춰야 한다.
                mask[i] = new Color(Mathf.Lerp(0.0f, 0.85f, gold), 0f, 0f, Mathf.Lerp(0.0f, 0.55f, gold));
            }

            string pBase = TexDir + "/T_쌍학월도_상감.png";
            string pNrm = TexDir + "/T_쌍학월도_노멀.png";
            string pGrain = TexDir + "/T_쌍학월도_결노멀.png";
            string pMask = TexDir + "/T_쌍학월도_마스크.png";
            WritePng(pBase, W, H, baseCol, true, false);
            WritePng(pNrm, W, H, nrm, false, true);
            WritePng(pGrain, GW, GH, grain, false, true);
            WritePng(pMask, W, H, mask, false, false);
            Object.DestroyImmediate(src);
            if (wood != null) Object.DestroyImmediate(wood);
            if (woodNrm != null) Object.DestroyImmediate(woodNrm);

            // ⑤ 재질 — 곱하기 데칼. 판 UV(0~1) → 상판 UV 로 가는 타일/오프셋을 함께 물린다.
            //    상판 UV는 uv.x가 world z, uv.y가 world x 에만 걸리므로 축이 분리된다(_ST로 표현 가능).
            Vector2 w00 = affine(PanelUvToWorld(0f, 0f));
            Vector2 w10 = affine(PanelUvToWorld(1f, 0f));
            Vector2 w01 = affine(PanelUvToWorld(0f, 1f));
            var woodTiling = new Vector2(w10.x - w00.x, w01.y - w00.y);
            var woodOffset = w00;

            string mp = MatDir + "/M_집무실_쌍학월도.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, mp); }
            m.shader = lit;
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(pBase));
            m.SetColor("_BaseColor", Color.white);   // 보정은 텍스처에 이미 구워져 있다
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(pGrain));
            m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_DetailNormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(pNrm));
            m.SetFloat("_DetailNormalMapScale", 0.8f);
            m.SetTextureScale("_DetailAlbedoMap", Vector2.one);
            m.SetTextureOffset("_DetailAlbedoMap", Vector2.zero);
            m.EnableKeyword("_DETAIL_MULX2");
            m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(pMask));
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetFloat("_Metallic", 1f);
            m.SetFloat("_Smoothness", 1f);
            m.SetFloat("_GlossMapScale", 1f);
            m.SetFloat("_SmoothnessTextureChannel", 0f);   // 0 = MetallicGlossMap 알파

            // ⚠️ **정반사(하이라이트)만 끈다** (2026-08-24 실측). 낮 햇살 스포트가 세기 150이라
            //    평평한 판에서는 광택을 0으로 낮춰도 정반사만으로 밝기가 0.347→0.462로 뛴다.
            //    (상판은 결 노멀이 그 정반사를 잘게 부숴 놓아 그런 띠가 안 생긴다.)
            //    금 상감의 광택은 **그림에 이미 그려져 있으므로** 꺼도 금으로 읽힌다.
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.SetFloat("_SpecularHighlights", 0f);
            // ⚠️ **환경반사는 켜 둔다** (3차). 상판은 이게 켜져 있어 앰비언트에서 하늘빛을 받아
            //    푸르게 식는데, 판만 꺼 두면 판 혼자 따뜻해 보인다(실측: 앰비언트 시점 R/G 2.02 대 1.33).
            //    광택이 0.10이라 넓고 옅게 깔릴 뿐, 정반사 같은 띠는 만들지 않는다.
            m.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            m.SetFloat("_EnvironmentReflections", 1f);
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
            Debug.Log($"[렌즈퍼즐] 판 재질 구움 — 톤 이득 {g:F3} (그림 바탕 {field} → 상판 {woodMean}), " +
                      $"결노멀 {GW}x{GH}, 상판UV 타일={woodTiling:F4} 오프셋={woodOffset:F4}, " +
                      $"교차항 {(w10.y - w00.y):F5}/{(w01.x - w00.x):F5} (0에 가까워야 정상)");
            return m;
        }

        /// <summary>0→0, 1→1 을 지키면서 어두운 쪽만 g배로 드는 곡선. 금빛이 타지 않는다.</summary>
        static float Soft(float c, float g) => c * g / (1f + c * (g - 1f));

        /// <summary>판 UV(0~1) → 월드 좌표. 판 로컬 +X = 월드 −Z, 로컬 +Z = 월드 +X.</summary>
        static Vector3 PanelUvToWorld(float u, float v)
        {
            float lx = (u - 0.5f) * PanelSize.x;
            float lz = (v - 0.5f) * PanelSize.y;
            return new Vector3(PanelCenterXZ.x + lz, TopY, PanelCenterXZ.y - lx);
        }

        /// <summary>
        /// 문갑 상판의 UV는 (world z, world x)에 대한 **아핀 사상**이다 (실측: uv.x ∝ z, uv.y ∝ −x).
        /// 세 점만 찍어 계수를 풀어 두면 150만 텍셀을 삼각형 탐색 없이 곧바로 옮길 수 있다 —
        /// 텍셀마다 삼각형을 훑으면 굽기가 몇 분씩 걸린다.
        /// </summary>
        static System.Func<Vector3, Vector2> TopFaceAffine(TopFaceUv map)
        {
            Vector3 p0 = new Vector3(PanelCenterXZ.x - PanelSize.y * 0.5f, TopY, PanelCenterXZ.y - PanelSize.x * 0.5f);
            Vector3 p1 = new Vector3(PanelCenterXZ.x - PanelSize.y * 0.5f, TopY, PanelCenterXZ.y + PanelSize.x * 0.5f);
            Vector3 p2 = new Vector3(PanelCenterXZ.x + PanelSize.y * 0.5f, TopY, PanelCenterXZ.y - PanelSize.x * 0.5f);
            Vector2 u0, u1, u2;
            if (!map.TryUv(p0, out u0) || !map.TryUv(p1, out u1) || !map.TryUv(p2, out u2))
                return w => new Vector2(0.5f, 0.5f);
            Vector2 dz = (u1 - u0) / (p1.z - p0.z);      // z 1m 당 UV 변화
            Vector2 dx = (u2 - u0) / (p2.x - p0.x);      // x 1m 당 UV 변화
            return w => u0 + dz * (w.z - p0.z) + dx * (w.x - p0.x);
        }

        /// <summary>상판이 1m 당 몇 개의 목재 텍셀을 쓰는가 — 결 노멀을 원본 밀도로 굽기 위한 값.</summary>
        static float WoodTexelsPerMeter(System.Func<Vector3, Vector2> affine, int texWidth)
        {
            var a = affine(new Vector3(PanelCenterXZ.x, TopY, PanelCenterXZ.y));
            var b = affine(new Vector3(PanelCenterXZ.x, TopY, PanelCenterXZ.y + 1f));
            return Mathf.Max(1f, Mathf.Abs(b.x - a.x) * texWidth);
        }

        /// <summary>
        /// 그림 **안쪽 바탕**의 평균색. 테두리(inset 바깥)를 잘라 내고, 남은 면에서도
        /// 금빛(학·달·금선)은 빼고 **빈 나무 면만** 센다 — 그래야 나뭇결 색조가 나온다.
        /// </summary>
        static Color MeanField(Color[] px, int W, int H, float inset)
        {
            int mx = Mathf.RoundToInt(W * inset), my = Mathf.RoundToInt(H * inset);
            double r = 0, g = 0, b = 0; int n = 0;
            for (int y = my; y < H - my; y++)
                for (int x = mx; x < W - mx; x++)
                {
                    var c = px[y * W + x];
                    float hi = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), lo = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                    float sat = hi > 1e-4f ? (hi - lo) / hi : 0f;
                    if (hi > 0.30f || sat > 0.55f) continue;    // 금·상감 제외
                    r += c.r; g += c.g; b += c.b; n++;
                }
            if (n == 0) return new Color(0.2f, 0.14f, 0.12f, 1f);
            return new Color((float)(r / n), (float)(g / n), (float)(b / n), 1f);
        }

        static Color MeanOfWood(Texture2D wood, TopFaceUv map)
        {
            double r = 0, g = 0, b = 0; int n = 0;
            for (int i = 0; i < 24; i++)
                for (int j = 0; j < 24; j++)
                {
                    var w = new Vector3(
                        PanelCenterXZ.x + ((j + 0.5f) / 24f - 0.5f) * PanelSize.y, TopY,
                        PanelCenterXZ.y - ((i + 0.5f) / 24f - 0.5f) * PanelSize.x);
                    Vector2 uv;
                    if (!map.TryUv(w, out uv)) continue;
                    var c = wood.GetPixelBilinear(uv.x, uv.y);
                    r += c.r; g += c.g; b += c.b; n++;
                }
            return n == 0 ? Color.gray : new Color((float)(r / n), (float)(g / n), (float)(b / n), 1f);
        }

        /// <summary>상판 면의 삼각형을 XZ로 눕혀 두고, 월드 점이 어느 삼각형 안인지 찾아 UV를 보간한다.</summary>
        class TopFaceUv
        {
            readonly List<Vector2[]> tris = new List<Vector2[]>();   // (x,z) 3점
            readonly List<Vector2[]> uvs = new List<Vector2[]>();

            public TopFaceUv(Mesh mesh, Transform tr, float topY)
            {
                var v = mesh.vertices; var uv = mesh.uv; var idx = mesh.triangles;
                if (uv == null || uv.Length != v.Length) return;
                for (int i = 0; i < idx.Length; i += 3)
                {
                    Vector3 a = tr.TransformPoint(v[idx[i]]), b = tr.TransformPoint(v[idx[i + 1]]), c = tr.TransformPoint(v[idx[i + 2]]);
                    if (a.y < topY - 0.001f || b.y < topY - 0.001f || c.y < topY - 0.001f) continue;
                    tris.Add(new[] { new Vector2(a.x, a.z), new Vector2(b.x, b.z), new Vector2(c.x, c.z) });
                    uvs.Add(new[] { uv[idx[i]], uv[idx[i + 1]], uv[idx[i + 2]] });
                }
            }

            public bool TryUv(Vector3 world, out Vector2 result)
            {
                var p = new Vector2(world.x, world.z);
                for (int t = 0; t < tris.Count; t++)
                {
                    var q = tris[t];
                    float d = (q[1].y - q[2].y) * (q[0].x - q[2].x) + (q[2].x - q[1].x) * (q[0].y - q[2].y);
                    if (Mathf.Abs(d) < 1e-9f) continue;
                    float l0 = ((q[1].y - q[2].y) * (p.x - q[2].x) + (q[2].x - q[1].x) * (p.y - q[2].y)) / d;
                    float l1 = ((q[2].y - q[0].y) * (p.x - q[2].x) + (q[0].x - q[2].x) * (p.y - q[2].y)) / d;
                    float l2 = 1f - l0 - l1;
                    if (l0 < -0.001f || l1 < -0.001f || l2 < -0.001f) continue;
                    result = uvs[t][0] * l0 + uvs[t][1] * l1 + uvs[t][2] * l2;
                    return true;
                }
                result = Vector2.zero;
                return false;
            }
        }

        // ══════════════ 열쇠 ══════════════

        static void BuildKey(GameObject root, FurnitureParts chestParts)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(KeyFbx);
            if (fbx == null) { Debug.LogWarning("[렌즈퍼즐] 열쇠 FBX가 없다 — 열쇠는 건너뛴다."); return; }

            // ① 재질
            string kmp = MatDir + "/M_수령열쇠.mat";
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var km = AssetDatabase.LoadAssetAtPath<Material>(kmp);
            if (km == null) { km = new Material(lit); AssetDatabase.CreateAsset(km, kmp); }
            km.shader = lit;
            var kt = AssetDatabase.LoadAssetAtPath<Texture2D>(KeyTex);
            if (kt != null) km.SetTexture("_BaseMap", kt);
            km.SetColor("_BaseColor", new Color(0.92f, 0.86f, 0.72f, 1f));
            km.SetFloat("_Metallic", 0.85f);
            km.SetFloat("_Smoothness", 0.48f);
            EditorUtility.SetDirty(km);

            // ② 프리팹 (상세 보기 모델도 이것을 쓴다)
            var tmp = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            tmp.name = "수령열쇠";
            tmp.transform.localScale = Vector3.one * 5f;      // 원본이 2cm짜리라 5배 = 10cm 열쇠
            foreach (var r in tmp.GetComponentsInChildren<Renderer>()) r.sharedMaterial = km;
            PrefabUtility.UnpackPrefabInstance(tmp, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var prefab = PrefabUtility.SaveAsPrefabAsset(tmp, KeyPrefab);
            Object.DestroyImmediate(tmp);

            // ③ 소지품 정의
            var item = AssetDatabase.LoadAssetAtPath<InventoryItem>(KeyItem);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<InventoryItem>();
                AssetDatabase.CreateAsset(item, KeyItem);
            }
            item.itemId = "SURYEONG_KEY";
            item.displayName = "수령의 비밀 열쇠";
            item.pickupVerb = "꺼내기";
            item.description =
                "문갑 서랍 바닥에 오래 눌려 있던 자국이 남은 열쇠다.\n\n" +
                "손잡이에는 학 두 마리가 달을 사이에 두고 마주 선 무늬가 새겨져 있다. " +
                "상판에 새겨진 그림과 같은 손이다.\n\n" +
                "날은 거의 닳지 않았다. 자주 쓰는 열쇠가 아니라, 한 곳만 여는 열쇠다.";
            item.modelPrefab = prefab;
            item.previewEuler = new Vector3(-78f, 0f, 10f);
            item.previewZoom = 1.05f;
            item.usable = false;
            item.autoShowOnPickup = true;
            item.journalKey = "suryeong_key";
            item.journalText = "수령의 문갑 서랍에서 나온 열쇠. 학과 달 무늬가 새겨져 있다.";
            item.worldFlag = GyeonuWorld.F_수령열쇠;
            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssets();

            // ④ 서랍 속 실물 — 서랍 본(Dummy053)에 매달아야 서랍과 함께 밀려 나온다.
            //    ⚠️ 열쇠는 퍼즐 루트가 아니라 **가구 밑**에 붙으므로 루트를 지워도 안 사라진다.
            //       멱등하려면 여기서 직접 치워야 한다 (안 그러면 다시 빌드할 때마다 겹쳐 쌓인다).
            var chest = GameObject.Find(ChestPath);
            var drawerBone = chest != null ? chest.transform.Find("Dummy053") : null;
            if (chest != null)
            {
                var dup = new List<GameObject>();
                foreach (var tr in chest.GetComponentsInChildren<Transform>(true))
                    if (tr.name == "수령의열쇠") dup.Add(tr.gameObject);
                foreach (var g0 in dup) Object.DestroyImmediate(g0);
                if (dup.Count > 0) Debug.Log($"[렌즈퍼즐] 이전 열쇠 {dup.Count}개 제거");
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = "수령의열쇠";
            go.transform.SetParent(drawerBone != null ? drawerBone : root.transform, true);
            go.transform.SetPositionAndRotation(new Vector3(2.500f, 0.2865f, 0.100f),
                                                Quaternion.Euler(0f, 8f, 0f));
            go.transform.localScale = ScaleForWorld(go.transform, 5f);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var bc = go.AddComponent<BoxCollider>();
            bc.size = new Vector3(0.011f, 0.007f, 0.024f);   // 로컬 — 스케일 5배라 월드로는 (0.055, 0.035, 0.12)
            bc.center = Vector3.zero;
            var pick = go.AddComponent<ItemPickup>();
            pick.item = item;
            pick.verbOverride = "꺼내기";
            pick.revealName = false;
            pick.insideFurniture = chestParts;
        }

        /// <summary>부모가 회전·스케일돼 있어도 월드 크기가 원하는 배율이 되게 로컬 스케일을 되푼다.</summary>
        static Vector3 ScaleForWorld(Transform t, float want)
        {
            var p = t.parent;
            if (p == null) return Vector3.one * want;
            var ls = p.lossyScale;
            return new Vector3(want / Mathf.Max(1e-5f, ls.x), want / Mathf.Max(1e-5f, ls.y), want / Mathf.Max(1e-5f, ls.z));
        }

        // ══════════════ 비밀문 ══════════════

        static void WireSecretDoor()
        {
            foreach (var d in Object.FindObjectsByType<LockedDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                d.keyFlag = GyeonuWorld.F_수령열쇠;
                d.lockedMessage = "굳게 잠겨 있다. 여는 방법이 따로 있는 모양이다.";
                d.keyUnlockMessage = "품에서 꺼낸 열쇠가 자물쇠에 꼭 맞는다. 빗장이 풀리고, 문 너머로 아래로 내려가는 계단이 보인다.";
                d.unlockedKey = GyeonuWorld.F_비밀문_해제;
                d.openKey = GyeonuWorld.F_비밀문_열림;
                d.announcedKey = GyeonuWorld.F_비밀문_안내함;
                EditorUtility.SetDirty(d);
            }
        }

        // ══════════════ 잡동사니 ══════════════

        static GameObject Find(string name)
        {
            foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (g.name == name) return g;
            return null;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int i = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, i));
            AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1));
        }

        /// <summary>임포터를 건드리지 않고 PNG를 읽는다 — 원본 에셋 폴더 불가침.</summary>
        static Texture2D LoadPng(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;
            string full = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            if (!File.Exists(full)) return null;
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!t.LoadImage(File.ReadAllBytes(full))) { Object.DestroyImmediate(t); return null; }
            t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        static void WritePng(string path, int w, int h, Color[] px, bool srgb, bool normalMap)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.SetPixels(px);
            t.Apply();
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), path), t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = srgb;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.mipmapEnabled = true;
            ti.maxTextureSize = 2048;
            ti.SaveAndReimport();
        }
    }
}
