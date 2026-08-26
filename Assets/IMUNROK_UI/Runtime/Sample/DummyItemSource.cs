using System;
using System.Collections.Generic;
using UnityEngine;

namespace IMUNROK.Ui
{
    /// <summary>
    /// 더미 소지품 넷을 만들어 <see cref="UiItems.Source"/> 에 꽂는다.
    ///
    /// 실물(3D)은 <b>기본 도형을 그 자리에서 만들어</b> 쓴다 — 꾸러미에 모델 파일을 넣지 않으려는
    /// 것이고, 미리보기·전체 화면 조사가 도는지 보는 데는 이걸로 충분하다.
    ///
    /// ⚠️ 이 파일에는 <b>MonoBehaviour 를 하나만</b> 둔다. 한 파일에 여럿을 넣으면 클래스 이름과
    ///    파일 이름이 어긋나 <b>씬에 붙인 것이 「missing script」로 떨어져 나간다</b> (2026-08-26 실측).
    /// </summary>
    [AddComponentMenu("이문록 UI/더미 소지품 (DummyItemSource)")]
    public class DummyItemSource : MonoBehaviour, IUiItemSource
    {
        readonly List<IUiItem> items = new List<IUiItem>();

        public IReadOnlyList<IUiItem> Items { get { return items; } }
        public event Action Changed;
        public event Action<IUiItem> Added;

        void Awake()
        {
            var hidden = new GameObject("더미_실물_보관");
            hidden.transform.SetParent(transform, false);
            hidden.SetActive(false);   // 본이라 씬에 보이면 안 된다

            items.Add(Make(hidden, "DUMMY_1", "낡은 목패", PrimitiveType.Cube, new Vector3(0.6f, 1f, 0.08f),
                new Color(0.45f, 0.33f, 0.20f),
                "글자가 지워진 나무 패. 끈을 매었던 자국이 남아 있다.\n\n뒷면에는 아무것도 없다."));
            items.Add(Make(hidden, "DUMMY_2", "청자 조각", PrimitiveType.Sphere, new Vector3(0.7f, 0.7f, 0.7f),
                new Color(0.42f, 0.62f, 0.58f),
                "푸른 빛이 도는 사금파리. 깨진 자리가 아직 날카롭다."));
            items.Add(Make(hidden, "DUMMY_3", "먹으로 쓴 쪽지", PrimitiveType.Quad, new Vector3(0.8f, 1.1f, 1f),
                new Color(0.86f, 0.80f, 0.67f),
                "접었다 편 자국이 여러 겹이다. 글씨는 급히 쓴 티가 난다.\n\n" +
                "긴 글이 어떻게 흐르는지 보려고 일부러 길게 적어 둔 더미 설명이다. " +
                "틀을 넘치면 휠로 굴려 읽을 수 있어야 한다."));
            var lamp = Make(hidden, "DUMMY_4", "손잡이 등잔", PrimitiveType.Cylinder, new Vector3(0.5f, 0.35f, 0.5f),
                new Color(0.72f, 0.58f, 0.28f),
                "기름이 조금 남아 있다. 심지는 아직 쓸 만하다.");
            lamp.ShowUseButton = true;      // 「사용하기」 단추가 어떻게 보이는지 확인용
            lamp.UseLabel = "불 켜기";
            items.Add(lamp);

            UiItems.Source = this;
        }

        static DummyItem Make(GameObject parent, string key, string name,
                              PrimitiveType shape, Vector3 scale, Color color, string desc)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = "더미_" + name;
            go.transform.SetParent(parent.transform, false);
            go.transform.localScale = scale;
            var col = go.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);          // 본에 콜라이더는 필요 없다
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (mat.shader == null) mat = new Material(Shader.Find("Standard"));
                mat.color = color;
                mr.sharedMaterial = mat;
            }
            return new DummyItem(key, name, desc) { ModelPrefab = go };
        }

        /// <summary>목록이 바뀌었다고 알린다 (더미에서는 쓸 일이 없지만 규약을 지킨다).</summary>
        public void Raise() { if (Changed != null) Changed(); }
    }
}
