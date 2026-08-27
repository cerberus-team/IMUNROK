using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using IMUNROK.Seocheon.Environment;

namespace IMUNROK.Seocheon.Editor
{
    /// <summary>
    /// 다리에서 좌표를 읽어 성 밖 안개 구역 트리거(FogZone_Bridge)를 만들고 씬에 배치한다.
    /// 좌표 하드코딩 없이 _Bridge 렌더러 bounds에서 폭/데크 높이/중심 Z를 도출한다.
    /// 저장되는 씬 기본값은 fog=false(마을 기본) — 런타임에 SeocheonFogZone이 true로 켠다.
    /// </summary>
    public static class SeocheonFogZoneSetup
    {
        private const string ZoneName = "FogZone_Bridge";

        [MenuItem("Seocheon/Fog/Setup Bridge Fog Zone")]
        public static void Setup()
        {
            var bridge = GameObject.Find("_Bridge");
            if (bridge == null) { Debug.LogError("[FogZone] _Bridge 를 찾을 수 없습니다."); return; }
            var rs = bridge.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) { Debug.LogError("[FogZone] _Bridge 에 렌더러가 없습니다."); return; }
            var b = rs[0].bounds; for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);

            float deckTop = b.max.y;                 // 데크 상면
            float centerZ = b.center.z;              // 다리 중심 Z = 마을/성밖 경계면
            float widthX = b.size.x;                 // 다리 폭

            var go = GameObject.Find(ZoneName) ?? new GameObject(ZoneName);
            Undo.RegisterCreatedObjectUndo(go, "create fog zone");
            go.transform.position = new Vector3(b.center.x, deckTop, centerZ);
            go.transform.rotation = Quaternion.identity;

            var box = go.GetComponent<BoxCollider>() ?? Undo.AddComponent<BoxCollider>(go);
            box.isTrigger = true;
            box.center = new Vector3(0f, 1.5f, 0f);                 // 데크에서 위로 3m
            box.size = new Vector3(widthX + 6f, 3f, 3f);           // 폭은 다리보다 넉넉히, Z는 얇게

            if (go.GetComponent<SeocheonFogZone>() == null) Undo.AddComponent<SeocheonFogZone>(go);

            // 씬 기본값: 마을은 안개 없음. 색/모드는 유지.
            RenderSettings.fog = false;
            RenderSettings.fogMode = FogMode.Linear;

            EditorUtility.SetDirty(go);
            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveOpenScenes();
            Selection.activeGameObject = go;
            Debug.Log($"[FogZone] 배치 완료 @({go.transform.position.x:F1},{go.transform.position.y:F1},{centerZ:F1}) " +
                      $"box=({box.size.x:F1},{box.size.y:F1},{box.size.z:F1}) / 저장 씬 fog=false");
        }
    }
}
