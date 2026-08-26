// 1부 시작에 어사의 대화 스타일을 고르는 화면입니다. 카드 4장 중 하나를 레이로 누릅니다.
using System.Collections.Generic;
using IMUNROK.Seocheon.AI;
using IMUNROK.Seocheon.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IMUNROK.Seocheon
{
    /// <summary>
    /// 대화 스타일 선택 화면.
    ///
    /// ★"어사가 어떤 사람인가" 를 정하는 화면입니다. 난이도 선택이 아닙니다.
    ///   어느 것을 골라도 조각에는 도달합니다. ★얻는 것과 잃는 것이 다를 뿐입니다.
    ///
    /// ★VR 대비: 버튼이 큽니다(카드 380×520). 레이 포인터로 눌러도 놓치지 않는 크기입니다.
    /// </summary>
    public sealed class SeocheonStyleScreen : MonoBehaviour
    {
        [Header("데이터")]
        [SerializeField] private SeocheonTalkStyles styleTable;

        [Header("카드")]
        [Tooltip("스타일 카드 버튼 4개")]
        [SerializeField] private Button[] cardButtons;
        [SerializeField] private TMP_Text[] nameLabels;
        [SerializeField] private TMP_Text[] oneLineLabels;
        [SerializeField] private TMP_Text[] exampleLabels;

        [Header("표시")]
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text hintLabel;

        [Header("동작")]
        [Tooltip("아직 고르지 않았으면 시작하자마자 띄웁니다")]
        [SerializeField] private bool openOnStart = true;

        private readonly List<TalkStyle> shown = new List<TalkStyle>();
        private bool isOpen;
        private bool playerLocked;

        public bool IsOpen { get { return isOpen; } }

        private void Awake()
        {
            BindButtons();

            // ★여기서 자기 자신을 끄면 Start() 가 오지 않습니다. 끄는 판단은 Start() 에서 합니다.
        }

        private void Start()
        {
            // ★1부 시작 — 아직 안 골랐으면 여기서 고르게 합니다.
            if (openOnStart && !SeocheonTalkStyleState.HasChosen) { Open(); return; }

            // ★도입부 화면이 자기보다 먼저 Start 를 돌며 이미 나를 열어 두었을 수 있습니다.
            //   (Start 순서는 정해져 있지 않습니다) 그때는 닫지 않습니다.
            if (!isOpen) gameObject.SetActive(false);
        }

        private void BindButtons()
        {
            if (cardButtons == null) return;
            for (int i = 0; i < cardButtons.Length; i++)
            {
                if (cardButtons[i] == null) continue;
                int index = i;
                cardButtons[i].onClick.AddListener(delegate { OnCardClicked(index); });
            }
        }

        public void Open()
        {
            if (isOpen) return;
            if (styleTable == null || styleTable.styles == null || styleTable.styles.Length == 0)
            {
                Debug.LogError("[서천스타일] 스타일표가 비어 있습니다. 선택 화면을 열지 않습니다.", this);
                gameObject.SetActive(false);   // ★빈 화면을 띄운 채로 두지 않습니다.
                return;
            }

            gameObject.SetActive(true);
            isOpen = true;
            if (!playerLocked) { PlayerControlLock.Push(); playerLocked = true; }

            if (titleLabel != null) titleLabel.text = "그대는 어떤 장사꾼으로 보이겠소?";
            if (hintLabel != null) hintLabel.text = "정답은 없소. 무엇을 얻고 무엇을 잃을지가 다를 뿐이오.";

            shown.Clear();
            for (int i = 0; i < cardButtons.Length; i++)
            {
                bool used = i < styleTable.styles.Length && styleTable.styles[i] != null;
                if (cardButtons[i] != null) cardButtons[i].gameObject.SetActive(used);
                if (!used) continue;

                TalkStyle st = styleTable.styles[i];
                shown.Add(st);
                if (nameLabels != null && i < nameLabels.Length && nameLabels[i] != null)
                    nameLabels[i].text = st.displayName;
                if (oneLineLabels != null && i < oneLineLabels.Length && oneLineLabels[i] != null)
                    oneLineLabels[i].text = st.oneLine;
                if (exampleLabels != null && i < exampleLabels.Length && exampleLabels[i] != null)
                    exampleLabels[i].text = "“" + st.exampleLine + "”";
            }
        }

        public void Close()
        {
            if (!isOpen) return;
            isOpen = false;
            if (playerLocked) { playerLocked = false; PlayerControlLock.Pop(); }
            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            isOpen = false;
            if (playerLocked) { playerLocked = false; PlayerControlLock.Pop(); }
        }

        private void OnCardClicked(int index)
        {
            if (index < 0 || index >= shown.Count) return;
            SeocheonTalkStyleState.Choose(shown[index]);
            Debug.Log("[서천스타일] 선택 = " + shown[index].displayName);
            Close();
        }
    }
}
