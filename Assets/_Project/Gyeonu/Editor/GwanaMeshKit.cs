using System.Collections.Generic;
using UnityEngine;

namespace IMUNROK.Gyeonu.Editor
{
    /// <summary>
    /// 관아 구조물(외삼문·담장·월대·어도) 절차 생성용 메시 빌더.
    ///
    /// UV는 전부 "월드 평면 투영" — 면의 지배 축을 골라 좌표를 그대로 UV로 쓰고
    /// metersPerUV로 나눈다. 동헌 FBX 실측 UV 스케일에 맞추면 문·담장·동헌의
    /// 돌결·기왓골 크기가 정확히 같아진다.
    ///
    /// 실측 스케일 (동헌 FBX, 머티리얼 타일링 반영 후 1 UV = 몇 m):
    ///   화강암 1.00 / 석축 2.81 / 기와 1.12 / 부연 0.81 / 목재 0.40
    /// </summary>
    public class GwanaMeshKit
    {
        public const float MpuGranite = 1.00f;
        public const float MpuRubble = 2.81f;
        public const float MpuRoof = 1.12f;
        public const float MpuBuyeon = 0.81f;
        public const float MpuWood = 0.40f;

        readonly List<Vector3> _v = new List<Vector3>();
        readonly List<Vector3> _n = new List<Vector3>();
        readonly List<Vector2> _uv = new List<Vector2>();
        readonly List<int> _t = new List<int>();
        readonly float _mpu;

        public GwanaMeshKit(float metersPerUV) { _mpu = metersPerUV; }

        public int TriCount => _t.Count / 3;

        /// <summary>사각면. a→b→c→d 반시계(바깥에서 볼 때). UV는 면 법선 기준 평면 투영.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Vector3 nrm = Vector3.Cross(b - a, c - a).normalized;
            int i0 = _v.Count;
            foreach (var p in new[] { a, b, c, d })
            {
                _v.Add(p); _n.Add(nrm); _uv.Add(PlanarUV(p, nrm));
            }
            _t.Add(i0); _t.Add(i0 + 1); _t.Add(i0 + 2);
            _t.Add(i0); _t.Add(i0 + 2); _t.Add(i0 + 3);
        }

