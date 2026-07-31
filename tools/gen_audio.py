# 消灯 — 音の生成
#
# 監査したところ、AudioSystem にクリップが1つも割り当てられておらず、
# **ゲームは完全に無音だった。** 既存の wav はサイン波のプレースホルダで、
# しかも3フロア分の BGM が同一ファイル（55Hz の持続音、rms 0.238）。
# ホラーで音が無いのは、暗くない暗闇と同じで成立しない。
#
# ここで作るのは「曲」ではなく**空間の音**。廃病院に立っているときに
# 実際に聞こえるものを積む:
#   - 建物の低い唸り（空調・configuration の残響）
#   - 蛍光灯の電源ハム（50Hz とその倍音）
#   - 空気のノイズ
#   - 遠くの金属の軋み、水滴
#
# 音量について。環境音は rms 0.02〜0.05（-34〜-26 dBFS）に収める。
# プレースホルダは 0.238 で、環境音としては 20dB ほど過大だった。
# 常時鳴る音が大きいと、驚かせたい瞬間に上げる余地が無くなる。
#
# ループの継ぎ目について。**周波数領域で作ってから逆FFTする。**
# 時間領域でノイズを作って切ると端が不連続になり、ループのたびにプツッと鳴る。
# 周波数領域から作った信号は定義上その長さで周期的なので継ぎ目が出ない。
#
# 実行: python tools/gen_audio.py

import os
import struct
import wave

import numpy as np

SR = 44100
OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                   "Assets", "Audio")

rng = np.random.default_rng(20260731)   # 固定。再生成で音が変わらないように


# ----------------------------------------------------------------------
# 土台
# ----------------------------------------------------------------------
def spectral_noise(seconds, shape_fn):
    """
    周波数領域でスペクトルを作ってから時間領域に戻す。
    こうして作った信号は seconds ちょうどで周期的になるので、
    ループさせても継ぎ目が鳴らない。
    """
    n = int(SR * seconds)
    freqs = np.fft.rfftfreq(n, 1 / SR)
    mag = shape_fn(freqs)
    phase = rng.uniform(0, 2 * np.pi, freqs.size)
    spec = mag * np.exp(1j * phase)
    spec[0] = 0.0                      # 直流は落とす
    out = np.fft.irfft(spec, n)
    peak = np.abs(out).max()
    return out / peak if peak > 0 else out


def band(freqs, low, high, slope=2.0):
    """low〜high を通す。両端はなだらかに落とす（急峻だと不自然に響く）"""
    m = np.zeros_like(freqs)
    inside = (freqs >= low) & (freqs <= high)
    m[inside] = 1.0
    below = freqs < low
    m[below] = np.clip((freqs[below] / max(low, 1e-6)) ** slope, 0, 1)
    above = freqs > high
    m[above] = np.clip((high / np.maximum(freqs[above], 1e-6)) ** slope, 0, 1)
    return m


def hum(seconds, base=50.0, harmonics=(1, 2, 3, 4, 6), levels=(1.0, 0.55, 0.3, 0.18, 0.08)):
    """
    電源ハム。日本の東側は 50Hz。蛍光灯とトランスの音の芯になる。
    倍音を混ぜないと「サイン波」に聞こえてしまう。
    周期がちょうど収まるよう周波数を丸めて、ループの継ぎ目を消す。
    """
    n = int(SR * seconds)
    t = np.arange(n) / SR
    out = np.zeros(n)
    for h, lv in zip(harmonics, levels):
        f = base * h
        # seconds の中に整数周期入るよう丸める
        f = round(f * seconds) / seconds
        out += lv * np.sin(2 * np.pi * f * t + rng.uniform(0, 2 * np.pi))
    return out / np.abs(out).max()


def slow_drift(n, rate=0.05, depth=0.35):
    """ゆっくりした音量のうねり。一定だと機械の音に聞こえない"""
    periods = max(1, round(rate * n / SR))
    t = np.arange(n) / n
    d = np.zeros(n)
    for k in range(1, 4):
        d += np.sin(2 * np.pi * periods * k * t + rng.uniform(0, 2 * np.pi)) / k
    d /= np.abs(d).max()
    return 1.0 + depth * d


