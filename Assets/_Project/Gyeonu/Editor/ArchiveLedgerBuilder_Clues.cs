using System.Text;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Gyeonu.EditorTools
{
    /// <summary>
    /// 종막 장부 — ⑦ 단서 소지품 세 가지와 창고방의 최종 물증.
    ///
    /// <code>
    ///   C3 선아의 풀이표   → 1단계를 열쇠처럼 연다 (없으면 장부가 뒤엉킨 채로만 보인다)
    ///   C1 아버지의 검수 기록 → 2단계의 대조 기준. 온전한 문장은 여기 적혀 있다
    ///   C2 후고 반출 대장    → 3단계 뒤 창고방 서랍장에서 나온다. 이것이 최종 물증이다
    /// </code>
    ///
    /// C1·C3는 아직 얻을 길(관측실 조사·선아 구출)이 없으므로 디버그 메뉴로 지급한다.
    /// </summary>
    public static partial class ArchiveLedgerBuilder
    {
        public const string C1_ID = "C1";
        public const string C2_ID = "C2";
        public const string C3_ID = "C3";

        static void BuildClueItems(LedgerPuzzle puzzle)
        {
            var c1 = MakeItem(C1_ID, "아버지의검수기록", "아버지의 검수 기록", RecordText(),
                              GyeonuWorld.F_아버지검수기록, "살피기");
            var c3 = MakeItem(C3_ID, "선아의풀이표", "선아의 풀이표", SolveTableText(),
                              GyeonuWorld.F_선아풀이표, "받기");
            var c2 = MakeItem(C2_ID, "후고반출대장", "후고 반출 대장", ProofText(), "", "꺼내기");

            // 상세·조사 화면에서 돌려 볼 실물 — 없으면 어두운 막에 아무것도 안 뜬다 (Play 실측)
            c1.modelPrefab = DocumentPrefab("문서_C1", 0.200f, 0.268f, "T_문서_C1", new[]
            {
                new HanjiTextBaker.Line("검 수 기", 74, new Color(0.098f, 0.082f, 0.075f)) { gap = 40 },
                new HanjiTextBaker.Line("혼상 외환 연결쇠", 40) { gap = 20 },
                new HanjiTextBaker.Line("혼천의 방위 회전축", 40) { gap = 20 },
                new HanjiTextBaker.Line("관측경 수정편", 40),
            }, 씨앗 + 2100, seal: false);
            c3.modelPrefab = DocumentPrefab("문서_C3", 0.190f, 0.255f, "T_문서_C3", new[]
            {
                new HanjiTextBaker.Line("표찰과 기물", 64, new Color(0.098f, 0.082f, 0.075f)) { gap = 36 },
                new HanjiTextBaker.Line("매화  학  구름  거북", 40) { gap = 24 },
                new HanjiTextBaker.Line("관아 — 붉은 관인", 36) { gap = 16 },
                new HanjiTextBaker.Line("외고 — 짚끈 목패", 36) { gap = 16 },
                new HanjiTextBaker.Line("후고 — 먹점뿐", 36),
            }, 씨앗 + 2200, seal: false);
            c2.modelPrefab = DocumentPrefab("문서_C2", 0.170f, 0.224f, "T_문서_C2", new[]
            {
                new HanjiTextBaker.Line("후 고", 80, new Color(0.098f, 0.082f, 0.075f)) { gap = 20 },
                new HanjiTextBaker.Line("반출 대장", 54) { gap = 34 },
                new HanjiTextBaker.Line("처리 — 최", 44),
            }, 씨앗 + 1300, seal: true);

            c2.journalKey = "C2";
            c2.journalText = "후고로 돌려진 기물들의 반출 대장. 처리인은 모두 최, 마지막 장에 수결이 남아 있다.";
            EditorUtility.SetDirty(c1);
            EditorUtility.SetDirty(c3);
            EditorUtility.SetDirty(c2);

            // ⚠️ 제자리로 고쳐 쓴 메시 에셋을 디스크와 맞춘 뒤 프리팹을 다시 읽는다
            //    (안 그러면 프리팹의 MeshFilter가 메모리에서 NULL로 뜬다 — 앞서 표찰에서 물린 자리)
            AssetDatabase.SaveAssets();
            foreach (var n in new[] { "문서_C1", "문서_C2", "문서_C3" })
                AssetDatabase.ImportAsset(프리팹폴더 + "/" + n + ".prefab", ImportAssetOptions.ForceUpdate);

            PlaceFinalProof(c2);
        }

        /// <summary>창고방(서고 남서 구석) 문갑 서랍 속에 최종 물증을 넣고, 목표 표시를 붙인다.</summary>
        static void PlaceFinalProof(InventoryItem c2)
        {
            var cabinet = GameObject.Find("서고_소품/Table04_Key");
            if (cabinet == null) { Debug.LogWarning("[종막 장부] 창고방 서랍장(Table04_Key)을 찾지 못했다"); return; }
            var furn = cabinet.GetComponent<FurnitureParts>();

            // 서랍 본을 찾는다 — 물증이 서랍과 함께 밀려 나와야 한다
            Transform drawer = cabinet.transform;
            if (furn != null)
                foreach (var p in furn.parts)
                    if (p.node != null && p.slideLocal.sqrMagnitude > 1e-6f) { drawer = p.node; break; }

            // 멱등 — 지난번에 넣은 것을 먼저 치운다.
            // ⚠️ 물증은 서랍 본 밑, 목표 표시는 가구 뿌리 밑이다. **둘 다** 훑어야 한다
            //    (서랍만 치웠더니 목표 표시가 재실행마다 쌓여 여섯 겹이 됐다, 실측).
            foreach (var parent in new[] { drawer, cabinet.transform })
                for (int i = parent.childCount - 1; i >= 0; i--)
                {
                    var ch = parent.GetChild(i);
                    if (ch.name == "최종물증" || ch.name == "목표표시") Object.DestroyImmediate(ch.gameObject);
                }

            // 서랍 속 대장 — 얇은 한지 묶음
            string tp = 텍스처폴더 + "/T_대장.png";
            HanjiTextBaker.Bake(tp, 380, 500, new[]
            {
                new HanjiTextBaker.Line("후 고", 74, new Color(0.098f, 0.082f, 0.075f)) { gap = 18 },
                new HanjiTextBaker.Line("반출 대장", 52) { gap = 40 },
                new HanjiTextBaker.Line("처리 — 최", 42),
            }, seed: 씨앗 + 1300, border: true, seal: true);

            // ⚠️ 이 문갑의 서랍은 **속이 없는 통짜 상자**다 (스킨드 메시를 구워 실측: Object023이
            //    0.48×0.12×0.36 한 덩어리). "서랍 안"에 두면 나무 속에 파묻힌다.
            //    빠져나온 서랍의 **윗면 앞쪽**에 얹는다 — 서랍과 함께 밀려 나와 위에서 내려다보인다.
            //    문갑은 X-270으로 서 있어 서랍 본의 로컬 Z가 세계의 위쪽, 로컬 −Y가 앞쪽이다.
            var go = MakePlate("최종물증", 0.130f, 0.170f, 0.010f, MakePaperMaterial("M_대장", tp));
            go.transform.SetParent(drawer, false);
            go.transform.localPosition = new Vector3(0f, -0.292f, 0.062f);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, 5f);

            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(0.150f, 0.190f, 0.045f);

            var pick = go.AddComponent<ItemPickup>();
            pick.item = c2;
            pick.verbOverride = "꺼내기";
            pick.insideFurniture = furn;
            pick.requiredFlags = new[] { GyeonuWorld.F_후고단서 };

            // 목표 표시 — 서랍장 위에 빛 한 점
            var glowGo = new GameObject("목표표시");
            glowGo.transform.SetParent(cabinet.transform, false);
            glowGo.transform.localPosition = new Vector3(0f, -0.02f, 0.30f);   // 문갑 상판 위
            var glow = glowGo.AddComponent<LedgerGoalGlow>();
            glow.until = pick;

            // 서랍을 열면 대장 자체에도 여린 빛 — 문갑 상판이 그림자를 드리워 대장이 거의 안 보인다.
            // 닫혀 있는 동안에는 가구에 가려 이 빛도 보이지 않는다(깊이 판정을 그대로 탄다).
            var glow2Go = new GameObject("목표표시");
            glow2Go.transform.SetParent(go.transform, false);
            glow2Go.transform.localPosition = new Vector3(0f, 0f, 0.012f);
            var glow2 = glow2Go.AddComponent<LedgerGoalGlow>();
            glow2.until = pick;
            glow2.size = 0.115f;
            glow2.period = 2.0f;
        }

        /// <summary>
        /// 단서 문서 한 장 — 한지 텍스처를 굽고 얇은 판 프리팹으로 저장한다.
        /// 소지품 상세·전체 화면 조사에서 **돌려 보는 실물**로 쓴다
        /// (<see cref="InventoryItem.modelPrefab"/>이 비면 어두운 막에 아무것도 안 뜬다).
        /// </summary>
        static GameObject DocumentPrefab(string name, float w, float h, string texName,
                                         HanjiTextBaker.Line[] lines, int seed, bool seal)
        {
            int tw = Mathf.RoundToInt(460f * (w / h));
            string tp = 텍스처폴더 + "/" + texName + ".png";
            HanjiTextBaker.Bake(tp, tw, 460, lines, seed: seed, border: true, seal: seal);

            var go = MakePlate(name, w, h, 0.010f, MakePaperMaterial("M_" + name, tp));
            string pp = 프리팹폴더 + "/" + name + ".prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(go, pp);
            Object.DestroyImmediate(go);
            return saved;
        }

        static InventoryItem MakeItem(string id, string fileTag, string name, string desc, string flag, string verb)
        {
            string p = 소지품폴더 + "/Item_" + id + "_" + fileTag + ".asset";
            var it = AssetDatabase.LoadAssetAtPath<InventoryItem>(p);
            if (it == null) { it = ScriptableObject.CreateInstance<InventoryItem>(); AssetDatabase.CreateAsset(it, p); }
            it.itemId = id;
            it.displayName = name;
            it.description = desc;
            it.pickupVerb = verb;
            it.worldFlag = flag;
            // 미리보기 카메라는 모델의 뒷면을 본다 — 글이 적힌 앞면을 돌려 준다
            it.previewEuler = new Vector3(0f, 180f, 0f);
            it.usable = false;
            it.autoShowOnPickup = true;
            EditorUtility.SetDirty(it);
            return it;
        }

        // ── 문서 본문 ────────────────────────────────────────
        static string RecordText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("관측실 기물을 손보며 아버지가 남긴 검수 기록. 세 건이 적혀 있다.");
            sb.AppendLine("이름이 아니라 생김새와 남은 흔적으로 적어 두었다.");
            sb.AppendLine();
            foreach (var r in LedgerData.Records)
            {
                sb.AppendLine("─ " + r.name);
                foreach (var t in r.traits) sb.AppendLine("   · " + t);
                sb.AppendLine();
            }
            sb.AppendLine("세 가지 모두 관측실에서 쓰던 것이다. 어디로 갔는지는 적혀 있지 않다.");
            return sb.ToString();
        }

        static string SolveTableText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("선아가 수령의 장부를 들여다보며 알아낸 것을 적어 둔 쪽지.");
            sb.AppendLine();
            sb.AppendLine(LedgerData.메모_1단계);
            sb.AppendLine();
            sb.AppendLine("─ 흔적 읽는 법");
            sb.AppendLine("   · 관아 — 붉은 관인이 찍힌 종이 꼬리표");
            sb.AppendLine("   · 외고 — 짚끈으로 열십자 묶고 작은 목패");
            sb.AppendLine("   · 후고 — 검은 먹점과 먹줄, 반출패 없음");
            return sb.ToString();
        }

        static string ProofText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("후고로 돌려진 기물의 반출 대장.");
            sb.AppendLine();
            foreach (var d in LedgerData.Answers())
                sb.AppendLine("   " + d.name + "   —   처리 " + d.handler + "   보낸 곳 " + d.destination);
            sb.AppendLine();
            sb.AppendLine("셋 다 같은 손을 거쳐 같은 곳으로 갔다. 명칭은 모두 고쳐 적혀 있다.");
            sb.AppendLine("아버지가 기물을 빼돌린 것이 아니다. 이름과 처리 기록을 바꾸어 감춘 쪽이 있었다.");
            sb.AppendLine("마지막 장에 수령의 수결이 남아 있다.");
            return sb.ToString();
        }
    }
}
