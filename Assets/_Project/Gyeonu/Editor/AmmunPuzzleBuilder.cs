using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 암문 개미수열 자물쇠 — 돌 버튼 두 개 + 퍼즐 컴포넌트. 멱등 (2026-08-23).
    ///
    /// ■ 자리 — 사용자가 씬에 놓아 둔 큐브를 따른다
    ///     Cube 1 (왼쪽 = 一) pos (29.824, 2.7665, −7.630) / scale (0.20, 0.50, 0.50)
    ///     Cube 2 (오른쪽 = 二) pos (29.824, 2.7665, −8.340) / 같은 크기
    ///   문은 −X(강)를 보므로 **문 앞에 선 사람 기준 왼쪽이 +Z**, 즉 z −7.63 쪽이 1번이다.
    ///   큐브는 만든 뒤 지운다(⑤). 지워진 뒤에도 다시 지을 수 있게 값은 상수로 박아 둔다.
    ///
    /// ■ 왜 높이가 0.50이 아니라 0.50 그대로인데 앞면이 계단인가
    ///   교대 서면은 **매끈한 경사면이 아니라 0.40m마다 0.022m씩 물러나는 켜(course)** 다
    ///   (실측: y 2.5675 / 2.9690 에서 x 29.8234 → 29.8455 → 29.8674).
    ///   버튼(0.50 높이)은 그 이음매 두 줄을 가로지른다. 앞면을 평평한 판으로 만들면
    ///   아래 켜에서는 7.8cm 튀어나오고 위 켜에서는 파묻힌다 — 석축 일부로 보일 수가 없다.
    ///   그래서 **앞면을 주변 켜와 똑같이 계단으로** 깎아, 어느 켜에서든 정확히
    ///   <see cref="Proud"/>(3.4cm)만 튀어나오게 했다. 음각은 가운데 켜(0.40m) 안에만 넣는다.
    ///
    /// ■ 재질
    ///   몸통은 석축이 쓰는 **바로 그 재질**(FBX의 SM_24ET0014_M002)을 참조한다 — 복제하지
    ///   않으므로 색이 어긋날 수 없다. 음각 속만 어두운 재질(M_암문_음각)로 갈아 글자가 읽힌다.
    ///
    /// ■ 부모는 문짝(석축덩어리)
    ///   버튼은 석축에 박힌 돌이므로 문이 열려 미끄러질 때 함께 간다. 콜라이더를 달아 두면
    ///   조준 판정은 GetComponentInParent로 올라가 SecretStoneDoor에 닿는다 — 버튼을 조준해도
    ///   "석축 — 살펴보기"가 뜬다 (퍼즐 컴포넌트는 콜라이더가 없어 직접 조준되지 않는다).
    /// </summary>
    public static class AmmunPuzzleBuilder
    {
        const string SceneName = "Gyeonu_EunhaDam";
        const string DoorRoot  = "교대_암문";
        const string SlabName  = "석축덩어리";
        const string PuzzleGo  = "암문_잠금퍼즐";
        const string MatDir    = "Assets/_Project/Gyeonu/Art/Materials/Ammun";
        const string MeshDir   = "Assets/_Project/Gyeonu/Art/Models/Ammun";

        // ── 사용자 큐브 (위치·크기) ──
        const float ButtonY  = 2.7665f;
        const float OneZ     = -7.630f;   // 왼쪽 = 一 = 1
        const float TwoZ     = -8.340f;   // 오른쪽 = 二 = 2
        const float HalfW    = 0.250f;    // z 반폭
        const float HalfH    = 0.250f;    // y 반높이
        const float Depth    = 0.100f;    // 석축 속으로 박히는 깊이

        // ── 면 맞춤 ──
        // 튀어나온 양은 **최대 눌린 깊이보다 커야 한다** — 안 그러면 마지막 누름에서 돌이
        // 벽 속으로 잠겨 버린다. 6칸(0.004×6=0.024) + 행정(0.018) = 0.042 → 3mm 여유.
        // ⚠️ 3.4cm로 잡았다가 Play에서 보니 **움직임이 안 읽혔다** (정면에서 1.3cm는 시차가
        //    거의 없다). 자물쇠 진행 상황을 글로 알리지 않는 퍼즐이라 움직임이 유일한 채널이므로
        //    돌출·행정을 함께 키웠다. 주변 석축에도 4~6cm 튀어나온 돌이 여럿이라 튀지 않는다.
        const float Proud    = 0.045f;    // 주변 켜보다 앞으로 나온 양
        const float Groove   = 0.013f;    // 음각 깊이 — 얕게
        const float SlopeDz  = 0.0232f;   // 서면이 z를 따라 눕는 물매 (dx/dz, 실측)

        [MenuItem("Tools/이문록/암문 자물쇠 퍼즐 생성", priority = 322)]
        public static void Build()
        {
            if (SceneManager.GetActiveScene().name != SceneName)
            { Debug.LogError($"[암문자물쇠] '{SceneName}' 씬에서 실행하세요."); return; }

            var root = GameObject.Find(DoorRoot);
            if (root == null) { Debug.LogError("[암문자물쇠] 암문을 먼저 지으세요 (Tools ▸ 이문록 ▸ 암문 생성)."); return; }
            var slab = root.transform.Find(SlabName);
            if (slab == null) { Debug.LogError("[암문자물쇠] 석축덩어리를 찾지 못했습니다."); return; }
            var door = slab.GetComponent<SecretStoneDoor>();
            var mf = slab.GetComponent<MeshFilter>();
            var mr = slab.GetComponent<MeshRenderer>();
            if (door == null || mf == null || mr == null) { Debug.LogError("[암문자물쇠] 문짝 구성이 예상과 다릅니다."); return; }

            EnsureFolder(MatDir); EnsureFolder(MeshDir);

            // ── ① 문짝 앞면 실측 — 켜 이음매 y와 켜마다의 x ──
            float loJoint, hiJoint, midOne, midTwo, stepLo, stepHi;
            if (!Survey(slab, mf.sharedMesh, out loJoint, out hiJoint, out midOne, out midTwo, out stepLo, out stepHi))
                return;

            // ── ② 낡은 것 정리 (멱등) ──
            for (int i = slab.childCount - 1; i >= 0; i--)
            {
                var c = slab.GetChild(i);
                if (c.name == PuzzleGo || c.name.StartsWith("잠금버튼")) Object.DestroyImmediate(c.gameObject);
            }

            var matBody = mr.sharedMaterial;                       // 석축과 **같은** 재질
            var matCut  = CutMat(matBody);

            // ── ③ 돌 버튼 두 장 ──
            //   서면이 z를 따라 눕는다 → 앞면이 벽과 나란하도록 Y축으로 조금 비튼다.
            //   그래야 폭 0.50 전체에서 튀어나온 양이 똑같다 (안 비틀면 2.8cm ~ 4.0cm로 벌어진다).
            float tilt = Mathf.Atan(SlopeDz) * Mathf.Rad2Deg;      // 약 1.33°
            var one = MakeButton(slab, "잠금버튼_一", new Vector3(midOne - Proud, ButtonY, OneZ), tilt,
                                 loJoint, hiJoint, stepLo, stepHi, BarsOne(), matBody, matCut, "암문_버튼_一");
            var two = MakeButton(slab, "잠금버튼_二", new Vector3(midTwo - Proud, ButtonY, TwoZ), tilt,
                                 loJoint, hiJoint, stepLo, stepHi, BarsTwo(), matBody, matCut, "암문_버튼_二");

            // ── ④ 퍼즐 컴포넌트 ──
            //   콜라이더가 없는 빈 오브젝트에 얹는다 — 직접 조준되면 안 된다
            //   (조준은 늘 문짝의 SecretStoneDoor가 받고, 조건을 통과했을 때만 이쪽을 부른다).
            var pgo = new GameObject(PuzzleGo);
            pgo.transform.SetParent(slab, false);
            pgo.transform.localPosition = Vector3.zero;
            var pz = pgo.AddComponent<AmmunLockPuzzle>();
            pz.door = door;
            pz.buttonOne = one.transform;
            pz.buttonTwo = two.transform;
            float nx = Mathf.Cos(tilt * Mathf.Deg2Rad), nz = Mathf.Sin(tilt * Mathf.Deg2Rad);
            pz.pressAxis = new Vector3(nx, 0f, -nz);               // 석축 속으로
            pz.viewNormal = new Vector3(-nx, 0f, nz);              // 강 쪽 (플레이어가 서는 쪽)
            pz.answer = new[] { 1, 1, 1, 2, 2, 1 };                // 개미수열 — 좌 좌 좌 우 우 좌
            pz.displayName = "돌 자물쇠";
            // 카메라가 다가서는 거리 (2026-08-23 완화: 0.85 → 1.00).
            //   문 앞에 선 눈높이(28.5, 2.39, −7.95)에서 두 돌 한가운데까지가 **1.354m**.
            //   0.85로 들어가면 다가서는 양이 0.504m라 코앞에 들이대는 느낌이었다.
            //   다가서는 양을 그 **70%**(0.353m)로 줄여 1.00m에 선다 —
            //   두 돌(폭 1.21m)이 화면 가로의 59%를 차지해 여전히 크되 사방에 여백이 생긴다.
            //   (0.85일 때는 69%였다.) 더 물러서고 싶으면 이 값만 키우면 된다.
            pz.focusDistance = 1.00f;
            pz.transitionTime = 0.6f;
            pz.dimStrength = 0.7f;

            // ── ⑤ 문에 물리기 — 이제 조건을 다 갖춰도 곧장 열리지 않는다 ──
            Undo.RecordObject(door, "암문 퍼즐 연결");
            door.puzzle = pz;
            door.puzzleFlag = GyeonuWorld.F_암문퍼즐;
            door.promptPuzzle = "살펴보기";
            EditorUtility.SetDirty(door);

            // ── ⑥ 위치 지정용 큐브 정리 ──
            int killed = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t == null || t.parent != null) continue;
                string n = t.name;
                if (n != "Cube 1" && n != "Cube 2" && n != "Cube" && !n.StartsWith("Cube (")) continue;
                Object.DestroyImmediate(t.gameObject); killed++;
            }

            EditorUtility.SetDirty(root);
            EditorSceneManagerMarkDirty();
            Debug.Log($"[암문자물쇠] 돌 버튼 2개 — 一 z{OneZ} / 二 z{TwoZ}, 앞면 {Proud * 100:F1}cm 돌출, " +
                      $"음각 {Groove * 100:F1}cm. 켜 이음매 y {loJoint + ButtonY:F4}·{hiJoint + ButtonY:F4} " +
                      $"(단 {stepLo:F4}/{stepHi:F4}). 위치용 큐브 {killed}개 제거. 정답 1 1 1 2 2 1.");
        }

        // ══════════════════════════════════════════════════
        //  실측 — 문짝 앞면의 켜 이음매를 찾는다
        // ══════════════════════════════════════════════════

        /// <summary>
        /// 임시 MeshCollider를 세우고 버튼 자리 위아래를 훑어 켜 이음매 y와 켜마다의 x를 찾는다.
        /// 값을 손으로 박지 않는 이유 — 문짝 메시는 AmmunBuilder가 원본 FBX에서 다시 오려 내므로
        /// 재빌드 때마다 미세하게 달라질 수 있다. 매번 실측해서 맞추면 어긋날 일이 없다.
        /// </summary>
        static bool Survey(Transform slab, Mesh mesh, out float loJoint, out float hiJoint,
                           out float midOne, out float midTwo, out float stepLo, out float stepHi)
        {
            loJoint = hiJoint = midOne = midTwo = stepLo = stepHi = 0f;
            var tmp = new GameObject("__암문면실측") { hideFlags = HideFlags.HideAndDontSave };
            tmp.transform.SetPositionAndRotation(slab.position, slab.rotation);
            tmp.layer = 31;
            var mc = tmp.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;

            System.Func<float, float, float> surf = (y, z) =>
            {
                RaycastHit h;
                return Physics.Raycast(new Vector3(slab.position.x - 2f, y, z), Vector3.right, out h, 6f, 1 << 31)
                     ? h.point.x : float.NaN;
            };

            midOne = surf(ButtonY, OneZ);
            midTwo = surf(ButtonY, TwoZ);
            if (float.IsNaN(midOne) || float.IsNaN(midTwo))
            { Object.DestroyImmediate(tmp); Debug.LogError("[암문자물쇠] 문짝 앞면을 맞히지 못했습니다."); return false; }

            // 버튼 범위 안에서 x가 확 바뀌는 지점 = 켜 이음매. 이음매는 짧은 물매라 중간을 취한다.
            float below = midOne, above = midOne;
            loJoint = FindJoint(surf, ButtonY, ButtonY - HalfH, OneZ, ref below);
            above = midOne;
            hiJoint = FindJoint(surf, ButtonY, ButtonY + HalfH, OneZ, ref above);
            Object.DestroyImmediate(tmp);

            if (float.IsNaN(loJoint) || float.IsNaN(hiJoint))
            { Debug.LogError("[암문자물쇠] 켜 이음매를 찾지 못했습니다 — 앞면 형상이 바뀐 듯합니다."); return false; }

            stepLo = below - midOne;     // 음수 — 아래 켜는 앞으로 나와 있다
            stepHi = above - midOne;     // 양수 — 위 켜는 뒤로 물러나 있다
            loJoint -= ButtonY;          // 버튼 중심 기준 로컬 y로
            hiJoint -= ButtonY;
            return true;
        }

        /// <summary>
        /// from에서 to 쪽으로 1mm씩 훑어 켜 이음매 y를 찾는다.
        /// 이음매는 딱 끊어진 턱이 아니라 8mm쯤의 짧은 물매다 — 물매가 **시작되는 y**와
        /// 다음 켜 값에 **다다르는 y**의 중간을 이음매로 본다.
        /// <paramref name="far"/> 에는 이음매 너머 켜의 x를 담아 준다 (버튼 끝 근처에서 잰다 —
        /// 물매 도중에 재면 단이 실제보다 얕게 나온다).
        /// </summary>
        static float FindJoint(System.Func<float, float, float> surf, float from, float to, float z, ref float far)
        {
            float baseX = surf(from, z);
            float dir = Mathf.Sign(to - from);
            far = surf(to - dir * 0.004f, z);                  // 버튼 끝 바로 안쪽 = 다음 켜의 제 값
            if (float.IsNaN(far) || Mathf.Abs(far - baseX) < 0.005f) return float.NaN;

            float yA = float.NaN, yB = float.NaN;
            for (float y = from; dir > 0 ? y <= to : y >= to; y += dir * 0.001f)
            {
                float x = surf(y, z);
                if (float.IsNaN(x)) continue;
                if (float.IsNaN(yA) && Mathf.Abs(x - baseX) > 0.0005f) yA = y;
                if (!float.IsNaN(yA) && Mathf.Abs(x - far) < 0.0005f) { yB = y; break; }
            }
            if (float.IsNaN(yA) || float.IsNaN(yB)) return float.NaN;
            return (yA + yB) * 0.5f;
        }

        // ══════════════════════════════════════════════════
        //  버튼 메시
        // ══════════════════════════════════════════════════

        struct Bar { public float y0, y1, z0, z1; }

        /// <summary>「一」 — 가로 한 획.</summary>
        static Bar[] BarsOne() => new[]
        {
            new Bar { y0 = -0.019f, y1 = 0.019f, z0 = -0.135f, z1 = 0.135f },
        };

        /// <summary>「二」 — 위가 짧고 아래가 길다.</summary>
        static Bar[] BarsTwo() => new[]
        {
            new Bar { y0 =  0.036f, y1 =  0.070f, z0 = -0.095f, z1 = 0.095f },
            new Bar { y0 = -0.070f, y1 = -0.036f, z0 = -0.130f, z1 = 0.130f },
        };

        static GameObject MakeButton(Transform parent, string name, Vector3 worldPos, float tiltY,
                                     float loJoint, float hiJoint, float stepLo, float stepHi,
                                     Bar[] bars, Material body, Material cut, string meshName)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            go.transform.localRotation = Quaternion.Euler(0f, tiltY, 0f);

            var mesh = ButtonMesh(loJoint, hiJoint, stepLo, stepHi, bars);
            mesh.name = meshName;
            go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(mesh, meshName);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = new[] { body, cut };

            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3((stepLo + Depth) * 0.5f, 0f, 0f);
            col.size = new Vector3(Depth - stepLo, HalfH * 2f, HalfW * 2f);
            return go;
        }

        /// <summary>
        /// 로컬 좌표계: x = 0 이 가운데 켜의 앞면, +x가 석축 **속**. y·z는 버튼 중심 기준.
        /// 서브메시 0 = 돌 몸통, 1 = 음각 속.
        /// </summary>
        static Mesh ButtonMesh(float loJoint, float hiJoint, float stepLo, float stepHi, Bar[] bars)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>();
            var triBody = new List<int>(); var triCut = new List<int>();

            System.Action<Vector3, Vector3, Vector3, Vector3, Vector3, List<int>> quad =
            (a, b, c, d, nor, tri) =>
            {
                int i0 = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                for (int k = 0; k < 4; k++) n.Add(nor);
                uv.Add(new Vector2(a.z, a.y)); uv.Add(new Vector2(b.z, b.y));
                uv.Add(new Vector2(c.z, c.y)); uv.Add(new Vector2(d.z, d.y));
                // 감김은 자동으로 맞춘다 — 면 법선과 원하는 법선의 부호만 보면 된다
                bool ok = Vector3.Dot(Vector3.Cross(b - a, c - a), nor) >= 0f;
                if (ok) { tri.Add(i0); tri.Add(i0 + 1); tri.Add(i0 + 2); tri.Add(i0); tri.Add(i0 + 2); tri.Add(i0 + 3); }
                else    { tri.Add(i0); tri.Add(i0 + 2); tri.Add(i0 + 1); tri.Add(i0); tri.Add(i0 + 3); tri.Add(i0 + 2); }
            };
            // 앞면 조각 (x 고정, 바깥 = −x)
            System.Action<float, float, float, float, float> face =
            (x, y0, y1, z0, z1) => quad(new Vector3(x, y0, z0), new Vector3(x, y0, z1),
                                        new Vector3(x, y1, z1), new Vector3(x, y1, z0), Vector3.left, triBody);

            float lo = -HalfH, hi = HalfH;

            // ── 앞면: 아래 켜 / 가운데 켜(음각 포함) / 위 켜 ──
            face(stepLo, lo, loJoint, -HalfW, HalfW);
            face(stepHi, hiJoint, hi, -HalfW, HalfW);

            // 가운데 켜 — 획이 지나는 y 띠만 좌우로 갈라 남긴다
            var sorted = new List<Bar>(bars);
            sorted.Sort((a, b) => a.y0.CompareTo(b.y0));
            float cur = loJoint;
            foreach (var b in sorted)
            {
                if (b.y0 > cur) face(0f, cur, b.y0, -HalfW, HalfW);
                face(0f, b.y0, b.y1, -HalfW, b.z0);
                face(0f, b.y0, b.y1, b.z1, HalfW);
                cur = b.y1;
            }
            if (cur < hiJoint) face(0f, cur, hiJoint, -HalfW, HalfW);

            // ── 음각 속 (서브메시 1) ──
            //   ⚠️ 홈 바닥을 앞면과 나란한 **평면**으로 파면 안 된다. 석축 재질은 텍스처가 없는
            //      민 회색이라, 정면에서 보면 홈 전체가 한 가지 명도로 칠해져 **먹으로 그린 획**처럼
            //      보인다 (첫 빌드에서 그대로 나왔다). 실제 음각처럼 **비스듬히 깎아** 위·아래 사면의
            //      명암이 갈리게 해야 빛이 어느 쪽에서 오든 파인 것으로 읽힌다.
            //   단면은 좁은 바닥을 남긴 사다리꼴(V에 가까운 U) — 삼각형 없이 사각형만으로 만든다.
            foreach (var b in sorted)
            {
                float g = Groove;
                float ym = (b.y0 + b.y1) * 0.5f;
                float floorHalf = (b.y1 - b.y0) * 0.5f * 0.18f;      // 바닥에 남기는 폭
                float fb = ym - floorHalf, ft = ym + floorHalf;
                float up = b.y1 - ft, dn = fb - b.y0;                // 사면의 높이

                quad(new Vector3(0f, b.y1, b.z0), new Vector3(0f, b.y1, b.z1),
                     new Vector3(g, ft, b.z1), new Vector3(g, ft, b.z0),
                     new Vector3(-up, -g, 0f).normalized, triCut);                    // 위 사면 (아래·바깥을 본다)
                quad(new Vector3(0f, b.y0, b.z0), new Vector3(0f, b.y0, b.z1),
                     new Vector3(g, fb, b.z1), new Vector3(g, fb, b.z0),
                     new Vector3(-dn, g, 0f).normalized, triCut);                     // 아래 사면 (위·바깥을 본다)
                quad(new Vector3(g, fb, b.z0), new Vector3(g, fb, b.z1),
                     new Vector3(g, ft, b.z1), new Vector3(g, ft, b.z0), Vector3.left, triCut);   // 바닥
                quad(new Vector3(0f, b.y0, b.z0), new Vector3(0f, b.y1, b.z0),
                     new Vector3(g, ft, b.z0), new Vector3(g, fb, b.z0), Vector3.forward, triCut); // 획 끝 (사다리꼴)
                quad(new Vector3(0f, b.y0, b.z1), new Vector3(0f, b.y1, b.z1),
                     new Vector3(g, ft, b.z1), new Vector3(g, fb, b.z1), Vector3.back, triCut);
            }

            // ── 켜 이음매 턱 (위를 본다) ──
            quad(new Vector3(stepLo, loJoint, -HalfW), new Vector3(stepLo, loJoint, HalfW),
                 new Vector3(0f, loJoint, HalfW), new Vector3(0f, loJoint, -HalfW), Vector3.up, triBody);
            quad(new Vector3(0f, hiJoint, -HalfW), new Vector3(0f, hiJoint, HalfW),
                 new Vector3(stepHi, hiJoint, HalfW), new Vector3(stepHi, hiJoint, -HalfW), Vector3.up, triBody);

            // ── 옆면 (켜마다 앞이 다르므로 세 토막) ──
            foreach (int s in new[] { -1, 1 })
            {
                float z = s * HalfW;
                var nor = s > 0 ? Vector3.forward : Vector3.back;
                quad(new Vector3(stepLo, lo, z), new Vector3(Depth, lo, z),
                     new Vector3(Depth, loJoint, z), new Vector3(stepLo, loJoint, z), nor, triBody);
                quad(new Vector3(0f, loJoint, z), new Vector3(Depth, loJoint, z),
                     new Vector3(Depth, hiJoint, z), new Vector3(0f, hiJoint, z), nor, triBody);
                quad(new Vector3(stepHi, hiJoint, z), new Vector3(Depth, hiJoint, z),
                     new Vector3(Depth, hi, z), new Vector3(stepHi, hi, z), nor, triBody);
            }

            // ── 위·아래·뒤 ──
            quad(new Vector3(stepHi, hi, -HalfW), new Vector3(stepHi, hi, HalfW),
                 new Vector3(Depth, hi, HalfW), new Vector3(Depth, hi, -HalfW), Vector3.up, triBody);
            quad(new Vector3(stepLo, lo, -HalfW), new Vector3(stepLo, lo, HalfW),
                 new Vector3(Depth, lo, HalfW), new Vector3(Depth, lo, -HalfW), Vector3.down, triBody);
            quad(new Vector3(Depth, lo, -HalfW), new Vector3(Depth, lo, HalfW),
                 new Vector3(Depth, hi, HalfW), new Vector3(Depth, hi, -HalfW), Vector3.right, triBody);

            var m = new Mesh();
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv);
            m.subMeshCount = 2;
            m.SetTriangles(triBody, 0);
            m.SetTriangles(triCut, 1);
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }

        // ══════════════════════════════════════════════════

        /// <summary>음각 속 재질 — 몸통과 같은 셰이더에 색만 죽인다.
        /// 텍스처 없는 회색 석축이라 형상만으로는 얕은 홈이 거의 안 읽힌다.</summary>
        static Material CutMat(Material body)
        {
            string path = MatDir + "/M_암문_음각.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(body != null ? body.shader : Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            var baseCol = body != null && body.HasProperty("_BaseColor") ? body.GetColor("_BaseColor") : Color.gray;
            // 형상(사다리꼴 음각)이 이미 명암을 만드므로 색은 살짝만 죽인다 —
            // 더 어둡게 하면 파인 것이 아니라 **먹으로 칠한 획**으로 보인다.
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseCol * 0.62f);
            if (m.HasProperty("_Color")) m.SetColor("_Color", baseCol * 0.62f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.08f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = MeshDir + "/" + name + ".asset";
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old != null)
            {
                // 덮어쓴다 — 지우고 새로 만들면 씬 참조가 끊긴다 (AmmunBuilder에서 배운 것)
                old.Clear();
                old.SetVertices(new List<Vector3>(mesh.vertices));
                old.SetNormals(new List<Vector3>(mesh.normals));
                old.SetUVs(0, new List<Vector2>(mesh.uv));
                old.subMeshCount = mesh.subMeshCount;
                for (int i = 0; i < mesh.subMeshCount; i++) old.SetTriangles(mesh.GetTriangles(i), i);
                old.RecalculateTangents();
                old.RecalculateBounds();
                EditorUtility.SetDirty(old);
                Object.DestroyImmediate(mesh);
                return old;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int i = path.LastIndexOf('/');
            string parent = path.Substring(0, i), leaf = path.Substring(i + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static void EditorSceneManagerMarkDirty() =>
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }
}
