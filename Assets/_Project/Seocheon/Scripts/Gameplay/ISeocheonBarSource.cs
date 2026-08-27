// 서천 대화 바가 말 상대에게 <더> 묻는 것 — 선택지와 어절 후보. 견우 IDialogueBackend 를 대체하지 않고 얹는다.
using System.Collections.Generic;
using IMUNROK.Seocheon.AI;

namespace IMUNROK.Seocheon
{
    /// <summary>
    /// 서천에만 있는 두 가지를 화면에 넘기는 계약 (2026-08-27).
    ///
    /// ■ 왜 <see cref="IMUNROK.Ui.IDialogueBackend"/> 를 안 늘렸나
    ///   그건 꾸러미 파일이다 — 고치면 다음 배포본에 덮여 사라진다.
    ///   그래서 <b>따로 두고 함께 구현</b>한다. 바는 <c>as ISeocheonBarSource</c> 로 물어보고,
    ///   아니면 선택지·어절 없이 견우 화면 그대로 돈다.
    ///
    /// ■ ★화면은 판정을 모른다
    ///   유효 조각인지 아닌지는 여기 어디에도 나오지 않는다.
    ///   <see cref="NotifyWordPicked"/> 를 받은 <b>말 상대</b>가 저장소에 적을 뿐이다.
    /// </summary>
    public interface ISeocheonBarSource
    {
        /// <summary>다음에 물을 것 넷. 비면 선택지 칸이 감춰진다.</summary>
        IReadOnlyList<SeocheonAsk> Asks { get; }

        /// <summary>
        /// 지금 화면에 뜬 대사와 <b>글자 그대로 같은</b> 문장 + 그 안의 어절 후보.
        /// ★같지 않으면 바가 어절 판정을 아예 하지 않는다(글자 번호가 어긋나므로).
        /// </summary>
        WordPickNote.Sentence PickSource { get; }

        /// <summary>선택지를 골랐다. 자유 입력과 <b>같은 길</b>로 흘려보낼 것.</summary>
        void ChooseAsk(int index);

        /// <summary>어절을 지목했다. 수첩에 적는 일은 <b>말 상대</b>가 한다.</summary>
        void NotifyWordPicked(WordPickNote.WordOption option);
    }
}
