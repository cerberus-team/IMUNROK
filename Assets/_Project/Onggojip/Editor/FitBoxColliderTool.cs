using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IMUNROK.Onggojip.Editor
{
    /// <summary>
    /// 선택한 오브젝트에 BoxCollider를 "자식 전체를 덮게" 자동으로 맞춰주는 도구.
    /// 대문처럼 문짝이 음수 스케일이라 문짝엔 Box를 못 붙일 때, 부모에 클릭영역을 한 번에 만든다.
    ///
    /// 사용법: Hierarchy에서 대문(바깥행랑채)을 선택 → 메뉴 실행 → 끝.
    /// 메뉴: [이문록 ▸ 상호작용: 선택 오브젝트에 박스 콜라이더 자동 맞춤].
    /// </summary>
    public static class FitBoxColliderTool
    {
        [MenuItem("이문록/상호작용: 선택 오브젝트에 박스 콜라이더 자동 맞춤")]
        public static void Fit()
        {
            var go = Selection.activeGameObject;
            if (go == null)
            {
                EditorUtility.DisplayDialog("이문록", "Hierarchy에서 대상(예: 대문(바깥행랑채))을 먼저 선택하세요.", "확인");
                return;
            }

            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0)
            {
                EditorUtility.DisplayDialog("이문록", "선택한 오브젝트 안에 보이는 물체(Renderer)가 없습니다.", "확인");
                return;
            }

            // 자식들의 월드 경계 합치기
            Bounds b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);

            var box = go.GetComponent<BoxCollider>();
            if (box == null) box = go.AddComponent<BoxCollider>();

            // 월드 경계 → 이 오브젝트 로컬 기준으로 변환
            Vector3 centerLocal = go.transform.InverseTransformPoint(b.center);
            Vector3 ls = go.transform.lossyScale;
            Vector3 sizeLocal = new Vector3(
                b.size.x / Mathf.Max(1e-4f, Mathf.Abs(ls.x)),
                b.size.y / Mathf.Max(1e-4f, Mathf.Abs(ls.y)),
                b.size.z / Mathf.Max(1e-4f, Mathf.Abs(ls.z)));

            box.center = centerLocal;
            box.size = sizeLocal;
            box.isTrigger = false;

            EditorSceneManager.MarkSceneDirty(go.scene);
            EditorUtility.DisplayDialog("이문록",
                $"'{go.name}'에 클릭용 BoxCollider를 문 전체에 맞췄습니다.\n\n" +
                $"Center = ({box.center.x:0.00}, {box.center.y:0.00}, {box.center.z:0.00})\n" +
                $"Size   = ({box.size.x:0.00}, {box.size.y:0.00}, {box.size.z:0.00})\n\n" +
                "이제 Play에서 대문을 클릭해보세요. (문짝에 붙였던 Box는 지워도 됨)", "확인");
        }
    }
}
