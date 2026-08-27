using UnityEngine;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// **목표 표시** — 조건이 서면 그 자리에 은은한 빛이 맴돈다 (2026-08-24, 서고 창고방 서랍장).
    ///
    /// ■ 왜 광원이 아니라 빛나는 판인가
    ///   URP 추가 광원은 한 물체에 넷까지다. 서고 아래층에는 이미 다섯 개가 있어
    ///   하나를 더 두면 어느 벽에서 하나가 조용히 꺼진다. 자기만 밝은 판이면 예산을 안 쓴다.
    ///
    /// ■ 게임은 "후고로 가라"고 말하지 않는다
    ///   3단계에서 드러나는 것은 「崔」와 「後庫」뿐이다. 그것을 읽고 방을 떠올린 사람에게만
    ///   이 표시가 눈에 들어오도록, 문구 대신 **빛 한 점**으로만 알린다 (기획 요구).
    /// </summary>
    public class LedgerGoalGlow : MonoBehaviour
    {
        [Tooltip("이 진행 플래그가 서야 나타난다")]
        public string requireFlag = GyeonuWorld.F_후고단서;

        [Tooltip("이 물건을 아직 안 집었을 때만 나타난다 (비우면 늘 나타난다)")]
        public ItemPickup until;

        [Tooltip("맴도는 주기(초)")]
        public float period = 2.4f;

        [Tooltip("판 크기(m)")]
        public float size = 0.26f;

        public Color tint = new Color(1f, 0.86f, 0.55f);

        Renderer quad;
        MaterialPropertyBlock mpb;
        Texture2D tex;          // ⚠️ static 캐시 금지 — 도메인 리로드가 꺼진 프로젝트
        Transform eye;

        void OnDestroy()
        {
            if (tex != null) Destroy(tex);
            if (quad != null && quad.sharedMaterial != null) Destroy(quad.sharedMaterial);
        }

        void LateUpdate()
        {
            bool on = GyeonuWorld.Has(requireFlag) && (until == null || until.gameObject.activeInHierarchy);
            if (!on) { if (quad != null) quad.enabled = false; return; }

            Ensure();
            quad.enabled = true;

            // 늘 눈을 마주 본다 — 어느 쪽에서 들어와도 같은 크기로 보인다
            if (eye == null)
            {
                var cam = Camera.main;
                if (cam != null) eye = cam.transform;
            }
            if (eye != null) quad.transform.rotation = Quaternion.LookRotation(quad.transform.position - eye.position);

            float t = Mathf.Sin(Time.time * Mathf.PI * 2f / Mathf.Max(0.2f, period));
            float a = 0.30f + 0.26f * (t * 0.5f + 0.5f);
            quad.transform.localScale = Vector3.one * size * (0.92f + 0.10f * (t * 0.5f + 0.5f));
            mpb.SetColor("_Color", new Color(tint.r, tint.g, tint.b, a));
            quad.SetPropertyBlock(mpb);
        }

        void Ensure()
        {
            if (quad != null) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "목표빛";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;

            var mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = Blob() };
            mat.renderQueue = 3800;
            quad = go.GetComponent<Renderer>();
            quad.sharedMaterial = mat;
            quad.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mpb = new MaterialPropertyBlock();
        }

        /// <summary>가운데가 밝고 가장자리로 사그라지는 동그란 빛.</summary>
        Texture2D Blob()
        {
            const int res = 128;
            tex = new Texture2D(res, res, TextureFormat.RGBA32, false)
            { name = "목표빛_텍스처", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            float c = (res - 1) * 0.5f;
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    // 심이 또렷하고 둘레가 넓게 번지는 모양 — 먼 데서도 점으로 보인다
                    float a = Mathf.Clamp01(Mathf.Exp(-d * d * 7f)) * 0.75f + Mathf.Clamp01(1f - d) * 0.25f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a) * (d < 1f ? 1f : 0f)));
                }
            tex.Apply();
            return tex;
        }
    }
}
