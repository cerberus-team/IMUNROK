using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 혼상(渾象) 절차 생성 (2026-08-10, 멱등) — 창경궁 혼상이 목표 형태.
    /// 혼천의(고리 여러 겹)와 달리 "구가 주인공": 지름 1.2m 청동 구 + 자오환·지평환 2개
    /// + 곡선 다리 4개 + 십자 받침. 전체 높이 약 1.8m.
    ///
    /// 발광 구조: 구는 속이 빈 이중 셸 —
    /// - 외피(r 0.60): 청동 패티나 + 별구멍 알파 컷아웃 (구멍 수백 개, 크기 편차)
    /// - 내피(r 0.572, 법선 안쪽): 같은 구멍 텍스처, 평소 흑색 / 점등 시 이미시브
    /// 내부 포인트라이트(그림자 소프트)가 켜지면 구멍으로만 빛이 샌다.
    /// 둘 다 ShadowCastingMode.TwoSided — 중심 광원 기준 뒷면도 그림자를 드리워야 차광된다.
    /// 켜고 끄기는 HonsangController(SetLit/StarNight)가 담당, 빌더가 참조를 연결한다.
    ///
    /// 헬퍼(BuildBand·Add·Combine·SaveMesh 등)는 HoncheonuiBuilder에서 복사 —
    /// 원본은 건드리지 않는다. 씬 인스턴스는 위치·회전 보존 후 재생성.
    /// </summary>
    public static class HonsangBuilder
    {
        const string RootName = "혼상";
        const string ModelDir = "Assets/_Project/Gyeonu/Art/Models/Observatory";
        const string MatDir = "Assets/_Project/Gyeonu/Art/Materials/Observatory";
        const string TexDir = "Assets/_Project/Gyeonu/Art/Textures/Observatory";
        const string PrefabDir = "Assets/_Project/Gyeonu/Prefabs/Observatory";

        const float SphereR = 0.60f;   // 지름 1.2m
        const float ShellGap = 0.028f; // 외피-내피 간격 (구멍 시차로 두께감)
        const float CenterY = 1.12f;   // 구 중심 높이 — 자오환 상단 약 1.80m

        static readonly List<Mesh> tempMeshes = new List<Mesh>();

        [MenuItem("Tools/이문록/혼상 생성")]
        public static void Build()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Gyeonu_Observatory")
            {
                Debug.LogError("[혼상] 활성 씬이 Gyeonu_Observatory가 아닙니다 — 씬을 먼저 열고 실행하세요");
                return;
            }
            tempMeshes.Clear();
            EnsureFolder(ModelDir); EnsureFolder(MatDir); EnsureFolder(TexDir); EnsureFolder(PrefabDir);

            // 기존 인스턴스 위치·회전 보존
            // 기본 위치 = 방 중심 좌대 위 (2026-08-10 이설) — 천장 별 투영이 고르게 퍼지려면
            // 광원이 방 중심에 있어야 한다. 회전 0이면 자오환이 입구를 향해 정면 원으로 보인다
            Vector3 prevPos = new Vector3(18.2f, -2.91f, 34.6f);
            Quaternion prevRot = Quaternion.identity;
            var prev = FindRootIncludingInactive(RootName);
            if (prev != null)
            {
                prevPos = prev.transform.position;
                prevRot = prev.transform.rotation;
                Object.DestroyImmediate(prev);
            }

            var holeTex = BuildHoleTexture();
            var normalTex = BuildSurfaceNormal();
            var maskTex = BuildSurfaceMask();
            // 외피: 검게 삭은 청동 (2026-08-10). 알베도를 거의 검정까지 내리고 거칠기·메탈릭을
            // 마스크맵으로 얼룩지게 — 금속감은 남기되 구멍 빛이 도드라지게 한다.
            // 마스크가 있으면 스무스니스 = 마스크 A × _Smoothness 라서 스칼라는 1로 둔다
            var matShell = Mat("M_혼상_외피", new Color(0.205f, 0.185f, 0.158f), 0.80f, 1f, holeTex,
                alphaClip: true, normal: normalTex, bumpScale: 0.85f, mask: maskTex);
            // 내피: 흑색 기본 + 이미시브는 컨트롤러가 MPB로 제어 (키워드는 켜 둔다)
            var matInterior = Mat("M_혼상_내피", new Color(0.03f, 0.025f, 0.02f), 0f, 0.1f, holeTex, alphaClip: true, emission: Color.black);
            // 틀: 고리·다리·받침 — 혼천의 청동보다 어둡고 무광 (주물 패티나)
            var matFrame = Mat("M_혼상_틀청동", new Color(0.34f, 0.28f, 0.21f), 0.78f, 0.42f);

            var root = new GameObject(RootName);

            // ── 구 (2026-08-14: 극축 회전 구조 확정) ──
            // 계층: 구(극축 기울기만) → 구_회전(로컬 Y 회전만 — 퍼즐이 돌리는 대상) → 외피·내피·광원.
            // 극축은 북쪽(+Z, 돔 안쪽) 앙각 37.5°(조선 위도) — 천구가 도는 축이 실제 하늘과 같다.
            // 회전을 별도 자식으로 분리한 이유: 기울기 피벗의 localEulerAngles를 직접 만지면
            // 기울기가 깨진다. 구_회전은 순수 Y 회전이라 OrbAngle 읽고 쓰기가 깔끔하다
            var axisDir = new Vector3(0f, Mathf.Sin(37.5f * Mathf.Deg2Rad), Mathf.Cos(37.5f * Mathf.Deg2Rad));
            var orb = new GameObject("구");
            orb.transform.SetParent(root.transform, false);
            orb.transform.localPosition = new Vector3(0, CenterY, 0);
            orb.transform.localRotation = Quaternion.FromToRotation(Vector3.up, axisDir);
            var spin = new GameObject("구_회전");
            spin.transform.SetParent(orb.transform, false);
            var outer = MeshGO("구_외피", SaveMesh(SphereShell(SphereR, 64, 32, false), "혼상_외피"), matShell, spin.transform);
            outer.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            var inner = MeshGO("구_내피", SaveMesh(SphereShell(SphereR - ShellGap, 64, 32, true), "혼상_내피"), matInterior, spin.transform);
            inner.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            // 조작용 콜라이더 — 정적 아님 (구가 돈다). 보행 차단은 ObservatoryWalkSetup이 따로 만든다
            var grab = spin.AddComponent<SphereCollider>();
            grab.radius = SphereR + 0.01f;

            // ── 틀: 자오환·지평환·축·다리·십자 받침 ──
            var c = new List<CombineInstance>();
            Mesh cube = PrimitiveMesh(PrimitiveType.Cube);
            Mesh cyl = PrimitiveMesh(PrimitiveType.Cylinder);
            Mesh sph = PrimitiveMesh(PrimitiveType.Sphere);

            var jipyeong = BuildBand(0.715f, 0.11f, 0.17f, 96);   // 지평환 — 넓은 수평 띠 (사진의 장식 띠)
            Add(c, jipyeong, new Vector3(0, CenterY, 0), Quaternion.identity, Vector3.one);
            // 자오환 — 남북 세로 고리(면 = Y-Z, 법선 X). 2026-08-14: 극축이 기울면서
            // 축이 자오환 면 안에 놓이고 핀이 고리에 얹힌다 (실제 혼상의 거치 방식)
            var jao = BuildBand(0.655f, 0.075f, 0.045f, 96);
            Add(c, jao, new Vector3(0, CenterY, 0), Quaternion.Euler(0, 0, 90), Vector3.one);
            // 극축 핀 + 북극 꼭지 — 앙각 37.5° 축 방향 (구 피벗과 동일)
            var pinRot = Quaternion.FromToRotation(Vector3.up, axisDir);
            Add(c, cyl, new Vector3(0, CenterY, 0) + axisDir * (SphereR + 0.032f), pinRot, new Vector3(0.05f, 0.035f, 0.05f));
            Add(c, cyl, new Vector3(0, CenterY, 0) - axisDir * (SphereR + 0.032f), pinRot, new Vector3(0.05f, 0.035f, 0.05f));
            Add(c, sph, new Vector3(0, CenterY, 0) + axisDir * (SphereR + 0.085f), Quaternion.identity, Vector3.one * 0.085f);

            // 다리 4개 — S자 곡선 (베지어 로프트), 무릎 장식·발판 포함
            for (int k = 0; k < 4; k++)
            {
                var d = Quaternion.Euler(0, 45 + 90 * k, 0) * Vector3.right;
                var p0 = d * 0.62f + Vector3.up * (CenterY - 0.075f);  // 지평환 밑면
                var pc = d * 1.02f + Vector3.up * 0.58f;               // 바깥으로 부푼 무릎
                var p1 = d * 0.80f + Vector3.up * 0.14f;               // 십자 팔 위 착지
                Vector3 prevPt = p0;
                for (int i = 1; i <= 9; i++)
                {
                    float t = i / 9f;
                    var pt = (1 - t) * (1 - t) * p0 + 2 * (1 - t) * t * pc + t * t * p1;
                    float w = Mathf.Lerp(0.13f, 0.085f, t);
                    AddDiag(c, cube, prevPt, pt, w);
                    prevPt = pt;
                }
                var knee = 0.3025f * p0 + 0.495f * pc + 0.2025f * p1; // t=0.45
                Add(c, sph, knee, Quaternion.identity, Vector3.one * 0.15f);
                Add(c, cube, p1 + Vector3.up * -0.04f, Quaternion.LookRotation(d), new Vector3(0.17f, 0.09f, 0.24f)); // 발판
            }
            // 십자 받침대 (다리 방위각과 정렬) + 중앙 좌대·기둥
            Add(c, cube, new Vector3(0, 0.065f, 0), Quaternion.Euler(0, 45, 0), new Vector3(0.17f, 0.13f, 1.95f));
            Add(c, cube, new Vector3(0, 0.065f, 0), Quaternion.Euler(0, 135, 0), new Vector3(0.17f, 0.13f, 1.95f));
            Add(c, cyl, new Vector3(0, 0.155f, 0), Quaternion.identity, new Vector3(0.15f, 0.03f, 0.15f));
            Add(c, cyl, new Vector3(0, 0.26f, 0), Quaternion.identity, new Vector3(0.09f, 0.08f, 0.09f));
            Add(c, sph, new Vector3(0, 0.37f, 0), Quaternion.identity, Vector3.one * 0.13f);
            Add(c, cyl, new Vector3(0, 0.45f, 0), Quaternion.identity, new Vector3(0.055f, 0.05f, 0.055f));
            Add(c, cyl, new Vector3(0, 0.505f, 0), Quaternion.identity, new Vector3(0.10f, 0.015f, 0.10f)); // 구 받침 접시
            MeshGO("틀", SaveMesh(Combine(c), "혼상_틀"), matFrame, root.transform);

            // ── 내부 광원 (평소 꺼짐) — 구_회전 자식이라 구가 돌아도 중심에 남는다 ──
            var lightGo = new GameObject("내부광원");
            lightGo.transform.SetParent(spin.transform, false);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.66f, 0.34f);
            l.intensity = 2.4f;
            l.range = 7f;
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.95f;
            l.enabled = false;

            // ── 컨트롤러 연결 ──
            var ctrl = root.AddComponent<IMUNROK.Gyeonu.HonsangController>();
            ctrl.innerLight = l;
            ctrl.interiorRenderer = inner.GetComponent<MeshRenderer>();
            ctrl.orb = spin.transform;   // 퍼즐은 RotateOrb/OrbAngle로 극축 회전만 시키면 된다

            // 포커스 조작 (2026-08-14) — 클릭하면 카메라가 정면(±X)으로 미끄러져 고정되고
            // 드래그로 구를 돌린다. 입력은 DebugFocusRig(마우스 임시)/추후 VR 리그가 담당
            var focus = root.AddComponent<IMUNROK.Gyeonu.HonsangFocusOrb>();
            focus.honsang = ctrl;
            focus.displayName = "혼상";
            focus.focusDistance = 1.7f;

            // 불씨 등잔은 2026-08-15 제거 — "여기 쓰세요" 하고 놔둔 것처럼 어색했다.
            // 점등 흐름: 혼상 회전 → "빛이 필요하다" 안내(하단 고정, 포커스 자동 해제) →
            // 작업실 큰 탁자의 촛대(LanternPickup — 안내 전에는 집기 게이트 잠김)를 들고 와서
            // 혼상 사용 → HonsangFocusOrb가 촛대를 소모하며 PlayIgnite 시퀀스 시작.

            foreach (var m in tempMeshes) Object.DestroyImmediate(m);
            tempMeshes.Clear();

            PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabDir + "/혼상.prefab", InteractionMode.AutomatedAction);
            root.transform.SetPositionAndRotation(prevPos, prevRot);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[혼상] 생성 완료 — {CountTris(root):n0}tri, 높이 약 {CenterY + SphereR + 0.13f:F2}m, 프리팹 저장. " +
                      "점등: Tools ▸ 이문록 ▸ 혼상 별밤 켜기/끄기");
        }

        // ── 에디터 점등 토글 (스크린샷·연출 확인용, 런타임은 HonsangController 직접 호출) ──

        [MenuItem("Tools/이문록/혼상 별밤 켜기")]
        public static void StarNightOn() => ToggleStarNight(true);

        [MenuItem("Tools/이문록/혼상 별밤 끄기")]
        public static void StarNightOff() => ToggleStarNight(false);

        static void ToggleStarNight(bool on)
        {
            var root = FindRootIncludingInactive(RootName);
            if (root == null) { Debug.LogError("[혼상] 씬에 혼상이 없습니다 — 먼저 '혼상 생성' 실행"); return; }
            root.GetComponent<IMUNROK.Gyeonu.HonsangController>().StarNight(on);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[혼상] 별밤 {(on ? "켬 — 등잔 소등·천장 별 점등" : "끔 — 등잔 점등·천장 별 소등")}");
        }

        // ── 구 셸 메시: 등장방형 UV 구 (inward=true면 내피: 법선 안쪽·감김 반전) ──
        static Mesh SphereShell(float r, int lonSeg, int latSeg, bool inward)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>();
            var uv = new List<Vector2>(); var tr = new List<int>();
            for (int i = 0; i <= latSeg; i++)
            {
                float theta = (float)i / latSeg * Mathf.PI;
                for (int j = 0; j <= lonSeg; j++)
                {
                    float phi = (float)j / lonSeg * Mathf.PI * 2f;
                    var d = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                    v.Add(d * r);
                    n.Add(inward ? -d : d);
                    uv.Add(new Vector2((float)j / lonSeg, 1f - (float)i / latSeg));
                }
            }
            int W = lonSeg + 1;
            for (int i = 0; i < latSeg; i++)
                for (int j = 0; j < lonSeg; j++)
                {
                    int a = i * W + j, b = a + 1, cc = a + W, dd = cc + 1;
                    if (inward) { tr.Add(a); tr.Add(cc); tr.Add(b); tr.Add(cc); tr.Add(dd); tr.Add(b); }
                    else { tr.Add(a); tr.Add(b); tr.Add(cc); tr.Add(cc); tr.Add(b); tr.Add(dd); }
                }
            var m = new Mesh();
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tr, 0);
            m.RecalculateTangents(); // 노멀맵 필수 — 없으면 외피 요철이 깨진다
            m.RecalculateBounds();
            return m;
        }

        /// <summary>경도 방향으로 이음매 없는 펄린 노이즈 (u는 0..1 주기). 등장방형 UV의 0/360° 봉합선 제거.</summary>
        static float SeamlessU(float u, float v, float fu, float fv, float ox, float oy)
        {
            float a = Mathf.PerlinNoise(u * fu + ox, v * fv + oy);
            float b = Mathf.PerlinNoise((u - 1f) * fu + ox, v * fv + oy);
            return Mathf.Lerp(a, b, u);
        }

        /// <summary>부식·녹 얼룩 필드 (0=성한 주물, 1=삭은 자국). 알베도·노멀·마스크 세 텍스처가
        /// 같은 필드를 공유해야 얼룩이 색·요철·거칠기에서 같은 자리에 나타난다.</summary>
        static float Corrosion(float u, float v)
        {
            float big = SeamlessU(u, v, 3.2f, 1.6f, 11.3f, 4.7f);
            float mid = SeamlessU(u, v, 8.5f, 4.2f, 27.1f, 9.3f);
            float fine = SeamlessU(u, v, 21f, 10.5f, 53.7f, 17.9f);
            return Mathf.SmoothStep(0.34f, 0.72f, big * 0.55f + mid * 0.31f + fine * 0.14f);
        }

        /// <summary>별구멍 + 패티나 텍스처 (1024×512 등장방형). RGB=패티나 얼룩, A=구멍(0=뚫림).
        /// 위도별 가로 반경 보정으로 구면에서 원형 구멍이 되게 한다. 시드 고정 — 재실행 동일.
        /// 구멍 배치 RNG는 얼룩 계산과 분리돼 있어 색감을 바꿔도 구멍 위치는 그대로다.</summary>
        static Texture2D BuildHoleTexture()
        {
            const int W = 1024, H = 512;
            string path = TexDir + "/T_혼상_구멍.png";
            var rnd = new System.Random(77070707);
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (float)x / W, vv = (float)y / H;
                    float cor = Corrosion(u, vv);
                    float grain = SeamlessU(u, vv, 48f, 24f, 5.1f, 71.3f);
                    float v = Mathf.Lerp(1.0f, 0.58f, cor) * (0.86f + grain * 0.28f);
                    // 삭은 자리는 청록 녹빛으로 살짝 기운다
                    var tint = Color.Lerp(new Color(1.00f, 0.95f, 0.88f), new Color(0.76f, 0.94f, 0.86f), cor);
                    px[y * W + x] = new Color(v * tint.r, v * tint.g, v * tint.b, 1f);
                }
            // 구멍: 대 14 / 중 96 / 소 330 — 극지방(왜곡·축핀)은 피한다
            var holes = new List<(int cx, int cy, float r)>();
            for (int i = 0; i < 440; i++)
            {
                float rad = i < 14 ? Mathf.Lerp(6.5f, 9f, (float)rnd.NextDouble())
                          : i < 110 ? Mathf.Lerp(3.8f, 6.2f, (float)rnd.NextDouble())
                          : Mathf.Lerp(1.8f, 3.6f, (float)rnd.NextDouble());
                float lat = Mathf.Asin(Mathf.Lerp(-0.94f, 0.94f, (float)rnd.NextDouble())); // 면적 균일
                int cy = Mathf.RoundToInt((0.5f - lat / Mathf.PI) * H);
                int cx = rnd.Next(0, W);
                holes.Add((cx, cy, rad));
            }
            foreach (var h in holes)
            {
                float lat = (0.5f - (float)h.cy / H) * Mathf.PI;
                float su = 1f / Mathf.Max(0.25f, Mathf.Cos(lat)); // 가로 늘림 보정
                int bx = Mathf.CeilToInt(h.r * su) + 2, by = Mathf.CeilToInt(h.r) + 2;
                for (int dy = -by; dy <= by; dy++)
                {
                    int y = h.cy + dy;
                    if (y < 0 || y >= H) continue;
                    for (int dx = -bx; dx <= bx; dx++)
                    {
                        int x = ((h.cx + dx) % W + W) % W; // 경도 랩
                        float d = Mathf.Sqrt(dx / su * (dx / su) + dy * dy);
                        int idx = y * W + x;
                        var p = px[idx];
                        if (d < h.r) p.a = Mathf.Min(p.a, Mathf.Clamp01(d - h.r + 0.8f));
                        else if (d < h.r * 1.9f) // 구멍 둘레 그을음
                        {
                            float t = Mathf.InverseLerp(h.r * 1.9f, h.r, d) * 0.35f;
                            p.r *= 1f - t; p.g *= 1f - t; p.b *= 1f - t;
                        }
                        px[idx] = p;
                    }
                }
            }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>주물 기복 + 부식 파임 노멀맵. 부식 자국은 음각(-)이라 빛이 걸린다.</summary>
        static Texture2D BuildSurfaceNormal()
        {
            const int W = 1024, H = 512;
            string path = TexDir + "/T_혼상_노멀.png";
            var height = new float[W, H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (float)x / W, v = (float)y / H;
                    height[x, y] = SeamlessU(u, v, 5.5f, 2.8f, 63.1f, 21.7f) * 0.55f   // 주물 기복
                                 + SeamlessU(u, v, 44f, 22f, 5.1f, 71.3f) * 0.18f      // 거친 결
                                 - Corrosion(u, v) * 0.45f;                            // 삭아 파인 자국
                }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false, true);
            const float strength = 2.6f;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float dx = height[(x + 1) % W, y] - height[(x - 1 + W) % W, y];
                    float dy = height[x, Mathf.Min(y + 1, H - 1)] - height[x, Mathf.Max(y - 1, 0)];
                    var n = new Vector3(-dx * strength, -dy * strength * 0.5f, 1f).normalized;
                    tex.SetPixel(x, y, new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f));
                }
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (imp.textureType != TextureImporterType.NormalMap)
            {
                imp.textureType = TextureImporterType.NormalMap;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>URP Lit 메탈릭/글로스 맵 — R=메탈릭, A=스무스니스. 삭은 자리일수록
        /// 둘 다 낮아져 거칠고 광이 죽는다 (성한 데는 금속 광택이 남아야 돌처럼 안 보인다).
        /// 데이터 텍스처이므로 sRGB 해제·무압축.</summary>
        static Texture2D BuildSurfaceMask()
        {
            const int W = 1024, H = 512;
            string path = TexDir + "/T_혼상_마스크.png";
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false, true);
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (float)x / W, v = (float)y / H;
                    float cor = Corrosion(u, v);
                    float grain = SeamlessU(u, v, 62f, 31f, 88.3f, 12.1f);
                    float metallic = Mathf.Lerp(0.90f, 0.44f, cor);
                    float smooth = Mathf.Clamp01(Mathf.Lerp(0.46f, 0.13f, cor) * (0.82f + grain * 0.36f));
                    px[y * W + x] = new Color(metallic, 0f, 0f, smooth);
                }
            tex.SetPixels(px);
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (imp.sRGBTexture || imp.textureCompression != TextureImporterCompression.Uncompressed)
            {
                imp.sRGBTexture = false;
                imp.alphaSource = TextureImporterAlphaSource.FromInput;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ── 이하 헬퍼는 HoncheonuiBuilder에서 복사 (원본 불변) ──

        static Mesh BuildBand(float radius, float width, float thick, int seg)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>();
            var uv = new List<Vector2>(); var tr = new List<int>();
            float rO = radius + thick * 0.5f, rI = radius - thick * 0.5f, hw = width * 0.5f;
            void Strip(float r0, float y0, float r1, float y1, float nr, float ny)
            {
                int start = v.Count;
                for (int i = 0; i <= seg; i++)
                {
                    float a = (float)i / seg * Mathf.PI * 2f;
                    var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    var nrm = (d * nr + Vector3.up * ny).normalized;
                    v.Add(d * r0 + Vector3.up * y0); n.Add(nrm); uv.Add(new Vector2((float)i / seg * 8f, 0));
                    v.Add(d * r1 + Vector3.up * y1); n.Add(nrm); uv.Add(new Vector2((float)i / seg * 8f, 1));
                }
                for (int i = 0; i < seg; i++)
                {
                    int b = start + i * 2;
                    tr.Add(b); tr.Add(b + 1); tr.Add(b + 2);
                    tr.Add(b + 2); tr.Add(b + 1); tr.Add(b + 3);
                }
            }
            Strip(rO, -hw, rO, hw, 1, 0);
            Strip(rI, hw, rI, -hw, -1, 0);
            Strip(rO, hw, rI, hw, 0, 1);
            Strip(rI, -hw, rO, -hw, 0, -1);
            var m = new Mesh();
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tr, 0);
            return m;
        }

        static void Add(List<CombineInstance> list, Mesh m, Vector3 pos, Quaternion rot, Vector3 scale)
            => list.Add(new CombineInstance { mesh = m, transform = Matrix4x4.TRS(pos, rot, scale) });

        static void AddDiag(List<CombineInstance> list, Mesh cube, Vector3 from, Vector3 to, float thick)
        {
            var dir = to - from;
            Add(list, cube, (from + to) * 0.5f, Quaternion.FromToRotation(Vector3.up, dir.normalized),
                new Vector3(thick, dir.magnitude + thick * 0.35f, thick));
        }

        static Mesh Combine(List<CombineInstance> list)
        {
            var m = new Mesh();
            m.CombineMeshes(list.ToArray(), true, true);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static Mesh PrimitiveMesh(PrimitiveType t)
        {
            var go = GameObject.CreatePrimitive(t);
            var m = go.GetComponent<MeshFilter>().sharedMesh; // 빌트인 메시 — 파괴 금지
            Object.DestroyImmediate(go);
            return m;
        }

        static GameObject MeshGO(string name, Mesh mesh, Material mat, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static Mesh SaveMesh(Mesh built, string name)
        {
            string path = $"{ModelDir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                built.name = name;
                AssetDatabase.CreateAsset(built, path);
                return built;
            }
            existing.Clear();
            existing.indexFormat = built.indexFormat;
            existing.vertices = built.vertices;
            existing.normals = built.normals;
            existing.tangents = built.tangents; // 빠뜨리면 외피 노멀맵이 무효가 된다 (혼천의 헬퍼엔 없던 항목)
            existing.uv = built.uv;
            existing.triangles = built.triangles;
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(built);
            return existing;
        }

        static Material Mat(string name, Color c, float metallic, float smooth,
            Texture2D baseMap = null, bool alphaClip = false, Color? emission = null,
            Texture2D normal = null, float bumpScale = 1f, Texture2D mask = null)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smooth);
            m.SetTexture("_BaseMap", baseMap);
            m.SetTexture("_BumpMap", normal);
            m.SetFloat("_BumpScale", bumpScale);
            if (normal != null) m.EnableKeyword("_NORMALMAP"); else m.DisableKeyword("_NORMALMAP");
            // 마스크맵이 있으면 메탈릭=R, 스무스니스=A×_Smoothness.
            // 베이스맵 A는 알파컷 전용이므로 스무스니스 채널은 반드시 0(마스크 A)으로 둔다
            m.SetTexture("_MetallicGlossMap", mask);
            m.SetFloat("_SmoothnessTextureChannel", 0f);
            if (mask != null) m.EnableKeyword("_METALLICSPECGLOSSMAP"); else m.DisableKeyword("_METALLICSPECGLOSSMAP");
            if (alphaClip)
            {
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", 0.5f);
                m.EnableKeyword("_ALPHATEST_ON");
            }
            else
            {
                m.SetFloat("_AlphaClip", 0f);
                m.DisableKeyword("_ALPHATEST_ON");
            }
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetColor("_EmissionColor", emission.Value);
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static GameObject FindRootIncludingInactive(string name)
        {
            foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (g.name == name) return g;
            return null;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        static int CountTris(GameObject root)
        {
            int n = 0;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) n += mf.sharedMesh.triangles.Length / 3;
            return n;
        }
    }
}
