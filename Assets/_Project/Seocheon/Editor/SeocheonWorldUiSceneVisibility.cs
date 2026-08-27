// 월드 스페이스 UI 판을 ★씬 뷰에서만 감춘다. 게임 뷰·빌드에는 손대지 않는다.
using IMUNROK.Ui;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Seocheon.EditorTools
{
    /// <summary>
    /// <see cref="VrPanel"/> 을 단 오브젝트를 씬 뷰에서 감춘다 (2026-08-27).
    ///
    /// ■ 왜 필요한가
    ///   월드 판은 캔버스 1단위 = 화면 1픽셀이라 <b>판 하나가 2900 × 461 「월드 단위」</b>다.
    ///   편집 중에는 원점에 그 크기로 누워 있어 <b>마을을 통째로 관통한다</b> — 씬 뷰에서
    ///   지형·건물을 고르려 하면 이 판이 먼저 잡히고 시야도 가린다.
    ///
    /// ■ 왜 <see cref="SceneVisibilityManager"/> 인가 (다른 방법을 안 쓴 이유)
    ///   · 오브젝트를 <b>비활성</b>으로 두면 — 플레이 시작에 누가 켜 줘야 한다. 켜는 것을 잊으면
    ///     대화창이 통째로 안 뜬다. 씬 상태(activeSelf)를 건드리는 것이라 위험이 실제 게임까지 간다.
    ///   · <b>레이어</b>로 빼면 — 카메라 컬링 마스크·충돌 매트릭스까지 함께 움직인다.
    ///     UI 판정(GraphicRaycaster)이 레이어를 보므로 눌리지 않는 사고가 난다.
    ///   · <b>씬 가시성</b>은 에디터의 씬 뷰에만 걸린다. <b>게임 뷰·빌드·플레이에 아무 영향이 없다</b> —
    ///     정확히 원하는 범위다. 눈 아이콘으로 사람이 언제든 다시 켤 수도 있다.
    ///
    /// ■ 앞으로 생기는 판도 저절로 걸린다
    ///   이름을 박지 않고 <see cref="VrPanel"/> 상속 여부로 찾는다. 씬을 열 때와 계층이 바뀔 때
    ///   다시 훑으므로, 새 월드 판을 넣어도 따로 할 일이 없다.
    /// </summary>
    [InitializeOnLoad]
    public static class SeocheonWorldUiSceneVisibility
    {
        static SeocheonWorldUiSceneVisibility()
        {
            EditorSceneManager.sceneOpened += (s, m) => Apply();
            EditorApplication.hierarchyChanged += ApplyVoid;
            EditorApplication.delayCall += ApplyVoid;
        }

        [MenuItem("Tools/이문록/서천/월드 UI 판 씬뷰에서 감추기", false, 20)]
        public static void ApplyMenu()
        {
            int n = Apply();
            Debug.Log("[서천] 월드 UI 판 " + n + "개를 씬 뷰에서 감췄다. (게임 뷰·빌드에는 영향 없음)");
        }

        [MenuItem("Tools/이문록/서천/월드 UI 판 다시 보이기", false, 21)]
        public static void ShowMenu()
        {
            foreach (var p in Object.FindObjectsByType<VrPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                SceneVisibilityManager.instance.Show(p.gameObject, true);
        }

        /// <summary>델리게이트가 void 를 요구한다 — 반환값만 버리는 껍데기.</summary>
        static void ApplyVoid() { Apply(); }

        static int Apply()
        {
            // ★플레이 중에는 손대지 않는다 — 씬 뷰에서 확인할 일이 있을 수 있다.
            if (EditorApplication.isPlayingOrWillChangePlaymode) return 0;

            int n = 0;
            var panels = Object.FindObjectsByType<VrPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < panels.Length; i++)
            {
                var go = panels[i].gameObject;
                if (go == null || !go.scene.IsValid()) continue;      // 프리팹 자산은 건너뛴다
                if (SceneVisibilityManager.instance.IsHidden(go, true)) { n++; continue; }
                SceneVisibilityManager.instance.Hide(go, true);
                n++;
            }
            return n;
        }
    }
}