def reverb(x, decay=1.6, mix=0.35, predelay=0.02, circular=False):
    """
    合成した部屋鳴り。指数減衰するノイズを畳み込む。
    廊下も地下も硬い面ばかりなので、残響が無いと録音ブースの音になる。

    `circular=True` はループ素材用。
    通常の畳み込みは信号を伸ばすので、周期性が壊れて**ループの継ぎ目でプツッと鳴る**。
    実測すると 1F の環境音は継ぎ目の段差が平均の7倍あった。
    巡回畳み込みにすれば、末尾の残響が先頭に回り込むので周期性が保たれる。
    """
    n_ir = int(SR * decay)
    ir = rng.normal(0, 1, n_ir) * np.exp(-np.linspace(0, 7, n_ir))
    # 低域は長く、高域は早く減衰させる（実際の部屋はそうなる）
    ir_spec = np.fft.rfft(ir)
    f = np.fft.rfftfreq(n_ir, 1 / SR)
    ir_spec *= np.clip(1.0 / (1.0 + (f / 1800.0) ** 1.5), 0, 1)
    ir = np.fft.irfft(ir_spec, n_ir)
    ir /= np.abs(ir).max()

    pre = int(SR * predelay)

    if circular:
        # 巡回畳み込み。x と同じ長さのまま回り込ませる
        n = x.size
        kernel = np.zeros(n)
        take = min(n, ir.size)
        kernel[pre: pre + take] = ir[:take] if pre + take <= n else ir[: n - pre]
        wet = np.fft.irfft(np.fft.rfft(x) * np.fft.rfft(kernel), n)
    else:
        wet = np.convolve(x, ir)[: x.size + pre]
        wet = np.concatenate([np.zeros(pre), wet])[: x.size]

    peak = np.abs(wet).max()
    if peak > 0:
        wet /= peak
    return (1 - mix) * x + mix * wet


def normalize_rms(x, target):
    r = np.sqrt((x ** 2).mean())
    if r <= 0:
        return x
    y = x * (target / r)
    # 過大なピークだけ抑える。全体を割ると狙った音量が崩れる
    peak = np.abs(y).max()
    if peak > 0.95:
        y *= 0.95 / peak
    return y


def save(name, data, folder="SE"):
    path = os.path.join(OUT, folder, name)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    clipped = np.clip(data, -1.0, 1.0)
    pcm = (clipped * 32767).astype(np.int16)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    rms = np.sqrt((clipped ** 2).mean())
    print(f"  {name:34s} {data.size / SR:6.2f}s  rms={rms:.4f} peak={np.abs(clipped).max():.3f}")


# ----------------------------------------------------------------------
# 環境音（フロアごと）
# ----------------------------------------------------------------------
def room_tone(seconds, low_gain, hvac_gain, hum_gain, air_gain, rev_decay, rev_mix):
    n = int(SR * seconds)

    rumble = spectral_noise(seconds, lambda f: band(f, 18, 90, 2.5)) * low_gain
    hvac = spectral_noise(seconds, lambda f: band(f, 90, 500, 1.6)) * hvac_gain
    hvac *= slow_drift(n, rate=0.06, depth=0.4)
    mains = hum(seconds) * hum_gain
    air = spectral_noise(seconds, lambda f: band(f, 900, 9000, 1.0)) * air_gain

    mix = rumble + hvac + mains + air
    # ループ素材なので巡回畳み込みを使う
    return reverb(mix, decay=rev_decay, mix=rev_mix, circular=True)


def add_events(base, seconds, events):
    """
    遠くの物音を混ぜる。等間隔にせず、位置をずらす（規則的だと嘘に聞こえる）。

    末尾からはみ出した分は**先頭に回り込ませる**。切り捨てるとそこが不連続になり、
    ループのたびに同じ場所で段差が出る。
    """
    out = base.copy()
    n = out.size
    for maker, count, gain in events:
        for _ in range(count):
            clip = maker() * gain
            start = int(rng.uniform(0, n))
            idx = (np.arange(clip.size) + start) % n
            np.add.at(out, idx, clip)
    return out


