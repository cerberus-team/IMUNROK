# -*- coding: utf-8 -*-
"""문서 종이의 뒷면을 굽는다 — 앞면을 좌우로 뒤집고, 먹만 옅게 비치게.

한지는 얇다. 뒤집으면 앞의 글이 <b>거울에 비친 채 흐리게</b> 배어 보이는 것이 제 모습이고,
민 한지빛 한 장을 보이면 종이가 아니라 판자다(수첩에서 「눌러서 뒤집는다」가 그랬다).

  · 종이 결·얼룩·가장자리(알파)는 앞면 그대로 — 같은 종이의 두 면이어야 한다.
  · 먹 자리만 둘레 종이빛 쪽으로 84% 끌어올려, 16% 만 비치게 한다.
  · 좌우를 뒤집는다. 판(DetailTurner·DocumentView)은 뒷면을 그릴 때 한 번 더 뒤집지 않는다.

앞면을 다시 구웠으면(make_docs.ps1) 이것도 다시 돌린다.
타다 만 종이(J10)처럼 뒤에 따로 적힌 것이 있는 종이는 docs.json 에 _back 을 따로 두고,
여기서는 건너뛴다.
"""
import glob, os, sys
from PIL import Image, ImageFilter, ImageChops

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
FOLDER = os.path.join(ROOT, 'Assets', '_Project', 'Onggojip', 'Art', 'Textures')
SHOW = 0.16          # 먹이 비치는 정도
SKIP_SUFFIX = ('_ko', '_lit', '_lit_ko', '_back')


def bake(src, dst):
    im = Image.open(src).convert('RGBA')
    rgb = im.convert('RGB')
    alpha = im.getchannel('A')

    # 먹을 걷어 낸 종이빛: 획보다 넓은 창으로 밝은 쪽을 고르고 부드럽게 편다.
    paper = rgb.filter(ImageFilter.MaxFilter(21)).filter(ImageFilter.GaussianBlur(8))

    # 얼마나 먹인가 — 종이빛보다 어두운 만큼. 0.25 이상 어두우면 온전한 먹으로 친다.
    lum_rgb = rgb.convert('L')
    lum_pap = paper.convert('L')
    dark = ImageChops.subtract(lum_pap, lum_rgb)                 # 0..255
    mask = dark.point(lambda v: min(255, int(v * 255 / 64)))     # 64 (≈0.25) 에서 가득

    faint = Image.blend(paper, rgb, SHOW)                          # 먹이 16% 만 남은 면
    out = Image.composite(faint, rgb, mask)                        # 먹 자리만 갈아 끼운다
    # 뒷면은 볕을 덜 받은 쪽이라 아주 조금 어둡고 거칠다.
    out = out.point(lambda v: int(v * 0.965))

    out = out.transpose(Image.FLIP_LEFT_RIGHT)
    a = alpha.transpose(Image.FLIP_LEFT_RIGHT)
    out.putalpha(a)
    out.save(dst)


def main():
    made = []
    for src in sorted(glob.glob(os.path.join(FOLDER, 'T_Doc_*.png'))):
        base = os.path.splitext(os.path.basename(src))[0]
        if base.endswith(SKIP_SUFFIX):
            continue
        if os.path.exists(os.path.join(FOLDER, base + '_back.png')) and base + '_back' not in AUTO:
            continue
        dst = os.path.join(FOLDER, base + '_back.png')
        bake(src, dst)
        made.append(base + '_back')
    print('\n'.join(made))


# 이 스크립트가 만든 뒷면 — 다시 돌리면 덮어쓴다. 여기 없는 _back(손으로 적은 뒷면)은 건드리지 않는다.
AUTO = {
    'T_Doc_Case1_Field_back', 'T_Doc_J06_Jangbu_back', 'T_Doc_J06_Yeonseup_back',
    'T_Doc_J07_Mulmok_back', 'T_Doc_J08_Chayong_back', 'T_Doc_J09_Jeungseo_back',
    'T_Doc_J11_Byeolgeup_back', 'T_Doc_J12_Hojeok_back', 'T_Doc_J13_Hogu_back',
    'T_Doc_G01_Hojeok_Wonbon_back', 'T_Doc_G02_Hogu_Wonbon_back', 'T_Doc_G03_Ipan_back',
    'T_Doc_G04_Hwansang_back', 'T_Doc_G12_Myeoncheon_back', 'T_Doc_G14_Soji_back',
}

if __name__ == '__main__':
    main()
