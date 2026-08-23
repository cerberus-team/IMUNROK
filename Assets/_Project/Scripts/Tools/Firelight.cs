using System.Collections.Generic;
using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>불 하나</b> — 촛불이든 등잔이든 손에 든 등불이든.
    ///
    /// 배접 속을 읽는 것은 <b>등불의 재주가 아니라 불의 재주</b>다. 종이 뒤에 빛이
    /// 들면 겹 사이가 비치고, 그 빛이 무엇에서 나왔는지는 종이가 알 바 아니다.
    /// 그래서 이 부품은 도구가 아니라 <b>자리</b>다 — 방에 놓인 촛불에도 붙고,
    /// 손에 들고 다니는 등불에도 붙는다.
    ///
    /// 그렇게 두면 두 가지가 저절로 풀린다.
    ///   · 방의 불이 <b>쓸모를 얻는다</b>. 여태 촛불과 등잔은 그저 밝기만 했다.
    ///     이제 종이를 들고 그 앞에 서야 할 까닭이 생긴다.
    ///   · 손짓이 <b>필요 없어진다</b>. 단추를 눌러 무엇을 하는 것이 아니라,
    ///     불과 종이와 눈이 한 줄에 서면 비친다. VR 에서는 그냥 몸으로 하는 일이고,
    ///     모니터에서는 고개를 돌려 불을 마주 보는 일이다.
    ///
    /// 붙이는 곳: 불꽃이 있는 자리(촛불·등잔·등불 소품). 꺼져 있으면 세지 않는다.
    /// </summary>
    public class Firelight : MonoBehaviour
    {
        [Tooltip("이 불이 종이를 비출 수 있는 거리(m). 촛불은 짧고 등불은 길다")]
        [SerializeField] private float _reach = 2.2f;

        [Tooltip("이 불의 세기(0~1). 촛불 한 대는 약하고 등불은 세다")]
        [Range(0.2f, 1f)] [SerializeField] private float _strength = 1f;

        [Tooltip("이 빛(Light)이 꺼져 있으면 불도 꺼진 것으로 친다. 비우면 늘 켜진 것으로 본다")]
        [SerializeField] private Light _light;

        /// <summary>지금 켜져 있는 불 전부.</summary>
        public static readonly List<Firelight> All = new List<Firelight>();

        public float Reach => _reach;
        public float Strength => _strength;

        /// <summary>지금 이 불이 살아 있나 — 꺼진 등불은 종이를 못 비춘다.</summary>
        // 촛불은 <b>흔들린다</b>(CandleFlicker). 문턱을 높이 두면 흔들려 어두워진
        // 찰나마다 불이 꺼진 것으로 쳐서, 비추던 것이 자꾸 끊긴다. 아주 낮게 둔다.
        public bool Lit => isActiveAndEnabled && (_light == null || (_light.enabled && _light.intensity > 0.01f));

        private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        private void OnDisable() { All.Remove(this); }

        /// <summary>불꽃 자리. 부품이 붙은 곳을 그대로 쓴다.</summary>
        public Vector3 Where => transform.position;

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.75f, 0.35f, 0.45f);
            Gizmos.DrawWireSphere(transform.position, _reach);
        }
#endif
    }
}