def drip():
    """水滴。落ちた瞬間の高い成分と、そのあとの共鳴"""
    dur = 0.45
    n = int(SR * dur)
    t = np.arange(n) / SR
    f = 1500 * np.exp(-t * 28) + 320
    body = np.sin(2 * np.pi * np.cumsum(f) / SR)
    env = np.exp(-t * 16)
    click = rng.normal(0, 1, n) * np.exp(-t * 400) * 0.3
    return reverb((body * env + click) * 0.9, decay=1.2, mix=0.5)


def metal_groan():
    """遠くで金属が軋む音。建物が動いている感じを作る"""
    dur = rng.uniform(1.4, 2.6)
    n = int(SR * dur)
    t = np.arange(n) / SR
    f0 = rng.uniform(70, 160)
    wobble = 1.0 + 0.06 * np.sin(2 * np.pi * rng.uniform(3, 7) * t)
    tone = np.zeros(n)
    for h, lv in ((1, 1.0), (2, 0.5), (3, 0.28), (5, 0.12)):
        tone += lv * np.sin(2 * np.pi * np.cumsum(f0 * h * wobble) / SR)
    env = np.sin(np.pi * np.clip(t / dur, 0, 1)) ** 1.5
    return reverb(tone / np.abs(tone).max() * env, decay=2.2, mix=0.5)


def distant_door():
    """遠くの扉。低い衝撃と残響だけ聞こえる"""
    dur = 0.9
    n = int(SR * dur)
    t = np.arange(n) / SR
    thud = np.sin(2 * np.pi * 62 * t) * np.exp(-t * 12)
    crack = rng.normal(0, 1, n) * np.exp(-t * 55) * 0.35
    return reverb(thud + crack, decay=2.4, mix=0.65)


def floor_ambiences():
    print("環境音（フロアごとに別の音にする。同じ音だとフロアの違いが伝わらない）")
    seconds = 24.0

    # 1F: 受付階。空調が生きていて、いちばん「建物らしい」音がする
    a = room_tone(seconds, low_gain=0.35, hvac_gain=0.55, hum_gain=0.10,
                  air_gain=0.16, rev_decay=1.5, rev_mix=0.30)
    a = add_events(a, seconds, [(distant_door, 1, 0.22), (metal_groan, 1, 0.10)])
    save("Ambient_1F.wav", normalize_rms(a, 0.035), "Ambient")

    # 2F: 病棟。空調が弱く、静けさが勝る
    a = room_tone(seconds, low_gain=0.30, hvac_gain=0.34, hum_gain=0.13,
                  air_gain=0.12, rev_decay=1.8, rev_mix=0.34)
    a = add_events(a, seconds, [(metal_groan, 2, 0.12), (drip, 2, 0.10)])
    save("Ambient_2F.wav", normalize_rms(a, 0.030), "Ambient")

    # 3F: 最上階。空調がほぼ止まり、外の風が入る
    wind = spectral_noise(seconds, lambda f: band(f, 200, 2200, 1.2))
    wind *= slow_drift(int(SR * seconds), rate=0.09, depth=0.6)
    a = room_tone(seconds, low_gain=0.22, hvac_gain=0.18, hum_gain=0.09,
                  air_gain=0.10, rev_decay=2.0, rev_mix=0.36)
    a += wind * 0.10
    a = add_events(a, seconds, [(metal_groan, 3, 0.14), (drip, 1, 0.08)])
    save("Ambient_3F.wav", normalize_rms(a, 0.028), "Ambient")

    # 地下: 低音が支配的で残響が長い。水の音が絶えない
    a = room_tone(seconds, low_gain=0.85, hvac_gain=0.40, hum_gain=0.16,
                  air_gain=0.06, rev_decay=3.0, rev_mix=0.48)
    a = add_events(a, seconds, [(drip, 7, 0.16), (metal_groan, 2, 0.16),
                                (distant_door, 1, 0.18)])
    save("Ambient_Basement.wav", normalize_rms(a, 0.040), "Ambient")


