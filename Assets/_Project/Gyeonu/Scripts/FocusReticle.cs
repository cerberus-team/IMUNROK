using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 포커스 조작 중 <b>지금 어디를 겨누고 있는지</b> 보여 주는 조준점 (2026-08-25).
    ///
    /// ■ 왜 필요한가
    ///   <see cref="DebugFocusRig"/> 는 포커스에 들어가면서 <b>하드웨어 커서를 감춘다</b>
    ///   (창 밖으로 빠져나가지 않게 가두고 우리가 직접 그리기 위해서다). 그런데 그려 주는 쪽을
    ///   빠뜨리면 <b>아무것도 안 보이는 채로 조준해야 한다</b> — 렌즈 퍼즐에서 실제로 그랬다.
    ///   서고 장부가 먼저 겪고 판 위에 직접 그려 해결했고, 그 방식을 여기 공용으로 옮겼다.
    ///
    /// ■ 왜 월드 쿼드인가 (IMGUI가 아니라)
    ///   IMGUI는 VR HMD에 아예 보이지 않는다. 조준점은 조작의 일부라 반드시 월드에 있어야
    ///   지금도 스크린샷에 찍히고, 나중에 헤드셋에서도 그대로 보인다.
    ///
    /// ⚠️ 정적 캐시 금지 — 도메인 리로드가 꺼진 프로젝트에서 정적 텍스처 참조가 플레이 세션을
    ///    넘겨 살아남는데 내용은 죽는다(비네트·소지품 판에서 실측). 인스턴스로 들고 다닌다.
    /// </summary>
    public class FocusReticle
    {
        // ── 차림새 (2026-08-27 공개) ─────────────────────────────────────
        //   ⚠️ 값은 그대로다. 자기 조준점을 그리는 사람이 <b>같은 색·같은 굵기</b>로
        //      맞출 수 있게 이름을 붙여 열어 둔 것뿐이다.

        /// <summary>조준점 색 — 한지빛 미색. 알파는 <see cref="Place"/> 가 받은 값으로 덮는다.</summary>
        public static readonly Color Tint = new Color(1f, 0.93f, 0.72f, 1f);

        /// <summary>테 동그라미의 반지름 (0~1, 텍스처 중심에서의 거리).</summary>
        public const float RingRadius = 0.78f;
        /// <summary>테의 굵기 · 가운데 점의 크기 (같은 값을 쓴다).</summary>
        public const float RingWidth = 0.16f;
        /// <summary>정렬 순서 — 투명한 것들보다 뒤라 판·조각 위에 늘 얹힌다.</summary>
        public const int RenderQueue = 3850;

        GameObject go;
        Renderer rend;
        MaterialPropertyBlock mpb;
        Texture2D tex;

        /// <summary>겨눈 자리에 조준점을 세운다. <paramref name="faceNormal"/> 은 보는 쪽을 향한 법선.</summary>
        public void Place(Transform parent, Vector3 worldPos, Vector3 faceNormal, Vector3 up,
                          float size, float alpha)
        {
            Ensure(parent);
            go.SetActive(true);
            go.transform.position = worldPos;
            // ⚠️ 유니티 기본 Quad는 **−Z 면이 보인다**. forward를 보는 쪽으로 두면 뒤통수를
            //    보여 주게 돼 통째로 안 보인다(서고 장부에서 실측). 반대쪽을 향하게 세운다.
            go.transform.rotation = Quaternion.LookRotation(-faceNormal, up);
            go.transform.localScale = Vector3.one * size;
            var tint = Tint; tint.a = alpha;
            mpb.SetColor("_Color", tint);
            rend.SetPropertyBlock(mpb);
        }

        public void Hide()
        {
            if (go != null) go.SetActive(false);
        }

        public void Dispose()
        {
            if (go != null) { Object.Destroy(go); go = null; }
            if (tex != null) { Object.Destroy(tex); tex = null; }
            rend = null;
        }

        void Ensure(Transform parent)
        {
            if (mpb == null) mpb = new MaterialPropertyBlock();
            if (go != null) return;

            go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "포커스_조준점";
            Object.Destroy(go.GetComponent<Collider>());
            // 부모를 두면 대상이 돌아도 같이 따라 도는데, 자리는 매 프레임 월드로 다시 잡으므로
            // 부모는 **정리 편의**를 위한 것일 뿐이다 (대상이 사라지면 조준점도 함께 사라진다).
            go.transform.SetParent(parent, false);

            var mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = MakeTex() };
            mat.renderQueue = RenderQueue;   // 투명한 것들보다 뒤 — 판·조각 위에 늘 얹힌다
            rend = go.GetComponent<Renderer>();
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }

        /// <summary>가운데가 빈 동그라미 + 가운데 점 — 겨눈 자리를 가리지 않으면서 눈에 걸린다.</summary>
        Texture2D MakeTex()
        {
            const int res = 64;
            tex = new Texture2D(res, res, TextureFormat.RGBA32, false)
            {
                name = "포커스_조준점_텍스처",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
            };
            float c = (res - 1) * 0.5f;
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(d - RingRadius) / RingWidth);
                    float dot = Mathf.Clamp01(1f - d / RingWidth);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Max(ring * ring, dot)));
                }
            tex.Apply();
            return tex;
        }
    }
}