        public void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 nrm = Vector3.Cross(b - a, c - a).normalized;
            int i0 = _v.Count;
            foreach (var p in new[] { a, b, c }) { _v.Add(p); _n.Add(nrm); _uv.Add(PlanarUV(p, nrm)); }
            _t.Add(i0); _t.Add(i0 + 1); _t.Add(i0 + 2);
        }

        Vector2 PlanarUV(Vector3 p, Vector3 n)
        {
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            Vector2 uv;
            if (ay >= ax && ay >= az) uv = new Vector2(p.x, p.z);        // 수평면
            else if (ax >= az) uv = new Vector2(p.z, p.y);               // x를 보는 면
            else uv = new Vector2(p.x, p.y);                             // z를 보는 면
            return uv / _mpu;
        }

        /// <summary>축정렬 직육면체.</summary>
        public void Box(Vector3 center, Vector3 size) => BoxRot(center, size, Quaternion.identity);

        /// <summary>두 코너로 지정하는 직육면체. 인자 순서가 뒤집혀 있어도 정상 동작한다
        /// (좌우 대칭 배치에서 s*(a) ~ s*(b) 꼴이 흔해 뒤집히기 쉽다 — 뒤집히면 면이 뒤집힌다).</summary>
        public void BoxMinMax(float x0, float x1, float y0, float y1, float z0, float z1)
        {
            if (x0 > x1) (x0, x1) = (x1, x0);
            if (y0 > y1) (y0, y1) = (y1, y0);
            if (z0 > z1) (z0, z1) = (z1, z0);
            Box(new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f),
                new Vector3(x1 - x0, y1 - y0, z1 - z0));
        }

        public void BoxRot(Vector3 center, Vector3 size, Quaternion rot)
        {
            Vector3 h = size * 0.5f;
            Vector3 P(float sx, float sy, float sz) =>
                center + rot * new Vector3(sx * h.x, sy * h.y, sz * h.z);

            Quad(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1));      // 상
            Quad(P(-1, -1, 1), P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1));  // 하
            Quad(P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1));  // -Z
            Quad(P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), P(-1, -1, 1));      // +Z
            Quad(P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), P(-1, -1, -1));  // -X
            Quad(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1));      // +X
        }

        /// <summary>사다리꼴 각기둥(민흘림 기둥·물매 있는 담장 몸체 등). 아래/위 반폭이 다른 상자.</summary>
        public void Tapered(float x0, float x1, float y0, float y1,
                            float zc, float halfZBottom, float halfZTop)
        {
            Vector3 A0 = new Vector3(x0, y0, zc - halfZBottom), B0 = new Vector3(x1, y0, zc - halfZBottom);
            Vector3 C0 = new Vector3(x1, y0, zc + halfZBottom), D0 = new Vector3(x0, y0, zc + halfZBottom);
            Vector3 A1 = new Vector3(x0, y1, zc - halfZTop), B1 = new Vector3(x1, y1, zc - halfZTop);
            Vector3 C1 = new Vector3(x1, y1, zc + halfZTop), D1 = new Vector3(x0, y1, zc + halfZTop);
            Quad(A1, D1, C1, B1);          // 상
            Quad(A0, B0, C0, D0);          // 하
            Quad(A0, A1, B1, B0);          // -Z
            Quad(C0, C1, D1, D0);          // +Z
            Quad(D0, D1, A1, A0);          // -X
            Quad(B0, B1, C1, C0);          // +X
        }

        /// <summary>원기둥 (기둥·초석). 위아래 반지름을 달리하면 민흘림.</summary>
        public void Cylinder(Vector3 baseCenter, float rBottom, float rTop, float height, int seg = 14)
        {
            int i0 = _v.Count;
            float circ = 2f * Mathf.PI * rBottom;
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float u = circ * i / seg / _mpu;
                _v.Add(baseCenter + dir * rBottom); _n.Add(dir); _uv.Add(new Vector2(u, baseCenter.y / _mpu));
                _v.Add(baseCenter + dir * rTop + Vector3.up * height); _n.Add(dir);
                _uv.Add(new Vector2(u, (baseCenter.y + height) / _mpu));
            }
            for (int i = 0; i < seg; i++)
            {
                int b = i0 + i * 2;
                _t.Add(b); _t.Add(b + 1); _t.Add(b + 3);
                _t.Add(b); _t.Add(b + 3); _t.Add(b + 2);
            }
            // 상단 뚜껑 (아래는 가려지므로 생략)
            int c0 = _v.Count;
            _v.Add(baseCenter + Vector3.up * height); _n.Add(Vector3.up);
            _uv.Add(new Vector2(baseCenter.x, baseCenter.z) / _mpu);
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                Vector3 p = baseCenter + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * rTop + Vector3.up * height;
                _v.Add(p); _n.Add(Vector3.up); _uv.Add(new Vector2(p.x, p.z) / _mpu);
            }
            for (int i = 0; i < seg; i++) { _t.Add(c0); _t.Add(c0 + 2 + i); _t.Add(c0 + 1 + i); }
        }

        /// <summary>
        /// 맞배지붕 한 채. 용마루가 X축과 평행, 처마가 ±Z로 흘러내린다.
        /// 두께가 있는 판이라 처마 끝 단면이 보인다.
        /// </summary>
        public void GableRoof(float x0, float x1, float zc, float ridgeY, float eaveHalfZ,
                              float eaveY, float thickness)
        {
            foreach (float s in new[] { 1f, -1f })
            {
                Vector3 rA = new Vector3(x0, ridgeY, zc), rB = new Vector3(x1, ridgeY, zc);
                Vector3 eA = new Vector3(x0, eaveY, zc + s * eaveHalfZ);
                Vector3 eB = new Vector3(x1, eaveY, zc + s * eaveHalfZ);
                Vector3 down = Vector3.down * thickness;
                if (s > 0f)
                {
                    Quad(rA, eA, eB, rB);                                   // 윗면
                    Quad(rB + down, eB + down, eA + down, rA + down);        // 아랫면
                    Quad(eA, eA + down, eB + down, eB);                      // 처마 끝 단면
                    Quad(rA, rA + down, eA + down, eA);                      // -X 박공 단면
                    Quad(eB, eB + down, rB + down, rB);                      // +X 박공 단면
                }
                else
                {
                    Quad(rB, eB, eA, rA);
                    Quad(rA + down, eA + down, eB + down, rB + down);
                    Quad(eB, eB + down, eA + down, eA);
                    Quad(eA, eA + down, rA + down, rA);
                    Quad(rB, rB + down, eB + down, eB);
                }
            }
        }

        /// <summary>
        /// 물매가 꺾이는 맞배지붕. profile[i] = (용마루에서의 반깊이, 높이, 그 지점의 판 두께).
        /// 한식 지붕의 오목한 처마곡을 구간 근사로 낸다 — 마루 쪽은 급하고(약 33°)
        /// 처마로 갈수록 눕다가 맨 끝에서 살짝 들린다(처마 들림). 마지막 점이 처마 끝.
        /// </summary>
        /// <param name="parts">0=전부, 1=밑면 제외(윗면·박공·처마단면), 2=밑면만.
        /// 지붕 밑면은 기와가 아니라 개판(널)이라 재질을 갈라야 해서 나눠 뽑을 수 있게 했다.</param>
        public void GableRoofProfile(float x0, float x1, float zc, Vector3[] profile, int parts = 0)
        {
            foreach (float s in new[] { 1f, -1f })
            {
                for (int i = 0; i < profile.Length - 1; i++)
                {
                    var p0 = profile[i];
                    var p1 = profile[i + 1];
                    Vector3 d0 = Vector3.down * p0.z, d1 = Vector3.down * p1.z;
                    Vector3 A = new Vector3(x0, p0.y, zc + s * p0.x), B = new Vector3(x1, p0.y, zc + s * p0.x);
                    Vector3 C = new Vector3(x0, p1.y, zc + s * p1.x), D = new Vector3(x1, p1.y, zc + s * p1.x);
                    bool last = i == profile.Length - 2;
                    if (s > 0f)
                    {
                        if (parts != 2) Quad(A, C, D, B);
                        if (parts != 1) Quad(B + d0, D + d1, C + d1, A + d0);
                        if (parts != 2) { Quad(A, A + d0, C + d1, C); Quad(D, D + d1, B + d0, B); }
                        if (last && parts != 2) Quad(C, C + d1, D + d1, D);
                    }
                    else
                    {
                        if (parts != 2) Quad(B, D, C, A);
                        if (parts != 1) Quad(A + d0, C + d1, D + d1, B + d0);
                        if (parts != 2) { Quad(C, C + d1, A + d0, A); Quad(B, B + d0, D + d1, D); }
                        if (last && parts != 2) Quad(D, D + d1, C + d1, C);
                    }
                }
            }
        }

        /// <summary>
        /// 서까래·부연 (처마 밑에 촘촘히 내민 각목). 지붕 profile의 바깥 구간 물매를 따라간다.
        /// 한식 처마의 인상을 결정하는 요소라 띠 하나로 뭉개지 않고 낱개로 낸다.
        /// </summary>
        public void Rafters(float x0, float x1, float zc, Vector3[] profile,
                            float spacing = 0.46f, float thick = 0.13f)
        {
            var pIn = profile[profile.Length - 3];
            var pOut = profile[profile.Length - 1];
            float run = pOut.x - pIn.x, rise = pOut.y - pIn.y;
            float ang = -Mathf.Atan2(rise, run) * Mathf.Rad2Deg;   // +Z쪽 끝이 내려가면 양수
            float len = Mathf.Sqrt(run * run + rise * rise) * 0.95f;
            int n = Mathf.Max(2, Mathf.FloorToInt((x1 - x0 - 0.3f) / spacing));
            float step = (x1 - x0 - 0.3f) / n;

            foreach (float s in new[] { 1f, -1f })
                for (int i = 0; i <= n; i++)
                {
                    float x = x0 + 0.15f + i * step;
                    float hz = (pIn.x + pOut.x) * 0.5f, y = (pIn.y + pOut.y) * 0.5f;
                    float drop = (pIn.z + pOut.z) * 0.5f + thick * 0.5f;
                    BoxRot(new Vector3(x, y - drop, zc + s * hz), new Vector3(thick, thick, len),
                           Quaternion.Euler(s * ang, 0f, 0f));
                }
        }

        /// <summary>박공벽 (맞배지붕 양 끝 삼각벽). x평면에 서는 삼각형 2장 + 두께.</summary>
        public void GablePanel(float x, float zc, float halfZ, float baseY, float ridgeY, float thick)
        {
            foreach (float s in new[] { 1f, -1f })
            {
                float xf = x + s * thick * 0.5f;
                Vector3 a = new Vector3(xf, baseY, zc - halfZ);
                Vector3 b = new Vector3(xf, baseY, zc + halfZ);
                Vector3 c = new Vector3(xf, ridgeY, zc);
                if (s > 0f) Tri(b, a, c); else Tri(a, b, c);
            }
        }

        public Mesh Build(string name)
        {
            var m = new Mesh { name = name };
            m.SetVertices(_v); m.SetNormals(_n); m.SetUVs(0, _uv); m.SetTriangles(_t, 0);
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }
    }
}
