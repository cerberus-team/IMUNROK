using System.Collections.Generic;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>단서 그림·종이·모형을 이름으로 되찾는 표</b> — 저장한 수첩을 불러올 때 쓴다.
    ///
    /// 수첩은 단서 줄(글)만 저장했다. 그림·종이·뒷면·한글 벌은 텍스처 <b>참조</b>라 JSON 에 못 담는다 —
    /// 그래서 이어하기를 하면 수첩 카드가 빈 칸이 되고 장계 증좌 카드에도 그림이 없었다(2026-09-15).
    /// 저장에는 <b>이름</b>만 적고, 불러올 때 이 표에서 같은 이름을 찾아 되붙인다.
    ///
    /// 표는 편집기 메뉴(이문록 ▸ 수첩 ▸ 단서 그림 표 채우기)가 채운다. Resources 에 둔다.
    /// </summary>
    [CreateAssetMenu(fileName = "이문록_단서그림", menuName = "이문록/단서 그림 표", order = 61)]
    public class ClueArtIndex : ScriptableObject
    {
        public const string ResourceName = "이문록_단서그림";

        public Texture2D[] 그림 = new Texture2D[0];
        public GameObject[] 모형 = new GameObject[0];

        private static ClueArtIndex _loaded;
        private static bool _tried;
        private Dictionary<string, Texture2D> _tex;
        private Dictionary<string, GameObject> _obj;

        public static ClueArtIndex Index
        {
            get
            {
                if (_tried) return _loaded;
                _tried = true;
                _loaded = Resources.Load<ClueArtIndex>(ResourceName);
                return _loaded;
            }
        }

        public Texture2D Texture(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (_tex == null)
            {
                _tex = new Dictionary<string, Texture2D>();
                foreach (var t in 그림) if (t != null) _tex[t.name] = t;
            }
            Texture2D found;
            return _tex.TryGetValue(name, out found) ? found : null;
        }

        public GameObject Model(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (_obj == null)
            {
                _obj = new Dictionary<string, GameObject>();
                foreach (var o in 모형) if (o != null) _obj[o.name] = o;
            }
            GameObject found;
            return _obj.TryGetValue(name, out found) ? found : null;
        }
    }
}