# ----------------------------------------------------------------------
# 蛍光灯
# ----------------------------------------------------------------------
def fluorescent():
    """
    蛍光灯の音。安定器のハムと、放電のちりちりした音。
    光源そのものから鳴らすと、廊下のどこに立っているかが音で分かるようになる。
    """
    print("蛍光灯")
    seconds = 6.0
    n = int(SR * seconds)

    # 安定器のハム。整流されるので基本は 100Hz（電源の倍）
    ballast = hum(seconds, base=100.0, harmonics=(1, 2, 3, 5), levels=(1.0, 0.4, 0.22, 0.08))
    # 放電のノイズ
    hiss = spectral_noise(seconds, lambda f: band(f, 2000, 12000, 1.0)) * 0.25

    tube = ballast * 0.7 + hiss
    save("Fluorescent_Hum.wav", normalize_rms(tube, 0.06), "Ambient")

    # 切れかけの管。不規則にちりちり鳴る
    crackle = np.zeros(n)
    pos = 0
    while pos < n:
        pos += int(rng.exponential(SR * 0.35))
        if pos >= n:
            break
        length = int(rng.uniform(0.01, 0.06) * SR)
        seg = rng.normal(0, 1, min(length, n - pos))
        seg *= np.exp(-np.linspace(0, 6, seg.size))
        crackle[pos: pos + seg.size] += seg * rng.uniform(0.3, 1.0)

    dying = ballast * 0.45 + hiss * 0.6 + crackle * 0.5
    save("Fluorescent_Dying.wav", normalize_rms(dying, 0.075), "Ambient")


# ----------------------------------------------------------------------
# 効果音
# ----------------------------------------------------------------------
def footsteps():
    """
    硬い床の足音を6種類。1種類だけだと歩くたびに同じ音が鳴って機械的になる。
    既存の SE_Footstep は 0.15 秒で高域しか無く、足音というより「クリック」だった。
    """
    print("足音（同じ音の繰り返しを避けるため複数作る）")
    for i in range(6):
        dur = 0.30
        n = int(SR * dur)
        t = np.arange(n) / SR

        # 踵が当たる瞬間
        attack = rng.normal(0, 1, n) * np.exp(-t * rng.uniform(120, 190))
        # 床の共鳴。タイルなので高めで短い
        f0 = rng.uniform(150, 230)
        body = (np.sin(2 * np.pi * f0 * t) * 0.6 +
                np.sin(2 * np.pi * f0 * 2.4 * t) * 0.25) * np.exp(-t * 26)
        # 靴底が擦れる音
        scuff = spectral_noise(dur, lambda f: band(f, 1800, 7000, 1.0))[:n]
        scuff *= np.exp(-t * 30) * 0.35

        step = reverb(attack * 0.55 + body + scuff, decay=0.9, mix=0.28)
        save(f"Footstep_{i + 1}.wav", normalize_rms(step, 0.10), "SE")


def heartbeat():
    """心音。低い二拍。追われているときに上げる"""
    print("心音")
    dur = 1.15
    n = int(SR * dur)
    t = np.arange(n) / SR
    out = np.zeros(n)

    for offset, level, freq in ((0.00, 1.0, 58), (0.27, 0.72, 48)):
        start = int(offset * SR)
        seg_n = n - start
        st = np.arange(seg_n) / SR
        thump = np.sin(2 * np.pi * freq * st) * np.exp(-st * 15)
        thump += np.sin(2 * np.pi * freq * 2 * st) * np.exp(-st * 22) * 0.3
        # 立ち上がりに body を足さないと「サイン波の断片」に聞こえる
        thump += rng.normal(0, 1, seg_n) * np.exp(-st * 90) * 0.12
        out[start:] += thump * level

    save("SE_Heartbeat.wav", normalize_rms(out, 0.12), "SE")


