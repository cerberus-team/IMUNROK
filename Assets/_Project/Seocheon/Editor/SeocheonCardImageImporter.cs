// 사군자 카드 그림이 Cards 폴더에 들어오면 지시서의 임포트 설정을 자동으로 먹입니다.
using UnityEditor;
using UnityEngine;

namespace IMUNROK.Seocheon.Editor
{
    /// <summary>
    /// 카드 그림 전용 임포터 (2026-08-27).
    ///
    /// ■ 왜 자동으로 하나
    ///   지시서가 정한 설정이 여덟 가지다(Sprite · Single · Full Rect · 밉맵 끔 · Clamp ·
    ///   Max 512 · Android ASTC 6x6). 손으로 넣으면 <b>넉 장 중 한 장은 빠진다</b> —
    ///   특히 Android 오버라이드는 인스펙터 탭을 따로 열어야 해서 잊기 쉽다.
    ///   폴더에 떨어뜨리기만 하면 맞게 들어오도록 여기서 못박는다.
    ///
    /// ■ ★이 폴더 밖에는 손대지 않는다
    ///   <see cref="Folder"/> 아래만 본다. 다른 사건·다른 아트의 임포트 설정을 건드리면
    ///   나중에 원인 찾기가 지옥이 된다.
    ///
    /// ■ 이미 들어와 있는 그림에 다시 먹이려면
    ///   <c>Tools ▸ 이문록 ▸ 서천 ▸ 카드 그림 임포트 설정 다시 먹이기</c>.
    ///   (임포터는 <b>처음 들어올 때</b>만 돌기 때문이다)
    /// </summary>
    public sealed class SeocheonCardImageImporter : AssetPostprocessor
    {
        private const string Folder = "Assets/_Project/Seocheon/Art/UI/Cards/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder, System.StringComparison.OrdinalIgnoreCase)) return;
            Apply((TextureImporter)assetImporter);
        }

        internal static void Apply(TextureImporter t)
        {
            if (t == null) return;

            t.textureType = TextureImporterType.Sprite;
            t.spriteImportMode = SpriteImportMode.Single;

            // ★Full Rect 는 TextureImporter 에 직접 없다 — 설정 뭉치를 꺼내 고쳐 다시 넣는다.
            //   Tight 로 두면 그림의 투명 여백만큼 메시가 깎여
            //   2:3 칸에 넣었을 때 카드가 <b>칸 안에서 제멋대로 어긋난다</b>.
            var st = new TextureImporterSettings();
            t.ReadTextureSettings(st);
            st.spriteMeshType = SpriteMeshType.FullRect;
            st.spriteGenerateFallbackPhysicsShape = false;
            t.SetTextureSettings(st);

            t.mipmapEnabled = false;          // UI 는 항상 원배율 근처라 밉맵이 흐림만 만든다
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            t.alphaIsTransparency = true;
            t.maxTextureSize = 512;

            var android = new TextureImporterPlatformSettings
            {
                name = "Android",
                overridden = true,
                maxTextureSize = 512,
                format = TextureImporterFormat.ASTC_6x6,
                textureCompression = TextureImporterCompression.Compressed,
            };
            t.SetPlatformTextureSettings(android);
        }

        [MenuItem("Tools/이문록/서천/카드 그림 임포트 설정 다시 먹이기")]
        private static void Reapply()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { Folder.TrimEnd('/') });
            int n = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var t = AssetImporter.GetAtPath(path) as TextureImporter;
                if (t == null) continue;
                Apply(t);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                n++;
            }
            Debug.Log("[서천] 카드 그림 임포트 설정을 " + n + "장에 먹였습니다. (" + Folder + ")");
        }
    }
}
