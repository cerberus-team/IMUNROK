using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using IMUNROK.Common;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>서고와 도구를 손본다.</b> 메뉴: [이문록 ▸ 관아 ▸ ㉕ 서고와 도구 손보기]
    ///
    /// 직접 한 바퀴 돌아 본 뒤에 나온 것들이다. 하나같이 <b>설계가 아니라
    /// 물려 둔 값</b>이 틀린 것이라, 코드는 안 고치고 씬과 재질만 바로잡는다.
    ///
    /// <b>㉠ 마패가 손에 안 잡혔다.</b> 도구벨트에서 골라도 아무것도 안 나타났다.
    ///    <see cref="HeldToolModel"/> 의 <c>_model</c> 이 <c>마패</c>(껍데기)가 아니라
    ///    그 <b>자식</b> <c>default</c> 를 가리키고 있었다. 부모가 꺼져 있으니 자식을
    ///    켜 봐야 안 보인다 — 유니티에서 가장 조용히 죽는 꼴이다. 껍데기를 가리키게 한다.
    ///
    /// <b>㉡ 등경에 얹은 등불이 누워 있었다.</b> 걸이에 걸린 것이 아니라 기둥에 꿰인
    ///    꼴이었다. 까닭은 <b>모델의 세로축이 z</b>라는 것이다 — 손에 든 등불
    ///    (<c>등불_모델</c>)에는 (270,180,0)이 걸려 있어 바로 서는데, 얹는 등불에는
    ///    아무 기울기도 없었다. 게다가 크기를 <b>y</b>(0.0109)로 재서 31.11 이 나왔다.
    ///    바로 세우고, 손에 든 것과 같은 19.5 로 맞추고, 걸이 밑에 매단다.
    ///    <b>불꽃</b>은 더 딱했다 — 31배로 커진 부모 밑에서 로컬 0.24 가
    ///    <b>7.5m</b> 가 되어, 서고 지붕 위에서 방을 내리쬐고 있었다.
    ///    "한쪽만 너무 밝다"던 것이 이것이다.
    ///
    /// <b>㉢ 서고가 너무 어두웠다.</b> 어두운 것은 <see cref="RoomDarkness"/> 가 일부러
    ///    하는 일이고 그래야 등불이 값을 한다. 다만 0.16 은 <b>대장이 어디 있는지도</b>
    ///    안 보이는 어둠이라 뒤질 수가 없다. 걸어 다닐 만큼만 올린다(0.24) — 잔글씨는
    ///    여전히 불을 대야 읽힌다.
    ///
    /// <b>㉣ 동헌 방 서벽에 구멍이 있었다.</b> 안에서 사방으로 쏘아 훑으니
    ///    z 5.20 · y 2.25~3.55 한 줄만 광선이 <b>빠져나간다</b>. 모델의 벽널 사이가
    ///    5cm 벌어져 하늘이 비쳤다. 그 자리에 널 한 장을 덧댄다.
    ///
    /// <b>㉤ 유척이 놋쇠로 안 보였다.</b> 재질의 바탕색이 잿빛(0.78)이라 놋쇠 그림 위에
    ///    잿빛을 덮어쓰고 있었다. 놋빛으로 물린다. <b>마패</b>는 반대로 거울(매끄러움 1.0,
    ///    쇠 1.0)이라 어두운 데서 <b>비출 것이 없어 새까맸다</b> — 놋쇠는 거울이 아니다.
    ///
    /// 두 번 눌러도 두 벌이 안 선다.
    /// </summary>
    public static class SeogoPolish
    {
        [MenuItem("이문록/관아/㉕ 서고와 도구 손보기")]
        public static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.name.Contains("Gwana"))
            {
                Debug.LogWarning("[관아] 관아 씬을 열고 누르십시오(지금은 " + scene.name + ").");
                return;
            }

            var log = new System.Text.StringBuilder("[관아] 서고와 도구를 손본다\n");
            Mapae(scene, log);
            Lantern(scene, log);
            Dark(scene, log);
            Patch(scene, log);
            Metals(log);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
        }

        // ── ㉠ 마패 ───────────────────────────────

        private static void Mapae(Scene scene, System.Text.StringBuilder log)
        {
            foreach (var h in Object.FindObjectsByType<HeldToolModel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(h);
                var slot = so.FindProperty("_model");
                var cur = slot.objectReferenceValue as GameObject;

                // 껍데기는 <b>이 부품이 달린 것의 바로 밑 자식</b>이다. 그보다 깊은 데를
                // 가리키고 있으면, 그 위의 어느 하나가 꺼져 있는 한 켜도 안 보인다.
                if (cur == null) continue;
                if (cur.transform.parent == h.transform) continue;      // 이미 껍데기다

                var shell = cur.transform;
                while (shell.parent != null && shell.parent != h.transform) shell = shell.parent;
                if (shell.parent != h.transform) continue;

                slot.objectReferenceValue = shell.gameObject;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(h);
                log.AppendLine("── " + h.name + " : 손에 들 것을 " + cur.name + " → " + shell.name + " 으로 (부모가 꺼져 있어 안 보였다)");
            }

            foreach (var h in Object.FindObjectsByType<HeldToolModel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Center(h.transform, log);
        }

        /// <summary>
        /// <b>그림이 자리에서 벗어나 있으면 끌어다 앉힌다.</b>
        ///
        /// 마패를 켜 놓고도 안 보였다. 켜져 있고 지워지지도 않았는데 화면 밖이었다 —
        /// 껍데기에 <b>-51.5cm</b> 가 적혀 있어서, 손에 든 자리보다 반 자 아래에
        /// 매달려 눈 밑으로 내려가 있었다. fbx 를 들일 때 딸려 온 값이다.
        ///
        /// <see cref="HeldRig"/> 는 <b>매단 자리</b>와 <b>크기</b>만 맞춘다. 그 밑에서
        /// 그림이 또 한 번 밀려나 있으면 손댈 데가 없다. 여기서 <b>재서</b> 끌어온다.
        ///
        /// 25cm 넘게 벗어난 것만 건드린다 — 유척은 15cm 비껴 있는데 그건 자를
        /// 비스듬히 뉘어 든 것이라 일부러 그런 것이다.
        /// </summary>
        private static void Center(Transform holder, System.Text.StringBuilder log)
        {
            foreach (Transform shell in holder)
            {
                Vector3 c;
                if (!MeshCenter(shell, out c)) continue;
                Vector3 off = c - holder.position;
                if (off.magnitude < 0.25f) continue;

                Undo.RecordObject(shell, "그림 끌어오기");
                shell.localPosition -= holder.InverseTransformVector(off);
                log.AppendLine("── " + holder.name + "/" + shell.name + " : 그림이 "
                             + off.magnitude.ToString("F2") + "m 벗어나 있어 자리로 끌어왔다");
            }
        }

        /// <summary>꺼져 있어도 잴 수 있게, 그물에서 곧장 잰다.</summary>
        private static bool MeshCenter(Transform t, out Vector3 center)
        {
            center = Vector3.zero;
            bool any = false;
            var lo = new Vector3(9e9f, 9e9f, 9e9f);
            var hi = new Vector3(-9e9f, -9e9f, -9e9f);
            foreach (var mf in t.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var p = b.center + Vector3.Scale(b.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var w = mf.transform.TransformPoint(p);
                    lo = Vector3.Min(lo, w); hi = Vector3.Max(hi, w);
                    any = true;
                }
            }
            if (!any) return false;
            center = (lo + hi) * 0.5f;
            return true;
        }

        // ── ㉡ 등경에 얹은 등불 ───────────────────

        /// <summary>손에 든 등불이 바로 서는 기울기. 모델의 세로축이 z 라서 이만큼 눕혀야 선다.</summary>
        private static readonly Vector3 Upright = new Vector3(270f, 180f, 0f);

        /// <summary>손에 든 등불과 같은 크기(HeldRig). 얹은 것이 든 것보다 크면 물건이 바뀐 것이 된다.</summary>
        private const float LampScale = 19.5f;

        private static void Lantern(Scene scene, System.Text.StringBuilder log)
        {
            var stand = Find(scene, "등경");
            var prop = Find(scene, "놓인등불");
            if (stand == null || prop == null) { log.AppendLine("── 등경이나 놓인등불을 못 찾았다"); return; }

            var hook = Deep(stand, "걸이");
            float hookY = hook != null ? hook.position.y : stand.position.y + 1.11f;
            float hookZ = hook != null ? hook.position.z : stand.position.z;

            Undo.RecordObject(prop, "등불 걸기");
            prop.localScale = Vector3.one * LampScale;
            prop.localRotation = Quaternion.Euler(Upright);

            // 걸이 밑에 <b>매단다</b>. 모델의 한가운데가 축이므로, 꼭대기를 걸이에
            // 맞추려면 반 키만큼 내려 앉힌다.
            var mf = prop.GetComponent<MeshFilter>();
            float tall = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.size.z * LampScale : 0.37f;
            prop.position = new Vector3(stand.position.x, hookY - tall * 0.5f, hookZ);

            log.AppendLine("── 등불을 바로 세워 걸이(y " + hookY.ToString("F2") + ") 밑에 매달았다 · 크기 "
                         + LampScale.ToString("F1") + " (손에 든 것과 같다, 키 " + tall.ToString("F2") + "m)");

            // 불꽃 — 31배 부모 밑에서 로컬 0.24 가 7.5m 였다. 등불 몸통 안으로 넣는다.
            var flame = Deep(prop, "불");
            if (flame != null)
            {
                Undo.RecordObject(flame, "불꽃 자리");
                // <b>로컬 값은 부모 배율을 탄다.</b> 부모가 19.5 배라 0.10 을 적으면
                // 1.95m 가 된다 — 여기서 한 번 더 밟은 함정이다(전에는 31배에 0.24 라
                // 7.5m 였다). 월드로 재서 앉힌다.
                flame.position = prop.position + Vector3.up * (tall * 0.15f);
                var lt = flame.GetComponent<Light>();
                if (lt != null)
                {
                    lt.range = 5.0f;
                    lt.intensity = 2.2f;
                    lt.color = new Color(1f, 0.78f, 0.50f);
                    lt.shadows = LightShadows.None;
                    EditorUtility.SetDirty(lt);
                }
                log.AppendLine("── 불꽃을 등불 속으로 (여태 지붕 위 7.5m 에서 방을 내리쬐고 있었다)");
            }
        }

        // ── ㉢ 서고 어둡기 ────────────────────────

        private static void Dark(Scene scene, System.Text.StringBuilder log)
        {
            var rd = Object.FindFirstObjectByType<RoomDarkness>(FindObjectsInactive.Include);
            if (rd == null) { log.AppendLine("── 서고_어둠을 못 찾았다"); return; }
            var so = new SerializedObject(rd);
            var p = so.FindProperty("_inside");
            float was = p.floatValue;
            p.floatValue = 0.24f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rd);
            log.AppendLine("── 서고 하늘빛 " + was.ToString("F2") + " → 0.24 (걸어 다닐 만큼만. 잔글씨는 여전히 불을 대야 읽힌다)");
        }

        // ── ㉣ 동헌 방 서벽 구멍 ──────────────────

        /// <summary>쏘아서 찾은 구멍 — 서벽(x 12.65) 의 z 5.20 한 줄, y 2.25~3.55.</summary>
        private static void Patch(Scene scene, System.Text.StringBuilder log)
        {
            var t = Find(scene, "동헌방_벽막이");
            if (t == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "동헌방_벽막이";
                Undo.RegisterCreatedObjectUndo(go, "벽막이");
                t = go.transform;
            }
            t.SetParent(null, true);
            t.position = new Vector3(12.68f, 2.95f, 5.20f);
            t.rotation = Quaternion.identity;
            t.localScale = new Vector3(0.10f, 1.60f, 0.28f);

            var mat = WallMaterial(scene);
            var mr = t.GetComponent<MeshRenderer>();
            if (mr != null && mat != null) mr.sharedMaterial = mat;

            log.AppendLine("── 서벽 구멍(z 5.20 · y 2.25~3.55)에 널을 덧댔다");
        }

        /// <summary>문서고 벽이 쓰는 결을 빌린다 — 같은 집의 같은 널이다.</summary>
        private static Material WallMaterial(Scene scene)
        {
            var wall = Find(scene, "벽");
            if (wall != null)
                foreach (var r in wall.GetComponentsInChildren<MeshRenderer>(true))
                    if (r.sharedMaterial != null) return r.sharedMaterial;
            return null;
        }

        // ── ㉤ 놋쇠와 구리 ────────────────────────

        private static void Metals(System.Text.StringBuilder log)
        {
            // 유척 — 잿빛을 덮어쓰고 있었다. 놋빛으로.
            var yu = LoadMat("M_유척_놋");
            if (yu != null)
            {
                yu.SetColor("_BaseColor", new Color(1.00f, 0.82f, 0.42f));
                if (yu.HasProperty("_Metallic")) yu.SetFloat("_Metallic", 0.85f);
                if (yu.HasProperty("_Smoothness")) yu.SetFloat("_Smoothness", 0.62f);
                EditorUtility.SetDirty(yu);
                log.AppendLine("── 유척 재질: 바탕 잿빛(0.78) → 놋빛 · 매끄러움 0.40 → 0.62");
            }

            // 마패 — 매끄러움 1.0 은 거울이다. 어두운 데서 비출 것이 없으면 새까맣다.
            var ma = LoadMat("M_마패_동");
            if (ma != null)
            {
                ma.SetColor("_BaseColor", new Color(0.95f, 0.72f, 0.45f));
                if (ma.HasProperty("_Metallic")) ma.SetFloat("_Metallic", 0.80f);
                if (ma.HasProperty("_Smoothness")) ma.SetFloat("_Smoothness", 0.55f);
                EditorUtility.SetDirty(ma);
                log.AppendLine("── 마패 재질: 거울(매끄러움 1.0) → 두들긴 구리(0.55)");
            }
        }

        private static Material LoadMat(string name)
        {
            foreach (var g in AssetDatabase.FindAssets(name + " t:Material"))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
                if (m != null && m.name == name) return m;
            }
            return null;
        }

        // ── 잔심부름 ──────────────────────────────

        private static Transform Deep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
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
