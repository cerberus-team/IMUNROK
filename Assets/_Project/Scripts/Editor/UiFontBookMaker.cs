using System.IO;
using UnityEditor;
using UnityEngine;
using IMUNROK.Common;

namespace IMUNROK.Common.EditorTools
{
    /// <summary>
    /// <b>글씨 한 장을 Resources 에 맞춰 둔다.</b>
    /// 메뉴: [이문록 ▸ 글씨 ▸ 글씨 한 장 맞추기]
    ///
    /// 프로젝트에서 <b>한글과 한자를 한 얼굴로</b> 그리는 글씨를 찾아 적어 둔다.
    /// 둘 중 하나라도 못 그리는 것은 고르지 않는다 — 못 그리는 글자는 운영체제가
    /// 대신 주워 오는데, 그 순간 한 줄 안에 필체가 둘이 된다.
    ///
    /// 두 번 눌러도 두 벌이 안 선다.
    /// </summary>
    public static class UiFontBookMaker
    {
        private const string Dir = "Assets/Resources";
        private static string Path_ { get { return Dir + "/" + UiFontBook.ResourceName + ".asset"; } }

        /// <summary>글씨가 이만큼은 그려야 한다 — 한글 몇 자와, 이 게임이 쓰는 한자.</summary>
        private const string MustDraw = "이문록어사봉서조사청옹고집異聞錄事目御史封書馬牌鍮尺眞僞";

        [MenuItem("이문록/글씨/글씨 한 장 맞추기")]
        public static void Make()
        {
            var log = new System.Text.StringBuilder("[글씨] 한 장 맞추기\n");

            Font best = null;
            int bestMiss = int.MaxValue;
            foreach (var guid in AssetDatabase.FindAssets("t:Font"))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.IndexOf("_Project") < 0) continue;      // 남의 패키지 글씨는 안 쓴다
                var f = AssetDatabase.LoadAssetAtPath<Font>(p);
                if (f == null) continue;

                int miss = 0;
                foreach (var c in MustDraw) if (!f.HasCharacter(c)) miss++;
                log.AppendLine("  " + f.name + " — 못 그리는 글자 " + miss + "자");
                if (miss < bestMiss) { best = f; bestMiss = miss; }
            }

            if (best == null)
            {
                Debug.LogWarning(log + "\n── 프로젝트에 글씨가 하나도 없다. 아트는 git 에 안 담기므로, "
                               + "공유 폴더의 글씨를 Assets/_Project/_Common/Art/Fonts 에 넣고 다시 누르십시오.");
                return;
            }
            if (bestMiss > 0)
                log.AppendLine("  ⚠ 가장 나은 것도 " + bestMiss + "자를 못 그린다 — 그 글자만 딴 얼굴로 떨어진다");

            if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
            var book = AssetDatabase.LoadAssetAtPath<UiFontBook>(Path_);
            if (book == null)
            {
                book = ScriptableObject.CreateInstance<UiFontBook>();
                AssetDatabase.CreateAsset(book, Path_);
                log.AppendLine("── 새로 만들었다: " + Path_);
            }
            else log.AppendLine("── 이미 있던 것을 고쳤다: " + Path_);

            book.korean = best;
            EditorUtility.SetDirty(book);
            AssetDatabase.SaveAssets();
            log.AppendLine("── 글씨 = " + best.name);
            Debug.Log(log.ToString());
        }
    }
}
