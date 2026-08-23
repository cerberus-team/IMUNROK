using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>등불에 종이를 비춘다.</b> 손에 등불을 들고 문서를 쥐고 있으면, 종이가
    /// 빛을 먹다가 배접 속에 숨은 글이 배어 나온다.
    ///
    /// <b>왜 이 짝인가</b>: 조선 문서에서 숨길 데는 종이 <b>사이</b>다. 배접(褙接)은
    /// 종이를 여러 겹 붙여 두껍게 만드는 일이고, 그 겹 사이에 한 장을 끼워 넣으면
    /// 겉으로는 아무것도 없다. 등잔 뒤에 대면 그때만 그림자가 비친다. 밀랍으로 눌러
    /// 쓴 자국도, 물에 지운 먹도 마찬가지다 — 전부 <b>빛을 뒤에서</b> 넣어야 나온다.
    ///
    /// <b>돋보기와 무엇이 다른가</b>: 돋보기는 <b>이미 그려진 것을 알아보는</b> 도구고,
    /// 등불은 <b>없던 것을 불러내는</b> 도구다. 그래서 돋보기로 읽은 것은 종이 밖에
    /// 적히고(종이에 글자를 보태지 않는다), 등불로 나온 것은 종이 위에 떠오른다.
    ///
    /// <b>손짓은 같게 두었다</b>: 대고 기다린다. 도구가 둘인데 쓰는 법이 서로 다르면
    /// 하나를 익혀도 다른 하나를 또 처음부터 익혀야 한다. 팀원이 도구를 하나 더
    /// 얹어도 같은 손짓으로 쓰게 된다.
    ///
    /// <b>씬에 놓을 것이 없다</b>: 게임이 시작될 때 저 혼자 하나 선다. 사건 씬마다
    /// 이것을 잊지 않고 놓아야 한다면, 잊는 씬이 반드시 생긴다.
    /// </summary>
    public class LanternReveal : MonoBehaviour
    {
        /// <summary>등불로 치는 도구 id.</summary>
        private const string ToolId = "lantern";

        /// <summary>다 배어 나오기까지 걸리는 시간(초).</summary>
        /// 배어 나오는 것은 <b>천천히</b>라야 배어 나온 것이 된다. 1.6초는 들자마자
        /// 나타나는 꼴이라, 빛에 드러난 것인지 그냥 켜진 것인지 구별이 안 됐다.
        private const float Seconds = 3.0f;

        /// <summary>등불을 내리면 이 빠르기로 도로 식는다(초당). 켤 때보다 느리다.</summary>
        private const float Cool = 0.45f;

        private float _t;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<LanternReveal>() != null) return;
            var go = new GameObject("_등불비추기");
            go.AddComponent<LanternReveal>();
            DontDestroyOnLoad(go);
        }

        private void Update()
        {
            if (!DocumentView.IsOpen) { _t = 0f; return; }

            // 종이에 등불로 볼 것이 없으면 아무 일도 안 한다. 그냥 옛 문서를 들고
            // 등불을 켜 든 것뿐인데 종이가 노랗게 달아오르면, 뭔가 있는 줄 알고
            // 한참을 서 있게 된다.
            if (!DocumentView.HasBacklight) { _t = 0f; return; }

            bool holding = ToolbeltHud.SelectedToolId == ToolId;
            if (holding) _t += Time.deltaTime / Seconds;
            else { _t -= Cool * Time.deltaTime; if (_t <= 0f) { _t = 0f; return; } }

            _t = Mathf.Clamp01(_t);
            DocumentView.Lighting(_t);
        }
    }
}