def door_creak():
    """扉の軋み。摩擦なので、細かく途切れながら音程が動く"""
    print("扉")
    dur = 1.8
    n = int(SR * dur)
    t = np.arange(n) / SR

    # 摩擦の断続。これが無いと「音程の動く笛」になる
    stick = (rng.random(n) < 0.0016).astype(np.float32)
    stick = np.convolve(stick, np.exp(-np.linspace(0, 5, int(SR * 0.02))))[:n]

    f = 380 + 260 * np.sin(2 * np.pi * 0.55 * t) + 90 * np.sin(2 * np.pi * 3.1 * t)
    tone = np.sin(2 * np.pi * np.cumsum(f) / SR)
    env = np.sin(np.pi * np.clip(t / dur, 0, 1)) ** 0.8

    creak = tone * env * (0.35 + 0.65 * stick)
    save("SE_DoorCreak.wav", normalize_rms(reverb(creak, decay=1.6, mix=0.4), 0.10), "SE")


def enemy_detect():
    """見つかった瞬間。不協和な塊を鋭く立ち上げる"""
    print("敵の検知")
    dur = 1.6
    n = int(SR * dur)
    t = np.arange(n) / SR

    out = np.zeros(n)
    # 短二度・三全音を重ねる。協和させると「効果音」ではなく「音楽」になる
    for f, lv in ((220, 1.0), (233, 0.85), (311, 0.7), (466, 0.45)):
        out += lv * np.sin(2 * np.pi * f * t + rng.uniform(0, 2 * np.pi))
    out /= np.abs(out).max()

    attack = np.clip(t / 0.012, 0, 1)
    out *= attack * np.exp(-t * 3.2)
    out += rng.normal(0, 1, n) * np.exp(-t * 40) * 0.25

    save("SE_EnemyDetect.wav", normalize_rms(reverb(out, decay=2.0, mix=0.4), 0.16), "SE")


def announcement_chime():
    """
    館内放送のチャイム。放送文の前に鳴らす。
    既存の Voice_Announcement は 523Hz のサイン波で、声でも合図でもなかった。
    合成音声は作れないので、合図として成立するものに置き換える。
    """
    print("放送チャイム")
    dur = 3.2
    n = int(SR * dur)
    t = np.arange(n) / SR
    out = np.zeros(n)

    # 下降する二音。病院や駅の呼び出しに近い形
    for i, (f, start) in enumerate(((660.0, 0.0), (523.25, 0.55))):
        s = int(start * SR)
        st = np.arange(n - s) / SR
        env = np.exp(-st * 1.7) * np.clip(st / 0.006, 0, 1)
        partial = np.zeros(n - s)
        for h, lv in ((1, 1.0), (2, 0.28), (3, 0.12), (4.2, 0.06)):
            partial += lv * np.sin(2 * np.pi * f * h * st)
        out[s:] += partial / np.abs(partial).max() * env * (1.0 - i * 0.15)

    save("SE_AnnounceChime.wav", normalize_rms(reverb(out, decay=2.6, mix=0.45), 0.10), "SE")


def stinger():
    """幻覚・遭遇の瞬間に差し込む短い音。低い衝撃 + 金属の擦過"""
    print("スティンガー")
    dur = 2.2
    n = int(SR * dur)
    t = np.arange(n) / SR

    sub = np.sin(2 * np.pi * (55 * np.exp(-t * 1.2) + 28) * t) * np.exp(-t * 2.2)
    scrape = spectral_noise(dur, lambda f: band(f, 1200, 9000, 1.0))[:n]
    scrape *= np.exp(-t * 5) * 0.5
    hit = rng.normal(0, 1, n) * np.exp(-t * 120) * 0.6

    out = sub * 1.0 + scrape + hit
    save("SE_Stinger.wav", normalize_rms(reverb(out, decay=2.4, mix=0.42), 0.15), "SE")


# ----------------------------------------------------------------------
if __name__ == "__main__":
    print("=== 音を生成 ===")
    floor_ambiences()
    fluorescent()
    footsteps()
    heartbeat()
    door_creak()
    enemy_detect()
    announcement_chime()
    stinger()
    print("=== 完了 ===")
