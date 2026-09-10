using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>부를 사람의 얼굴</b> — 뜰에 선 그 사람을 그 자리에서 찍어 이름표에 얹는다.
    ///
    /// <b>왜 얼굴인가.</b> 동헌의 부르기 판은 여태 <b>이름 넉 자</b>였다. 그런데 이 사건에서
    /// 가르는 것이 바로 <b>얼굴</b>이다 — 甲과 乙은 닮았고, 어느 쪽이 어느 쪽인지 헷갈리는
    /// 것이 이야기의 뼈대다. 그 둘을 「甲」「乙」이라는 <b>글자</b>로만 고르게 하면, 정작
    /// 플레이어는 두 사람을 한 번도 나란히 본 적 없이 판결까지 간다.
    ///
    /// <b>그림을 따로 그리지 않는다.</b> 초상을 한 장씩 그려 넣으면 사람이 옷을 갈아입거나
    /// 모델이 바뀔 때마다 그림도 같이 고쳐야 하고, 대개는 안 고쳐서 <b>이름표의 얼굴과
    /// 뜰에 선 사람이 다른</b> 날이 온다. 그래서 뜰에 <b>실제로 서 있는 그 사람</b>을 찍는다.
    /// 소매를 걷으면 걷은 채로, 발이 내려오면 내려온 채로 찍힌다.
    ///
    /// <b>값을 아끼는 법</b>
    ///   · 사람마다 카메라 하나. 다만 <b>명단이 떠 있는 동안만</b> 켠다(<see cref="Show"/>).
    ///   · 192칸 짜리 작은 그림이고, 뒤처리도 그림자도 끈다.
    ///   · 자름면을 코앞(0.05~1.1m)으로 좁혀 <b>얼굴만</b> 남긴다 — 뒤의 담이나 기둥이
    ///     같이 찍히면 이름표가 아니라 창문이 된다.
    ///   · 불을 하나 딸려 보낸다. 동헌 뜰은 밤이라, 안 그러면 <b>까만 네모 다섯</b>이 뜬다.
    ///     닿는 거리를 1.2m 로 묶어 두므로 방은 그대로 어둡다.
    /// </summary>
    public static class FacePortrait
    {
        private const int Size = 192;

        private class Shot
        {
            public Camera cam;
            public RenderTexture rt;
            public GameObject go;
        }

        private static readonly Dictionary<Transform, Shot> Shots = new Dictionary<Transform, Shot>();

        /// <summary>
        /// 이 사람의 얼굴 그림. 없으면 만든다.
        /// <paramref name="head"/> 는 머리뼈, <paramref name="body"/> 는 사람의 뿌리
        /// (어느 쪽을 보고 섰는지 알아야 앞에서 찍는다).
        /// </summary>
        public static RenderTexture Of(Transform head, Transform body)
        {
            if (head == null) return null;

            Shot s;
            if (Shots.TryGetValue(head, out s) && s.cam != null) { Place(s, head, body); return s.rt; }

            s = new Shot();
            s.rt = new RenderTexture(Size, Size, 16, RenderTextureFormat.ARGB32)
            {
                name = "얼굴_" + (body != null ? body.name : head.name),
                antiAliasing = 1,          // MSAA 렌더텍스처는 그대로 못 쓴다(청록 한 장이 된다)
            };
            s.rt.Create();

            s.go = new GameObject("얼굴찍개_" + (body != null ? body.name : head.name)) { hideFlags = HideFlags.DontSave };
            s.cam = s.go.AddComponent<Camera>();
            s.cam.clearFlags = CameraClearFlags.SolidColor;
            s.cam.backgroundColor = new Color(0.06f, 0.05f, 0.045f, 1f);   // 먹빛 바탕
            s.cam.fieldOfView = 26f;
            s.cam.nearClipPlane = 0.05f;
            s.cam.farClipPlane = 1.10f;                                    // 얼굴만 남긴다
            s.cam.targetTexture = s.rt;
            s.cam.enabled = false;

            var extra = s.go.GetComponent<UniversalAdditionalCameraData>();
            if (extra == null) extra = s.go.AddComponent<UniversalAdditionalCameraData>();
            extra.renderPostProcessing = false;
            extra.renderShadows = false;

            var lightGo = new GameObject("얼굴불");
            lightGo.transform.SetParent(s.go.transform, false);
            lightGo.transform.localPosition = new Vector3(0.12f, 0.16f, 0.05f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.94f, 0.84f);
            l.intensity = 3.2f;
            l.range = 1.2f;
            l.shadows = LightShadows.None;

            Shots[head] = s;
            Place(s, head, body);
            return s.rt;
        }

        /// <summary>얼굴 앞에 선다. 사람이 걸어 나가면 따라가야 하므로 볼 때마다 다시 잡는다.</summary>
        private static void Place(Shot s, Transform head, Transform body)
        {
            Vector3 fwd = body != null ? body.forward : head.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            fwd.Normalize();

            // 눈높이보다 조금 위에서 살짝 내려다본다 — 정면에서 곧게 찍으면 증명사진이 된다.
            Vector3 at = head.position + fwd * 0.52f + Vector3.up * 0.06f;
            s.go.transform.position = at;
            s.go.transform.rotation = Quaternion.LookRotation(head.position + Vector3.up * 0.02f - at, Vector3.up);
        }

        /// <summary>
        /// 찍개를 켜고 끈다. <b>명단이 떠 있는 동안만</b> 켠다 — 안 보는 그림을
        /// 매 칸 다시 그릴 까닭이 없다.
        /// </summary>
        public static void Show(bool on)
        {
            foreach (var kv in Shots)
            {
                var s = kv.Value;
                if (s == null || s.go == null) continue;
                if (on && kv.Key != null) Place(s, kv.Key, kv.Key.root);
                s.go.SetActive(on);
                if (s.cam != null) s.cam.enabled = on;
            }
        }

        /// <summary>씬을 갈아 끼울 때 걷는다. 안 걷으면 없는 사람의 얼굴을 계속 그린다.</summary>
        public static void Clear()
        {
            foreach (var kv in Shots)
            {
                var s = kv.Value;
                if (s == null) continue;
                if (s.go != null) Object.Destroy(s.go);
                if (s.rt != null) { s.rt.Release(); Object.Destroy(s.rt); }
            }
            Shots.Clear();
        }
    }
}
