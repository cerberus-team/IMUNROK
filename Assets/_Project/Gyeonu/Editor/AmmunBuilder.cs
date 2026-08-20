using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 오작교 교대_남 석축 암문 + 교대 콜라이더 + 조건부 발견 힌트. 멱등.
    ///
    /// ■ 문 자리 — 서면(강 쪽) 하단 (2026-08-20 3차, 사용자 지정)
    ///   사용자가 씬에 놓아 둔 위치 지정용 큐브
    ///     pos (29.300, 2.070, −7.950) / rot 0 / scale (0.30, 3.00, 1.80)
    ///   를 그대로 따른다. 큐브는 실제 석축 면보다 0.5m 앞에 떠 있었으므로
    ///   **실측한 석축 서면**(M002, z −8.85~−7.05 구간에서 x 29.712~29.822)에 스냅했다.
    ///   → 문 면 x 29.71 / 폭(z) 1.80 / 높이 y 0.60~3.57 / 바라보는 방향 −X(강 쪽).
    ///   이전 안(남면 z −7.07)과 다리 위 계단 안(A안)은 폐기.
    ///
    /// ■ 문은 문짝이 아니라 석축 한 덩어리다 (2026-08-20 5차: 진짜 구멍을 뚫는다)
    ///   원본 메시(SM_24ET0014_M002)에서 문 크기만큼의 삼각형을 **빼낸 사본**과 **빼낸 조각**을
    ///   각각 우리 폴더에 굽는다. 사본은 씬 인스턴스의 MeshFilter에만 물리므로 FBX·임포트
    ///   설정은 그대로다. 조각(문짝)을 제자리에 놓으면 닫힌 상태는 원본과 한 치도 다르지 않고,
    ///   열리면 석축에 **진짜 구멍**이 남는다.
    ///   열릴 때는 안(+X)으로 0.18 물러났다가 남(−Z, 문 앞에서 봤을 때 오른쪽)으로 2.05
    ///   미끄러져 석축 속으로 숨는다 — 두 동작이 겹쳐 자동문처럼 한 호흡에 이어진다 (약 1.9초).
    ///   옛 안은 문 앞에 검은 판을 덮어 원본 면을 가렸는데, 그 판이 물러난 석판까지 가려
    ///   미끄러지는 동작이 통째로 보이지 않았다.
    ///
    /// ■ 통로 — 개구부는 암문 크기 그대로, 재질은 관측실 통로와 같은 에셋 (2026-08-20 6차)
    ///   단면 1.80 × 2.95 = **암문 개구부 그대로**. 5차의 문설주·인방(문간 1.50 × 2.10)은
    ///   없앴다 — 문 위가 벽으로 막혀 답답했고, 무엇보다 CharacterController가
    ///   height+stepOffset(1.8+0.6=2.4) 헤드룸을 요구해 **아예 들어갈 수가 없었다.**
    ///   재질은 M_관측실_석벽 등을 **참조**한다 — 두 통로는 물리적으로 이어지는 같은 굴이다.
    ///   ㄱ자로 꺾어 총 11.7m: A구간 x 29.71~32.40(2.69) → B구간 북으로 z −7.05~+1.95(9.0).
    ///   곧게 더 못 파는 이유는 지형 절벽(x 32.4→32.6에서 0.59→3.57)이고, 석축 속은 비어 있어
    ///   꺾으면 얼마든지 벌 수 있다. 전환 트리거는 갈림목(문에서 1.0m)이라 몇 걸음이면 닿고,
    ///   B구간 9m는 불빛이 닿지 않는 **눈요기 깊이**다 — 끝이 어둠에 잠겨 보이지 않는다.
    ///
    /// ■ 접근 — 물가를 따라 걷는다. 북·남 **두 갈래** (2026-08-20 4차)
    ///   물벽에 같은 폭 5.2m 개구를 북(z 12.4~17.6)·남(z −38.0~−32.8) 하나씩 냈다.
    ///   그 두 곳 말고는 물가로 내려설 수 없다.
    ///
    /// ■ 길에는 아무 표시도 없다 (2026-08-20 방침 확정)
    ///   징검돌·다져진 흙길·디버그 안내 기둥은 **전부 없앴다.** 비밀문인데 가는 길이
    ///   표시돼 있으면 비밀이 아니다. 플레이어는 단서 문서로 위치를 알고 찾아가야 한다.
    ///   남은 힌트는 **문틈 빛** 하나뿐이고, 그것도 단서 + 밤에 문 앞까지 와야 보인다.
    ///
    /// ■ 교대 콜라이더 — 원본 FBX 정점 실측 기반 **박스만** (MeshCollider 금지)
    ///   서면 x최소가 z에 따라 28.85~31.29로 계단식이라 10구간으로 나눠 세운다.
    ///   상단 8.45 — 지형 터라스(7.12)보다 높아 석축 지붕 위로 못 올라서고,
    ///   다리 마루(8.80)보다 낮아 오작교 통행은 막지 않는다.
    ///
    /// ■ 원본은 읽기만 한다 — FBX와 임포트 설정은 바뀌지 않는다.
    /// </summary>
    public static class AmmunBuilder
    {
        const string SceneName = "Gyeonu_EunhaDam";
        const string RootName  = "교대_암문";
        const string MatDir    = "Assets/_Project/Gyeonu/Art/Materials/StarPath";
        const string MeshDir   = "Assets/_Project/Gyeonu/Art/Models/StarPath";

        // ── 문 자리 (사용자 큐브 → 실측 석축 면에 스냅) ──
        public const float FaceX      = 29.71f;
        public const float DoorCZ     = -7.95f;
        public const float DoorWidth  = 1.80f;
        public const float DoorBottom = 0.60f;
        public const float DoorTop    = 3.57f;
        const float BandHalf   = 0.30f;
        // 석판은 **오려 낸 자리 그대로** 놓는다. 옛 안은 원본 면을 가리려고 3cm 바깥에 띄웠지만,
        // 이제 원본 메시에서 그 삼각형을 빼내므로 제자리가 곧 이음매 없는 자리다 (2026-08-20 5차).
        const float SlabX      = FaceX;

        // ── 통로 — 개구부는 암문 크기 그대로, 재질은 관측실 통로와 같은 에셋 ──
        //   (2026-08-20 6차) 폭·높이를 **암문 개구부에 맞췄다.** 5차의 관측실 규격
        //   (폭 1.80 × 높이 2.35 + 문설주·인방으로 좁힌 문간 1.50 × 2.10)은 두 가지를 망쳤다.
        //     ① 암문은 2.97 높이인데 통로가 2.35라 문 위쪽이 벽으로 막혀 답답했다.
        //     ② CharacterController의 스텝 판정은 **height + stepOffset = 1.8 + 0.6 = 2.4**
        //        헤드룸을 요구한다. 문간 2.10 / 통로 2.35 는 둘 다 그보다 낮아
        //        **문 앞에서 아예 들어갈 수가 없었다** (서고에서 이미 한 번 당한 규칙).
        //   이제 유효 높이 = DoorTop − PassFloor = 2.95, 유효 폭 = DoorWidth = 1.80.
        //   문설주·인방은 없앴다 — 뜯어낸 구멍의 거친 테두리가 곧 문틀이다.
        const float PassFloor  = 0.62f;
        const float CorW       = DoorWidth;              // 1.80 — 암문 폭 그대로
        const float CorT       = 0.30f;                  // 벽 두께 (관측실 T)
        const float UvStone    = 0.40f;                  // 관측실 UvStone — 막돌이 약 2.5m마다 한 번 반복
        const string ObsMatDir = "Assets/_Project/Gyeonu/Art/Materials/Observatory";

        // ── 통로 배치 — ㄱ자 (2026-08-20 6차) ──
        //   **곧게 더 파고들 수가 없다.** 지형 절벽이 x 32.4(0.59) → 32.6(3.57) → 32.8(6.91)로
        //   거의 수직으로 솟아, 동쪽으로 더 뻗으면 지형 표면이 통로 **단면을 가로질러** 흙벽처럼
        //   드러나고 캐릭터도 밀려 튀어 오른다. 계단으로 내려가도 15:1 절벽을 앞지를 수 없다.
        //   대신 석축 속은 비어 있다 (실측: x 31.5 / y 2.0 에서 z −16~+1.5 구간 내내
        //   위 5.24, 서 29.1~29.3 말고는 삼각형이 없다). 그래서 **꺾어서** 깊이를 번다.
        //     A구간: 문 → 동쪽 x 32.40 (2.69m)
        //     B구간: 거기서 **북(+Z)** 으로 z −7.05 → +1.95 (9.0m). 끝은 불빛이 닿지 않아 캄캄하다.
        //   ⚠️ 북으로 꺾는 이유 — 남쪽 z −10.9~−8.8 / x 29.78~30.00 은 **열린 석판이 숨는 자리**다.
        const float PassInnerX = 32.40f;                 // A구간 동쪽 끝 (지형 절벽 한계)
        const float BranchZ    = 1.95f;                  // B구간 북쪽 끝 (world z)

        // ── 교대_남 서벽 계단 프로파일 (z0, z1, 서면 x) — FBX 정점 실측 ──
        static readonly (float z0, float z1, float x0)[] WestWall =
        {
            (-34.0f, -30.0f, 29.15f),
            (-30.0f, -22.0f, 29.22f),
            (-22.0f, -14.0f, 29.40f),
            (-14.0f,  -8.90f, 29.58f),
            //  −8.90 ~ −7.00 은 문 구간 — 위·아래만 막는다
            ( -7.00f,  -4.0f, 28.82f),
            ( -4.0f,   0.0f, 28.95f),
            (  0.0f,   4.0f, 29.15f),
            (  4.0f,   6.0f, 29.40f),
            (  6.0f,  10.0f, 30.05f),
            ( 10.0f,  13.5f, 31.25f),
        };
        const float WallEastX  = 33.60f;
        const float WallTopY   = 8.45f;
        const float WallBotY   = -1.00f;
        const float DoorBandZ0 = -8.90f;
        const float DoorBandZ1 = -7.00f;

        // ── 물가 진입구 — 암문 양쪽에서 접근한다 (2026-08-20 4차) ──
        //   같은 폭 5.2m 로 북쪽·남쪽 각각 하나씩. 그 밖에는 전부 막혀 있다.
        const float GapNorthZ0 =  12.4f, GapNorthZ1 =  17.6f;
        const float GapSouthZ0 = -38.0f, GapSouthZ1 = -32.8f;

        [MenuItem("Tools/이문록/암문 생성 (교대_남 석축)", priority = 321)]
        public static void Build()
        {
            if (SceneManager.GetActiveScene().name != SceneName)
            { Debug.LogError($"[암문] '{SceneName}' 씬에서 실행하세요."); return; }

            var src = FindAbutmentPiece("SM_24ET0014_M002");
            if (src == null) { Debug.LogError("[암문] 교대 M002를 찾지 못했습니다."); return; }

            EnsureFolder(MeshDir); EnsureFolder(MatDir);

            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);

            var pivot = new Vector3(FaceX, DoorBottom, DoorCZ);
            var root = new GameObject(RootName);
            root.transform.position = pivot;
            Undo.RegisterCreatedObjectUndo(root, "암문 생성");

            var stone = src.GetComponent<MeshRenderer>().sharedMaterial;

            // ── ① 석축에 **진짜 구멍**을 뚫고, 뜯어낸 조각을 문짝으로 삼는다 ──
            //   옛 안은 원본 삼각형을 지울 수 없다고 보고 문 앞에 검은 판(원본가림)을 덮었는데,
            //   그 판이 **안으로 밀린 석판까지 가려** 옆으로 미끄러지는 동작이 통째로 보이지 않았다
            //   (2026-08-20 사용자 지적: "살짝 뒤로 들어가고 끝"). 이제는 원본 메시에서 문 삼각형을
            //   빼낸 **사본**을 우리 폴더에 굽고 씬 인스턴스에만 물린다 —
            //   FBX·임포트 설정은 그대로이고(팀에 전파되지 않는 원본은 읽기만), 씬에는 진짜 구멍이
            //   생기므로 석판이 슬롯 안으로 물러나 옆으로 미끄러지는 모습이 그대로 보인다.
            Mesh slabMesh, wallMesh;
            if (!BuildDoorMeshes(src, pivot, out slabMesh, out wallMesh))
            { Debug.LogError("[암문] 오려 낼 삼각형이 없습니다."); return; }
            Undo.RecordObject(src, "암문 구멍");
            src.sharedMesh = wallMesh;
            EditorUtility.SetDirty(src);

            var slab = new GameObject("석축덩어리");
            slab.transform.SetParent(root.transform, false);
            slab.transform.localPosition = new Vector3(SlabX - FaceX, 0f, 0f);
            slab.AddComponent<MeshFilter>().sharedMesh = slabMesh;
            slab.AddComponent<MeshRenderer>().sharedMaterial = stone;
            var sc = slab.AddComponent<BoxCollider>();
            sc.center = new Vector3(0f, (DoorTop - DoorBottom) * 0.5f, 0f);
            sc.size = new Vector3(0.22f, DoorTop - DoorBottom, DoorWidth);

            // ── ② 통로 — 관측실 암문 통로와 같은 재질·치수 ──
            //   관측실 쪽 재질 에셋(M_관측실_석벽 등)을 **그대로 참조**한다. 색을 새로 뽑지 않는
            //   이유는 단순하다 — 이 통로는 관측실 통로와 물리적으로 이어지는 같은 굴이다.
            //   UV도 관측실과 같은 UvStone(0.40) 스케일로 구워야 막돌 무늬 크기가 맞는다.
            //   (Unity 기본 큐브는 면당 UV 0~1이라 2.7m 벽에 무늬가 한 장 늘어붙는다.)
            var matStone = ObsMat("M_관측실_석벽",     stone);
            var matDark  = ObsMat("M_관측실_암문어둠", stone);
            var matIron  = ObsMat("M_관측실_흑철",     stone);
            var matFlame = ObsMat("M_관측실_불꽃",     stone);

            var pass = new GameObject("통로");
            pass.transform.SetParent(root.transform, false);

            float depth = PassInnerX - FaceX;               // 2.69 — A구간(곧은 구간) 길이
            float fy = PassFloor - DoorBottom;              // 0.02  통로 바닥
            float ceil = DoorTop - DoorBottom;              // 2.97  통로 천장 = 암문 위 끝. 유효 2.95
            float half = CorW * 0.5f;
            float branchX = depth - CorW;                   // 0.89  B구간이 갈라져 나가는 자리
            float legB = BranchZ - DoorCZ;                  // 9.90  B구간 북쪽 끝 (로컬 z)

            // 구멍은 뜯어낸 조각 모양 그대로라 가장자리가 너덜너덜하다(정삼각형 단위로 잘려서).
            // 껍데기 두께 방향으로 뚫린 자리라 **틈으로 석축 속이 비쳐 보이면 뒤가 다 보인다.**
            // → 통로 상자들을 구멍보다 넉넉히 키워 테두리를 통째로 덮는다.
            var hb = slabMesh.bounds;                       // 로컬 기준 구멍 범위 (= 정확히 1.80 × 2.97)
            float zPad = Mathf.Max(1.35f, Mathf.Max(Mathf.Abs(hb.min.z), Mathf.Abs(hb.max.z)) + 0.15f);
            // ⚠️ 천장 상자를 너무 높이 세우지 말 것. 서면 물매가 계속 뒤로 눕기 때문에
            //    y 4.0 위로 가면 어귀 평면(mouth)이 도로 석축 **앞**으로 튀어나온다
            //    (실측: z −7.2 에서 y3.9→29.921, y4.2→29.944). 통로 안에서는 천장 위가 안 보이므로
            //    0.40 두께면 충분하다.
            float yTop = ceil + 0.40f;
            // ⚠️ 통로 어귀를 x=0(문 면)에 두면 안 된다. 석축 서면은 **뒤로 누운 물매**라
            //    바닥에서 29.71, 3.5m 높이에서 29.90까지 밀려 들어간다. 어귀를 수직으로 0에
            //    세우면 위로 갈수록 통로 상자가 석축 밖으로 0.19m 튀어나와, 닫혀 있어도
            //    문 둘레에 시커먼 테두리가 둘러친다 — 비밀문 위치를 대놓고 알려 주는 꼴이다
            //    (2026-08-20 실측: 첫 빌드에서 그대로 나왔다).
            //    뜯어낸 껍질의 가장 깊은 켜(hb.max.x)보다 안쪽에서 시작하면 어디서도 안 튄다.
            float mouth = hb.max.x + 0.06f;                 // ≈ 0.25 (월드 29.96)

            var stoneParts = new List<CombineInstance>();
            var colParts   = new List<(Vector3 c, Vector3 s)>();
            // (min, max) 두 모서리로 상자 하나 — 벽 원장은 렌더·콜라이더가 1:1이다
            System.Action<float, float, float, float, float, float> slab6 = (x0, x1, y0, y1, z0, z1) =>
            {
                var c = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f);
                var s = new Vector3(x1 - x0, y1 - y0, z1 - z0);
                AddBox(stoneParts, c, s, UvStone);
                colParts.Add((c, s));
            };

            // ── A구간 (문 → 동쪽) ── 개구부를 좁히는 문설주·인방은 두지 않는다.
            //    암문 크기 그대로 뚫려 보여야 하고, 문간을 2.10으로 낮추면 CC가 못 들어온다.
            slab6(mouth, depth, fy - CorT, fy, -zPad, half);                 // 바닥
            slab6(mouth, depth, ceil, yTop, -zPad, half);                    // 천장 (구멍 윗자락까지 덮는다)
            slab6(mouth, depth, fy - CorT, yTop, -zPad, -half);              // 벽 남 (A구간 전체)
            slab6(mouth, branchX, fy - CorT, yTop, half, zPad);              // 벽 북 — 갈림목 앞까지만
                                                                             //   (구멍 북쪽 너덜 테두리 z 1.16 도 이 상자가 덮는다)

            // ── B구간 (북쪽으로 꺾어 9m) ── 불빛이 닿지 않는 안쪽은 그대로 캄캄하다
            slab6(branchX - CorT, depth, fy - CorT, fy, half, legB);         // 바닥
            slab6(branchX - CorT, depth, ceil, yTop, half, legB);            // 천장
            slab6(branchX - CorT, branchX, fy - CorT, yTop, half, legB + CorT); // 벽 서
            slab6(branchX - CorT, depth + CorT, fy - CorT, yTop, legB, legB + CorT); // 막이 (북쪽 끝)

            // 동쪽 벽 — A구간 막이와 B구간 동벽이 한 장으로 이어진다
            slab6(depth, depth + CorT, fy - CorT, yTop, -zPad, legB + CorT);

            var stoneGo = new GameObject("통로_석조");
            stoneGo.transform.SetParent(pass.transform, false);
            stoneGo.AddComponent<MeshFilter>().sharedMesh = SaveMesh(Combine(stoneParts), "교대_암문_통로석조");
            stoneGo.AddComponent<MeshRenderer>().sharedMaterial = matStone;

            var colGo = new GameObject("통로_콜라이더");
            colGo.transform.SetParent(pass.transform, false);
            foreach (var cp in colParts)
            {
                var cg = new GameObject("벽");
                cg.transform.SetParent(colGo.transform, false);
                cg.transform.localPosition = cp.c;
                cg.AddComponent<BoxCollider>().size = cp.s;
            }

            // B구간 끝의 어둠 — 관측실 암문_어둠과 같은 재질(알베도 0.012)이라 빛이 닿아도 검게 죽는다.
            // 9m 밖이라 사방등 불빛이 이미 닿지 않지만, 밤 앰비언트에도 끝이 드러나지 않게 못을 박는다.
            var dark = Box(pass.transform, "통로_끝어둠", matDark,
                           new Vector3((branchX + depth) * 0.5f, (fy + ceil) * 0.5f, legB - 0.015f),
                           new Vector3(CorW - 0.02f, ceil - fy - 0.02f, 0.03f));
            Object.DestroyImmediate(dark.GetComponent<BoxCollider>());

            // 문지방 앞치마 — 물가 지형(0.60~0.62)과 통로 바닥(0.62)을 이어 턱을 없앤다.
            // ⚠️ 여기 단차가 stepOffset(0.6)을 넘으면 문 앞에서 완전히 막힌다 (2026-08-20 실측).
            // 렌더러는 뗀다 — 지형(0.60~0.62)과 높이가 같아 보일 필요가 없는데, 텍스처 없는
            // 석축 재질이라 밝은 판때기로 튀어 문 앞에 흰 돌이 놓인 것처럼 보였다 (2026-08-20).
            // 어귀가 0.21 안쪽으로 물러났으므로 앞치마도 거기까지 이어 준다 (틈이 생기면 그 자리에서 걸린다)
            var sill = Box(pass.transform, "문지방", stone,
                           new Vector3((mouth - 0.90f) * 0.5f, fy - 0.15f, 0f),
                           new Vector3(mouth + 0.90f, 0.3f, CorW));
            Object.DestroyImmediate(sill.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(sill.GetComponent<MeshFilter>());

            // ── ②-b 조명 — 관측실 통로 사방등 그대로. 문이 열려야 켜진다 ──
            //   ⚠️ 그림자를 반드시 켠다. 관측실 통로 사방등은 그림자가 없는데(전부 실내라 새어 나갈
            //      곳이 없다) 여기서 그대로 두면 사거리 5.5m 점광이 **석축을 뚫고 나가** 강가 벽면이
            //      까닭 없이 따뜻하게 빛난다 — 비밀문 위치를 그대로 알려 주는 꼴이다.
            var lights = new GameObject("조명");
            lights.transform.SetParent(pass.transform, false);
            //   둘만 건다 — A구간 남벽(들어가면서 오른쪽)과 갈림목 지나 B구간 동벽.
            //   B구간은 9m라 두 번째 등에서 4~5m만 지나면 사거리(5.5) 밖이라 그대로 캄캄하다.
            //   ⚠️ 어귀에 바짝 붙이면 안으로 들어선 순간 등이 뒤로 넘어가 통로가 통째로 어두워진다.
            Sconce(lights.transform, new Vector3(1.50f, fy + 1.62f, -half + 0.12f),
                   Vector3.forward, matIron, matFlame);
            Sconce(lights.transform, new Vector3(depth - 0.12f, fy + 1.62f, 2.60f),
                   Vector3.left, matIron, matFlame);

            // ── ③ 통로 안 전환 트리거 ──
            //   갈림목(A구간 끝의 1.8×1.8 모퉁이) 전체를 덮는다. 문에서 1.0m만 들어서면 닿으므로
            //   "몇 걸음이면 전환"이 되고, B구간 9m는 걸어 들어갈 일이 없는 **눈요기 깊이**다.
            var trg = new GameObject("출구_관측실");
            trg.transform.SetParent(pass.transform, false);
            trg.transform.localPosition = new Vector3(branchX + CorW * 0.5f, fy + (ceil - fy) * 0.5f, 0f);
            var tb = trg.AddComponent<BoxCollider>();
            tb.isTrigger = true;
            tb.size = new Vector3(CorW - 0.2f, ceil - fy, CorW - 0.2f);
            var ex = trg.AddComponent<SceneExit>();
            ex.mode = SceneExit.Trigger.걸어서_트리거;
            ex.targetScene = "Gyeonu_Observatory";
            ex.targetSpawn = "SpawnPoint_FromEunhaDam";
            ex.displayName = "암문 통로";
            ex.requireExitFirst = false;

            // ── ④ 문 동작 — 밤 + 단서 ──
            // ⚠️ 컴포넌트를 **석축덩어리(문짝)에** 붙인다. 루트에 붙이면 조준 판정이
            //    루트 밑 **모든** 콜라이더로 번진다 — DebugInteractor(그리고 공통 인터랙션)는
            //    맞은 콜라이더에서 GetComponentInParent로 올라가며 찾기 때문에,
            //    교대 차단 박스(남_서벽_*)에 커서를 대도 "석축 — 문 열기"가 떴다
            //    (2026-08-20 실측: z −13, −11, −6, −4, −1, +3 전부 문으로 잡힘).
            //    문짝에 붙이면 문 구멍(x 29.57~29.79 / y 0.60~3.57 / z −8.85~−7.05)
            //    안에서만 잡힌다. 차단 콜라이더는 손대지 않는다.
            var door = slab.AddComponent<SecretStoneDoor>();
            door.leaf = slab.transform;
            // 통로 구조물은 늘 켜 둔다 — 구멍이 진짜로 뚫려 있어 꺼 두면 테두리 틈으로 석축 속이
            // 비친다. 대신 **조명만** 문에 물려 둔다: 닫혀 있는 동안은 굴 속이 캄캄하다.
            door.revealed = lights;
            door.pushAxis = Vector3.right;            // ① 안쪽(+X, 석축 속)으로 살짝 물러난다
            // ② 그다음 **문 앞에서 봤을 때 오른쪽**(−Z, 남)으로 미끄러진다 (2026-08-20 사용자 지정).
            //    문은 −X(강)를 보므로 보는 사람의 오른손 방향이 −Z다.
            //    ⚠️ 물러나는 깊이가 얕으면 남쪽으로 미끄러진 석판이 석축 면 밖으로 삐져나온다.
            //       실측한 남쪽(z −11.2~−8.8) 석축 앞면은 y 0.7에서 29.66, y 3.8에서 29.88 —
            //       석판 자신의 앞면도 같은 기울기라(y 0.7 29.68 → y 3.5 29.87) 0.18이면 어디서나
            //       0.18m 뒤에 숨는다. 0.18보다 줄이지 말 것.
            door.slideAxis = Vector3.back;
            // 6차부터 석판은 문 폭 그대로 1.80이다(사각형으로 잘라 냈다). 제 몸 하나만큼 + 여유.
            door.slideDistance = 1.92f;
            door.pushDepth = 0.18f;
            door.duration = 1.9f;                     // 물러남 0.6초 + 미끄러짐 1.4초 (겹침 포함)
            door.openKey = "ammun_opened";
            door.requiredFlag = GyeonuWorld.F_암문단서;
            door.displayName = "석축";
            door.lockedMessage = "물때 낀 석축이다. 돌은 차고 단단하다.";
            door.openMessage = "돌 한 덩이가 안으로 밀리더니 옆으로 미끄러진다. 어둠 속으로 통로가 이어진다.";

            // ── ⑤ 경로 힌트는 두지 않는다 (2026-08-20 방침 확정) ──
            //   징검돌·다져진 흙길·발자국 같은 **길 안내를 전부 없앴다.** 비밀문인데 가는 길이
            //   표시돼 있으면 비밀이 아니다. 플레이어는 단서 문서로 위치를 알고 찾아가야 한다.
            //   남은 힌트는 아래 ⑥ 문틈 빛 하나뿐이고, 그것도 문 앞까지 와야 보인다.

            // ── ⑤-b 바위 디딤 (콜라이더 전용, 안 보임) ──
            // 물가 회랑 z 3.6~8.5 를 대형 바위 소품 `바위_물가_4`(비볼록 MeshCollider,
            // 상단 1.30)가 **회랑 폭 전체**(x 26.5~31.2)에 걸쳐 가로막는다. 물가(0.69)와의
            // 단차가 0.61m로 stepOffset(0.60)을 아슬아슬하게 넘어 z 6.2에서 완전히 막혔다
            // (2026-08-20 실측). 소품은 건드리지 않고 보이지 않는 디딤 3장으로 넘게 한다.
            // 세 조각은 **끝점 높이를 반드시 맞춘다** — 어긋나면 이음매가 수직 벽이 된다.
            //   북램프 z 9.7~8.5 (0.68→1.36) / 상판 z 8.5~3.7 (1.36) / 남램프 z 3.7~2.5 (1.36→0.68)
            var rocks = new GameObject("바위디딤");
            rocks.transform.SetParent(root.transform, true);
            void Step(string nm, Vector3 pos, Vector3 size, float rotX)
            {
                var r = new GameObject(nm);
                r.transform.SetParent(rocks.transform, true);
                r.transform.position = pos;
                r.transform.rotation = Quaternion.Euler(rotX, 0f, 0f);
                r.AddComponent<BoxCollider>().size = size;
            }
            Step("바위디딤_북",   new Vector3(28.5f, 0.959f, 9.066f), new Vector3(1.5f, 0.14f, 1.38f),  29.5f);
            Step("바위디딤_상판", new Vector3(28.5f, 1.290f, 6.100f), new Vector3(1.5f, 0.14f, 4.80f),   0f);
            Step("바위디딤_남",   new Vector3(28.5f, 0.959f, 3.134f), new Vector3(1.5f, 0.14f, 1.38f), -29.5f);

            // 남쪽 접근로용 — `바위_물가_6`(비볼록 MeshCollider, AABB x 25.6~32.5 / 상단 1.69 /
            // z −16.67~−9.40)가 물가 회랑을 **폭 전체로** 막는다. 위에서 쏜 레이는 지형(0.63)만
            // 맞는데 캡슐은 바위 안쪽 얇은 면에 걸려, x 27.9~29.1 어디로도 z −15 을 못 넘었다
            // (2026-08-20 실측). 바위 최고점(1.69)보다 위로 띄운 디딤 3장으로 넘어간다.
            //   남램프 z −18.4~−16.4 (0.70→1.78) / 상판 z −16.4~−9.5 (1.78)
            //   북램프 z −9.5~−8.3 (1.78→0.72) — 문 앞(z −7.95)은 건드리지 않는다.
            //   ⚠️ 북램프를 문 앞까지 늘리면 문 앞에 서려는 플레이어가 램프에 밀린다.
            Step("바위디딤6_남",   new Vector3(28.5f, 1.178f, -17.367f), new Vector3(1.5f, 0.14f, 2.273f), -28.37f);
            Step("바위디딤6_상판", new Vector3(28.5f, 1.710f, -12.950f), new Vector3(1.5f, 0.14f, 6.900f),   0f);
            Step("바위디딤6_북",   new Vector3(28.5f, 1.198f,  -8.946f), new Vector3(1.5f, 0.14f, 1.601f),  41.45f);

            // ── ⑥ 문틈 빛 (서면) ──
            var seam = new GameObject("문틈빛");
            seam.transform.SetParent(root.transform, false);
            var bb = slabMesh.bounds;
            float sh = bb.size.y, sy = bb.center.y;
            float lx = SlabX - FaceX - 0.012f;
            SeamStrip(seam.transform, "틈_남", new Vector3(lx, sy, bb.min.z), new Vector3(0.01f, sh, 0.016f));
            SeamStrip(seam.transform, "틈_북", new Vector3(lx, sy, bb.max.z), new Vector3(0.01f, sh, 0.016f));
            SeamStrip(seam.transform, "틈_상", new Vector3(lx, bb.max.y, bb.center.z),
                      new Vector3(0.01f, 0.016f, bb.size.z));

            // ── ⑦ 힌트 배선 — 남은 것은 문틈 빛 하나뿐 (단서 + 밤) ──
            var hint = root.AddComponent<AmmunHintController>();
            hint.revealOnClue = new[] { seam };
            hint.Apply();

            // ── ⑧ 교대 콜라이더 + 물벽 재조정 ──
            BuildAbutmentColliders(root.transform);
            AdjustWaterWalls(root.transform);
            ClearPassageDetails();

            // ── ⑨ 위치 지정용 큐브 정리 ──
            int killed = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t == null || t.parent != null) continue;
                if (t.name != "Cube" && !t.name.StartsWith("Cube (")) continue;
                Object.DestroyImmediate(t.gameObject); killed++;
            }

            Debug.Log($"[암문] 서면 암문 완성 — 면 x{FaceX} / z {DoorCZ - DoorWidth * 0.5f}~{DoorCZ + DoorWidth * 0.5f} / " +
                      $"y {DoorBottom}~{DoorTop} / 통로 x{FaceX}~{PassInnerX}. 위치용 큐브 {killed}개 제거.");
            MarkDirty();
        }

        /// <summary>
        /// 통로 발자국 안의 지형 Detail(잔디·들꽃)을 지운다.
        ///
        /// 통로 바닥(0.62)이 지형면(0.53~0.62) 바로 위에 얹혀 있어서, 지형에 심긴 들꽃이
        /// **석조 통로 안에서 자라 올라왔다** (2026-08-20 사용자 지적 — 문이 열리기 전에는
        /// 보이지 않던 자리라 여태 몰랐다). 지우는 범위는 통로 발자국 + 문지방 한 뼘뿐이라
        /// 물가 풀밭은 그대로다.
        ///
        /// ⚠️ Detail 배열은 <c>[z, x]</c> 순서다 — <c>SetDetailLayer</c> 가 GetLength(1)을 폭,
        ///    GetLength(0)을 높이로 읽는다. 뒤집으면 엉뚱한 자리가 지워진다.
        /// ⚠️ TerrainData 에셋(우리 폴더)이 바뀐다 — 씬이 아니라 에셋 diff로 남는다.
        /// </summary>
        static void ClearPassageDetails()
        {
            var tg = GameObject.Find("은하담_지형");
            var ter = tg != null ? tg.GetComponentInChildren<Terrain>() : Object.FindFirstObjectByType<Terrain>();
            if (ter == null) { Debug.LogWarning("[암문] 지형을 찾지 못해 Detail 정리를 건너뜁니다"); return; }
            var td = ter.terrainData;
            if (td.detailPrototypes.Length == 0) return;

            // A구간 + B구간 발자국을 한 번에 덮는 사각형 (문지방 쪽으로 0.2 여유)
            float x0 = FaceX - 0.20f, x1 = PassInnerX + 0.40f;
            float z0 = DoorCZ - CorW * 0.5f - 0.55f, z1 = BranchZ + 0.55f;

            var org = ter.transform.position;
            var size = td.size;
            int dw = td.detailWidth, dh = td.detailHeight;
            int xB = Mathf.Clamp(Mathf.FloorToInt((x0 - org.x) / size.x * dw), 0, dw - 1);
            int zB = Mathf.Clamp(Mathf.FloorToInt((z0 - org.z) / size.z * dh), 0, dh - 1);
            int xE = Mathf.Clamp(Mathf.CeilToInt((x1 - org.x) / size.x * dw), 0, dw);
            int zE = Mathf.Clamp(Mathf.CeilToInt((z1 - org.z) / size.z * dh), 0, dh);
            int w = xE - xB, h = zE - zB;
            if (w <= 0 || h <= 0) return;

            Undo.RegisterCompleteObjectUndo(td, "암문 통로 Detail 정리");
            var zero = new int[h, w];
            int wiped = 0;
            for (int layer = 0; layer < td.detailPrototypes.Length; layer++)
            {
                var cur = td.GetDetailLayer(xB, zB, w, h, layer);
                int sum = 0;
                foreach (var v in cur) sum += v;
                if (sum == 0) continue;          // 이미 비었으면 건드리지 않는다 (멱등)
                td.SetDetailLayer(xB, zB, layer, zero);
                wiped += sum;
            }
            if (wiped > 0) EditorUtility.SetDirty(td);
            Debug.Log($"[암문] 통로 Detail 정리 — x {x0:F2}~{x1:F2} / z {z0:F2}~{z1:F2} " +
                      $"(셀 {w}×{h}), 밀도 합 {wiped} 제거");
        }

        /// <summary>지형 표면 높이 (교대·암문 콜라이더와 무관하게 지형만).</summary>
        static float GroundY(float x, float z)
        {
            var tg = GameObject.Find("은하담_지형");
            var ter = tg != null ? tg.GetComponentInChildren<Terrain>() : Object.FindFirstObjectByType<Terrain>();
            if (ter == null) return 0.6f;
            return ter.SampleHeight(new Vector3(x, 0f, z)) + ter.transform.position.y;
        }

        // ══════════════════════════════════════════════════
        //  교대 콜라이더 — 원본 FBX 정점 실측 기반 박스 세트
        // ══════════════════════════════════════════════════

        /// <summary>
        /// 교대_남 서면을 계단식 박스로 통째로 막고, 문 구간만 위·아래로 나눠 구멍을 남긴다.
        /// 교대_북은 앞쪽·하단 전부 차단.
        /// </summary>
        static void BuildAbutmentColliders(Transform ammunRoot)
        {
            var grp = new GameObject("교대_콜라이더");
            grp.transform.SetParent(ammunRoot, true);
            grp.transform.position = Vector3.zero;

            // B구간(북으로 꺾인 통로)이 지나는 z 구간에서는 **통로 단면만큼 벽을 비운다.**
            // 안 비우면 모퉁이를 돌자마자 보이지 않는 벽에 막힌다 (차단 박스는 x 최서면부터
            // 33.60까지 통째로 채우기 때문). 서쪽 토막이 그대로 남으므로 바깥에서 들어올 길은 없다.
            float tz0 = DoorCZ + CorW * 0.5f;     // −7.05  B구간 남쪽 끝
            float tz1 = BranchZ;                  //  +1.95 B구간 북쪽 끝
            float tx0 = PassInnerX - CorW;        //  30.60 통로 서벽 안쪽
            float tx1 = PassInnerX;               //  32.40 통로 동벽 안쪽

            int n = 0;
            foreach (var w in WestWall)
            {
                float o0 = Mathf.Max(w.z0, tz0), o1 = Mathf.Min(w.z1, tz1);
                if (o1 - o0 < 0.05f)   // 통로와 안 겹치는 띠는 통째로
                {
                    ColBox(grp.transform, $"남_서벽_{w.z0:F0}",
                           new Vector3(w.x0, WallBotY, w.z0), new Vector3(WallEastX, WallTopY, w.z1));
                    n++;
                    continue;
                }
                if (o0 - w.z0 > 0.05f)
                { ColBox(grp.transform, $"남_서벽_{w.z0:F0}", new Vector3(w.x0, WallBotY, w.z0), new Vector3(WallEastX, WallTopY, o0)); n++; }
                if (w.z1 - o1 > 0.05f)
                { ColBox(grp.transform, $"남_서벽_{w.z0:F0}b", new Vector3(w.x0, WallBotY, o1), new Vector3(WallEastX, WallTopY, w.z1)); n++; }
                // 겹치는 구간 — 통로 단면(x 30.60~32.40, y 0.60~3.57)만 빼고 네 토막
                ColBox(grp.transform, $"남_통로서_{w.z0:F0}", new Vector3(w.x0, WallBotY, o0), new Vector3(tx0, WallTopY, o1));
                ColBox(grp.transform, $"남_통로동_{w.z0:F0}", new Vector3(tx1, WallBotY, o0), new Vector3(WallEastX, WallTopY, o1));
                ColBox(grp.transform, $"남_통로하_{w.z0:F0}", new Vector3(tx0, WallBotY, o0), new Vector3(tx1, DoorBottom, o1));
                ColBox(grp.transform, $"남_통로상_{w.z0:F0}", new Vector3(tx0, DoorTop, o0), new Vector3(tx1, WallTopY, o1));
                n += 4;
            }
            // 문 구간 — 통로 높이(0.60~3.57)만 비우고 위·아래는 막는다
            ColBox(grp.transform, "남_문하부",
                   new Vector3(29.58f, WallBotY, DoorBandZ0), new Vector3(WallEastX, DoorBottom, DoorBandZ1));
            ColBox(grp.transform, "남_문상부",
                   new Vector3(29.58f, DoorTop, DoorBandZ0), new Vector3(WallEastX, WallTopY, DoorBandZ1));
            n += 2;

            // 남·북 끝단 마구리 — 석축 끝에서 옆으로 파고들지 못하게
            ColBox(grp.transform, "남_마구리_남",
                   new Vector3(29.15f, WallBotY, -34.4f), new Vector3(WallEastX, WallTopY, -34.0f));
            ColBox(grp.transform, "남_마구리_북",
                   new Vector3(31.25f, WallBotY, 13.5f), new Vector3(WallEastX, WallTopY, 13.9f));
            n += 2;

            // ── 교대_북 — 통째로 차단 (AABB 실측 x −43.4~−28.9, y 0.57~8.79, z −30.4~+15.1) ──
            // 상단 8.55 — 다리 마루(8.80) 아래라 마루 보행은 막지 않는다.
            ColBox(grp.transform, "북_전체",   new Vector3(-45f, -1.5f, -31f), new Vector3(-27.5f, 8.55f, 16f));
            ColBox(grp.transform, "북_어귀북", new Vector3(-36f, 0f, 16f),     new Vector3(-27.5f, 7.5f, 21f));
            ColBox(grp.transform, "북_어귀남", new Vector3(-36f, 0f, -36f),    new Vector3(-27.5f, 7.5f, -31f));
            n += 3;

            Debug.Log($"[암문] 교대 콜라이더 {n}개 (박스만) — 남 서벽 계단 {WestWall.Length} + 문 상·하 + 마구리 2 / 북 3");
        }

        /// <summary>min~max 두 모서리로 박스 콜라이더 하나 (렌더러 없음).</summary>
        static void ColBox(Transform parent, string name, Vector3 a, Vector3 b)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            var min = Vector3.Min(a, b); var max = Vector3.Max(a, b);
            go.transform.position = (min + max) * 0.5f;
            var bc = go.AddComponent<BoxCollider>();
            bc.size = max - min;
        }

        // ══════════════════════════════════════════════════
        //  물벽 재조정 — 북쪽 진입로 한 곳만 연다 (2026-08-20 3차)
        // ══════════════════════════════════════════════════

        /// <summary>
        /// 동안 물 진입 차단벽(x 30.20~30.80 라인)에서 **암문으로 가는 두 곳만** 연다.
        ///   ① 북쪽 하강 진입 z 12.4~17.6 — 둑에서 물가로 내려서는 유일한 입구
        ///   ② 문 구간 z −8.90~−7.00 — 이 라인이 문 **바로 안쪽**을 지나므로, 뚫지 않으면
        ///      문을 열고 0.08m 앞에서 막힌다 (2026-08-20 실측: CapsuleCast d=0.08 n=(−1,0,0))
        /// 나머지는 전부 원래대로. 서·북 보조벽으로 깊은 물을 막아 **얕은 물가만** 지나간다.
        ///
        /// ■ 왜 원본 벽을 자르지 않고 끄고 다시 세우는가 (2026-08-20)
        ///   BoxCollider의 center 오프셋을 무시하고 트랜스폼만 옮겼더니 벽이 2.6m 어긋난 자리에
        ///   놓였고, 그 상태가 씬에 저장돼 다음 실행 때 기준값이 오염됐다. 이제는
        ///   ① 라인 전체를 원래 격자(z −36.4부터 8m 간격, 길이 8.8)로 **되돌린 뒤**
        ///   ② 개구에 걸치는 벽만 비활성화하고 남는 토막을 암문 루트 밑에 새로 세운다.
        ///   원본은 크기·위치가 그대로라 몇 번을 돌려도 같은 결과가 나온다.
        /// ⚠️ EunhaDamWalkSetup을 다시 돌리면 물벽이 원복된다 — 그때 이 메뉴를 한 번 더.
        /// </summary>
        static void AdjustWaterWalls(Transform ammunRoot)
        {
            var walkRoot = GameObject.Find("은하담_보행콜라이더");
            if (walkRoot == null) { Debug.LogWarning("[암문] 은하담_보행콜라이더 없음 — 물벽 조정 건너뜀"); return; }

            // ── ① 라인을 원래 격자로 되돌린다 ──
            //    벽 i : z (−36.4 + 8i) ~ (−27.6 + 8i),  i = 0..8
            var line = new List<(Transform t, BoxCollider bc)>();
            foreach (Transform c in walkRoot.transform)
            {
                if (!c.name.Contains("물벽")) continue;
                var bc = c.GetComponent<BoxCollider>();
                if (bc == null) continue;
                var b = bc.bounds;
                if (b.min.x < 29.9f || b.max.x > 31.1f) continue;   // 동안 x≈30.5 라인만
                line.Add((c, bc));
            }
            line.Sort((p, q) => p.bc.bounds.center.z.CompareTo(q.bc.bounds.center.z));

            int snapped = 0;
            var haveSlot = new HashSet<int>();
            // 각 벽이 어느 칸인지 기억해 둔다 — 아래 개구 계산에서 bounds를 다시 읽으면
            // 방금 바꾼 크기·위치가 반영되지 않은 값이 나와 토막이 엉뚱하게 잘린다
            // (2026-08-20 실측: 17.6~20.4 가 17.6~23.4 로 나옴).
            var slotZ = new List<(float z0, float z1)>();
            foreach (var w in line)
            {
                float cz = w.bc.bounds.center.z;
                int i = Mathf.Clamp(Mathf.RoundToInt((cz + 32.0f) / 8.0f), 0, 8);
                haveSlot.Add(i);
                float z0 = -36.4f + 8f * i, z1 = z0 + 8.8f;
                slotZ.Add((z0, z1));
                if (Mathf.Abs(w.bc.bounds.min.z - z0) > 0.05f || Mathf.Abs(w.bc.bounds.max.z - z1) > 0.05f)
                { Undo.RecordObject(w.t, "물벽 격자 복원"); SetWallZ(w.t, w.bc, z0, z1); snapped++; }
                w.t.gameObject.SetActive(true);
            }
            // ── ② 개구 세 곳 ──
            //   북쪽 5.2m / 남쪽 5.2m (같은 폭) / 문 구간
            var gaps = new (float z0, float z1)[] {
                (GapNorthZ0, GapNorthZ1),
                (GapSouthZ0, GapSouthZ1),
                (DoorBandZ0, DoorBandZ1),
            };

            // 남는 토막만 돌려주는 자름틀 — 벽 하나가 여러 개구에 걸릴 수 있다
            System.Func<float, float, List<(float, float)>> cut = (lo, hi) =>
            {
                var segs = new List<(float, float)> { (lo, hi) };
                foreach (var g in gaps)
                {
                    var next = new List<(float, float)>();
                    foreach (var s in segs)
                    {
                        if (g.z1 <= s.Item1 || g.z0 >= s.Item2) { next.Add(s); continue; }
                        if (s.Item1 < g.z0) next.Add((s.Item1, g.z0));
                        if (g.z1 < s.Item2) next.Add((g.z1, s.Item2));
                    }
                    segs = next;
                }
                return segs;
            };

            int stubs = 0;
            System.Action<string, float, float, Vector3, Vector3> place =
                (nm, z0, z1, center, size) =>
            {
                foreach (var s in cut(z0, z1))
                {
                    if (s.Item2 - s.Item1 < 0.1f) continue;
                    var piece = new GameObject(nm + "_" + s.Item1.ToString("F1"));
                    piece.transform.SetParent(ammunRoot, true);
                    piece.transform.position = new Vector3(center.x, center.y, (s.Item1 + s.Item2) * 0.5f);
                    piece.AddComponent<BoxCollider>().size = new Vector3(size.x, size.y, s.Item2 - s.Item1);
                    stubs++;
                }
            };

            // 지난 작업에서 아예 지워 버린 자리(남쪽 z −36.4~−27.6)는 같은 규격으로 메운다.
            int filled = 0;
            for (int i = 0; i <= 8; i++)
            {
                if (haveSlot.Contains(i)) continue;
                float z0 = -36.4f + 8f * i;
                place("물벽_메움", z0, z0 + 8.8f, new Vector3(30.5f, 1.5f, 0f), new Vector3(0.6f, 2.6f, 0f));
                filled++;
            }

            int opened = 0;
            for (int wi = 0; wi < line.Count; wi++)
            {
                var w = line[wi];
                var b = w.bc.bounds;
                float lo = slotZ[wi].z0, hi = slotZ[wi].z1;
                var segs = cut(lo, hi);
                if (segs.Count == 1 && segs[0].Item1 == lo && segs[0].Item2 == hi) continue;   // 안 걸린다

                w.t.gameObject.SetActive(false);        // 원본은 크기 그대로 두고 끈다
                opened++;
                place("물벽_잔여", lo, hi, b.center, b.size);
            }

            // 남쪽 끝 벽(x 28.70~29.30, z −44.4~−35.6)도 물가 띠를 가로지른다 —
            // 남쪽 개구에 걸치는 만큼 똑같이 잘라 준다. 안 그러면 남쪽으로 내려서도
            // 물가 띠를 따라 북상할 수가 없다 (2026-08-20 실측).
            foreach (Transform c in walkRoot.transform)
            {
                if (!c.name.Contains("물벽")) continue;
                var bc = c.GetComponent<BoxCollider>();
                if (bc == null || !c.gameObject.activeSelf) continue;
                var b = bc.bounds;
                if (b.min.x < 28.4f || b.max.x > 29.6f) continue;      // 남쪽 끝 라인만
                var segs = cut(b.min.z, b.max.z);
                if (segs.Count == 1 && Mathf.Abs(segs[0].Item1 - b.min.z) < 0.01f
                    && Mathf.Abs(segs[0].Item2 - b.max.z) < 0.01f) continue;
                c.gameObject.SetActive(false);
                opened++;
                place("물벽_남끝잔여", b.min.z, b.max.z, b.center, b.size);
            }

            // 서쪽 보조벽 — 물가 띠 서쪽의 깊은 물 차단 (강 한복판으로 못 나간다)
            var west = new GameObject("물벽_암문접근_서");
            west.transform.SetParent(ammunRoot, true);
            west.transform.position = new Vector3(27.35f, 0.75f, -0.25f);
            west.AddComponent<BoxCollider>().size = new Vector3(0.4f, 4.5f, 54.5f);  // z −27.5 ~ +27

            // 여울 서쪽 벽 — 하강 지점(z 13.6~20.6)은 물가 띠가 아직 물에 잠긴 구간이라
            // 서쪽으로 더 나가면 무릎을 넘어 허벅지까지 잠긴다 (2026-08-20 실측: x 27.9에서
            // 바닥 −0.11 = 깊이 0.61m). 얕은 여울(깊이 0.2~0.35m)만 건너도록 x 29.6에서 막는다.
            // ⚠️ z 하단을 13.6보다 남쪽으로 내리면 물가 띠로 내려서는 길목 자체가 막힌다.
            var shoal = new GameObject("물벽_여울서");
            shoal.transform.SetParent(ammunRoot, true);
            shoal.transform.position = new Vector3(29.6f, 0.75f, 17.1f);
            shoal.AddComponent<BoxCollider>().size = new Vector3(0.4f, 4.5f, 7.0f);   // z 13.6~20.6

            // 남쪽 여울 서쪽 벽 — 남쪽 물가(z −40~−27.5)도 아직 물에 잠긴 구간이라
            // 서쪽으로 나가면 바닥이 −0.45까지 떨어진다(깊이 0.95m). x 27.9에서 막아
            // 걸어갈 수 있는 폭은 x 28.2~28.85, 깊이 0.41~0.60m — 무릎~허리 정도다.
            // (석축 서면이 z −34~−30 구간에서 x 29.15까지 나와 있어 더 동쪽으로는 못 붙는다)
            var shoalS = new GameObject("물벽_여울서_남");
            shoalS.transform.SetParent(ammunRoot, true);
            shoalS.transform.position = new Vector3(27.9f, 0.75f, -33.75f);
            shoalS.AddComponent<BoxCollider>().size = new Vector3(0.4f, 4.5f, 12.5f);  // z −40 ~ −27.5

            // 남쪽 마구리 — 하강 지점 남쪽으로 더 못 가게 (씬 밖·깊은 물 차단)
            var southCap = new GameObject("물벽_암문접근_남");
            southCap.transform.SetParent(ammunRoot, true);
            southCap.transform.position = new Vector3(29.2f, 1.0f, -40.2f);
            southCap.AddComponent<BoxCollider>().size = new Vector3(3.0f, 4f, 0.4f);   // x 27.7~30.7

            // 북쪽 마구리 — 하강 지점 북쪽으로 더 못 가게
            var north = new GameObject("물벽_암문접근_북");
            north.transform.SetParent(ammunRoot, true);
            north.transform.position = new Vector3(28.9f, 1.0f, 20.6f);
            north.AddComponent<BoxCollider>().size = new Vector3(3.5f, 4f, 0.4f);   // x 27.15~30.65

            Debug.Log($"[암문] 물벽 — 라인 {line.Count}장 / 격자 복원 {snapped} / 메움 {filled} / " +
                      $"개구 3곳(북 {GapNorthZ0}~{GapNorthZ1}, 남 {GapSouthZ0}~{GapSouthZ1}, 문)에 걸린 벽 {opened}장 끔 / " +
                      $"잔여 토막 {stubs} / 보조벽 서·여울북·여울남·남끝·북끝 5");
        }

        /// <summary>
        /// 벽의 z 범위를 world 기준 z0~z1 로 맞춘다.
        /// ⚠️ BoxCollider.center 의 z 오프셋을 반드시 보정해야 한다 — 무시하면 벽이
        ///    center.z 만큼(실측 2.6m) 밀린 자리에 놓인다 (2026-08-20).
        /// </summary>
        static void SetWallZ(Transform wall, BoxCollider bc, float z0, float z1)
        {
            var ls = wall.lossyScale;
            var s = bc.size;
            bc.size = new Vector3(s.x, s.y, (z1 - z0) / Mathf.Max(0.0001f, ls.z));
            var p = wall.position;
            wall.position = new Vector3(p.x, p.y, (z0 + z1) * 0.5f - bc.center.z * ls.z);
            EditorUtility.SetDirty(wall);
            EditorUtility.SetDirty(bc);
        }

        static void SeamStrip(Transform parent, string name, Vector3 localPos, Vector3 size)
        {
            var s = Box(parent, name, SeamMat(), localPos, size);
            Object.DestroyImmediate(s.GetComponent<BoxCollider>());
        }

        // ══════════════════════════════════════════════════

        /// <summary>
        /// 원본 메시에서 문 사각형 안 삼각형을 골라 두 장을 굽는다.
        ///   ① <paramref name="slabMesh"/> — 그 삼각형만 모은 **문짝** (암문 루트 기준 로컬)
        ///   ② <paramref name="wallMesh"/> — 그 삼각형을 **빼낸 석축** (원본 오브젝트 기준 로컬)
        /// 둘은 정확히 서로의 여집합이라 닫혀 있으면 원래 석축과 한 치도 다르지 않고,
        /// 열리면 **진짜 구멍**이 남는다 — 그래야 뒤로 물러난 석판이 보인다.
        ///
        /// 서면이 배부른 경사면(하단 29.712 → 상단 29.822)이라 바깥(−X)을 향한 삼각형을 추린 뒤
        /// 최외곽부터 BandHalf 두께만 남긴다 — 더 깊은 켜까지 뜯으면 석축이 속까지 뚫린다.
        ///
        /// ⚠️ **반드시 원본 FBX에서 다시 뜬다.** 씬 인스턴스는 지난 실행에서 이미 구멍 난 사본을
        ///    물고 있을 수 있고(그러면 오려 낼 삼각형이 하나도 없다), 스태틱 배칭 중이면
        ///    Combined Mesh 일 수도 있다. 원본은 **읽기만** 하므로 FBX·임포트 설정은 그대로다.
        /// ⚠️ 구멍 난 사본은 우리 폴더(MeshDir)에 굽고 **씬 인스턴스의 MeshFilter에만** 물린다.
        ///    gitignore된 원본 폴더는 건드리지 않으므로 팀원에게는 씬 diff로만 전달된다.
        /// </summary>
        static bool BuildDoorMeshes(MeshFilter src, Vector3 pivot, out Mesh slabMesh, out Mesh wallMesh)
        {
            slabMesh = null; wallMesh = null;

            var m = OriginalAbutmentMesh(src);
            if (m == null) { Debug.LogError("[암문] 원본 FBX 메시 로드 실패"); return false; }
            Matrix4x4 mtx = src.transform.localToWorldMatrix;
            Matrix4x4 inv = src.transform.worldToLocalMatrix;

            var v = m.vertices;
            var nrms = m.normals;
            bool hasN = nrms != null && nrms.Length == v.Length;
            var uvs = m.uv;
            bool hasUV = uvs != null && uvs.Length == v.Length;
            var tris = m.triangles;

            float z0 = DoorCZ - DoorWidth * 0.5f, z1 = DoorCZ + DoorWidth * 0.5f;

            // ── 1차: 문 사각형에 **걸치는** 바깥(−X)향 삼각형 후보 + 최외곽 x ──
            //    걸치기만 해도 후보다 — 아래에서 사각형 경계로 잘라 낼 것이므로.
            var cand = new HashSet<int>();
            float outerX = 999f;
            for (int t = 0; t < tris.Length; t += 3)
            {
                var a = mtx.MultiplyPoint3x4(v[tris[t]]);
                var b = mtx.MultiplyPoint3x4(v[tris[t + 1]]);
                var c = mtx.MultiplyPoint3x4(v[tris[t + 2]]);
                if (Mathf.Min(a.z, Mathf.Min(b.z, c.z)) > z1 || Mathf.Max(a.z, Mathf.Max(b.z, c.z)) < z0) continue;
                if (Mathf.Min(a.y, Mathf.Min(b.y, c.y)) > DoorTop || Mathf.Max(a.y, Mathf.Max(b.y, c.y)) < DoorBottom) continue;
                var nr = Vector3.Cross(b - a, c - a).normalized;
                if (Vector3.Dot(nr, Vector3.left) < 0.30f) continue;
                cand.Add(t);
                var g = (a + b + c) / 3f;
                if (g.z >= z0 && g.z <= z1 && g.y >= DoorBottom && g.y <= DoorTop && g.x < outerX) outerX = g.x;
            }
            if (cand.Count == 0 || outerX > 900f) return false;

            // 최외곽 켜(BandHalf) 밖은 도로 물린다 — 더 깊은 켜까지 뜯으면 석축이 속까지 뚫린다
            var drop = new List<int>();
            foreach (var t in cand)
            {
                var g = (mtx.MultiplyPoint3x4(v[tris[t]]) + mtx.MultiplyPoint3x4(v[tris[t + 1]])
                       + mtx.MultiplyPoint3x4(v[tris[t + 2]])) / 3f;
                if (g.x > outerX + BandHalf) drop.Add(t);
            }
            foreach (var t in drop) cand.Remove(t);
            if (cand.Count == 0) return false;

            // ── 2차: 후보를 문 사각형 **네 면으로 잘라** 안쪽은 문짝, 바깥 자투리는 석축에 되돌린다 ──
            //    센트로이드로 삼각형을 통째로 떼면 테두리가 너덜너덜해진다. 5차가 그랬고,
            //    구멍이 y 3.757 / z −6.786 까지 삐져나가 문 위·옆에 검은 자투리가 남았다
            //    (2026-08-20 6차 실측). 잘라 내면 구멍이 정확히 1.80 × 2.97 직사각형이 되어
            //    "암문 크기 그대로 뚫린" 그림이 나오고, 통로 단면과도 딱 맞아떨어진다.
            var planes = new System.Func<Vector3, float>[]
            {
                p => p.z - z0, p => z1 - p.z, p => p.y - DoorBottom, p => DoorTop - p.y
            };

            var wv = new List<Vector3>(v);
            var wn = new List<Vector3>(hasN ? nrms : new Vector3[v.Length]);
            var wuv = new List<Vector2>(hasUV ? uvs : new Vector2[v.Length]);

            var sv = new List<Vector3>(); var sn = new List<Vector3>(); var suv = new List<Vector2>();
            var stri = new List<int>();
            int cutTri = 0, keptFrag = 0;

            var subTris = new List<List<int>>();
            int offset = 0;
            for (int s = 0; s < m.subMeshCount; s++)
            {
                var sub = m.GetTriangles(s);
                var keep = new List<int>(sub.Length);
                for (int i = 0; i < sub.Length; i += 3)
                {
                    int t = offset + i;
                    if (!cand.Contains(t))
                    { keep.Add(sub[i]); keep.Add(sub[i + 1]); keep.Add(sub[i + 2]); continue; }

                    cutTri++;
                    var tri = new Vtx[3];
                    for (int k = 0; k < 3; k++)
                    {
                        int vi = sub[i + k];
                        tri[k] = new Vtx {
                            p = mtx.MultiplyPoint3x4(v[vi]),
                            n = hasN ? mtx.MultiplyVector(nrms[vi]).normalized : Vector3.left,
                            uv = hasUV ? uvs[vi] : Vector2.zero };
                    }
                    var cur = new List<Vtx[]> { tri };
                    var frag = new List<Vtx[]>();
                    foreach (var pl in planes)
                    {
                        var next = new List<Vtx[]>();
                        foreach (var tt in cur) SplitTri(tt, pl, next, frag);
                        cur = next;
                    }
                    foreach (var f in cur)                       // 안쪽 = 문짝
                        for (int k = 0; k < 3; k++)
                        { stri.Add(sv.Count); sv.Add(f[k].p - pivot); sn.Add(f[k].n); suv.Add(f[k].uv); }
                    foreach (var f in frag)                      // 바깥 자투리 = 석축에 남는다
                    {
                        keptFrag++;
                        for (int k = 0; k < 3; k++)
                        {
                            keep.Add(wv.Count);
                            wv.Add(inv.MultiplyPoint3x4(f[k].p));
                            wn.Add(inv.MultiplyVector(f[k].n).normalized);
                            wuv.Add(f[k].uv);
                        }
                    }
                }
                subTris.Add(keep);
                offset += sub.Length;
            }
            if (stri.Count == 0) return false;

            var sm = new Mesh { name = "교대_암문_석축덩어리" };
            sm.SetVertices(sv); sm.SetNormals(sn); sm.SetUVs(0, suv);
            sm.SetTriangles(stri, 0);
            sm.RecalculateBounds();
            slabMesh = SaveMesh(sm, "교대_암문_석축덩어리");

            var wall = new Mesh { name = "교대_남_M002_암문구멍", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            wall.SetVertices(wv); wall.SetNormals(wn); wall.SetUVs(0, wuv);
            wall.subMeshCount = subTris.Count;
            for (int s = 0; s < subTris.Count; s++) wall.SetTriangles(subTris[s], s);
            wall.RecalculateBounds();
            wallMesh = SaveMesh(wall, "교대_남_M002_암문구멍");

            AssetDatabase.SaveAssets();
            Debug.Log("[암문] 오려 냄 — 최외곽 x " + outerX.ToString("F3") + ", 걸친 삼각 " + cutTri +
                      "개를 잘라 문짝 " + (stri.Count / 3) + "삼각 / 석축에 되돌린 자투리 " + keptFrag + "삼각. " +
                      "구멍 크기 " + slabMesh.bounds.size.ToString("F3") +
                      " (문 규격 " + DoorWidth + " × " + (DoorTop - DoorBottom) + ")");
            return true;
        }

        /// <summary>자르는 동안 들고 다니는 정점 — 위치·법선·UV를 함께 보간해야 이음매가 안 보인다.</summary>
        struct Vtx { public Vector3 p, n; public Vector2 uv; }

        static Vtx VLerp(Vtx a, Vtx b, float t) => new Vtx {
            p = Vector3.Lerp(a.p, b.p, t),
            n = Vector3.Slerp(a.n, b.n, t).normalized,
            uv = Vector2.Lerp(a.uv, b.uv, t) };

        /// <summary>
        /// 삼각형 하나를 평면(<paramref name="f"/> ≥ 0 이 안쪽)으로 가른다.
        /// 한쪽에 정점 하나만 남으면 삼각형 1 + 사각형(삼각형 2장)으로 갈라지며 감기 방향은 유지된다.
        /// </summary>
        static void SplitTri(Vtx[] t, System.Func<Vector3, float> f, List<Vtx[]> inList, List<Vtx[]> outList)
        {
            float d0 = f(t[0].p), d1 = f(t[1].p), d2 = f(t[2].p);
            int pos = (d0 >= 0 ? 1 : 0) + (d1 >= 0 ? 1 : 0) + (d2 >= 0 ? 1 : 0);
            if (pos == 3) { inList.Add(t); return; }
            if (pos == 0) { outList.Add(t); return; }

            var d = new[] { d0, d1, d2 };
            int lone = 0;
            for (int i = 0; i < 3; i++)
                if ((pos == 1) == (d[i] >= 0)) { lone = i; break; }
            int i1 = (lone + 1) % 3, i2 = (lone + 2) % 3;
            Vtx A = t[lone], B = t[i1], C = t[i2];
            var AB = VLerp(A, B, d[lone] / (d[lone] - d[i1]));
            var AC = VLerp(A, C, d[lone] / (d[lone] - d[i2]));
            var aSide = new[] { A, AB, AC };
            var q1 = new[] { AB, B, C };
            var q2 = new[] { AB, C, AC };
            if (pos == 1) { inList.Add(aSide); outList.Add(q1); outList.Add(q2); }
            else { outList.Add(aSide); inList.Add(q1); inList.Add(q2); }
        }

        /// <summary>원본 FBX의 교대 서편 조각(M002). 씬 인스턴스는 이미 구멍 난 사본일 수 있으므로 쓰지 않는다.</summary>
        static Mesh OriginalAbutmentMesh(MeshFilter src)
        {
            const string fbx = "Assets/woljeonggyo/south-side-abutment-in-woljeonggyo-bridge/source/SM_24ET0014_월정교 남측교대.fbx";
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(fbx))
                if (obj is Mesh mm && mm.name == "SM_24ET0014_M002") return mm;
            // 팩이 아직 임포트되지 않았다면 씬 것으로라도 시도 (구멍이 이미 났다면 아래에서 걸린다)
            var cur = src.sharedMesh;
            return (cur != null && !cur.name.Contains("Combined")) ? cur : null;
        }

        static MeshFilter FindAbutmentPiece(string name)
        {
            var g = GameObject.Find("은하담_다리/교대_남");
            if (g == null) return null;
            foreach (var mf in g.GetComponentsInChildren<MeshFilter>())
                if (mf.name == name) return mf;
            return null;
        }

        // ══════════════════════════════════════════════════

        static GameObject Box(Transform parent, string name, Material mat, Vector3 localPos, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static Material SeamMat() => SolidMat("M_암문_문틈빛", new Color(0.9f, 0.75f, 0.45f), 0.0f,
                                              new Color(1.6f, 1.1f, 0.45f));

        /// <summary>관측실 통로가 쓰는 재질을 **그대로** 가져온다 (복제하지 않는다) —
        /// 두 통로는 같은 굴이므로 관측실 색감을 손보면 이쪽도 함께 따라와야 한다.
        /// 관측실을 아직 한 번도 짓지 않아 에셋이 없으면 원본 석축 재질로 물러선다.</summary>
        static Material ObsMat(string name, Material fallback)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(ObsMatDir + "/" + name + ".mat");
            if (m == null) Debug.LogWarning($"[암문] 관측실 재질 없음: {name} — 관측실을 한 번 짓고 다시 실행하세요");
            return m != null ? m : fallback;
        }

        // ══════════════════════════════════════════════════
        //  메시 유틸 — ObservatoryBuilder와 같은 방식 (UV 스케일을 정점에 굽는다)
        // ══════════════════════════════════════════════════

        static readonly List<Mesh> tempMeshes = new List<Mesh>();

        /// <summary>월드(여기서는 루트 로컬) 크기에 비례한 UV를 구운 상자 하나.
        /// ⚠️ Unity 기본 큐브는 면당 UV가 0~1이라 2.7m 벽에 막돌 무늬가 한 장 늘어붙는다.
        ///    관측실과 무늬 크기를 맞추려면 같은 UvStone 스케일로 구워야 한다.</summary>
        static void AddBox(List<CombineInstance> list, Vector3 center, Vector3 size, float uvScale)
        {
            var off = new Vector2(Mathf.Abs(center.x * 0.173f + center.z * 0.331f) % 1f,
                                  Mathf.Abs(center.y * 0.257f + center.z * 0.119f) % 1f);
            var mesh = BoxMesh(size, uvScale, off);
            tempMeshes.Add(mesh);
            list.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(center, Quaternion.identity, Vector3.one) });
        }

        static Mesh BoxMesh(Vector3 s, float uv, Vector2 off)
        {
            var h = s * 0.5f;
            var v = new List<Vector3>(24); var u = new List<Vector2>(24); var tr = new List<int>(36);
            System.Action<Vector3, Vector3, Vector3, Vector3, float, float> quad = (a, b, c, d, uw, vh) =>
            {
                int i0 = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                u.Add(off); u.Add(off + new Vector2(uw * uv, 0)); u.Add(off + new Vector2(uw * uv, vh * uv)); u.Add(off + new Vector2(0, vh * uv));
                tr.Add(i0); tr.Add(i0 + 1); tr.Add(i0 + 2); tr.Add(i0); tr.Add(i0 + 2); tr.Add(i0 + 3);
            };
            quad(new Vector3(-h.x, -h.y, h.z), new Vector3(h.x, -h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(-h.x, h.y, h.z), s.x, s.y);
            quad(new Vector3(h.x, -h.y, -h.z), new Vector3(-h.x, -h.y, -h.z), new Vector3(-h.x, h.y, -h.z), new Vector3(h.x, h.y, -h.z), s.x, s.y);
            quad(new Vector3(h.x, -h.y, h.z), new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, h.y, -h.z), new Vector3(h.x, h.y, h.z), s.z, s.y);
            quad(new Vector3(-h.x, -h.y, -h.z), new Vector3(-h.x, -h.y, h.z), new Vector3(-h.x, h.y, h.z), new Vector3(-h.x, h.y, -h.z), s.z, s.y);
            quad(new Vector3(-h.x, h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(h.x, h.y, -h.z), new Vector3(-h.x, h.y, -h.z), s.x, s.z);
            quad(new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, -h.y, h.z), new Vector3(-h.x, -h.y, h.z), s.x, s.z);
            var mm = new Mesh();
            mm.SetVertices(v); mm.SetUVs(0, u); mm.SetTriangles(tr, 0);
            return mm;
        }

        static Mesh Combine(List<CombineInstance> list)
        {
            var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.CombineMeshes(list.ToArray(), true, true);
            m.RecalculateNormals();
            m.RecalculateTangents();
            m.RecalculateBounds();
            foreach (var t in tempMeshes) if (t != null) Object.DestroyImmediate(t);
            tempMeshes.Clear();
            return m;
        }

        /// <summary>메시를 우리 폴더에 굽는다. 이미 있으면 **덮어쓴다** — 지웠다 새로 만들면
        /// 씬이 물고 있던 참조가 끊긴다 (구멍 난 석축 메시가 바로 그 경우다).</summary>
        static Mesh SaveMesh(Mesh built, string name)
        {
            string path = MeshDir + "/" + name + ".asset";
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
            existing.tangents = built.tangents;
            existing.uv = built.uv;
            existing.uv2 = built.uv2;
            existing.subMeshCount = built.subMeshCount;
            for (int s = 0; s < built.subMeshCount; s++) existing.SetTriangles(built.GetTriangles(s), s);
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(built);
            return existing;
        }

        // ══════════════════════════════════════════════════
        //  사방등 — ObservatoryBuilder.Sconce와 같은 구성·같은 빛
        // ══════════════════════════════════════════════════

        static void Sconce(Transform parent, Vector3 localPos, Vector3 facing, Material iron, Material flame)
        {
            var go = new GameObject("사방등");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.LookRotation(facing);
            Prim(go.transform, "받침", PrimitiveType.Cube, new Vector3(0, -0.06f, -0.09f), new Vector3(0.10f, 0.05f, 0.20f), iron);
            Prim(go.transform, "접시", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.16f, 0.015f, 0.16f), iron);
            Prim(go.transform, "불꽃", PrimitiveType.Sphere, new Vector3(0, 0.055f, 0), Vector3.one * 0.075f, flame);
            var l = new GameObject("불빛").AddComponent<Light>();
            l.transform.SetParent(go.transform, false);
            l.transform.localPosition = new Vector3(0, 0.14f, 0.05f);
            l.type = LightType.Point;
            l.color = new Color(1f, 0.68f, 0.38f);
            l.intensity = 1.7f;
            l.range = 5.5f;
            // ⚠️ 관측실과 달리 **그림자를 켠다** — 야외 씬이라 그림자가 없으면 이 점광이 석축을
            //    뚫고 나가 강가 벽면을 따뜻하게 밝힌다 (비밀문 위치를 그대로 알려 주는 꼴).
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.9f;
            l.shadowNearPlane = 0.05f;
        }

        static void Prim(Transform parent, string name, PrimitiveType t, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(t);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static Material SolidMat(string name, Color baseCol, float smooth, Color? emis)
        {
            string path = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", baseCol);
            m.SetFloat("_Smoothness", smooth);
            if (emis.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emis.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static void MarkDirty()
            => UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts = path.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
