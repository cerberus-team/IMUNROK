using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>장계 자리를 놓는다.</b> 메뉴: [이문록 ▸ 관아 ▸ ⑳ 장계 자리]
    ///
    /// 사건이 끝나는 자리다. 신문하던 교의에서 <b>일어나 옆으로 옮겨 앉는</b> 그
    /// 한 걸음이 마지막 장면을 연다 — 물어보는 일이 끝났고 이제 적는 일이라는 것을,
    /// 자리를 바꾸는 것으로 말한다.
    ///
    /// <b>대청 안에 둔다.</b> 신문도 여기서 하고 장계도 여기서 쓴다. 어사가 고을에
    /// 와서 하는 일이 다 한 지붕 아래라야, 이 집을 나가고 드는 것이 곧 사건의 마디가 된다.
    /// 자리는 <b>층계 머리의 칸</b>(12.90, z 2.10)이다 — 뜰에서 오르는 돌층계가 그 칸으로
    /// 들므로, 앉으면 층계 너머로 뜰이 곧게 내다보인다.
    ///
    /// <b>교의(交椅) 하나로 끝낸다.</b> 여기 놓은 것이 세 번 바뀌었다:
    ///   ① 임시 큐브 다섯(상판·다리·의자) — 앉는 자리라 부르기 민망한 널이었다.
    ///   ② 떠 있는 경상 — 다리 없는 소반이라 무엇에 쓰는지도 안 보였다.
    ///   ③ 낙안읍성 평상 — 올라앉을 데는 됐으나 <b>판때기가 너무 넓어</b> 대청을 먹었다.
    /// 관아 대청에서 수령이 앉는 것은 <b>등받이와 팔걸이가 있는 교의</b>다. 그것 하나와
    /// 방석 하나. 앉는 높이·앉는 자리·장계·바라보는 곳은 모델이 바뀌어도 안 흔들린다.
    ///
    /// 함께 하는 것 둘:
    ///   · <b>장계 자산</b>(<see cref="CaseReport"/>)을 만들어 채운다. 말과 답은
    ///     인스펙터에서 고치라고 만든 것이다 — 어느 것이 참인지는 이야기를 쓰는 쪽이
    ///     정할 일이지 이 도구가 정할 일이 아니다.
    ///   · <b>마패</b>를 도구벨트에 넣는다. 장계는 마패로 봉해야 서므로, 벨트에 없으면
    ///     다 채워 놓고도 끝을 못 맺는다.
    ///
    /// 두 번 눌러도 두 벌이 안 서고, <b>이미 있는 장계는 안 덮어쓴다</b>.
    /// </summary>
    public static class ReportPlace
    {
        /// <summary>
        /// 대청 마루. <b>2.21 이 아니라 1.8345 다</b> — 화성행궁 유여택의 진짜 바닥을
        /// 광선으로 재서 얻은 값이다. 2.21 은 손으로 짓던 동헌 시절의 숫자인데
        /// 그대로 남아 있어서, 여기에 놓는 것마다 24cm 씩 떠 있었다.
        /// </summary>
        private const float Maru = 1.8345f;

        /// <summary>
        /// 교의가 놓이는 칸. 앞면 기둥이 z −5.3 / −2.3 / +0.6 / +3.65 / +6.7 에 서므로
        /// 칸 한가운데는 z 2.1 이고, <b>뜰에서 오르는 돌층계가 바로 그 칸으로 든다</b>.
        /// 앉으면 층계 너머 뜰이 곧게 내다보인다.
        /// </summary>
        private static readonly Vector3 Chair = new Vector3(12.90f, Maru, 2.10f);

        /// <summary>
        /// 교의의 나무. <b>이어 붙는(tiling) 결이라야 한다.</b>
        ///
        /// 처음에 낙안읍성 평상의 <c>M_Low_Wooden_Bench</c> 를 썼더니 의자가 검은
        /// 얼룩투성이가 됐다 — 그 재질의 그림은 <b>그 평상 하나를 위해 구운 아틀라스</b>라
        /// 자리마다 딴 부위(못 자국·그늘·빈 칸)가 들어 있다. 세계 좌표로 UV 를 따는
        /// 이쪽에서는 아무 데나 집히므로 못 쓴다. 화성행궁의 <c>MI_KoreanWood_*</c> 는
        /// 건물 넓은 면에 쓰는 <b>이어 붙는 결</b>이라 어디를 집어도 나무다.
        /// </summary>
        private const string ChairMat = "Assets/HwaseongHaenggung/Material/MI_KoreanWood_1.mat";
        private const string ChairMatAlt = "Assets/HwaseongHaenggung/Material/MI_R_Woods_1.mat";
        private const string CushionMesh = "Assets/KCDF/Restaurant/Models/Cushion/Mesh/M_Cushion_7.fbx";
        private const string CushionMat = "Assets/KCDF/Restaurant/Models/Cushion/Material/M_Cushion_1.mat";

        private const string FormPath = "Assets/_Project/Onggojip/Data/장계_옹고집.asset";

        [MenuItem("이문록/관아/⑳ 장계 자리")]
        public static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.name.Contains("Gwana"))
            {
                Debug.LogWarning("[관아] 관아 씬을 열고 누르십시오(지금은 " + scene.name + ").");
                return;
            }
            var log = new System.Text.StringBuilder("[관아] 장계 자리\n");
            var form = Form(log);
            Place(scene, form, log);
            Mapae(log);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(log.ToString());
        }

        // ── ㉠ 장계 ───────────────────────────────

        /// <summary>
        /// 옹고집 사건의 장계. <b>이미 있으면 안 덮어쓴다</b> — 말을 고쳐 둔 것을
        /// 도구가 도로 지워 버리면 고칠 마음이 안 난다.
        /// </summary>
        private static CaseReport Form(System.Text.StringBuilder log)
        {
            var have = AssetDatabase.LoadAssetAtPath<CaseReport>(FormPath);
            if (have != null) { log.AppendLine("  · 장계가 이미 있다 — 안 건드린다 (" + FormPath + ")"); return have; }

            var f = ScriptableObject.CreateInstance<CaseReport>();
            Author(f);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FormPath));
            AssetDatabase.CreateAsset(f, FormPath);
            AssetDatabase.SaveAssets();
            log.AppendLine("  · 장계를 만들었다 — " + FormPath);
            return f;
        }

        [MenuItem("이문록/관아/⑳-1 장계 다시 쓰기(덮어씀)")]
        public static void Rewrite()
        {
            var f = AssetDatabase.LoadAssetAtPath<CaseReport>(FormPath);
            if (f == null) { Debug.LogWarning("[관아] 장계가 없습니다 — ⑳ 장계 자리를 먼저 누르십시오."); return; }
            Author(f);
            EditorUtility.SetDirty(f);
            AssetDatabase.SaveAssets();
            Debug.Log("[관아] 장계를 새 틀(한 줄 = 사람 + 증좌)로 다시 썼다 — " + FormPath);
        }

        /// <summary>
        /// <b>옹고집 장계의 말과 점수</b>(2026-09-14).
        ///
        /// 한 줄 = 한 주장 + 그 증좌. 사람 칸에는 동헌의 여섯이 늘 다 뜬다.
        /// 증좌 점수는 <b>그 주장을 얼마나 못 박는가</b>로 매겼다 — 유척으로 잰 흉터(G07)는 사람의
        /// 말이 흔들려도 안 흔들리므로 가장 무겁고, 필적·정황은 곁증좌라 가볍다. 표에 없는 증좌를
        /// 넣으면 1점 깎인다(아무거나 다 넣는 장계는 성근 장계다).
        /// 2026-09-15: 甲의 자백(G13) · 게장(G10·G15) · 乙의 인정(G16·G17) · 소지(G14) 를 더했다 —
        /// 1막에서 차용증(J08)을 놓쳐도 2막 문서고의 소지로 갑리 줄을 세울 수 있다. 참기준 30.
        /// </summary>
        public static void Author(CaseReport f)
        {
            f.사건 = CaseId.Case1_Onggojip;
            f.머리말 = "臣이 옹진현 옹당촌에 이르러 옹씨 집안의 일을 살피오니,";
            f.맺음말 = "삼가 아뢰옵나이다.";
            f.오답감점 = 1;
            f.참기준 = 30;

            f.인물들 = new[]
            {
                new CaseReport.Person { id = "甲", 이름 = "옹덕구라 하는 자", 얼굴 = "甲" },
                new CaseReport.Person { id = "乙", 이름 = "성난 사내", 얼굴 = "乙" },
                new CaseReport.Person { id = "아내", 이름 = "안주인", 얼굴 = "아내" },
                new CaseReport.Person { id = "하인", 이름 = "늙은 하인", 얼굴 = "늙은하인" },
                new CaseReport.Person { id = "마름", 이름 = "마름", 얼굴 = "마름" },
                new CaseReport.Person { id = "서리", 이름 = "관아 서리", 얼굴 = "관아서리" },
            };

            System.Func<string, int, CaseReport.EvidenceScore> E = (k, p) => new CaseReport.EvidenceScore { 단서 = k, 점수 = p };

            f.줄들 = new[]
            {
                new CaseReport.Line
                {
                    앞 = "옹덕구를 사칭하고 그 가산을 차지한 자는",
                    인물칸 = true, 정답인물 = "甲", 인물점수 = 3,
                    가운데 = "이옵고, 그 증좌는",
                    증거칸 = true, 증거기준 = 3, 증거최대 = 3,
                    증거점수 = new[] { E("G13", 5), E("G07", 4), E("G08", 3), E("G01", 2), E("G10", 2), E("G15", 2), E("G06", 2), E("J06", 1), E("J02", 1), E("J03", 1), E("J04", 1), E("J05", 1), E("G04", 1), E("J10", 1) },
                    뒤 = "이옵니다.",
                    물음 = "옹덕구를 사칭한 자는 누구이며, 무엇으로 아는가.",
                    핵심 = true,
                },
                new CaseReport.Line
                {
                    앞 = "그 자는 본디 이 집의 종 복동이오니, 복동을 이복 아우로 둔 이는",
                    인물칸 = true, 정답인물 = "乙", 인물점수 = 2,
                    가운데 = "이옵고, 그 증좌는",
                    증거칸 = true, 증거기준 = 2, 증거최대 = 3,
                    증거점수 = new[] { E("G05", 3), E("G17", 3), E("J11", 2), E("J14", 1), E("G03", 1), E("J01", 1) },
                    뒤 = "이옵니다.",
                    물음 = "복동과 피를 나눈 이는 누구인가.",
                },
                new CaseReport.Line
                {
                    앞 = "복동은 이미 면천된 몸이었으나 관의 입안대장이 긁혀 고쳐져 종으로 남았사오니, 그 증좌는",
                    증거칸 = true, 증거기준 = 3, 증거최대 = 3,
                    증거점수 = new[] { E("G12", 4), E("G11", 3), E("G03", 1), E("J11", 1) },
                    뒤 = "이옵니다.",
                    물음 = "복동이 면천된 몸이었음을 무엇으로 아는가.",
                },
                new CaseReport.Line
                {
                    앞 = "복동을 죽은 것으로 적어 관에 올린 호구단자를 쓴 손은",
                    인물칸 = true, 정답인물 = "甲", 인물점수 = 2,
                    가운데 = "이오니, 그 증좌는",
                    증거칸 = true, 증거기준 = 2, 증거최대 = 3,
                    증거점수 = new[] { E("G02", 3), E("J13", 2), E("J06", 1), E("J12", 1), E("G13", 1) },
                    뒤 = "이옵니다.",
                    물음 = "호구단자에 복동이 죽었다고 써 올린 손은 누구인가.",
                },
                new CaseReport.Line
                {
                    앞 = "또 연 열 할의 갑리를 놓아 고을 사람을 괴롭힌 자는",
                    인물칸 = true, 정답인물 = "乙", 인물점수 = 2,
                    가운데 = "이오니, 그 증좌는",
                    증거칸 = true, 증거기준 = 2, 증거최대 = 3,
                    증거점수 = new[] { E("G16", 3), E("J08", 3), E("G14", 2), E("J09", 1) },
                    뒤 = "이옵니다.",
                    물음 = "갑리를 놓은 자는 누구인가.",
                    갑리줄 = true,
                },
                new CaseReport.Line
                {
                    앞 = "하오니 마땅히",
                    보기 = new[]
                    {
                        new CaseReport.Choice { 말 = "사칭한 복동을 엄히 다스리고 가산을 옹덕구에게 돌려주게", 판결 = Verdict.Truth, 점수 = 2 },
                        new CaseReport.Choice { 말 = "복동의 면천을 되살리고, 사칭의 죄는 사정을 헤아려 감하게", 필요단서 = "G11,G12", 판결 = Verdict.Mercy, 점수 = 2 },
                        new CaseReport.Choice { 말 = "성난 사내를 광인으로 내치고 지금의 주인을 그대로 두게", 판결 = Verdict.AcceptFake, 점수 = 0 },
                        new CaseReport.Choice { 말 = "두 사람을 다 두어 집안을 화목케", 판결 = Verdict.AcceptFake, 점수 = 0 },
                    },
                    뒤 = "하여야 할 것이옵니다.",
                    물음 = "어찌 처결하시라 아뢸 것인가.",
                },
            };

            f.참끝 = "삼 년 전 대장에 적힌 파기 한 줄이 사람을 짚었다. 복동은 사칭의 죗값을 치렀고, 옹덕구는 제 이름과 가산을 되찾았다. " +
                    "갑리의 장부도 불살라졌다 — 옹당촌에 옹덕구가 하나로 남는다.";
            f.반끝 = "판결은 섰다. 다만 장계의 몇 줄이 <b>증좌로 서지 못해</b> 끝까지 다스리지는 못했다. " +
                    "고을에는 아직도 어느 쪽이 참이었느냐는 말이 남았다.";
            f.자비끝 = "복동의 면천이 대장에 도로 올랐다. 사칭의 죄는 물었으되 매는 감했다 — 스무 해 종으로 적혀 산 몸이 " +
                     "제 이름을 찾기까지 걸린 길을 헤아린 것이다. 옹덕구는 가산과 함께 갑리의 빚도 돌려받았다.";
            f.헛끝 = "가짜가 옹덕구의 자리에 그대로 앉았다. 진짜는 제 집 문간에서 쫓겨났고, 복동의 이름은 <b>여태 죽은 종</b>으로 남았다.";
            f.이문록줄 = "옹진현 옹고집 — {끝}";
        }

        // ── ㉡ 자리 ───────────────────────────────

        private static void Place(Scene scene, CaseReport form, System.Text.StringBuilder log)
        {
            // 옛 이름으로 서 있으면 그대로 물려받아 이름만 고친다 —
            // 씬의 다른 부품이 이 오브젝트를 참조하고 있을 수 있다.
            var root = Find(scene, "판결_교의") ?? Find(scene, "판결_평상") ?? Find(scene, "서안_자리");
            if (root == null)
            {
                root = new GameObject("판결_교의");
                SceneManager.MoveGameObjectToScene(root, scene);
                Undo.RegisterCreatedObjectUndo(root, "판결 교의");
                log.AppendLine("  · 교의 자리를 놓았다 — 대청 층계 머리의 칸");
            }
            root.name = "판결_교의";
            root.transform.SetPositionAndRotation(Chair, Quaternion.Euler(0f, 270f, 0f));  // 뜰(−x)을 본다
            root.transform.localScale = Vector3.one;

            // 여태 놓았다 걷은 것들 — 임시 큐브 · 떠 있던 경상 · 평상
            foreach (var dead in new[] { "임시소품", "평상", "방석" })
            {
                var d = root.transform.Find(dead);
                if (d != null) { Undo.DestroyObjectImmediate(d.gameObject); log.AppendLine("  · " + dead + " 을(를) 걷었다"); }
            }
            foreach (var dead in new[] { "판결_경상", "판결_서안" })
            {
                var g = Find(scene, dead);
                if (g != null) { Undo.DestroyObjectImmediate(g); log.AppendLine("  · " + dead + " 를 걷었다 — 교의가 대신한다"); }
            }

            // ── 교의 ── 그물째 굽는다(GyouiMesh). 콜라이더는 여기 안 붙인다.
            var chairT = root.transform.Find("교의");
            GameObject chair;
            if (chairT == null)
            {
                chair = new GameObject("교의", typeof(MeshFilter), typeof(MeshRenderer));
                chair.transform.SetParent(root.transform, false);
                Undo.RegisterCreatedObjectUndo(chair, "교의");
            }
            else chair = chairT.gameObject;
            chair.transform.localPosition = Vector3.zero;
            chair.transform.localRotation = Quaternion.identity;
            chair.transform.localScale = Vector3.one;
            chair.GetComponent<MeshFilter>().sharedMesh = GyouiMesh.Build();
            var mat = AssetDatabase.LoadAssetAtPath<Material>(ChairMat);
            if (mat == null) mat = AssetDatabase.LoadAssetAtPath<Material>(ChairMatAlt);
            chair.GetComponent<MeshRenderer>().sharedMaterial = mat;
            log.AppendLine("  · 교의를 지었다 — 좌판 " + GyouiMesh.SeatTop + "m · 등받이 " + GyouiMesh.Height + "m");

            // ── 방석 ── 좌판 위에
            var cuT = root.transform.Find("좌방석");
            GameObject cu = cuT != null ? cuT.gameObject : null;
            if (cu == null)
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(CushionMesh);
                if (mesh == null)
                    foreach (var o in AssetDatabase.LoadAllAssetsAtPath(CushionMesh))
                    { var m = o as Mesh; if (m != null) { mesh = m; break; } }
                if (mesh == null) log.AppendLine("  ※ 방석 그물을 못 찾았다");
                else
                {
                    cu = new GameObject("좌방석", typeof(MeshFilter), typeof(MeshRenderer));
                    cu.transform.SetParent(root.transform, false);
                    cu.GetComponent<MeshFilter>().sharedMesh = mesh;
                    cu.GetComponent<MeshRenderer>().sharedMaterial =
                        AssetDatabase.LoadAssetAtPath<Material>(CushionMat);
                    Undo.RegisterCreatedObjectUndo(cu, "좌방석");
                    log.AppendLine("  · 방석을 얹었다");
                }
            }
            if (cu != null)
            {
                cu.transform.localPosition = new Vector3(0f, GyouiMesh.SeatTop, -0.01f);
                cu.transform.localRotation = Quaternion.identity;
                cu.transform.localScale = Vector3.one * 1.15f;
            }

            // ── 바라보는 곳 ── 뜰(층계 아래). 이것이 없으면 심문 자리가 잡아 둔 쪽을 본다
            var faceT = root.transform.Find("바라보는곳");
            if (faceT == null)
            {
                var g2 = new GameObject("바라보는곳");
                g2.transform.SetParent(root.transform, false);
                Undo.RegisterCreatedObjectUndo(g2, "바라보는 곳");
                faceT = g2.transform;
            }
            faceT.position = new Vector3(4.90f, 0f, Chair.z);

            // ── 앉는 자리 ──
            //
            // <b>좌판 위가 아니라 좌판 자리(바닥 높이)다.</b> <see cref="PlayerSeat"/> 는 이
            // 자리 밑으로 광선을 쏘아 <b>바닥</b>을 찾고 거기에 앉은 눈높이(1.2)를 얹는다.
            // 마루를 짚으면 눈이 마루에서 1.2 — 0.46 짜리 교의에 앉은 사람의 눈이 좌판 위
            // 0.74 다. 좌판을 짚게 두면 1.66 이 되어 <b>선 키(1.6)보다 높이</b> 앉는다.
            var seatT = root.transform.Find("앉는자리");
            if (seatT == null)
            {
                var s2 = new GameObject("앉는자리");
                s2.transform.SetParent(root.transform, false);
                Undo.RegisterCreatedObjectUndo(s2, "앉는 자리");
                seatT = s2.transform;
            }
            seatT.localPosition = new Vector3(0f, 0f, -0.02f);
            seatT.localRotation = Quaternion.identity;

            // ── 누르는 · 막는 몸 ──
            //
            // <b>앉는자리의 자식으로 둔다.</b> PlayerSeat 의 바닥 재기가 「앉을 자리와 그
            // 자식」을 건너뛰므로, 여기 두어야 의자가 바닥으로 안 잡힌다. 누름은 콜라이더에서
            // 부모를 거슬러 <see cref="ReportDesk"/> 를 찾으므로 그대로 먹는다.
            var bodyT = seatT.Find("몸");
            GameObject body;
            if (bodyT == null)
            {
                body = new GameObject("몸", typeof(BoxCollider));
                body.transform.SetParent(seatT, false);
                Undo.RegisterCreatedObjectUndo(body, "교의 몸");
            }
            else body = bodyT.gameObject;
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            var col = body.GetComponent<BoxCollider>();
            col.center = new Vector3(0f, GyouiMesh.Height * 0.5f, 0.02f);
            col.size = new Vector3(0.60f, GyouiMesh.Height, 0.54f);

            // 뿌리에 남아 있던 옛 상자는 걷는다 — 그것이 있으면 바닥 재기에 걸린다
            var oldCol = root.GetComponent<BoxCollider>();
            if (oldCol != null) { Undo.DestroyObjectImmediate(oldCol); log.AppendLine("  · 뿌리의 옛 상자를 걷었다"); }

            var desk = root.GetComponent<ReportDesk>();
            if (desk == null) desk = Undo.AddComponent<ReportDesk>(root);
            // <b>앉는 것은 막지 않는다.</b> 한동안 G01·G07 을 캐야 앉히게 해 두었는데,
            // 안 캤을 때 <b>아무 말 없이 안 눌릴 뿐</b>이라 고장으로 보였다. 앉는 것은
            // 늘 되고, 못 쓰는 줄은 장계가 제 입으로 말한다 — 문지기를 세울 자리는 여기가
            // 아니라 <b>봉함</b>이다. 증좌 줄은 캔 것이 하나도 없으면 채울 수가 없으므로,
            // 아무것도 안 캐고 봉하는 길은 애초에 막혀 있다.
            desk.Setup(form, seatT, new string[0], faceT);
            EditorUtility.SetDirty(desk);
            log.AppendLine("  · 앉는 자리 " + Chair.ToString("F2") + " — 언제든 앉는다. 못 채운 줄은 장계가 알린다");
        }

        // ── ㉢ 마패 ───────────────────────────────

        /// <summary>장계는 마패로 봉해야 선다. 벨트에 없으면 끝을 못 맺는다.</summary>
        private static void Mapae(System.Text.StringBuilder log)
        {
            var def = AssetDatabase.LoadAssetAtPath<ToolDef>("Assets/_Project/Data/Tools/Tool_mapae.asset");
            if (def == null) { log.AppendLine("  ※ Tool_mapae 를 못 찾았다"); return; }
            var belt = Object.FindFirstObjectByType<ToolbeltHud>();
            if (belt == null) { log.AppendLine("  ※ 도구벨트가 씬에 없다"); return; }

            var so = new SerializedObject(belt);
            var arr = so.FindProperty("_tools");
            for (int i = 0; i < arr.arraySize; i++)
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue == def)
                { log.AppendLine("  · 마패는 이미 벨트에 있다"); return; }
            arr.arraySize++;
            arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = def;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(belt);
            log.AppendLine("  · 마패를 도구벨트에 넣었다 — 이 물건에 드디어 할 일이 생겼다");
        }

        // ── 잔손 ─────────────────────────────────

        private static GameObject Find(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == name) return root;
            return null;
        }
    }
}
