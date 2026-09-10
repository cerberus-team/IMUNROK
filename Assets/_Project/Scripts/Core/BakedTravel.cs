using UnityEngine;

namespace IMUNROK.Common
{
    /// <summary>
    /// <b>클립에 구워진 이동을 걷어낸다 — 제자리걸음으로 만든다.</b>
    ///
    /// 믹사모에서 받아 온 걷기 클립은 <b>제자리가 아니다</b>. 한 바퀴(1.03초) 도는 동안
    /// 엉덩이뼈가 앞으로 1.9m 나아가고, 바퀴가 끝나면 처음 자리로 되돌아온다.
    /// 그 이동이 <b>뼈에 구워져</b> 있는 것이 문제다.
    ///
    /// 유니티에는 이런 이동을 뽑아내는 장치가 있다(Root Motion). 그런데 그것은
    /// <b>루트가 움직일 때</b>의 이야기다. 이 뼈대는 이동이 루트가 아니라 <b>엉덩이뼈</b>에
    /// 들어 있고, 유니티는 자식 뼈의 이동을 포즈에서 빼 주지 않는다 — Root Motion Node 를
    /// 엉덩이로 지목해도, 클립마다 Bake Into Pose 를 뒤집어도 마찬가지다.
    /// (실제로 재 보면 <c>applyRootMotion</c> 을 켜면 루트가 움직이는 <b>동시에</b> 포즈도
    /// 그대로 나아가 이동이 <b>두 번</b> 먹는다.)
    ///
    /// 그 결과가 이것이다: 코드가 몸을 1.1m/초로 옮기는 동안 <b>보이는 몸은 제 발치보다
    /// 1m 앞서 떠 있다가</b>, 바퀴가 끝나면 툭 되돌아온다. 걸음마다 미끄러지고 튄다.
    /// 말을 거는 판정(콜라이더)은 루트에 붙어 있으니 <b>보이는 곳과 닿는 곳도 어긋난다</b>.
    ///
    /// 그래서 매 칸, 애니메이터가 포즈를 다 쓴 뒤에 <b>뼈대 전체를 앞뒤로 되민다</b>.
    /// 되미는 양은 그 칸의 엉덩이가 앞뒤로 나가 있는 만큼이다 — 결국 엉덩이는 늘 루트의
    /// 앞뒤 한가운데 머문다. 클립이 몸을 어디로 옮기든 <b>보이는 몸은 루트에 붙어 있다</b>.
    ///
    /// <b>좌우(x)는 건드리지 않는다.</b> 걷기의 좌우 흔들림은 5cm 안쪽에서 오갈 뿐 쌓이지
    /// 않는다 — 그것까지 눌러 버리면 걸음이 널빤지처럼 뻣뻣해진다. 쌓이는 것은 앞뒤뿐이다.
    ///
    /// 덤으로 <b>클립 사이의 튐</b>도 사라진다. 앉는 클립은 끝에서 몸이 0.49m 뒤로 가 있고
    /// 앉아 있는 클립은 제자리인데, 둘을 이으면 앉자마자 몸이 0.5m 앞으로 튀어나왔다.
    /// 둘 다 앞뒤를 0에 붙들어 두면 이을 자리가 애초에 어긋나지 않는다.
    /// </summary>
    public sealed class BakedTravel
    {
        private Transform _root;      // 게임이 아는 자리(코드가 옮기는 그것)
        private Transform _skeleton;  // 루트 바로 밑의 뼈대 꼭지(Armature)
        private Transform _bone;      // 이동이 구워진 뼈(Hips)
        private Vector3 _rest;        // 뼈대 꼭지의 원래 자리

        /// <summary>
        /// 이동이 구워진 뼈를 찾아 묶는다. 못 찾으면 아무 일도 안 한다.
        ///
        /// <b><see cref="GroundFeet"/> 가 붙어 있으면 손을 떼고 물러난다.</b>
        /// 저쪽이 이미 같은 일을 한다 — 매 칸 엉덩이뼈를 마커 자리에 수평으로
        /// 끌어다 붙이므로 구워진 이동이 거기서 이미 걷힌다. 게다가 저쪽은
        /// <b>같은 칸의 높이</b>에 발밑 보정을 적어 둔다. 둘이 겹치면 이쪽이
        /// 그 높이를 매 칸 지워, 사람이 비탈에서 땅에 잠긴 채 걷게 된다.
        /// </summary>
        public void Bind(Transform root, Animator animator)
        {
            _root = null; _skeleton = null; _bone = null;
            if (root == null) return;
            if (root.GetComponentInParent<GroundFeet>() != null) return;

            Transform hips = null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (n == "hips" || n.EndsWith(":hips")) { hips = t; break; }
            }
            if (hips == null) return;

            // 뼈대 꼭지 = 루트 바로 아래 칸. 이 녀석을 되밀어야 뼈가 통째로 따라온다.
            // (살갗은 뼈를 따라 씌워지므로 뼈만 옮기면 몸도 같이 온다.)
            var skel = hips;
            while (skel.parent != null && skel.parent != root) skel = skel.parent;
            if (skel.parent != root) return;      // 루트 밑에 없다 — 손대지 않는다

            _root = root; _skeleton = skel; _bone = hips;
            _rest = skel.localPosition;
        }

        /// <summary>
        /// 앞뒤로 새어 나간 만큼 되민다. <b>LateUpdate 에서</b> 부른다 —
        /// 애니메이터가 포즈를 다 쓴 뒤라야 그 칸의 어긋남을 잴 수 있다.
        /// </summary>
        public void Cancel()
        {
            if (_root == null || _skeleton == null || _bone == null) return;

            // 먼저 제자리로 되돌린다. 안 그러면 <b>지난 칸에 되민 값이 섞여</b>
            // 재는 값이 제 꼬리를 물고 커진다.
            _skeleton.localPosition = _rest;

            float ahead = _root.InverseTransformPoint(_bone.position).z;
            _skeleton.localPosition = _rest - new Vector3(0f, 0f, ahead);
        }
    }
}
