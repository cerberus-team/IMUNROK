using System;
using System.Collections.Generic;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>사람마다 정해 둔 얼굴.</b>
    ///
    /// <b>왜 따로 두는가.</b> <see cref="FacePortrait"/> 는 여태 뜰에 <b>실제로 선 그 사람</b>을
    /// 찍어 썼다. 그 방식은 옷을 갈아입으면 갈아입은 채로 찍히니 어긋날 일이 없다는 장점이
    /// 있었지만, 값을 치른 데가 둘이다.
    ///
    ///   ① <b>배경이 제각각이다.</b> 담 앞에 선 사람과 마루 앞에 선 사람이 서로 다른
    ///      뒤를 업고 찍힌다. 다섯을 나란히 놓으면 얼굴이 아니라 <b>창문 다섯</b>이 된다.
    ///   ② <b>모델이 아직 얼굴이 아니다.</b> 지금 서 있는 몸들은 낯이 뭉개져 있어,
    ///      가려내야 할 甲·乙을 나란히 놓아도 <b>가려낼 것이 없다.</b>
    ///
    /// 그래서 그려 둔 얼굴이 있으면 그것을 쓰고, 없으면 예전처럼 그 자리에서 찍는다.
    /// 두 길을 다 열어 두므로, 얼굴을 아직 안 그린 사람은 저절로 옛 길로 간다.
    ///
    /// <b>배경은 우리가 입힌다.</b> 받은 그림들은 크기도 뒷배경도 서로 다르다(1024각짜리
    /// 여섯에 842×1264 한 장, 투명한 것 하나에 불투명한 것 여섯). 그것을 그대로 쓰면
    /// ①번 문제가 그대로 남으므로, <see cref="FaceCards"/> 가 <b>같은 크기·같은 바탕·
    /// 같은 테두리</b>의 카드 한 장으로 다시 굽는다. 가장자리는 바탕빛으로 번지게 해서
    /// 저마다 다른 뒤가 카드 안으로 스며들어 사라지게 한다.
    ///
    /// 어느 얼굴이 누구인지는 <see cref="Entry.이름조각"/> 으로 짚는다 — 씬 오브젝트의
    /// 이름(또는 그 어버이의 이름)이 조각 하나를 품으면 그 사람이다. 인스펙터에서 사람을
    /// 끌어다 놓지 않아도 되므로, 씬을 안 건드리고도 얼굴이 붙는다.
    /// </summary>
    [CreateAssetMenu(fileName = "이문록_얼굴", menuName = "이문록/얼굴 책", order = 60)]
    public class FaceBook : ScriptableObject
    {
        /// <summary>Resources 에서 이 이름으로 찾는다.</summary>
        public const string ResourceName = "이문록_얼굴";

        [Serializable]
        public class Entry
        {
            [Tooltip("사람 이름 — 알아보기 쉬우라고 적는다. 짚는 데는 안 쓴다")]
            public string 이름;

            [Tooltip("씬 오브젝트 이름이 이 중 하나를 품으면 이 얼굴이다. 어버이 이름까지 거슬러 본다")]
            public string[] 이름조각;

            [Tooltip("그려 둔 얼굴. 읽을 수 있게(Read/Write) 켜 두어야 한다")]
            public Texture2D 그림;

            [Tooltip("그림에서 얼굴이 놓인 자리(0~1). 가로")]
            [Range(0f, 1f)] public float 가운데가로 = 0.5f;

            [Tooltip("그림에서 얼굴이 놓인 자리(0~1). 세로 — 0 이 아래다. 사람 그림은 대개 위쪽")]
            [Range(0f, 1f)] public float 가운데세로 = 0.62f;

            [Tooltip("얼마나 넓게 담을까(그림의 짧은 변에 대한 몫). 작을수록 얼굴이 크다")]
            [Range(0.08f, 1f)] public float 담는넓이 = 0.5f;
        }

        [Tooltip("사람마다 한 줄")]
        public Entry[] 얼굴들 = new Entry[0];

        [Header("모두에게 같게 입히는 것")]
        [Tooltip("카드 한 변(칸). 이름표에 얹히는 크기다")]
        [Range(64, 512)] public int 카드크기 = 192;

        [Tooltip("카드 바탕 — 저마다 다른 뒷배경이 이 빛깔로 스며 사라진다")]
        public Color 바탕 = new Color(0.09f, 0.075f, 0.065f, 1f);

        [Tooltip("카드 테두리")]
        public Color 테두리 = new Color(0.30f, 0.24f, 0.18f, 1f);

        [Tooltip("가장자리가 바탕빛으로 번지는 몫. 0 이면 네모로 뚝 잘린다")]
        [Range(0f, 0.6f)] public float 가장자리번짐 = 0.30f;

        [Tooltip("테두리 두께(칸)")]
        [Range(0, 8)] public int 테두리두께 = 2;

        [Tooltip("뒷배경을 빼고 바탕빛으로 갈아 끼운다. 받은 그림이 저마다 다른 뒤를 업고 있으므로 켜 둔다")]
        public bool 배경빼기 = true;

        [Tooltip("바탕으로 칠 빛깔 차이. 크면 옷까지 파먹고, 작으면 뒤가 남는다")]
        [Range(0.01f, 0.30f)] public float 배경닮음 = 0.07f;

        // ── 한 벌만 읽는다 ──
        private static FaceBook _loaded;
        private static bool _tried;

        /// <summary>얼굴 책. 없으면 <c>null</c> — 그러면 예전처럼 그 자리에서 찍는다.</summary>
        public static FaceBook Book
        {
            get
            {
                if (_tried) return _loaded;
                _tried = true;
                _loaded = Resources.Load<FaceBook>(ResourceName);
                return _loaded;
            }
        }

        /// <summary>씬을 다시 읽을 때를 위해. 편집기에서만 쓸 일이 있다.</summary>
        public static void Forget() { _tried = false; _loaded = null; }

        /// <summary>이 사람의 얼굴을 짚는다. 못 짚으면 <c>null</c>.</summary>
        public Entry Find(Transform who)
        {
            if (who == null || 얼굴들 == null) return null;

            // 제 이름부터 보고, 없으면 어버이로 거슬러 올라간다 — 모델은 대개
            // 「甲_가짜/복동_모델」처럼 한 겹 안에 들어 있다.
            for (var t = who; t != null; t = t.parent)
            {
                var e = ByName(t.name);
                if (e != null) return e;
            }
            return null;
        }

        /// <summary>이름 조각 하나로 얼굴을 짚는다(씬에 사람이 없는 자리 — 장계의 카드).</summary>
        public Entry FindByName(string name) { return ByName(name); }

        private Entry ByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var e in 얼굴들)
            {
                if (e == null || e.그림 == null || e.이름조각 == null) continue;
                foreach (var frag in e.이름조각)
                {
                    if (string.IsNullOrEmpty(frag)) continue;
                    if (name.Contains(frag)) return e;
                }
            }
            return null;
        }
    }

    /// <summary>
    /// <b>얼굴을 카드 한 장으로 굽는다.</b>
    ///
    /// 받은 그림은 크기도 뒤도 제각각이라 그대로 늘어놓으면 한 벌로 안 보인다.
    /// 여기서 <b>같은 네모</b>로 잘라 <b>같은 바탕</b>에 앉히고 <b>같은 테두리</b>를 두른다.
    /// 가장자리는 바탕빛으로 번지게 한다 — 뒤가 서로 달라도 카드 밖으로 나가기 전에
    /// 다 같은 빛깔이 되므로 눈에는 한 벌로 보인다.
    ///
    /// 한 사람당 한 번만 굽고 <see cref="Cards"/> 에 담아 둔다. 192각짜리 한 장은
    /// 3만 칸 남짓이라 굽는 값이 눈에 띄지 않는다.
    /// </summary>
    public static class FaceCards
    {
        private static readonly Dictionary<FaceBook.Entry, Texture2D> Cards =
            new Dictionary<FaceBook.Entry, Texture2D>();

        public static Texture2D Card(FaceBook book, FaceBook.Entry e)
        {
            if (book == null || e == null || e.그림 == null) return null;

            Texture2D had;
            if (Cards.TryGetValue(e, out had) && had != null) return had;

            var src = e.그림;
            if (!src.isReadable)
            {
                Debug.LogWarning("[얼굴] " + src.name + " 은 Read/Write 가 꺼져 있어 카드로 못 굽는다. " +
                                 "그림 설정에서 Read/Write Enabled 를 켜 주십시오.");
                return null;
            }

            int n = Mathf.Clamp(book.카드크기, 64, 512);

            // ── ① 낯 둘레를 정사각형으로 오려 낸다 ─────────────────────────
            float shortSide = Mathf.Min(src.width, src.height);
            float box = shortSide * Mathf.Clamp(e.담는넓이, 0.08f, 1f);
            float cx = e.가운데가로 * src.width;
            float cy = e.가운데세로 * src.height;
            float left = Mathf.Clamp(cx - box * 0.5f, 0f, Mathf.Max(0f, src.width - box));
            float bot  = Mathf.Clamp(cy - box * 0.5f, 0f, Mathf.Max(0f, src.height - box));

            var cut = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float sx = (left + (x + 0.5f) / n * box) / src.width;
                float sy = (bot  + (y + 0.5f) / n * box) / src.height;
                cut[y * n + x] = src.GetPixelBilinear(sx, sy);
            }

            // ── ② 뒷배경을 뺀다 — <b>가장자리에서 번져 들어가며</b> ──────────
            //
            // 빛깔만 보고 빼면 <b>흰옷에 구멍</b>이 난다(마을사람·늙은하인·아내가 그랬다).
            // 뒷배경이 흰데 저고리도 희기 때문이다. 그래서 「빛깔이 닮았다」가 아니라
            // 「가장자리에서 <b>이어져 닿는다</b>」를 뺄 조건으로 삼는다 — 사람 한가운데는
            // 가장자리와 안 이어지므로 아무리 희어도 안 뚫린다.
            //
            // <b>밑변은 씨앗에서 뺀다.</b> 어깨와 가슴은 늘 카드 아래로 넘어가므로
            // 밑변은 「바깥」이 아니라 사람이다.
            var soft = new float[n * n];
            if (book.배경빼기)
            {
                Color key = src.GetPixelBilinear(0.004f, 0.996f);      // 원본 왼쪽 위 귀퉁이
                bool byAlpha = key.a < 0.98f;
                float tol = Mathf.Max(0.005f, book.배경닮음);

                var outside = new bool[n * n];
                var q = new Queue<int>();

                System.Func<int, bool> near = delegate(int i)
                {
                    var c = cut[i];
                    if (byAlpha) return c.a < 0.4f;
                    float dr = c.r - key.r, dg = c.g - key.g, db = c.b - key.b;
                    return Mathf.Sqrt(dr * dr + dg * dg + db * db) < tol;
                };
                System.Action<int> seed = delegate(int i)
                {
                    if (!outside[i] && near(i)) { outside[i] = true; q.Enqueue(i); }
                };

                int sideTo = Mathf.RoundToInt(n * 0.80f);
                for (int x = 0; x < n; x++) seed((n - 1) * n + x);          // 윗변 (v=1 이 위)
                for (int y = n - sideTo; y < n; y++) { seed(y * n); seed(y * n + n - 1); }

                while (q.Count > 0)
                {
                    int i = q.Dequeue();
                    int x = i % n, y = i / n;
                    if (x > 0)     seed(i - 1);
                    if (x < n - 1) seed(i + 1);
                    if (y > 0)     seed(i - n);
                    if (y < n - 1) seed(i + n);
                }

                for (int i = 0; i < soft.Length; i++) soft[i] = outside[i] ? 1f : 0f;

                // 자른 자리를 몇 칸 문질러 톱니를 없앤다
                for (int pass = 0; pass < 2; pass++)
                {
                    var next = (float[])soft.Clone();
                    for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        int i = y * n + x;
                        if (soft[i] >= 1f) continue;
                        float sum = 0f; int cnt = 0;
                        if (x > 0)     { sum += soft[i - 1]; cnt++; }
                        if (x < n - 1) { sum += soft[i + 1]; cnt++; }
                        if (y > 0)     { sum += soft[i - n]; cnt++; }
                        if (y < n - 1) { sum += soft[i + n]; cnt++; }
                        if (cnt > 0) next[i] = Mathf.Max(soft[i], sum / cnt * 0.75f);
                    }
                    soft = next;
                }
            }

            // ── ③ 바탕에 앉히고, 가를 녹이고, 테두리를 두른다 ────────────────
            var px = new Color[n * n];
            float vig = Mathf.Clamp01(book.가장자리번짐);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                Color c = cut[i];
                if (c.a < 0.999f) c = Color.Lerp(book.바탕, c, c.a);
                if (soft[i] > 0f) c = Color.Lerp(c, book.바탕, soft[i]);

                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                float r = Mathf.Max(Mathf.Abs(u - 0.5f), Mathf.Abs(v - 0.5f)) * 2f;
                if (vig > 0.0001f && r > 1f - vig)
                    c = Color.Lerp(c, book.바탕, Mathf.SmoothStep(0f, 1f, (r - (1f - vig)) / vig));

                c.a = 1f;
                px[i] = c;
            }

            int b = Mathf.Clamp(book.테두리두께, 0, 8);
            for (int i = 0; i < b; i++)
            for (int k = 0; k < n; k++)
            {
                px[i * n + k] = book.테두리;
                px[(n - 1 - i) * n + k] = book.테두리;
                px[k * n + i] = book.테두리;
                px[k * n + (n - 1 - i)] = book.테두리;
            }

            var tex = new Texture2D(n, n, TextureFormat.RGB24, false) { name = "얼굴카드_" + e.이름 };
            tex.SetPixels(px);
            tex.Apply(false, false);
            Cards[e] = tex;
            return tex;
        }

        /// <summary>구워 둔 카드를 버린다(편집기에서 값을 고쳐 다시 볼 때).</summary>
        public static void Clear()
        {
            foreach (var kv in Cards)
                if (kv.Value != null) UnityEngine.Object.Destroy(kv.Value);
            Cards.Clear();
        }
    }
}
