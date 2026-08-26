using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IMUNROK.Gyeonu
{
    /// <summary>
    /// 비밀지도를 펴 들면 바닥에 은하수처럼 깔리는 길 안내.
    ///
    /// 2026-08-18 개편 — 고정 경로에서 **플레이어 위치 기준 동적 경로**로 바꿨다.
    /// 어디에 서 있든 지금 자리에서 견우마을 입구까지의 길이 그려지고, 움직이면 다시 그린다.
    /// 경로 계산은 <see cref="StarPathRouter"/>(웨이포인트 그래프 + A*)가 맡는다.
    ///
    /// ■ 왜 파티클이 아닌가
    ///   관측실 천장 별·혼상 별과 같은 방식(가산 셰이더 메시)이다. 파티클은 에디터 포커스를
    ///   잃으면 시뮬레이션이 멎어 검증 중에 사라지고, 수천 개면 VR 90fps에서 CPU를 먹는다.
    ///
    /// ■ 다시 그리는 시점
    ///   매 프레임 다시 만들면 낭비다. 플레이어가 <see cref="rebuildDistance"/> 이상 움직였거나
    ///   목적지가 바뀌었을 때만 새로 만든다. 메시는 하나만 두고 내용을 갈아 끼운다.
    /// </summary>
    [AddComponentMenu("이문록/별 길 안내 (StarPathGuide)")]
    [DisallowMultipleComponent]
    public class StarPathGuide : MonoBehaviour
    {
        /// <summary>연출 강도 — 비교용 3단. 빌더 메뉴에서 갈아 끼운다.</summary>
        public enum Strength { 은은, 표준, 강렬 }

        [Header("경로")]
        public StarPathRouter router;
        [Tooltip("도착지. 비우면 destinationPoint를 쓴다.")]
        public Transform destination;
        public Vector3 destinationPoint;

        [Header("연출 강도")]
        // 2026-08-18 B안(표준) 확정. 은은은 낮에 묻히고 강렬은 바닥이 하얗게 뭉친다.
        public Strength strength = Strength.표준;

        [Header("드러나는 범위")]
        [Tooltip("플레이어 앞으로 이만큼까지 그린다(m). 0이면 목적지까지 전부")]
        public float aheadDistance = 90f;

        [Tooltip("플레이어가 이만큼 움직이면 경로를 다시 계산한다(m)")]
        public float rebuildDistance = 1.5f;

        [Header("디버그")]
        // 2026-08-23 — **정식 경로가 생겼다.** 이제 소지품에서 타공 비밀지도를 「펼쳐보기」하면
        // 켜지고 「접기」하면 꺼진다 (SecretMapUse). 선행 조건은 관측실에서 길을 밝혔을 것.
        // 이 키는 지도 없이 길만 확인할 때 쓰는 **디버그 수단으로만** 남겨 둔다.
        // 조건을 안 보므로 실제 진행 상태를 확인할 때는 이것 말고 지도로 켜 볼 것.
        [Tooltip("디버그 전용 즉시 토글 — 조건을 보지 않는다. Key.None으로 두면 꺼진다")]
        public Key debugKey = Key.M;
        public bool showOnStart = false;

        [Header("안내 문구")]
        public string onMessage = "비밀지도를 펴 들자, 발밑으로 별빛이 흘러 길을 그린다.";
        public string offMessage = "지도를 접자 별빛이 스러진다.";

        [Header("재질 (빌더가 채운다)")]
        public Material material;

        bool _visible;
        Transform _player;
        Vector3 _lastBuiltAt = new Vector3(9999f, 9999f, 9999f);
        MeshFilter _mf;
        MeshRenderer _mr;
        Mesh _mesh;

        public bool IsVisible => _visible;
        /// <summary>마지막으로 그린 경로 점 수 — 검증용.</summary>
        public int LastPointCount { get; private set; }
        /// <summary>마지막으로 그린 별 개수 — 검증용.</summary>
        public int LastStarCount { get; private set; }

        // 강도별 (별/m, 반폭, 크기배수, 밝기배수)
        static readonly float[,] Table =
        {
            //  밀도   반폭   크기   밝기
            {  26f,  1.05f, 1.00f, 1.00f },   // 은은
            {  48f,  1.30f, 1.35f, 1.90f },   // 표준
            {  80f,  1.55f, 1.70f, 3.10f },   // 강렬
        };

        void Awake()
        {
            var go = new GameObject("별길_메시") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            _mf = go.AddComponent<MeshFilter>();
            _mr = go.AddComponent<MeshRenderer>();
            _mr.sharedMaterial = material;
            _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _mr.receiveShadows = false;
            _mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            _mesh = new Mesh { name = "별길_동적", hideFlags = HideFlags.DontSave };
            _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            _mf.sharedMesh = _mesh;
            _mr.enabled = false;
        }

        void Start() => Show(showOnStart, silent: true);

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && debugKey != Key.None && kb[debugKey].wasPressedThisFrame)
                Show(!_visible);

            if (!_visible) return;

            var p = Player();
            if (p == null) return;
            if ((p.position - _lastBuiltAt).sqrMagnitude < rebuildDistance * rebuildDistance) return;
            Rebuild();
        }

        Transform Player()
        {
            if (_player != null) return _player;
            var w = FindFirstObjectByType<DebugWalkController>(FindObjectsInactive.Exclude);
            _player = w != null ? w.transform : (Camera.main != null ? Camera.main.transform : null);
            return _player;
        }

        Vector3 Destination()
            => destination != null ? destination.position : destinationPoint;

        /// <summary>지도를 펴거나(true) 접는다(false).</summary>
        public void Show(bool on, bool silent = false)
        {
            _visible = on;
            if (_mr != null) _mr.enabled = on;

            if (!on)
            {
                if (!silent && !string.IsNullOrEmpty(offMessage)) DebugToast.ShowPinned(offMessage);
                return;
            }

            _lastBuiltAt = new Vector3(9999f, 9999f, 9999f);
            Rebuild();
            if (!silent && !string.IsNullOrEmpty(onMessage)) DebugToast.ShowPinned(onMessage);
        }

        /// <summary>지금 위치에서 목적지까지 경로를 새로 구해 메시를 갈아 끼운다.</summary>
        public void Rebuild()
        {
            var p = Player();
            if (p == null || router == null) return;

            _lastBuiltAt = p.position;

            var pts = router.Route(p.position, Destination());
            LastPointCount = pts.Count;
            if (pts.Count < 2) { _mesh.Clear(); LastStarCount = 0; return; }

            if (aheadDistance > 0f) pts = Trim(pts, aheadDistance);
            BuildMesh(pts);
        }

        static List<Vector3> Trim(List<Vector3> pts, float maxLen)
        {
            var outPts = new List<Vector3> { pts[0] };
            float acc = 0f;
            for (int i = 1; i < pts.Count; i++)
            {
                acc += Vector3.Distance(pts[i - 1], pts[i]);
                outPts.Add(pts[i]);
                if (acc >= maxLen) break;
            }
            return outPts;
        }

        void BuildMesh(List<Vector3> pts)
        {
            int si = (int)strength;
            float perM = Table[si, 0], halfW = Table[si, 1], sizeMul = Table[si, 2], briMul = Table[si, 3];

            // 누적 거리 (흐름 애니메이션의 위상 기준)
            var dist = new float[pts.Count];
            for (int i = 1; i < pts.Count; i++) dist[i] = dist[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);
            float total = dist[pts.Count - 1];
            int count = Mathf.Clamp(Mathf.RoundToInt(total * perM), 4, 30000);

            var verts = new List<Vector3>(count * 4);
            var uvs = new List<Vector2>(count * 4);
            var uv1 = new List<Vector4>(count * 4);
            var cols = new List<Color>(count * 4);
            var tris = new List<int>(count * 6);

            // 위치를 시드로 삼으면 걸을 때마다 별이 튄다 → 경로상 거리로 결정론적 난수를 만든다
            var origin = transform.position;

            for (int i = 0; i < count; i++)
            {
                float d = (i + 0.5f) / count * total;
                Sample(pts, dist, d, out Vector3 center, out Vector3 fwd);

                float r1 = Hash(d * 7.13f), r2 = Hash(d * 3.71f + 11f), r3 = Hash(d * 1.97f + 23f);
                float lat = (r1 + r2 - 1f) * halfW;
                var right = new Vector3(-fwd.z, 0f, fwd.x).normalized;
                Vector3 pos = center + right * lat;

                float size, hdr;
                if (r3 > 0.965f) { size = Mathf.Lerp(0.36f, 0.52f, r1); hdr = Mathf.Lerp(2.6f, 4.0f, r2); }
                else if (r3 > 0.80f) { size = Mathf.Lerp(0.20f, 0.30f, r1); hdr = Mathf.Lerp(1.3f, 2.1f, r2); }
                else { size = Mathf.Lerp(0.09f, 0.17f, r1); hdr = Mathf.Lerp(0.45f, 0.95f, r2); }
                size *= sizeMul;
                float edge = 1f - Mathf.Abs(lat) / halfW;
                hdr *= Mathf.Lerp(0.35f, 1f, edge * edge) * briMul;

                Color tint;
                if (r2 > 0.93f) tint = new Color(1.00f, 0.86f, 0.70f);
                else if (r2 > 0.62f) tint = new Color(0.78f, 0.86f, 1.00f);
                else if (r2 > 0.34f) tint = new Color(0.88f, 0.82f, 1.00f);
                else tint = Color.white;

                int b = verts.Count;
                float h = size * 0.5f;
                verts.Add(pos + new Vector3(-h, 0f, -h) - origin);
                verts.Add(pos + new Vector3(h, 0f, -h) - origin);
                verts.Add(pos + new Vector3(-h, 0f, h) - origin);
                verts.Add(pos + new Vector3(h, 0f, h) - origin);

                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
                uvs.Add(new Vector2(0, 1)); uvs.Add(new Vector2(1, 1));

                // w = 경로상 거리 → 셰이더가 이걸로 빛을 흘려보낸다
                var tw = new Vector4(r1, r2, hdr, d);
                for (int k = 0; k < 4; k++) { uv1.Add(tw); cols.Add(tint); }

                tris.Add(b + 0); tris.Add(b + 2); tris.Add(b + 1);
                tris.Add(b + 2); tris.Add(b + 3); tris.Add(b + 1);
            }

            _mesh.Clear();
            _mesh.SetVertices(verts);
            _mesh.SetUVs(0, uvs);
            _mesh.SetUVs(1, uv1);
            _mesh.SetColors(cols);
            _mesh.SetTriangles(tris, 0);
            _mesh.RecalculateBounds();
            LastStarCount = count;
        }

        /// <summary>거리 d 지점의 위치와 진행 방향.</summary>
        static void Sample(List<Vector3> pts, float[] dist, float d, out Vector3 pos, out Vector3 fwd)
        {
            int i = 1;
            while (i < pts.Count - 1 && dist[i] < d) i++;
            float seg = Mathf.Max(1e-4f, dist[i] - dist[i - 1]);
            float t = Mathf.Clamp01((d - dist[i - 1]) / seg);
            pos = Vector3.Lerp(pts[i - 1], pts[i], t);
            fwd = pts[i] - pts[i - 1]; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward; else fwd.Normalize();
        }

        static float Hash(float x)
        {
            float s = Mathf.Sin(x * 127.1f) * 43758.5453f;
            return s - Mathf.Floor(s);
        }
    }
}
