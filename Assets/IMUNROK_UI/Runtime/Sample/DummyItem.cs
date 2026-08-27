using System;
using System.Collections.Generic;
using UnityEngine;

namespace IMUNROK.Ui
{
    /// <summary>
    /// <b>아무것도 구현하지 않아도 소지품 판이 돌게</b> 하는 더미 물건 (2026-08-26).
    ///
    /// 예제 씬이 이것으로 넷을 만들어 꽂는다. 팀원은 씬을 열어 화면을 눈으로 확인한 뒤,
    /// 자기 물건 클래스에 <see cref="IUiItem"/> 을 붙여 갈아 끼우면 된다.
    /// <b>ScriptableObject 가 아니라 그냥 클래스다</b> — 꾸러미에 .asset 파일을 넣지 않으려는 것이다.
    /// </summary>
    public class DummyItem : IUiItem
    {
        public string Key { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public GameObject ModelPrefab { get; set; }
        public Vector3 PreviewEuler { get; set; }
        public float PreviewZoom { get; set; }
        public bool ShowUseButton { get; set; }
        public string UseLabel { get; set; }
        public string UseNotReadyHint { get; set; }
        public bool AutoShowOnPickup { get; set; }

        public DummyItem(string key, string name, string desc)
        {
            Key = key; DisplayName = name; Description = desc;
            PreviewEuler = new Vector3(15f, 25f, 0f);
            PreviewZoom = 1f;
            UseLabel = "써 보기";
            UseNotReadyHint = "아직 여기서 쓸 수 없다.";
            AutoShowOnPickup = false;
        }
    }
}
