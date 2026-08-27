using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>발이 벽으로 보이던 것을 발로 되돌린다.</b>
    /// 메뉴: [이문록 ▸ 엔딩 ▸ 발 너머로 고치기]
    ///
    /// 재 보니 답이 한 줄에 있었다 — <b>발 뒤의 빛 0개, 앞의 빛 1개.</b>
    ///
    /// 발은 <b>뒤에서 빛이 와야</b> 발이다. 앞에서 때리면 그저 가로줄이 그어진 널이고,
    /// 뒤에서 오면 살 사이로 빛이 새고 그 너머의 것이 <b>어렴풋이 비친다</b>.
    /// 「왕은 안 보이고 발 너머 목소리만」이라는 말은 <b>안 보인다</b>는 뜻이 아니라
    /// <b>보일 듯 말 듯하다</b>는 뜻이다. 아주 안 보이면 그냥 벽을 보고 말하는 것이 된다.
    ///
    /// 셋을 고친다:
    ///   ① <b>어좌 뒤에 불을 켠다</b> — 발이 뒤에서 빛을 받아 살이 갈라져 보이고,
    ///      어좌가 그 위에 그림자로 앉는다. 사람은 안 보이되 <b>자리는 보인다</b>.
    ///   ② <b>발장대를 화면에 들인다</b> — 위가 잘려 있으면 매달린 것인지 세운 것인지
    ///      알 수가 없다. 무엇에 매달렸는지가 보여야 발이다.
    ///   ③ <b>부복시킨다</b> — 눈높이 1.05m 는 <b>서 있는 사람</b>이다. 어전에서 왕 앞에
    ///      선 어사는 없다. 낮추고 조금 올려다보게 하면 그 각이 곧 「부복」이 된다.
    ///
    /// 곁들여 촛불을 정리한다 — 같은 자리에 <b>여섯</b>이 겹쳐 있었다(짓는 도구를
    /// 세 번 눌러 세 벌이 선 것이다). 겹친 불은 밝기만 세 배가 되고 그림자는 세 겹이 된다.
    ///
    /// 두 번 눌러도 두 벌이 안 선다.
    /// </summary>
    public static class EndingVeil
    {
        private const string ScenePath = "Assets/_Project/Scenes/Core/EndingScene.unity";
        private const string BackLight = "어좌_뒷불";

        [MenuItem("이문록/엔딩/발 너머로 고치기")]
        public static void Fix()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[엔딩] 재생을 멈추고 다시 누르십시오.");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var log = new System.Text.StringBuilder("[엔딩] 발 너머로\n");

            Transform hall = null, veil = null, seat = null, rail = null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "어전") hall = t;
                    else if (t.name == "발") veil = t;
                    else if (t.name == "어좌") seat = t;
                    else if (t.name == "발장대") rail = t;
                }
            if (veil == null || seat == null) { Debug.LogWarning(log + "── 발이나 어좌를 못 찾았다"); return; }

            // ── 발의 실제 크기를 잰다. 값을 새로 정하지 않고 <b>있는 것에서 뽑는다</b>.
            Bounds vb = new Bounds(); bool f = true;
            foreach (var r in veil.GetComponentsInChildren<Renderer>(true))
            { if (f) { vb = r.bounds; f = false; } else vb.Encapsulate(r.bounds); }
            log.AppendLine("── 발: 위 " + vb.max.y.ToString("F2") + " · 아래 " + vb.min.y.ToString("F2")
                         + " · z " + vb.center.z.ToString("F2") + " · 너비 " + vb.size.x.ToString("F2"));

            // ── ① 겹친 촛불을 걷는다 ─────────────────────
            var seen = new System.Collections.Generic.Dictionary<string, Transform>();
            int dropped = 0;
            var all = new System.Collections.Generic.List<Transform>();
            foreach (var t in hall != null ? hall.GetComponentsInChildren<Transform>(true)
                                           : new Transform[0]) all.Add(t);
            foreach (var t in all)
            {
                if (t == null || t.GetComponent<Light>() == null) continue;
                if (t.name.IndexOf("촛불") < 0) continue;
                string key = t.name + "@" + t.position.ToString("F2");
                if (seen.ContainsKey(key)) { Object.DestroyImmediate(t.gameObject); dropped++; }
                else seen[key] = t;
            }
            if (dropped > 0) log.AppendLine("── 같은 자리에 겹친 촛불 " + dropped + "개를 걷었다(밝기가 그만큼 부풀어 있었다)");

            // ── ② 어좌 뒤에 불 ──────────────────────────
            Transform back = null;
            foreach (var t in hall.GetComponentsInChildren<Transform>(true))
                if (t.name == BackLight) back = t;
            if (back == null)
            {
                var go = new GameObject(BackLight);
                go.transform.SetParent(hall, false);
                back = go.transform;
                log.AppendLine("── 어좌 뒤에 불을 켰다");
            }
            else log.AppendLine("── 어좌 뒷불은 이미 있다 — 값만 다시 잡는다");

            // 어좌보다 <b>더 뒤에, 조금 높이</b>. 발과 어좌 사이가 아니라 어좌 너머라야
            // 어좌가 빛을 등지고 <b>실루엣</b>이 된다.
            back.position = new Vector3(0f, seat.position.y + 1.15f, seat.position.z + 1.30f);
            var lit = back.GetComponent<Light>();
            if (lit == null) lit = back.gameObject.AddComponent<Light>();
            lit.type = LightType.Point;
            lit.color = new Color(1f, 0.82f, 0.55f);      // 등잔빛
            lit.intensity = 3.4f;
            lit.range = 7.5f;
            lit.shadows = LightShadows.Soft;               // 어좌가 발에 그림자로 앉아야 한다
            lit.shadowStrength = 0.85f;

            // ── ③ 카메라 — 부복시키고 발장대를 화면에 들인다 ──
            var cam = Camera.main;
            if (cam == null)
                foreach (var root in scene.GetRootGameObjects())
                    if (root.name == "Main Camera") cam = root.GetComponent<Camera>();
            if (cam != null)
            {
                var was = cam.transform.position;
                float top = rail != null ? rail.position.y + 0.20f : vb.max.y + 0.25f;

                // 눈높이를 낮춰 부복시키고, 뒤로 물러 발 전체와 그 위 장대까지 담는다.
                var eye = new Vector3(0f, 0.78f, -3.35f);
                cam.transform.position = eye;

                // 발장대가 화면 위 0.93 쯤에 오게 각을 <b>계산해서</b> 잡는다.
                // 눈대중으로 각을 주면 발 높이를 바꿀 때마다 다시 맞춰야 한다.
                float half = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                float dz = vb.center.z - eye.z;
                float wantUp = (top - eye.y) / dz;                    // 장대까지의 기울기
                float pitch = Mathf.Atan(wantUp) * Mathf.Rad2Deg
                              - Mathf.Atan(half * 0.86f) * Mathf.Rad2Deg;
                cam.transform.rotation = Quaternion.Euler(-pitch, 0f, 0f);

                log.AppendLine("── 카메라 " + was.ToString("F2") + " → " + eye.ToString("F2")
                             + " · 올려봄 " + pitch.ToString("F1") + "도 (부복한 각)");
            }

            EditorUtility.SetDirty(hall.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(log.ToString());
        }
    }
}
