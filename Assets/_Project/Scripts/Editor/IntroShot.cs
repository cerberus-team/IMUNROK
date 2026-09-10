using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using IMUNROK.Common;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>봉서 굴러오는 장면만 찍을 채비를 한다.</b>
    /// 메뉴: [이문록 ▸ 영상 ▸ 인트로: 봉서 굴러오는 장면 채비] / [… 되돌리기]
    ///
    /// 찍고 싶은 것은 <b>대사가 아니라 그림</b>이다. 그런데 인트로를 그냥 재생하면
    /// 표제가 내려앉고, 왕이 다섯 마디를 하고, 그 뒤에야 문서가 나온다 — 찍으려면
    /// 이십 초를 기다렸다가 스무 초를 잘라내야 한다.
    ///
    /// 이 도구는 <b>그 그림만 남긴다</b>:
    ///   · 표제(TitleGate)와 어명(IntroController)을 재운다 — 글씨도 자막도 안 뜬다
    ///   · 소리계를 재운다 — 화면 왼쪽 아래 막대 두 줄이 영상에 박히지 않게
    ///   · 문서 셋에 <see cref="ScrollRollIn"/> 을 붙이고 <b>재생하면 곧바로</b> 굴린다
    ///   · 어전을 처음부터 밝혀 둔다 — 표제가 재워졌으니 불을 켤 사람이 없다
    ///
    /// 재생을 누르고 화면을 담으면 된다. 다 찍었으면 <b>되돌리기</b>를 누른다 —
    /// 재워 둔 것들이 도로 깨어난다. 되돌리기를 잊으면 게임을 켜도 표제가 안 뜨므로,
    /// 채비해 둔 동안에는 콘솔에 그렇게 적어 둔다.
    ///
    /// 두 번 눌러도 두 벌이 안 선다.
    /// </summary>
    public static class IntroShot
    {
        private const string ScenePath = "Assets/_Project/Scenes/Core/IntroScene.unity";

        [MenuItem("이문록/영상/인트로: 봉서 굴러오는 장면 채비")]
        public static void Setup() { Run(true); }

        [MenuItem("이문록/영상/인트로: 봉서 굴러오는 장면 되돌리기")]
        public static void Undo_() { Run(false); }

        private static void Run(bool forShot)
        {
            // 재생 중에는 손대지 않는다. 씬을 더럽히고 저장하려다 유니티에게
            // "This cannot be used during play mode" 를 듣고 반쯤 고쳐진 채로 끝난다.
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[영상] 재생을 멈추고 다시 누르십시오 — 재생 중에는 씬을 못 고칩니다.");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var log = new System.Text.StringBuilder(forShot ? "[영상] 봉서 굴러오는 장면 채비\n"
                                                            : "[영상] 채비를 되돌린다\n");

            // ① 씬에 없이 제 발로 서는 것들을 막는다.
            //
            // 소리계가 마이크를 여느라 주 실이 한 번 걸린다. 그것이 봉서가 굴러오는
            // 참에 걸리면 굴러오다 멈춰 선다. 씬에 놓여 있지 않아 재울 것이 없으므로,
            // 런타임이 읽는 스위치로 막는다.
            ShotMode.On = forShot;
            log.AppendLine("── 마이크 " + (forShot ? "막음 — 멈춤의 임자다" : "도로 품"));

            // ② 표제와 어명을 재운다 — 찍을 것은 그림뿐이다
            Sleep<TitleGate>(scene, !forShot, log, "표제");
            Sleep<IntroController>(scene, !forShot, log, "어명");
            Sleep<NoiseMeter>(scene, !forShot, log, "소리계");

            // ③ 어전 불. 표제가 재워졌으면 불을 켤 사람이 없다
            var amb = forShot ? new Color(0.10f, 0.10f, 0.12f) : new Color(0.012f, 0.012f, 0.018f);
            RenderSettings.ambientLight = amb;
            log.AppendLine("── 환경광 " + (forShot ? "밝힘(표제가 재워져 불 켤 사람이 없다)" : "도로 어둡게"));

            // ④ 문서 셋 — 굴리기를 붙이거나 뗀다
            var docs = Find(scene, "Documents");
            if (docs == null) { log.AppendLine("── Documents 를 못 찾았다"); }
            else
            {
                var roll = docs.GetComponent<ScrollRollIn>();
                if (forShot)
                {
                    if (roll == null) roll = UnityEditor.Undo.AddComponent<ScrollRollIn>(docs.gameObject);
                    var so = new SerializedObject(roll);
                    so.FindProperty("_playOnStart").boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(roll);
                    log.AppendLine("── 문서 셋이 재생과 함께 굴러온다 ("
                                 + so.FindProperty("_delay").floatValue + "초 뒤부터 · 한 통에 "
                                 + so.FindProperty("_seconds").floatValue + "초 · "
                                 + so.FindProperty("_stagger").floatValue + "초 시차)");
                }
                else if (roll != null)
                {
                    UnityEditor.Undo.DestroyObjectImmediate(roll);
                    log.AppendLine("── 굴리기를 뗐다");
                }

                // 문서를 집을 수 있게 켜 둔다(표제가 재워 두던 것)
                foreach (Transform d in docs) d.gameObject.SetActive(true);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            if (forShot)
                log.AppendLine("\n★ 다 찍었으면 [이문록 ▸ 영상 ▸ 인트로: 봉서 굴러오는 장면 되돌리기] 를 누르십시오.\n"
                             + "   지금은 게임을 켜도 표제가 안 뜹니다.");
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// 부품을 재우거나 깨운다.
        ///
        /// <b>enabled 를 내리는 것으로는 모자란다.</b> 유니티는 부품이 꺼져 있어도
        /// <c>Awake</c> 는 부른다 — 오브젝트가 살아 있기만 하면. 표제의 Awake 는
        /// 문서 셋을 감추고 환경광을 캄캄하게 내리므로, 부품만 꺼 두면
        /// <b>문서가 통째로 사라진 캄캄한 화면</b>이 남는다. 실제로 그렇게 됐다.
        /// 오브젝트째 재운다.
        /// </summary>
        private static void Sleep<T>(Scene scene, bool awake, System.Text.StringBuilder log, string name)
            where T : MonoBehaviour
        {
            int n = 0;
            foreach (var c in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                UnityEditor.Undo.RecordObject(c.gameObject, "재우기");
                UnityEditor.Undo.RecordObject(c, "재우기");
                c.gameObject.SetActive(awake);

                // <b>깨울 때는 부품도 켠다.</b> 이 도구의 첫 판은 오브젝트가 아니라
                // 부품을 껐다(그것만으로는 Awake 가 막히지 않아 오브젝트째 끄는 쪽으로
                // 고쳤다). 그런데 되돌리기는 오브젝트만 도로 켰으므로, 그때 꺼진
                // 부품이 씬에 그대로 저장돼 남았다 — 표제는 Awake 로 화면을 캄캄하게
                // 내려놓고 Start 가 안 돌아 글씨도 누름판도 세우지 못했고, Update 가
                // 안 도니 아무 키도 안 먹었다. 게임이 검은 화면에서 죽어 있었다.
                if (awake) c.enabled = true;

                EditorUtility.SetDirty(c.gameObject);
                EditorUtility.SetDirty(c);
                n++;
            }
            if (n > 0) log.AppendLine("── " + name + " " + (awake ? "깨웠다" : "재웠다(오브젝트째)"));
        }

        private static Transform Find(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root.transform;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t;
            }
            return null;
        }
    }
}
