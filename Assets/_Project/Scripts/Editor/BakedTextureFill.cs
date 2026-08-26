using System.IO;
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Common.Editor
{
    /// <summary>
    /// <b>구운 텍스처의 빈 곳을 메운다</b> — 옹덕구가 깨져 보이던 까닭.
    ///
    /// 블렌더에서 조각을 합치고 UV 를 다시 펴서 구우면, 칸(2048×2048) 안에 섬이
    /// 수천 개 흩어진다. 그런데 <b>섬과 섬 사이는 아무도 안 칠한다</b> — 순검정으로
    /// 남는다. 옹덕구의 경우 칸의 <b>10.7%</b> 가 그 검정이었다.
    ///
    /// 눈에는 안 보이는 빈틈인데 왜 깨져 보이나:
    ///   ① <b>이중선형 보간</b> — 섬 가장자리 픽셀을 읽을 때 옆의 검정을 같이 섞는다.
    ///      섬이 잘게 부서져 있을수록 거의 모든 픽셀이 가장자리라, 온몸에 때가 낀다.
    ///   ② <b>밉맵</b> — 멀어질수록 검정이 더 크게 번진다. 뜰에서 본 옹덕구가
    ///      유독 지저분했던 이유다.
    ///   ③ DXT1 은 4×4 덩어리로 뭉쳐 누르는데, 그 덩어리가 섬과 검정에 걸치면
    ///      가장자리가 얼룩진다.
    ///
    /// <b>노멀맵은 살리지 않고 뗀다.</b> 구운 노멀맵 자체는 멀쩡했다(평평한 접선노멀이
    /// 83%). 문제는 <b>탄젠트</b>다 — 유니티가 이 그물에서 뽑은 탄젠트는 <b>정점 37,765개
    /// 전부 w=-1</b>, 즉 UV 가 통째로 뒤집혀 들어와 있다. 블렌더가 구울 때 쓴 기준과
    /// 어긋나므로 요철이 반대쪽에서 눌린다. 초록 채널을 뒤집어 맞춰 보았으나 섬마다
    /// 어긋난 방향이 달라 여전히 얼룩졌다. 삼각형이 5만 장이나 되는 그물에 노멀맵이
    /// 보태 줄 것도 거의 없다 — <b>아내가 멀쩡한 것도 노멀맵을 안 쓰기 때문</b>이다.
    /// 그래서 여기서도 뗀다. 구운_N 은 지우지 않는다, 어디가 빈틈인지 아는 것은 그것뿐이라
    /// 이 도구가 <b>본(mask)으로만</b> 쓴다.
    ///
    /// 원본 메시(Meshy 가 준 <c>옹덕구_BC</c>)에는 검정이 <b>0%</b> 다 — 제대로 된
    /// 구이는 섬 색을 바깥으로 <b>불려서</b> 빈틈을 채운다. 이 도구가 그 불리기다.
    ///
    /// <b>덮어쓰지 않는다.</b> 아트는 git 밖이라 되돌릴 데가 없다. 그래서 원본은
    /// 그대로 두고 <c>_메운_</c> 을 따로 낳아 재질만 갈아 끼운다 — 몇 번을 눌러도
    /// 같은 입력에서 같은 결과가 나온다.
    ///
    /// 메뉴: [이문록 ▸ 에셋: 구운 텍스처 가장자리 메우기]
    /// </summary>
    public static class BakedTextureFill
    {
        private const string Dir = "Assets/_Project/Onggojip/Art/Characters/옹덕구/";
        private const string Baked = "옹덕구_구운_";
        private const string Filled = "옹덕구_메운_";
        private const string MatPath = Dir + "옹덕구_병합_Mat.mat";

        [MenuItem("이문록/에셋: 구운 텍스처 가장자리 메우기")]
        private static void Run()
        {
            // ── 어디를 안 칠했는지는 노멀맵이 제일 정직하게 말해 준다.
            //    접선공간 노멀은 파랑(=표면 바깥쪽)이 반드시 128 이상이다. 그보다 낮으면
            //    안 칠했거나, 가장자리에서 검정과 반쯤 섞인 픽셀이다. 베이스컬러로
            //    판별하면 머리채의 검정을 빈틈으로 잘못 잡는다.
            int w, h;
            var normal = Load(Baked + "N", out w, out h);
            if (normal == null) return;

            var covered = new bool[normal.Length];
            int holes = 0;
            for (int i = 0; i < normal.Length; i++)
            {
                covered[i] = normal[i].b >= 128;
                if (!covered[i]) holes++;
            }

            if (holes == 0)
            {
                EditorUtility.DisplayDialog("이문록", "메울 빈틈이 없습니다.", "확인");
                return;
            }

            int bw, bh;
            var basecolor = Load(Baked + "BC", out bw, out bh);
            if (basecolor != null && bw == w && bh == h)
            {
                Grow(basecolor, (bool[])covered.Clone(), w, h);
                Save(basecolor, bw, bh, Filled + "BC");
            }

            AssetDatabase.Refresh();
            Rewire();

            Debug.Log(string.Format("[이문록] 구운 텍스처를 메웠습니다 — 빈틈 {0:N0}칸 ({1:F1}%)를 이웃 색으로 불렸습니다.",
                holes, 100f * holes / normal.Length));
        }

        /// <summary>
        /// 칠한 곳에서 바깥으로 한 겹씩 번져 나가며 빈틈을 가장 가까운 색으로 채운다.
        /// 칠한 칸을 전부 씨앗으로 넣고 너비우선으로 퍼뜨리므로, 칸 하나가 큐에 한 번만
        /// 들어간다 — 2048² 라도 한 번에 훑고 끝난다.
        /// </summary>
        private static void Grow(Color32[] px, bool[] done, int w, int h)
        {
            var queue = new int[w * h];
            int head = 0, tail = 0;
            for (int i = 0; i < done.Length; i++) if (done[i]) queue[tail++] = i;

            while (head < tail)
            {
                int i = queue[head++];
                int x = i % w, y = i / w;
                for (int dy = -1; dy <= 1; dy++)
                {
                    int ny = y + dy; if (ny < 0 || ny >= h) continue;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx; if (nx < 0 || nx >= w) continue;
                        int j = ny * w + nx;
                        if (done[j]) continue;
                        done[j] = true;
                        px[j] = px[i];
                        queue[tail++] = j;
                    }
                }
            }
        }

        private static Color32[] Load(string file, out int w, out int h)
        {
            w = h = 0;
            string path = Dir + file + ".png";
            string full = Path.Combine(Directory.GetCurrentDirectory(), path);
            if (!File.Exists(full)) { Debug.LogError("[이문록] " + path + " 이 없습니다."); return null; }

            // 압축된 상태(DXT1)로 읽으면 이미 뭉개진 색을 다시 굽는 꼴이라, 파일에서 생으로 읽는다.
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            tex.LoadImage(File.ReadAllBytes(full));
            w = tex.width; h = tex.height;
            var px = tex.GetPixels32();
            Object.DestroyImmediate(tex);
            return px;
        }

        private static void Save(Color32[] px, int w, int h, string file)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            tex.SetPixels32(px); tex.Apply();
            string path = Dir + file + ".png";
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), path), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = true;
            imp.maxTextureSize = 2048;
            imp.SaveAndReimport();
        }

        /// <summary>재질을 메운 것으로 갈아 끼우고, 값밖에 없는 금속·거칠기 맵은 떼어낸다.</summary>
        private static void Rewire()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat == null) { Debug.LogWarning("[이문록] " + MatPath + " 을 못 찾아 재질은 그대로 둡니다."); return; }

            var bc = AssetDatabase.LoadAssetAtPath<Texture>(Dir + Filled + "BC.png");
            if (bc != null) mat.SetTexture("_BaseMap", bc);

            // 탄젠트가 통째로 뒤집힌 그물이라 구운 노멀맵을 걸면 요철이 반대로 눌린다.
            mat.SetTexture("_BumpMap", null);
            mat.DisableKeyword("_NORMALMAP");

            // 구운_MS 는 98.2% 가 순검정이라 담고 있는 것이 없다. 맵을 떼고 값으로 적는다 —
            // 맵을 뗀 채 _Metallic 이 1 로 남아 있으면 옹덕구가 쇳덩이가 되므로 같이 내린다.
            mat.SetTexture("_MetallicGlossMap", null);
            mat.DisableKeyword("_METALLICSPECGLOSSMAP");
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0.2f);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
        }
    }
}
