using System.Collections;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>도구를 내준다</b> — 이 씬에 들어서면 벨트에 얹힌다.
    ///
    /// <b>왜 필요했나</b>: 도구를 인스펙터의 벨트 목록에만 적어 두면 <b>처음부터 다
    /// 들고 있거나 아예 못 들거나</b> 둘 중 하나가 된다. 그러면 유척처럼 <b>2막에나
    /// 쓸 물건</b>이 1막 내내 벨트에 걸려 있게 된다 — 잴 것이 하나도 없는데 Q 를 돌릴
    /// 때마다 빈 칸을 헛돈다. 무엇보다 유척은 임금이 내린 어사의 표식이라, 과객 행세를
    /// 하는 동안 허리에 차고 다닐 물건이 아니다.
    ///
    /// 그래서 <b>쓸 데가 생기는 곳</b>에 이것을 하나 둔다. 관아에 들어서면 자가 생긴다.
    ///
    /// <b>벨트를 기다린다</b>: 벨트는 다른 씬에 있을 수 있고, 씬이 겹쳐 올라오는
    /// 차례는 정해져 있지 않다. Start 에서 한 번 찾아보고 없다고 물러나면 조용히
    /// 아무 일도 안 일어난다 — 오류도 안 뜨고 도구도 안 생긴다. 그래서 잠깐 기다려 본다.
    ///
    /// 붙이는 곳: 그 씬의 아무 빈 오브젝트 하나.
    /// </summary>
    public class ToolIssue : MonoBehaviour
    {
        [Tooltip("내줄 도구들. 이미 벨트에 있으면 그냥 넘어간다")]
        [SerializeField] private ToolDef[] _tools;

        [Tooltip("끄면 저절로 안 내주고, 다른 곳에서 Issue() 를 불러 줘야 한다")]
        [SerializeField] private bool _onStart = true;

        [Tooltip("벨트가 아직 안 올라왔으면 이만큼(초) 기다려 본다. 그 안에 안 오면 물러난다")]
        [SerializeField] private float _waitForBelt = 5f;

        [TextArea(2, 3)]
        [Tooltip("내줄 때 한 마디. 비우면 조용히 얹는다. *별표*로 감싼 낱말은 도드라진다")]
        [SerializeField] private string _line = "";

        [SerializeField] private string _speaker = "";

        private void Start() { if (_onStart) Issue(); }

        /// <summary>도구를 내준다. 벨트가 아직 없으면 잠깐 기다렸다 얹는다.</summary>
        public void Issue()
        {
            if (_tools == null || _tools.Length == 0) return;
            StartCoroutine(Give());
        }

        private IEnumerator Give()
        {
            float waited = 0f;
            while (ToolbeltHud.Instance == null && waited < _waitForBelt)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            var belt = ToolbeltHud.Instance;
            if (belt == null)
            {
                Debug.LogWarning("[도구] 벨트를 못 찾아 " + name + " 이 아무것도 못 내줬다.", this);
                yield break;
            }

            bool any = false;
            foreach (var t in _tools)
                if (belt.Grant(t)) any = true;

            if (any && !string.IsNullOrEmpty(_line)) SubtitleView.Show(_speaker, _line, "", true);
        }
    }
}
