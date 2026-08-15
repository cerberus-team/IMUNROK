using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 서고 여닫이 가구 리거 (2026-08-15) — 배치를 건드리지 않고 씬 인스턴스에
    /// FurnitureParts(클릭 토글 여닫이)만 설치한다. 배치기 재실행 금지 상황(사용자 수작업 배치 보존).
    ///
    /// 실험 계측 결과 (임시 회전 → 스크린샷 → 원복으로 판정):
    ///   ShelfCabinet 01/02  : 아랫칸 쌍문 — HingeL/HingeR 본, 자기 로컬 Z 회전
    ///   StorageCabinets 01  : 쌍문 — joint2/joint6 본, 자기 로컬 Z 회전
    ///   HalfChest 01        : 앞판 세로 경첩 문 — joint2 본, 자기 로컬 Z 회전.
    ///                         ⚠️ 저장 상태가 '활짝 열림'이었다 — 리거가 Z-180으로 닫아 이를 닫힘 기본값으로 삼는다
    ///   Table04_Key(문갑)   : 옆문 2짝 = Dummy051/052(Z 회전), 가운데 서랍 2개 = Dummy053 공유(로컬 -Y로 앞으로 빠짐)
    ///   SM_Pantry_Chest     : 통짜 정적 메시 2장 — 분리 불가, 제외
    ///
    /// 클릭은 가구의 기존 차단 콜라이더가 받는다 (컴포넌트는 가구 루트에, DebugInteractor는 부모 탐색).
    /// 멱등: 이미 설치돼 있으면 파트 구성만 갱신한다. 닫힘 정규화(반닫이 -180)는 최초 1회만.
    /// </summary>
    public static class ArchiveFurnitureRigger
    {
        [MenuItem("Tools/이문록/서고 여닫이 가구 설치")]
        public static void Rig()
        {
            if (SceneManager.GetActiveScene().name != "Gyeonu_Observatory")
            {
                Debug.LogError("[서고 여닫이] 활성 씬이 Gyeonu_Observatory가 아닙니다");
                return;
            }
            var root = GameObject.Find("서고_소품");
            if (root == null) { Debug.LogError("[서고 여닫이] 서고_소품 루트가 없습니다"); return; }

            int rigged = 0;

            foreach (Transform ch in root.transform)
            {
                switch (ch.name)
                {
                    case "ShelfCabinet 01":
                    case "ShelfCabinet 02":
                        rigged += RigParts(ch, "선반장", null, new[]
                        {
                            NewSwing(ch, "SM_ShelfCabinet_Close/Body01/Body02/HingeL", 100f),
                            NewSwing(ch, "SM_ShelfCabinet_Close/Body01/Body02/HingeR", -100f),
                        }) ? 1 : 0;
                        break;
                    case "StorageCabinets 01":
                        rigged += RigParts(ch, "수납장", null, new[]
                        {
                            NewSwing(ch, "SM_StorageCabinets_Close/joint1/joint2", 100f),
                            NewSwing(ch, "SM_StorageCabinets_Close/joint1/joint6", -100f),
                        }) ? 1 : 0;
                        break;
                    case "HalfChest 01":
                        // ⚠️ 원래 저장 상태는 앞판이 '활짝 열림'(kcisa 기본 포즈)이었고, 축 계측 실험 중
                        //    joint2를 Z-180으로 돌려 닫아 두었다 — 그 상태가 지금의 닫힘 기본값이다.
                        //    (추가 정규화 금지 — 다시 -180을 먹이면 도로 열린다)
                        rigged += RigParts(ch, "반닫이", null,
                            new[] { NewSwing(ch, "SM_HalfChest_Close/joint1/joint2", 110f) }) ? 1 : 0;
                        break;
                    case "Table04_Key":
                        rigged += RigParts(ch, "문갑", null, new[]
                        {
                            NewSwing(ch, "Dummy051", 95f),
                            NewSwing(ch, "Dummy052", -95f),
                            NewSlide(ch, "Dummy053", new Vector3(0, -0.16f, 0)),   // 서랍 — 로컬 -Y = 방 앞쪽
                        }) ? 1 : 0;
                        break;
                }
            }
            EditorSceneManager();
            Debug.Log($"[서고 여닫이] 설치 완료 — 가구 {rigged}개. 통짜 제외: SM_Pantry_Chest(찬장 — 문짝이 메시에 붙박이)");
        }

        static IMUNROK.Gyeonu.FurnitureParts.Part NewSwing(Transform furniture, string path, float angle)
        {
            var node = furniture.Find(path);
            if (node == null) Debug.LogWarning("[서고 여닫이] 본 없음: " + furniture.name + "/" + path);
            return new IMUNROK.Gyeonu.FurnitureParts.Part
            { node = node, axisInSelf = Vector3.forward, openAngle = angle, slideLocal = Vector3.zero };
        }

        static IMUNROK.Gyeonu.FurnitureParts.Part NewSlide(Transform furniture, string path, Vector3 slide)
        {
            var node = furniture.Find(path);
            if (node == null) Debug.LogWarning("[서고 여닫이] 본 없음: " + furniture.name + "/" + path);
            return new IMUNROK.Gyeonu.FurnitureParts.Part
            { node = node, axisInSelf = Vector3.zero, openAngle = 0, slideLocal = slide };
        }

        static bool RigParts(Transform furniture, string label,
            System.Action firstTimeNormalize, IMUNROK.Gyeonu.FurnitureParts.Part[] parts)
        {
            var fp = furniture.GetComponent<IMUNROK.Gyeonu.FurnitureParts>();
            if (fp == null)
            {
                firstTimeNormalize?.Invoke();   // 닫힘 정규화는 최초 설치 때만 (멱등 보호)
                fp = furniture.gameObject.AddComponent<IMUNROK.Gyeonu.FurnitureParts>();
            }
            fp.displayName = label;
            fp.duration = 0.9f;
            fp.parts.Clear();
            foreach (var p in parts) if (p.node != null) fp.parts.Add(p);
            return fp.parts.Count > 0;
        }

        static void EditorSceneManager()
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
    }
}
