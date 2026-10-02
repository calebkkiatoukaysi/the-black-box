"""
Audio generator for The Black Box. Every WAV in Content/Music and Content/Sfx comes out of
this script. Run it from the repo root:

    python tools/generate_audio.py                     # everything
    python tools/generate_audio.py music               # the three songs
    python tools/generate_audio.py sfx                 # every sound effect
    python tools/generate_audio.py holding revolver    # just these, by file name
    python tools/generate_audio.py -v holding          # also print each layer's level

Only needs CPython, same as generate_assets.py. No samples are used. Everything is built from
sines, wavetables, noise and a few filters, and the random numbers are all seeded so a rerun
writes the exact same files.

Music is 16-bit stereo at 32 kHz and loops cleanly with MediaPlayer.IsRepeating. Sound effects
are 16-bit mono at 44.1 kHz and already balanced against each other, so the game can play them
all at the same volume.
"""

import cmath
import math
import os
import random
import sys
import time
import wave
import zlib
from array import array
from itertools import accumulate, repeat
from operator import add, and_, mul, rshift, sub

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")

MUSIC_RATE = 32000
SFX_RATE = 44100

# Loudness targets. Music gets normalised to an RMS level and kept under the peak ceiling.
# Sound effects get normalised by peak, louder for big moments and quieter for ones that fire a lot.
MUSIC_PEAK_DB = -1.0
TITLE_RMS_DB = -22.0
LOBBY_RMS_DB = -21.0
ARENA_RMS_DB = -17.0
LIMIT_KNEE = 0.5        # the limiter only touches samples above this (linear)

PEAK_BIG = -1.0         # slams, shots, hits, the two stingers
PEAK_ITEM = -4.0        # items and most table sounds
PEAK_MENU = -6.0        # confirm and back
PEAK_CLICK = -8.0       # UI clicks
PEAK_REPEAT = -10.0     # footsteps and text blips, which fire constantly

TITLE_BPM, TITLE_BARS = 52, 16
LOBBY_BPM, LOBBY_BARS = 58, 16
ARENA_BPM, ARENA_BARS = 92, 24

TAU = 2.0 * math.pi
VERBOSE = False


# --------------------------------------------------------------------------- #
# WAV writer and meters
# --------------------------------------------------------------------------- #

def db(x):
    return 20.0 * math.log10(x) if x > 1e-12 else -240.0


def undb(d):
    return 10.0 ** (d / 20.0)


def rms(*chans):
    count = sum(len(c) for c in chans)
    return math.sqrt(sum(math.sumprod(c, c) for c in chans) / count) if count else 0.0


def peak(*chans):
    return max(max(map(abs, c)) for c in chans if c)


def to_pcm(buf):
    """Floats in -1..1 to 16-bit, rounded half up and clamped."""
    return array("h", [-32767 if v < -1.0 else 32767 if v > 1.0 else int(v * 32767.0 + 32768.5) - 32768
                       for v in buf])


def write_wav(rel, chans, rate):
    path = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    pcm = [to_pcm(c) for c in chans]
    n = len(pcm[0])
    if len(pcm) == 1:
        data = pcm[0]
    else:
        data = array("h", bytes(4 * n))
        data[0::2] = pcm[0]
        data[1::2] = pcm[1]
    if sys.byteorder == "big":
        data.byteswap()
    with wave.open(path, "wb") as w:
        w.setnchannels(len(pcm))
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes(data.tobytes())
    top = max(max(map(abs, p)) for p in pcm) / 32768.0
    line = "  %s  (%.*f s, peak %.1f dBFS, rms %.1f dBFS)" % (
        rel, 2 if n < rate else 1, n / rate, db(top), db(rms(*chans)))
    clipped = sum(p.count(32767) + p.count(-32767) for p in pcm)
    if clipped:
        line += "  [%d samples at full scale]" % clipped
    print(line)


def seam_report(chans, rate):
    """Prints how clean the loop point is. I compare the jump from the last sample back
    to the first against the average step in the 10 ms around it (a click would be a lot bigger),
    then print the level of the first and last 50 ms."""
    def mean_step(x):
        return sum(map(abs, map(sub, x[1:], x[:-1]))) / (len(x) - 1)
    k = int(0.01 * rate)
    jump = max(abs(c[0] - c[-1]) for c in chans)
    near = max(mean_step(c[-k:] + c[:k]) for c in chans)
    whole = max(mean_step(c) for c in chans)
    w = int(0.05 * rate)
    head = rms(*[c[:w] for c in chans])
    tail = rms(*[c[-w:] for c in chans])
    print("      seam: jump %.4f vs step %.4f nearby (x%.2f), %.4f overall; "
          "rms first 50 ms %.1f dBFS, last 50 ms %.1f dBFS"
          % (jump, near, jump / near if near else 0.0, whole, db(head), db(tail)))


# --------------------------------------------------------------------------- #
# Buffers, oscillators and envelopes
# --------------------------------------------------------------------------- #
# Buffers are plain lists of floats. When I timed it, map() and comprehensions over lists were
# faster than array('d') or explicit loops. Finished song layers get stored as array('d') since
# a list takes four times the memory.

def silence(n):
    return [0.0] * max(0, n)


def scale(x, g):
    return list(map(mul, x, repeat(g)))


def shaped(x, env):
    return list(map(mul, x, env))


def norm(x, top=1.0):
    m = max(map(abs, x)) if x else 0.0
    return scale(x, top / m) if m > 0 else x


def mix(dst, src, at=0, gain=1.0):
    """Adds src into dst starting at sample `at`. Anything past either end gets dropped."""
    if at < 0:
        src = src[-at:]
        at = 0
    n = min(len(src), len(dst) - at)
    if n <= 0:
        return
    seg = src if n == len(src) else src[:n]
    if gain != 1.0:
        seg = map(mul, seg, repeat(gain))
    dst[at:at + n] = map(add, dst[at:at + n], seg)


def mix_loop(dst, src, at, gain=1.0):
    """Same as mix() but wraps around the end of dst, so tails that run past the loop point
    end up at the start. This is the main thing that keeps the songs looping cleanly."""
    size = len(dst)
    at %= size
    off = 0
    while off < len(src):
        n = min(size - at, len(src) - off)
        seg = src[off:off + n]
        if gain != 1.0:
            seg = map(mul, seg, repeat(gain))
        dst[at:at + n] = map(add, dst[at:at + n], seg)
        off += n
        at = 0


def pan_gains(pan):
    """Constant-power pan, -1 left to +1 right."""
    a = (pan + 1.0) * math.pi / 4.0
    return math.cos(a), math.sin(a)


def midi_hz(m, cents=0.0):
    return 440.0 * 2.0 ** ((m - 69 + cents / 100.0) / 12.0)


def noise(n, rng):
    r = rng.random
    return [r() * 2.0 - 1.0 for _ in range(n)]


def sine(freq, n, rate, amp=1.0, tau=0.0):
    """A sine starting at zero phase. If tau > 0 it decays with that time constant (seconds).

    I rotate a complex phasor with accumulate() instead of calling sin() every sample. Same
    result, about twice as fast."""
    if n <= 0:
        return []
    r = math.exp(-1.0 / (tau * rate)) if tau > 0 else 1.0
    step = cmath.rect(r, TAU * freq / rate)
    return [z.imag for z in accumulate(repeat(step, n - 1), mul, initial=complex(amp, 0.0))]


def glide(f_start, f_end, tau, n, rate, amp=1.0):
    """A sine that slides from f_start toward f_end with time constant tau. I use it for kicks,
    thumps and swells. The phase is worked out directly, so there's no running state."""
    a = TAU * f_end / rate
    b = TAU * (f_start - f_end) * tau
    k = 1.0 / (tau * rate)
    s, e = math.sin, math.exp
    return [amp * s(a * i + b * (1.0 - e(-k * i))) for i in range(n)]


def sweep(f0, f1, n, rate, amp=1.0):
    """A sine gliding exponentially from f0 to f1 over n samples."""
    if abs(f1 - f0) < 1e-6 or n < 2:
        return sine(f0, n, rate, amp)
    k = math.log(f1 / f0) / n
    c = TAU * f0 / (rate * k)
    s, e = math.sin, math.exp
    return [amp * s(c * (e(k * i) - 1.0)) for i in range(n)]


def decay(n, tau, rate):
    if n <= 0:
        return []
    return list(accumulate(repeat(math.exp(-1.0 / (tau * rate)), n - 1), mul, initial=1.0))


def envelope(n, rate, attack=0.002, tau=0.0, gate=None, release=0.05):
    """Raised-cosine attack, exponential decay (tau, 0 means hold), and an exponential release
    after `gate` if one is given. Always ends with a short fade down to zero."""
    e = decay(n, tau, rate) if tau > 0 else [1.0] * n
    a = min(n, int(attack * rate))
    for i in range(a):
        e[i] *= 0.5 - 0.5 * math.cos(math.pi * i / a)
    if gate is not None:
        g = int(gate * rate)
        if 0 <= g < n:
            e[g:] = map(mul, e[g:], decay(n - g, release, rate))
    return fade(e, rate, 0.0, 0.003)


def lines(points, n, rate):
    """Piecewise-linear envelope through (seconds, level) points, held after the last one."""
    out = silence(n)
    last = 0
    for (t0, v0), (t1, v1) in zip(points, points[1:]):
        i0, i1 = int(t0 * rate), int(t1 * rate)
        if i1 <= i0:
            continue
        d = (v1 - v0) / (i1 - i0)
        stop = min(n, i1)
        if stop > i0:
            out[i0:stop] = [v0 + d * k for k in range(stop - i0)]
        last = i1
    if last < n:
        out[last:] = [points[-1][1]] * (n - last)
    return out


def fade(x, rate, fin=0.0005, fout=0.005):
    """Raised-cosine fade in and out, in place, so the first and last samples are exactly zero."""
    n = len(x)
    a = min(n // 2, int(fin * rate))
    b = min(n // 2, int(fout * rate))
    cos, pi = math.cos, math.pi
    if a > 0:
        for i in range(a):
            x[i] *= 0.5 - 0.5 * cos(pi * i / a)
    elif n:
        x[0] = 0.0
    if b > 0:
        for i in range(b):
            x[n - 1 - i] *= 0.5 - 0.5 * cos(pi * i / b)
    elif n:
        x[-1] = 0.0
    return x


def saturate(x, drive):
    """tanh soft clip, scaled so a full-scale input still comes out at full scale."""
    th = math.tanh
    k = 1.0 / th(drive)
    return [th(drive * v) * k for v in x]


def modes(partials, n, rate, f0=1.0, amp=1.0):
    """Sum of decaying sines, partials given as (ratio, gain, tau). I use this for anything
    that gets struck: music box tines, bells, glass, the metal tags, steel doors."""
    out = silence(n)
    top = 0.45 * rate
    for ratio, g, tau in partials:
        f = f0 * ratio
        if f >= top:
            continue
        m = min(n, int(tau * rate * 8) + 1)   # 8 tau is about -70 dB, so I stop rendering there
        mix(out, sine(f, m, rate, g * amp, tau))
    return out


# Band-limited wavetables. A naive saw at 32 kHz aliases badly, so each table only holds
# the harmonics that fit under Nyquist for the note that reads it.
TABLE_SIZE = 4096
TABLE_MASK = TABLE_SIZE - 1
_TABLES = {}


def _table(kind, harmonics):
    key = (kind, harmonics)
    if key not in _TABLES:
        acc = silence(TABLE_SIZE)
        for k in range(1, harmonics + 1):
            if kind == "saw" or k % 2:       # a square is the saw's odd harmonics only
                mix(acc, sine(k, TABLE_SIZE, TABLE_SIZE, 1.0 / k))
        _TABLES[key] = norm(acc)
    return _TABLES[key]


def table_for(kind, freq, rate):
    h = max(1, int(0.45 * rate / max(freq, 1.0)))
    h = min(256, 1 << (h.bit_length() - 1))   # powers of two keep the cache small
    return _table(kind, h)


FIX_BITS = 20       # fixed-point fraction bits for the oscillator phase


def osc(kind, freq, n, rate, amp=1.0, phase=0.0):
    """Wavetable oscillator. The phase is a fixed-point integer from range(), so the whole thing
    runs as map() calls with no Python code per sample."""
    tab = table_for(kind, freq, rate)
    if amp != 1.0:
        tab = [v * amp for v in tab]
    inc = max(1, round(freq * TABLE_SIZE / rate * (1 << FIX_BITS)))
    p = round(phase * TABLE_SIZE * (1 << FIX_BITS))
    steps = range(p, p + n * inc, inc)
    return list(map(tab.__getitem__, map(and_, map(rshift, steps, repeat(FIX_BITS)), repeat(TABLE_MASK))))


def osc_sweep(kind, f0, f1, n, rate, amp=1.0):
    """Wavetable oscillator gliding exponentially from f0 to f1."""
    tab = table_for(kind, max(f0, f1), rate)
    if abs(f1 - f0) < 1e-6:
        return osc(kind, f0, n, rate, amp)
    k = math.log(f1 / f0) / n
    c = f0 * TABLE_SIZE / (rate * k)
    e, m = math.exp, TABLE_MASK
    return [tab[int(c * (e(k * i) - 1.0)) & m] * amp for i in range(n)]


def vibrato_time(n, rate, cents, vib_hz, delay_s):
    """Sample times with a vibrato in them that fades in after delay_s. Reading an
    oscillator at these times instead of 0, 1, 2... gives it vibrato, and several voices can
    share the same list."""
    if not cents or n <= 0:
        return list(range(n))
    w = TAU * vib_hz / rate
    depth = (2.0 ** (cents / 1200.0) - 1.0) / w
    swing = lines(((0.0, 0.0), (delay_s, 0.0), (delay_s + 0.25, depth)), n, rate)
    wobble = [1.0 - z.real for z in accumulate(repeat(cmath.rect(1.0, w), n - 1), mul, initial=1 + 0j)]
    return list(map(add, range(n), map(mul, swing, wobble)))


def osc_at(kind, freq, times, rate, amp=1.0, phase=0.0):
    """Wavetable oscillator read at the given sample times (see vibrato_time)."""
    tab = table_for(kind, freq * 1.03, rate)
    if amp != 1.0:
        tab = [v * amp for v in tab]
    inc = freq * TABLE_SIZE / rate
    p = phase * TABLE_SIZE
    m = TABLE_MASK
    return [tab[int(p + inc * t) & m] for t in times]


# --------------------------------------------------------------------------- #
# Filters and effects
# --------------------------------------------------------------------------- #

def lowpass(x, fc, rate):
    """One-pole low-pass, 6 dB/octave."""
    a = 1.0 - math.exp(-TAU * fc / rate)
    y = 0.0
    return [y := y + a * (v - y) for v in x]


def highpass(x, fc, rate):
    """One-pole high-pass. At 20 Hz it's my DC blocker."""
    a = 1.0 / (1.0 + TAU * fc / rate)
    y = prev = 0.0
    out = silence(len(x))
    for i, v in enumerate(x):
        y = a * (y + v - prev)
        prev = v
        out[i] = y
    return out


def biquad(x, kind, fc, q, rate):
    """RBJ cookbook biquad: 'lp', 'hp' or 'bp' (band-pass with 0 dB at the centre)."""
    w = TAU * min(fc, 0.45 * rate) / rate
    cw, sw = math.cos(w), math.sin(w)
    al = sw / (2.0 * q)
    if kind == "lp":
        b0 = b2 = (1.0 - cw) / 2.0
        b1 = 1.0 - cw
    elif kind == "hp":
        b0 = b2 = (1.0 + cw) / 2.0
        b1 = -(1.0 + cw)
    else:
        b0, b1, b2 = al, 0.0, -al
    a0 = 1.0 + al
    a1, a2 = -2.0 * cw / a0, (1.0 - al) / a0
    b0, b1, b2 = b0 / a0, b1 / a0, b2 / a0
    s1 = s2 = 0.0
    out = silence(len(x))
    for i, v in enumerate(x):
        y = b0 * v + s1
        s1 = b1 * v - a1 * y + s2
        s2 = b2 * v - a2 * y
        out[i] = y
    return out


def svf(x, cutoff, q, rate, mode="lp", block=16):
    """Trapezoidal state-variable filter. cutoff is a number, or a function of the sample
    index that I read every `block` samples (for sweeps and filter envelopes)."""
    n = len(x)
    out = silence(n)
    k = 1.0 / q
    m0, m1, m2 = {"lp": (0.0, 0.0, 1.0), "bp": (0.0, k, 0.0), "hp": (1.0, -k, -1.0)}[mode]
    fixed = not callable(cutoff)
    if fixed:
        block = max(1, n)
    ic1 = ic2 = 0.0
    top, pr, tan = 0.45 * rate, math.pi / rate, math.tan
    for s in range(0, n, block):
        fc = cutoff if fixed else cutoff(s)
        g = tan(pr * min(max(fc, 10.0), top))
        a1 = 1.0 / (1.0 + g * (g + k))
        a2 = g * a1
        a3 = g * a2
        for i in range(s, min(n, s + block)):
            v0 = x[i]
            v3 = v0 - ic2
            v1 = a1 * ic1 + a2 * v3
            v2 = ic2 + a2 * ic1 + a3 * v3
            ic1 = 2.0 * v1 - ic1
            ic2 = 2.0 * v2 - ic2
            out[i] = m0 * v0 + m1 * v1 + m2 * v2
    return out


def preroll(rate, pre_s=1.0):
    return 16 * int(pre_s * rate / 16)


def looped(fn, x, rate, pre_s=1.0):
    """Runs a filter with short memory over a loop. I feed it the last second first so its
    state at sample 0 matches coming round from the end. Same result as a second full pass, just
    cheaper."""
    pre = min(len(x), preroll(rate, pre_s))
    return fn(x[-pre:] + x)[pre:]


REVERB_LINES_MS = (43.7, 53.9, 63.1, 75.3)
REVERB_DIFFUSERS_MS = (5.3, 7.7, 12.3)
REVERB_DIFFUSION = 0.62


def reverb(send, rate, rt60, damp_hz, size=1.0, loop=False):
    """Feedback-delay-network reverb: three allpass diffusers into four damped delay lines
    mixed through a Hadamard matrix. Mono in, (left, right) wet out.

    With loop=True I run the send through twice and keep the second pass, so the reverb tail from
    the end of the song is already there at sample 0 and the loop doesn't jump."""
    lens = [int(rate * ms * size / 1000.0) | 1 for ms in REVERB_LINES_MS]
    aps = [int(rate * ms / 1000.0) | 1 for ms in REVERB_DIFFUSERS_MS]
    g0, g1, g2, g3 = [0.5 * 10.0 ** (-3.0 * n / (rt60 * rate)) for n in lens]
    c = 1.0 - math.exp(-TAU * damp_hz / rate)
    l0, l1, l2, l3 = lens
    m0, m1, m2 = aps
    d0, d1, d2, d3 = [0.0] * l0, [0.0] * l1, [0.0] * l2, [0.0] * l3
    a0, a1, a2 = [0.0] * m0, [0.0] * m1, [0.0] * m2
    i0 = i1 = i2 = i3 = j0 = j1 = j2 = 0
    f0 = f1 = f2 = f3 = 0.0
    G = REVERB_DIFFUSION
    n = len(send)
    outl, outr = silence(n), silence(n)
    for _ in range(2 if loop else 1):
        for i in range(n):
            x = send[i]
            b = a0[j0]
            w = x + G * b
            a0[j0] = w
            x = b - G * w
            j0 += 1
            if j0 == m0:
                j0 = 0
            b = a1[j1]
            w = x + G * b
            a1[j1] = w
            x = b - G * w
            j1 += 1
            if j1 == m1:
                j1 = 0
            b = a2[j2]
            w = x + G * b
            a2[j2] = w
            x = b - G * w
            j2 += 1
            if j2 == m2:
                j2 = 0
            y0, y1, y2, y3 = d0[i0], d1[i1], d2[i2], d3[i3]
            f0 += c * (y0 - f0)
            f1 += c * (y1 - f1)
            f2 += c * (y2 - f2)
            f3 += c * (y3 - f3)
            s, t, u, v = f0 + f1, f0 - f1, f2 + f3, f2 - f3
            d0[i0] = x + g0 * (s + u)
            d1[i1] = g1 * (t + v) - x
            d2[i2] = x + g2 * (s - u)
            d3[i3] = g3 * (t - v) - x
            outl[i] = y0 + y2
            outr[i] = y1 + y3
            i0 += 1
            if i0 == l0:
                i0 = 0
            i1 += 1
            if i1 == l1:
                i1 = 0
            i2 += 1
            if i2 == l2:
                i2 = 0
            i3 += 1
            if i3 == l3:
                i3 = 0
    return outl, outr


def pingpong(send, rate, delay, feedback, damp_hz):
    """Stereo ping-pong echo over a loop (two passes, same reason as the reverb)."""
    n = len(send)
    d = int(delay)
    bl, br = [0.0] * d, [0.0] * d
    c = 1.0 - math.exp(-TAU * damp_hz / rate)
    fl = fr = 0.0
    j = 0
    outl, outr = silence(n), silence(n)
    for _ in range(2):
        for i in range(n):
            yl, yr = bl[j], br[j]
            fl += c * (yl - fl)
            fr += c * (yr - fr)
            bl[j] = send[i] + feedback * fr
            br[j] = feedback * fl
            outl[i] = yl
            outr[i] = yr
            j += 1
            if j == d:
                j = 0
    return outl, outr


def limit(x, gain, knee, ceiling):
    """Soft limiter with no memory: untouched below the knee, a tanh curve above it, never
    past the ceiling. Since it has no state it can't mess up the loop point."""
    span = ceiling - knee
    th = math.tanh
    return [v if -knee <= v <= knee else
            (knee + span * th((v - knee) / span) if v > 0 else -knee - span * th((-v - knee) / span))
            for v in map(mul, x, repeat(gain))]


# --------------------------------------------------------------------------- #
# Shared instruments
# --------------------------------------------------------------------------- #

# (ratio, gain, tau). The 5.43 and 8.21 partials are the out-of-tune tine modes. Without them
# it just sounds like an organ.
MUSIC_BOX = ((1.0, 1.0, 1.3), (2.0, 0.2, 0.5), (3.01, 0.05, 0.28), (5.43, 0.16, 0.11),
             (8.21, 0.07, 0.045), (11.9, 0.03, 0.022))
MUSIC_BOX_LENGTH = 3.6


def music_box_note(freq, rate, rng):
    n = int(MUSIC_BOX_LENGTH * rate)
    out = modes(MUSIC_BOX, n, rate, freq)
    # The pluck: a couple of ms of bright noise when the pin lets go of the tine.
    m = int(0.003 * rate)
    click = shaped(highpass(noise(m, rng), 2500.0, rate), envelope(m, rate, 0.0003, 0.0007))
    mix(out, click, 0, 0.06)
    return fade(out, rate, 0.0003, 0.4)


def heartbeat(rate, rng, gap=0.27, pitch=1.0):
    """Lub-dub: two soft low thumps `gap` s apart (0.33 at most), the second a little higher
    and quieter. pitch scales both."""
    n = int(0.75 * rate)
    out = silence(n)
    for t, f0, f1, g in ((0.0, 66.0, 41.0, 1.0), (gap, 76.0, 47.0, 0.7)):
        m = int(0.42 * rate)
        e = envelope(m, rate, attack=0.012, tau=0.075)
        thump = shaped(glide(f0 * pitch, f1 * pitch, 0.05, m, rate), e)
        flesh = shaped(norm(lowpass(lowpass(noise(m, rng), 220.0, rate), 220.0, rate)), e)
        mix(thump, flesh, 0, 0.25)
        mix(out, thump, int(t * rate), g)
    return out


def creak(dur, rate, rng, rates, resonances, q=28.0):
    """Stick-slip friction: a train of clicks whose rate follows `rates` (a list of
    (position 0..1, clicks per second)), run through a few narrow band-passes."""
    n = int(dur * rate)
    pulses = silence(n)
    t = 0.0
    while True:
        u = t / dur
        r = rates[0][1]
        for (p0, r0), (p1, r1) in zip(rates, rates[1:]):
            if p0 <= u <= p1:
                r = r0 + (r1 - r0) * (u - p0) / max(1e-9, p1 - p0)
        t += (1.0 / r) * rng.uniform(0.8, 1.2)
        i = int(t * rate)
        if i >= n:
            break
        pulses[i] = rng.uniform(0.5, 1.0)
    out = silence(n)
    for f, g in resonances:
        mix(out, norm(biquad(pulses, "bp", f, q, rate)), 0, g)
    return out


# --------------------------------------------------------------------------- #
# Song mixer
# --------------------------------------------------------------------------- #

class Song:
    """One looping song: the beat grid, and the layers that get summed into the left and
    right channels and the reverb send."""

    def __init__(self, bpm, bars, rate=MUSIC_RATE):
        self.rate = rate
        # I snap the beat to a whole number of samples (a multiple of 12, so swung eighths and
        # triplets land on samples too). The loop is then exactly `bars` bars long.
        self.beat = 12 * round(rate * 60.0 / bpm / 12)
        self.bar = 4 * self.beat
        self.length = bars * self.bar
        self.layers = []    # (samples, left gain, right gain, reverb send gain)

    def at(self, bar, beat=0.0):
        """Sample index of a 1-based bar and a 0-based beat inside it."""
        return (bar - 1) * self.bar + int(round(beat * self.beat))

    def secs(self, beats):
        return beats * self.beat / self.rate

    def track(self):
        return silence(self.length)

    def half_at(self, bar, beat=0.0):
        return self.at(bar, beat) // 2

    def half_track(self):
        return silence(self.length // 2)

    def add_half(self, track, gain_db=0.0, pan=0.0, send=0.0, label="", right=None):
        """Adds a layer rendered at half rate, doubled back up and smoothed. With `right` it's
        a stereo pair."""
        def up(x):
            return looped(lambda y: lowpass(y, 7000.0, self.rate), upsample2(x), self.rate, 0.01)
        if right is None:
            self.add(up(track), gain_db, pan, send, label)
        else:
            self.add_stereo(up(track), up(right), gain_db, send, label)

    def add(self, track, gain_db=0.0, pan=0.0, send=0.0, label=""):
        g = undb(gain_db)
        gl, gr = pan_gains(pan)
        self.layers.append((array("d", track), g * gl, g * gr, g * send))
        if VERBOSE:
            print("      %-12s rms %6.1f  peak %6.1f" % (label, db(rms(track) * g), db(peak(track) * g)))

    def add_stereo(self, left, right, gain_db=0.0, send=0.0, label=""):
        g = undb(gain_db)
        self.layers.append((array("d", left), g, 0.0, g * send * 0.5))
        self.layers.append((array("d", right), 0.0, g, g * send * 0.5))
        if VERBOSE:
            print("      %-12s rms %6.1f  peak %6.1f" % (
                label, db(rms(left, right) * g), db(peak(left, right) * g)))

    def bus(self, which):
        """Sums the layers into one bus (1 left, 2 right, 3 reverb send). Adding them one
        at a time was most of the run time. zip + sumprod does all of them in one pass."""
        picked = [(layer[0], layer[which]) for layer in self.layers if layer[which]]
        if not picked:
            return self.track()
        tracks = [t for t, _ in picked]
        gains = [g for _, g in picked]
        sp = math.sumprod
        return [sp(c, gains) for c in zip(*tracks)]

    def add_reverb(self, rt60, damp_hz, size, gain_db):
        # I run the reverb at half rate. The tail is dark anyway so you can't hear the difference,
        # and it's half the work.
        rate = self.rate
        send = looped(lambda x: lowpass(biquad(x, "lp", 6000.0, 0.7, rate), 7000.0, rate),
                      self.bus(3), rate, 0.05)
        wl, wr = reverb(send[0::2], rate // 2, rt60, damp_hz, size, loop=True)
        self.add_stereo(upsample2(wl), upsample2(wr), gain_db, label="reverb")

    def finish(self, rel, rms_db):
        rate = self.rate
        left = looped(lambda x: highpass(x, 18.0, rate), self.bus(1), rate)
        right = looped(lambda x: highpass(x, 18.0, rate), self.bus(2), rate)
        self.layers = []
        target = undb(rms_db)
        ceiling = undb(MUSIC_PEAK_DB)
        gain = target / rms(left, right)
        # The limiter takes a little RMS off the loud bits, so I re-aim the gain a few times.
        for _ in range(4):
            outl = limit(left, gain, LIMIT_KNEE, ceiling)
            outr = limit(right, gain, LIMIT_KNEE, ceiling)
            got = rms(outl, outr)
            if abs(db(got) - rms_db) < 0.05:
                break
            gain *= target / got
        if VERBOSE:
            print("      raw peak %.1f dB over rms, limiter gain %.1f dB" % (
                db(peak(left, right) / rms(left, right)), db(gain)))
        write_wav(rel, [outl, outr], rate)
        seam_report([outl, outr], rate)


def upsample2(x):
    """Doubles the rate of a loop by linear interpolation, wrapping at the end."""
    out = silence(2 * len(x))
    out[0::2] = x
    out[1::2] = [0.5 * (a + b) for a, b in zip(x, x[1:] + x[:1])]
    return out


def note_cache(render):
    """Memoises a note renderer. Most notes in a song repeat exactly, so I only build each once."""
    cache = {}

    def get(*key):
        if key not in cache:
            cache[key] = render(*key)
        return cache[key]
    return get


# --------------------------------------------------------------------------- #
# Score instruments
# --------------------------------------------------------------------------- #
# Strings, choir and piano render at half rate (16 kHz). Nothing they make goes much past
# 6 kHz, and it halves the slowest part of the run. Song.add_half doubles them back up.

STEEL = ((1.0, 1.0, 0.5), (1.47, 0.75, 0.4), (2.09, 0.55, 0.3), (2.83, 0.4, 0.22),
         (3.62, 0.3, 0.15), (4.71, 0.2, 0.1))
PIANO_STRETCH = 0.0004      # how far a real string's upper partials run sharp
VOWELS = {                  # (formant Hz, gain) per vowel, roughly a low choir
    "oo": ((320.0, 1.0), (800.0, 0.45), (2300.0, 0.15)),
    "oh": ((480.0, 1.0), (850.0, 0.55), (2500.0, 0.18)),
    "ah": ((700.0, 1.0), (1100.0, 0.6), (2550.0, 0.22)),
}
_BOW_HISS = {}


def bow_hiss(n, rate, rng):
    """Bow noise for one note. It's a slice of one long strip of band-passed noise, so I
    only have to filter it once."""
    if rate not in _BOW_HISS:
        _BOW_HISS[rate] = norm(biquad(noise(40 * rate, random.Random(rate)), "bp", 2000.0, 0.7, rate))
    strip = _BOW_HISS[rate]
    out = []
    while len(out) < n:
        start = rng.randrange(len(strip) // 2)
        out += strip[start:start + n - len(out)]
    return out


def bowed(freqs, dur, rate, rng, attack=1.0, release=1.5, vib_cents=10.0, bow=0.12, bright=1.0):
    """Bowed strings: two saws a few cents apart per note, one vibrato shared by all of them,
    low-passed for the body, plus bow hiss and slightly uneven bow pressure. dur is how long the
    bow stays on (s)."""
    n = int((dur + 3.0 * release) * rate)
    times = vibrato_time(n, rate, vib_cents, rng.uniform(4.3, 5.2), min(attack, 0.8))
    x = silence(n)
    for f in freqs:
        for c in (-4.0, 4.0):
            mix(x, osc_at("saw", f * 2 ** (c / 1200), times, rate, 0.5, rng.random()))
    body = min(0.4 * rate, 300.0 + 5.0 * min(freqs) * bright)
    x = lowpass(lowpass(x, body, rate), body, rate)
    mix(x, bow_hiss(n, rate, rng), 0, bow * len(freqs))
    pressure = lines([(0.3 * k, 1.0 + 0.1 * rng.uniform(-1.0, 1.0)) for k in range(int(n / rate / 0.3) + 2)], n, rate)
    return shaped(shaped(x, envelope(n, rate, attack=attack, gate=dur, release=release)), pressure)


def choir(freqs, dur, rate, rng, vowel="oo", attack=3.0, release=2.0, breath=0.35, glide_cents=0.0):
    """Choir pad: two saws per voice plus some breath noise, all through three vowel
    formants. With glide_cents the whole chord bends up over the note and there's no vibrato."""
    n = int((dur + 3.0 * release) * rate)
    if glide_cents:
        k = math.log(2.0) * glide_cents / 1200.0 / n
        times = [(math.exp(k * i) - 1.0) / k for i in range(n)]
    else:
        times = vibrato_time(n, rate, 8.0, rng.uniform(4.4, 5.0), 0.8)
    src = silence(n)
    for f in freqs:
        for c in (-7.0, 7.0):
            mix(src, osc_at("saw", f * 2 ** (c / 1200), times, rate, 1.0, rng.random()))
    src = norm(src)
    mix(src, noise(n, rng), 0, breath)
    out = silence(n)
    for fc, g in VOWELS[vowel]:
        mix(out, biquad(src, "bp", fc, 5.0, rate), 0, g)
    return shaped(out, envelope(n, rate, attack=attack, gate=dur, release=release))


def felt_piano(freq, rate, rng, vel=0.6, length=4.5, prepared=0.0, detune=3.0):
    """Felt piano: slightly stretched partials that drop off fast, each with a quick
    and a slow decay, a second string a few cents off so the note beats, and a soft knock from the
    hammer. `prepared` adds the clangy partials you get from a bolt stuck between the strings."""
    n = int(length * rate)
    slow = 2.4 * (262.0 / freq) ** 0.45
    parts, second = [], []
    for k in range(1, 6):
        r = k * math.sqrt(1.0 + PIANO_STRETCH * k * k)
        g = vel ** (0.5 * (k - 1)) / k ** 1.7          # softer notes lose the high partials first
        tau = slow / (1.0 + 0.6 * (k - 1))
        parts += [(r, 0.6 * g, 0.15 * tau), (r, 0.4 * g, tau)]
        if k <= 2:
            second.append((r, 0.35 * g, tau))
    x = modes(parts, n, rate, freq)
    mix(x, modes(second, n, rate, freq * 2 ** (detune / 1200)))
    if prepared:
        mix(x, modes(((2.41, 0.5, 0.18), (3.73, 0.35, 0.11), (5.18, 0.2, 0.06)), n, rate, freq), 0, prepared)
    m = int(0.012 * rate)
    knock = shaped(lowpass(noise(m, rng), 700.0, rate), envelope(m, rate, 0.001, 0.003))
    mix(x, norm(knock), 0, 0.05)
    return fade(lowpass(x, 1500.0 + 2500.0 * vel, rate), rate, 0.0005, 0.4)


def reverse_swell(notes, dur, rate, rng):
    """A piano chord played backwards into the next downbeat, with some noise
    rising under it. It ends on the chord's attack, so I fade the last few ms."""
    n = int(dur * rate)
    x = silence(n)
    for m in notes:
        mix(x, felt_piano(midi_hz(m), rate, rng, 0.7, dur + 0.1))
    x = norm(x[::-1])
    air = shaped(norm(lowpass(noise(n, rng), 2500.0, rate)), [(i / n) ** 3 for i in range(n)])
    mix(x, air, 0, 0.3)
    return fade(x, rate, 0.05, 0.015)


def sub_impact(rate, rng, length=3.5):
    """A low impact: a sub sine dropping toward 23 Hz through a tanh, with some low
    noise rumble under it."""
    n = int(length * rate)
    x = saturate(shaped(glide(54.0, 23.0, 0.4, n, rate), envelope(n, rate, attack=0.003, tau=0.9)), 1.8)
    rumble = norm(lowpass(lowpass(noise(n, rng), 140.0, rate), 140.0, rate))
    mix(x, shaped(rumble, envelope(n, rate, attack=0.01, tau=0.5)), 0, 0.3)
    m = int(0.08 * rate)
    mix(x, shaped(norm(lowpass(noise(m, rng), 900.0, rate)), envelope(m, rate, 0.001, 0.015)), 0, 0.25)
    return x


def sub_swell(freq, dur, rate, rng):
    """A distorted sub swell: saw and sine on the root, rising over dur and cut at the end."""
    n = int(dur * rate)
    x = list(map(add, osc("saw", freq, n, rate, 0.5, rng.random()), sine(freq, n, rate, 0.8)))
    x = saturate(norm(lowpass(lowpass(x, 260.0, rate), 260.0, rate)), 2.5)
    return fade(shaped(x, [(i / n) ** 2 for i in range(n)]), rate, 0.0, 0.03)


def heavy_kick(rate, rng):
    """A distorted sub kick: a long low sweep driven hard into a tanh."""
    n = int(0.8 * rate)
    x = shaped(glide(140.0, 36.0, 0.05, n, rate), envelope(n, rate, attack=0.0006, tau=0.3))
    m = int(0.004 * rate)
    click = shaped(highpass(noise(m, rng), 1500.0, rate), envelope(m, rate, 0.0002, 0.0012))
    mix(x, norm(click), 0, 0.3)
    return lowpass(saturate(x, 3.0), 4000.0, rate)


def clang(rate, rng, f0):
    """Metal hit for the backbeat: partials for two steel plates, a noise crack and a low
    thud, all through a tanh for some grit."""
    n = int(1.1 * rate)
    x = norm(modes([(r, g, t * 1.3) for r, g, t in STEEL], n, rate, f0))
    mix(x, norm(modes([(r, g, t * 0.8) for r, g, t in STEEL], n, rate, f0 * 1.37)), 0, 0.6)
    m = int(0.06 * rate)
    mix(x, shaped(norm(biquad(noise(m, rng), "bp", 2500.0, 0.8, rate)), envelope(m, rate, 0.0003, 0.012)), 0, 0.8)
    mix(x, shaped(glide(120.0, 60.0, 0.03, n, rate), envelope(n, rate, 0.001, 0.08)), 0, 0.6)
    return norm(saturate(x, 2.0))


def stutter(src, rate, repeats, first_s, shrink):
    """Glitch stutter: the start of a sound repeated, each repeat a bit shorter, then
    bit-crushed (sample-and-hold and only a few levels)."""
    out = []
    length = first_s
    for k in range(repeats):
        piece = fade(src[:max(32, int(length * rate))], rate, 0.0005, 0.002)
        out += scale(piece, 1.0 - 0.02 * k)
        length *= shrink
    held = 0.0
    for i in range(len(out)):
        if i % 4 == 0:
            held = round(out[i] * 6.0) / 6.0
        out[i] = held
    return fade(out, rate, 0.001, 0.004)


def clock_tick(rate, rng, high):
    """Clock tick for the table music. Tick is higher, tock is lower."""
    n = int(0.05 * rate)
    f0 = 3200.0 if high else 2350.0
    x = modes(((1.0, 1.0, 0.006), (1.73, 0.5, 0.004), (2.9, 0.3, 0.003)), n, rate, f0)
    m = int(0.002 * rate)
    mix(x, shaped(highpass(noise(m, rng), 3000.0, rate), envelope(m, rate, 0.0002, 0.0005)), 0, 0.3)
    mix(x, sine(f0 / 4.0, n, rate, 0.25, 0.008))
    return norm(x)


# --------------------------------------------------------------------------- #
# it-is-watching.wav: title theme
# --------------------------------------------------------------------------- #
# D minor, 52 BPM, 16 bars (about 74 s). A detuned music box plays short phrases with long gaps
# between them, over a bowed bass on D, slow string swells a semitone off the D, a quiet choir,
# a heartbeat that skips twice and two sub impacts. I left it mostly empty on purpose.

TITLE_BASS = (         # (first bar, bars, MIDI): root and fifth, the fifth drops to Bb in 9-12
    (1, 4, (38, 45)),
    (5, 4, (38, 45)),
    (9, 4, (38, 46)),
    (13, 4, (38, 45)),
)
TITLE_SWELLS = (       # (bar, bars, MIDI, gain), each one a semitone against the D
    (3, 2, (50, 51), 0.8),          # D3 + Eb3
    (7, 2, (49, 50), 0.7),          # C#3 under D3
    (11, 2, (44, 45), 0.8),         # Ab2 against A2, a tritone over the bass
    (14, 3, (50, 51, 56), 1.0),     # D3, Eb3, Ab3, still going when the loop wraps
)
TITLE_CHOIR = (        # (first bar, bars, MIDI, vowel)
    (1, 8, (50, 53, 57), "oo"),
    (9, 8, (50, 53, 58), "oh"),
)
TITLE_MELODY = (       # (bar, beat, MIDI, velocity); bars 3-4, 7-8, 11-12 and 16 are left empty
    (1, 0.0, 81, 0.85), (1, 1.0, 77, 0.60), (1, 2.0, 76, 0.65), (1, 3.5, 74, 0.55),
    (2, 1.0, 73, 0.60),                                      # C#, left unresolved
    (5, 0.0, 81, 0.80), (5, 1.0, 82, 0.65), (5, 2.5, 81, 0.55),
    (6, 0.0, 77, 0.60), (6, 2.0, 75, 0.70),                  # Eb, left hanging
    (9, 0.0, 86, 0.85), (9, 1.0, 81, 0.60), (9, 2.0, 77, 0.65), (9, 3.0, 76, 0.55),
    (10, 1.0, 74, 0.60), (10, 3.0, 69, 0.45),
    (13, 0.0, 87, 0.85), (13, 1.0, 86, 0.60), (13, 2.5, 81, 0.60),
    (14, 0.0, 80, 0.70),                                     # Ab: the tritone
    (15, 2.0, 77, 0.40),
)
TITLE_TINES = ((1, 62), (5, 62), (9, 58), (13, 63))
TITLE_MISSED_BEATS = (8, 12)    # bars where the heartbeat skips
TITLE_IMPACTS = ((9, 0.0), (13, 2.5))
TITLE_DETUNE = -10.0            # cents, the whole music box is a bit flat


def build_title():
    rng = random.Random(5201)
    s = Song(TITLE_BPM, TITLE_BARS)
    R, L, H = s.rate, s.length, s.rate // 2

    # The bowed bass and the swells share one string track. Each bass note starts a second early
    # and rings a second over, so the bow changes overlap.
    strings = s.half_track()
    for first, bars, notes in TITLE_BASS:
        note = bowed([midi_hz(m) for m in notes], s.secs(4 * bars) + 1.0, H, rng, attack=2.5, release=1.0,
                     vib_cents=5.0, bow=0.06, bright=0.8)
        mix_loop(strings, note, s.half_at(first) - H)
    for bar, bars, notes, g in TITLE_SWELLS:
        dur = s.secs(4 * bars)
        note = bowed([midi_hz(m) for m in notes], 0.75 * dur, H, rng, attack=0.6 * dur, release=0.12 * dur,
                     vib_cents=14.0, bow=0.15)
        mix_loop(strings, note, s.half_at(bar), 0.6 * g)
    s.add_half(strings, -14.0, send=0.25, label="strings")

    # A D1 sine under everything, nudged to a whole number of cycles per loop, with a slow level
    # swell twice a loop.
    k = round(midi_hz(26) * L / R)
    w = TAU * 2 / L
    sub = [v * (0.7 + 0.3 * math.cos(w * i)) for i, v in enumerate(sine(k * R / L, L, R))]
    s.add(sub, -27.0, label="sub")

    # The choir, kept very quiet.
    ch = s.half_track()
    for first, bars, notes, vowel in TITLE_CHOIR:
        c = choir([midi_hz(m) for m in notes], s.secs(4 * bars), H, rng, vowel, attack=4.0, release=1.5)
        mix_loop(ch, c, s.half_at(first) - 2 * H)
    s.add_half(ch, -23.0, pan=0.15, send=0.5, label="choir")

    # Music box. Each tine gets its own small detune (seeded), then the whole track goes through
    # wow() for some tape-style wobble.
    tine_cents = {}
    box = note_cache(lambda m: music_box_note(midi_hz(m, TITLE_DETUNE + tine_cents.setdefault(
        m, rng.uniform(-7.0, 7.0))), R, rng))
    mb = s.track()
    for bar, beat, m, vel in TITLE_MELODY:
        jitter = int(rng.gauss(0.0, 0.008) * R)
        mix_loop(mb, box(m), s.at(bar, beat) + jitter, vel * rng.uniform(0.94, 1.0))
    for bar, m in TITLE_TINES:
        mix_loop(mb, box(m), s.at(bar), 0.35)
    mb = wow(mb, R, wow_cycles=40, wow_depth=0.0032, flutter_cycles=450, flutter_depth=0.0005)
    s.add(mb, -9.0, pan=-0.05, send=0.6, label="music box")

    # Heartbeat: slower, lower and darker than the SFX one, and mostly in the reverb so it sounds
    # far away. It skips twice.
    beat_once = lowpass(lowpass(heartbeat(R, rng, gap=0.33, pitch=0.85), 220.0, R), 220.0, R)
    hb = s.track()
    for bar in range(1, TITLE_BARS + 1):
        if bar not in TITLE_MISSED_BEATS:
            mix_loop(hb, beat_once, s.at(bar, 2.0))
    s.add(hb, -12.0, send=0.35, label="heartbeat")

    # Two sub impacts and one distant creak.
    fx = s.track()
    for bar, beat in TITLE_IMPACTS:
        mix_loop(fx, sub_impact(R, rng), s.at(bar, beat))
    s.add(fx, -11.0, send=0.4, label="impacts")
    c = creak(1.7, R, rng, ((0.0, 16.0), (0.4, 34.0), (0.75, 22.0), (1.0, 12.0)),
              ((231.0, 1.0), (517.0, 0.7), (873.0, 0.45), (1420.0, 0.25)))
    c = shaped(lowpass(c, 1600.0, R), lines(((0, 0), (0.3, 1), (1.2, 0.8), (1.7, 0)), len(c), R))
    part = s.track()
    mix_loop(part, norm(c), s.at(6, 2.0))
    s.add(part, -20.0, pan=-0.6, send=0.9, label="creak")

    # Tape hiss on both sides and a low room rumble in the middle.
    s.add_stereo(hiss(L, rng), hiss(L, rng), -56.0, label="hiss")
    rumble = looped(lambda x: lowpass(lowpass(x, 90.0, R), 90.0, R), noise(L, rng), R)
    s.add(norm(rumble), -46.0, label="room")

    s.add_reverb(rt60=5.5, damp_hz=2000.0, size=1.4, gain_db=-8.0)
    s.finish("Content/Music/it-is-watching.wav", TITLE_RMS_DB)


def wow(x, rate, wow_cycles, wow_depth, flutter_cycles, flutter_depth):
    """Tape-style wow and flutter: a delay whose length wobbles, read with linear
    interpolation. Depths are the peak pitch deviation (0.003 = 0.3 %). Whole cycles per loop,
    and the read wraps around, so it needs no second pass."""
    L = len(x)
    a1 = wow_depth * L / (TAU * wow_cycles)        # delay swing that gives that much pitch change
    a2 = flutter_depth * L / (TAU * flutter_cycles)
    off = L - (a1 + a2 + 2.0)                      # read behind, and offset by L so int() floors
    pos = [i + off - u - v for i, u, v in zip(range(L), sine(wow_cycles * rate / L, L, rate, a1),
                                               sine(flutter_cycles * rate / L, L, rate, a2))]
    return [x[j - L] + (x[j + 1 - L] - x[j - L]) * (p - j) for p, j in zip(pos, map(int, pos))]


def hiss(n, rng):
    """Tape hiss for a loop: white noise differenced once (a rising, hissy spectrum). It's
    differenced round the loop, so it has no seam."""
    x = noise(n, rng)
    return norm(list(map(sub, x, x[-1:] + x[:-1])))


# --------------------------------------------------------------------------- #
# holding.wav: lobby theme
# --------------------------------------------------------------------------- #
# D minor, 58 BPM, 16 bars (about 66 s), two bars per chord: Dm(add9), Bbmaj7, Gm(add9), A7b9,
# Dm(maj7), Ebmaj7(#11), Gm/D, A7b9. A detuned felt piano plays the chords slowly with one high
# A repeating over them, with bowed strings, a soft low pulse each bar and the fluorescent hum
# underneath. The A drops out for bars 13-14, and reversed piano chords lead into bars 9 and 1.

LOBBY_CHORDS = (       # one per two bars: (bass MIDI, piano voicing, string voicing)
    (38, (50, 57, 64, 65), (38, 45, 53)),    # Dm(add9), with the E and F a semitone apart
    (34, (46, 53, 57, 62), (34, 41, 50)),    # Bbmaj7
    (31, (43, 50, 57, 58), (31, 38, 46)),    # Gm(add9)
    (33, (45, 52, 55, 58), (33, 40, 49)),    # A7b9
    (38, (50, 53, 57, 61), (38, 45, 53)),    # Dm(maj7)
    (39, (51, 55, 62, 69), (39, 46, 55)),    # Ebmaj7(#11), the Phrygian chord
    (38, (50, 55, 58, 62), (38, 43, 50)),    # Gm over D
    (33, (45, 55, 61, 70), (33, 40, 49)),    # A7b9 again, back to Dm next
)
LOBBY_OSTINATO = ((0.0, 0.55), (0.75, 0.35), (2.0, 0.5), (2.75, 0.3))   # (beat, velocity), every bar
LOBBY_OSTINATO_NOTE = 81        # A5
LOBBY_SIGHS = (10, 12)          # bars where the last note goes up to Bb
LOBBY_GAP = (13, 14)            # bars where the A drops out
LOBBY_CREAKS = ((6, 1.0, -0.7), (12, 2.5, 0.6))
LOBBY_FLICKERS = ((5, 2.3), (11, 0.7))


def fluorescent(s, rng, flickers):
    """Fluorescent light hum: 60 Hz harmonics with a buzzy 120, nudged so a whole number
    of cycles fits in the loop, plus a few flickers with a click of noise each. Returns (hum, zaps)."""
    R, L = s.rate, s.length
    f = round(60.0 * L / R) * R / L
    hum = silence(L)
    for h, g in ((1, 0.35), (2, 1.0), (3, 0.45), (4, 0.2), (6, 0.08)):
        mix(hum, sine(f * h, L, R, g))
    buzz = looped(lambda x: highpass(x, 600.0, R), saturate(sine(2 * f, L, R), 4.0), R, 0.1)
    mix(hum, buzz, 0, 0.12)
    flick = [1.0] * L
    zaps = s.track()
    for bar, beat in flickers:
        start = s.at(bar, beat)
        for _ in range(rng.randint(3, 5)):
            off = start + int(rng.uniform(0.0, 0.35) * R)
            m = int(rng.uniform(0.02, 0.07) * R)
            for i in range(m):
                j = (off + i) % L
                flick[j] = min(flick[j], 0.15 + 0.85 * (1 - math.sin(math.pi * i / m)))
            z = int(0.004 * R)
            zap = shaped(highpass(noise(z, rng), 1500.0, R), envelope(z, R, 0.0005, 0.001))
            mix_loop(zaps, zap, off, 0.5)
    return shaped(hum, flick), zaps


def build_holding():
    rng = random.Random(5801)
    s = Song(LOBBY_BPM, LOBBY_BARS)
    R, H = s.rate, s.rate // 2
    cents = {}
    piano = note_cache(lambda m, v: felt_piano(midi_hz(m, cents.setdefault(m, rng.uniform(-6.0, 6.0))),
                                               H, rng, v, 5.0, prepared=0.12))

    # Piano: each chord rolled up from the bass, its top two notes again (quieter) in the second
    # bar, and the repeating A over all of it.
    keys = s.half_track()
    for i, (bass, voicing, _) in enumerate(LOBBY_CHORDS):
        first = 1 + 2 * i
        start = s.half_at(first) + int(rng.gauss(0.0, 0.004) * H)
        for k, m in enumerate((bass,) + voicing):
            mix_loop(keys, piano(m, 0.6), start + int(k * 0.035 * H), 0.55 if k == 0 else 0.36)
        for k, m in enumerate(voicing[-2:]):
            mix_loop(keys, piano(m, 0.4), s.half_at(first + 1, 2.0) + int(k * 0.05 * H), 0.3)
    for bar in range(1, LOBBY_BARS + 1):
        if bar in LOBBY_GAP:
            continue
        back = 0.7 if bar == LOBBY_GAP[-1] + 1 else 1.0
        for j, (beat, vel) in enumerate(LOBBY_OSTINATO):
            m = LOBBY_OSTINATO_NOTE + (1 if bar in LOBBY_SIGHS and j == 3 else 0)
            v = round(vel * rng.uniform(0.85, 1.0), 1)
            mix_loop(keys, piano(m, v), s.half_at(bar, beat) + int(rng.gauss(0.0, 0.006) * H), v * back)
    s.add_half(keys, -7.0, pan=-0.1, send=0.35, label="piano")

    # Strings under each chord, a bit louder in bars 13-14 where the A drops out.
    strings = s.half_track()
    for i, (_, _, voicing) in enumerate(LOBBY_CHORDS):
        first = 1 + 2 * i
        note = bowed([midi_hz(m) for m in voicing], s.secs(8) + 0.5, H, rng, attack=2.5, release=1.2,
                     vib_cents=8.0, bow=0.08)
        mix_loop(strings, note, s.half_at(first) - H // 2, 1.5 if first in LOBBY_GAP else 1.0)
    s.add_half(strings, -18.0, pan=0.1, send=0.3, label="strings")

    # A soft low pulse on every bar, every other one weaker.
    pulse = shaped(glide(58.0, 40.0, 0.04, int(1.2 * R), R), envelope(int(1.2 * R), R, attack=0.006, tau=0.35))
    pulse = lowpass(pulse, 200.0, R)
    pl = s.track()
    for bar in range(1, LOBBY_BARS + 1):
        mix_loop(pl, pulse, s.at(bar), 1.0 if bar % 2 else 0.55)
    s.add(pl, -15.0, label="pulse")

    # Reversed chords leading into bars 9 and 1, and two distant creaks.
    fl, fr = s.track(), s.track()
    for bar in (9, 1):
        voicing = LOBBY_CHORDS[(bar - 1) // 2][1]
        rev = upsample2(reverse_swell(voicing, s.secs(2.0), H, rng))
        mix_loop(fl, rev, s.at(bar) - len(rev), 0.6)
        mix_loop(fr, rev, s.at(bar) - len(rev), 0.6)
    for bar, beat, pan in LOBBY_CREAKS:
        c = creak(1.4, R, rng, ((0.0, 18.0), (0.5, 30.0), (1.0, 14.0)),
                  ((260.0, 1.0), (590.0, 0.6), (1010.0, 0.35)))
        c = shaped(lowpass(norm(c), 1400.0, R), lines(((0, 0), (0.25, 1), (1.0, 0.7), (1.4, 0)), len(c), R))
        gl, gr = pan_gains(pan)
        mix_loop(fl, c, s.at(bar, beat), 0.35 * gl)
        mix_loop(fr, c, s.at(bar, beat), 0.35 * gr)
    s.add_stereo(fl, fr, -14.0, send=0.5, label="swells/creaks")

    hum, zaps = fluorescent(s, rng, LOBBY_FLICKERS)
    s.add(hum, -45.0, pan=0.1, label="hum")
    s.add(zaps, -30.0, pan=0.1, label="flicker")

    s.add_reverb(rt60=3.0, damp_hz=3000.0, size=1.2, gain_db=-8.5)
    s.finish("Content/Music/holding.wav", LOBBY_RMS_DB)


# --------------------------------------------------------------------------- #
# place-your-hand.wav: table theme
# --------------------------------------------------------------------------- #
# D Phrygian, 92 BPM in half time, 24 bars (about 63 s). Bars 1-12 are the main groove: distorted
# sub kick, metal clang on the backbeat, clock ticks, low tritone strings, sub swells and glitches
# (plus a choir cluster in 9-12). Bars 13-18 drop down to a detuned prepared piano over a held
# tritone, then 19-24 build back up with the kick, a rising choir and a noise riser into a
# stutter roll at the loop point.

ARENA_OSTINATO = (     # low strings in eighths over two bars: D, the tritone Ab, the Phrygian Eb
    (38, 38, 44, 38, 38, 39, 38, 44),
    (38, 38, 44, 38, 50, 44, 39, 38),
)
ARENA_PIANO = (        # (bar, beat, MIDI, velocity) for the breakdown
    (13, 0.0, 74, 0.7), (13, 1.0, 75, 0.5), (13, 2.5, 69, 0.55),
    (14, 0.0, 73, 0.6),
    (15, 0.0, 74, 0.7), (15, 1.0, 75, 0.5), (15, 2.5, 70, 0.55),
    (16, 0.0, 69, 0.6),
    (17, 0.0, 74, 0.7), (17, 0.5, 75, 0.5), (17, 1.0, 80, 0.65),
    (18, 0.0, 79, 0.5), (18, 2.0, 77, 0.45),
)
ARENA_IMPACTS = (1, 13, 19)
ARENA_SUB_SWELLS = (3, 7, 11, 23)     # each one rises over two bars into the next phrase
ARENA_STUTTERS = ((4, 3.0, 8, 0.7), (8, 3.0, 8, 0.7), (12, 2.5, 12, 0.8), (16, 3.0, 6, 0.4))


def arena_kicks(bar):
    """(beat, velocity) of the kicks in a bar."""
    if bar <= 12:
        return ((0.0, 1.0), (1.5, 0.6)) if bar % 2 else ((0.0, 1.0), (2.75, 0.55))
    if bar <= 18:
        return ()
    if bar <= 20:
        return tuple((b, 0.55) for b in range(4))
    if bar <= 22:
        return tuple((b, 0.75) for b in range(4)) + ((3.5, 0.5),)
    if bar == 23:
        return tuple((0.5 * b, 0.8) for b in range(8))
    return ((0.0, 0.9), (0.5, 0.85))      # bar 24: two kicks, then the stutter roll


def build_arena():
    rng = random.Random(9201)
    s = Song(ARENA_BPM, ARENA_BARS)
    R, L, H = s.rate, s.length, s.rate // 2

    def heavy(bar):
        return bar <= 12 or bar >= 21

    kick_at = [(s.at(bar, beat), vel) for bar in range(1, ARENA_BARS + 1) for beat, vel in arena_kicks(bar)]
    k = heavy_kick(R, rng)
    kk = s.track()
    for p, vel in kick_at:
        mix_loop(kk, k, p, vel)
    s.add(kk, -4.0, send=0.03, label="kick")

    # Side-chain: the strings and sub swells duck under each kick. Eighth-note kicks can stack
    # their dips, so it has a floor.
    duck_shape = shaped(decay(int(0.5 * R), 0.1, R), envelope(int(0.5 * R), R, attack=0.004))
    reduction = s.track()
    for p, vel in kick_at:
        mix_loop(reduction, duck_shape, p, vel)
    duck = [max(0.2, 1.0 - 0.7 * v) for v in reduction]

    # Clang on the backbeat (beat 3 in half time), plus a quiet one before some phrase ends.
    clangs = [clang(R, rng, rng.uniform(170.0, 210.0)) for _ in range(3)]
    cl = s.track()
    for bar in range(1, ARENA_BARS + 1):
        if bar <= 12 or 21 <= bar <= 23:
            mix_loop(cl, rng.choice(clangs), s.at(bar, 2.0))
        if bar in (4, 8, 22):
            mix_loop(cl, rng.choice(clangs), s.at(bar, 3.5), 0.35)
    s.add(cl, -5.0, pan=0.1, send=0.3, label="clang")

    # Clock ticks in every bar, breakdown included.
    tick, tock = clock_tick(R, rng, True), clock_tick(R, rng, False)
    clock = s.track()
    for bar in range(1, ARENA_BARS + 1):
        for e in range(8):
            on_beat = e % 2 == 0
            mix_loop(clock, tick if on_beat else tock, s.at(bar, e * 0.5), 1.0 if on_beat else 0.75)
    s.add(clock, -14.0, pan=-0.3, send=0.15, label="clock")

    # Strings: the tritone ostinato (ducked), the held tritone under the breakdown and the two
    # high notes a semitone apart in 9-12, all in one half-rate track.
    marcato = note_cache(lambda m, variant: bowed([midi_hz(m)], s.secs(0.42), H, rng, attack=0.012,
                                                  release=0.07, vib_cents=0.0, bow=0.35, bright=2.2))
    ost = s.half_track()
    for bar in range(1, ARENA_BARS + 1):
        if not heavy(bar):
            continue
        for e, m in enumerate(ARENA_OSTINATO[(bar - 1) % 2]):
            mix_loop(ost, marcato(m, rng.randrange(3)), s.half_at(bar, 0.5 * e), 1.0 if e % 2 == 0 else 0.75)
    strings = shaped(ost, duck[0::2])
    held = bowed([midi_hz(38), midi_hz(44)], s.secs(24), H, rng, attack=2.0, release=1.5, vib_cents=6.0,
                 bow=0.1, bright=0.8)
    mix_loop(strings, held, s.half_at(13), 0.8)
    rub = bowed([midi_hz(68), midi_hz(69)], s.secs(16), H, rng, attack=3.0, release=1.2, vib_cents=12.0, bow=0.12)
    mix_loop(strings, rub, s.half_at(9), 0.25)
    s.add_half(strings, -9.0, pan=-0.1, send=0.2, label="strings")

    # Distorted sub swells, each cut off by the next phrase's first kick.
    sw = s.track()
    for bar in ARENA_SUB_SWELLS:
        mix_loop(sw, sub_swell(midi_hz(26), s.secs(8), R, rng), s.at(bar))
    s.add(shaped(sw, duck), -12.0, send=0.05, label="sub swells")

    # Choir: a held cluster in 9-12, then one that rises a semitone through the build.
    ch = s.half_track()
    cluster = choir([midi_hz(m) for m in (50, 51, 56)], s.secs(16), H, rng, "oo", attack=3.0, release=1.0)
    mix_loop(ch, cluster, s.half_at(9))
    rise = choir([midi_hz(m) for m in (50, 51, 56, 57)], s.secs(24), H, rng, "ah", attack=s.secs(20),
                 release=0.3, glide_cents=100.0)
    mix_loop(ch, rise, s.half_at(19), 1.3)
    s.add_half(ch, -9.0, pan=0.15, send=0.4, label="choir")

    # The breakdown piano, detuned and prepared, with a dotted-eighth echo.
    pc = {}
    keys = s.half_track()
    for bar, beat, m, vel in ARENA_PIANO:
        f = midi_hz(m, pc.setdefault(m, rng.uniform(-12.0, 12.0)))
        mix_loop(keys, felt_piano(f, H, rng, vel, 4.0, prepared=0.35, detune=7.0), s.half_at(bar, beat), vel)
    s.add_half(keys, -8.0, pan=-0.05, send=0.4, label="piano")
    el, er = pingpong(keys, H, 3 * s.beat // 8, 0.4, 2500.0)
    s.add_half(el, -14.0, send=0.3, label="piano echo", right=er)

    # Glitch stutters at the phrase ends, and the roll that ends right at the loop point.
    gl = s.track()
    for bar, beat, reps, g in ARENA_STUTTERS:
        mix_loop(gl, stutter(clangs[0], R, reps, 0.09, 0.85), s.at(bar, beat), g)
    roll = stutter(clangs[1], R, 26, 0.16, 0.9)
    mix_loop(gl, roll, L - len(roll))
    s.add(gl, -11.0, pan=0.2, send=0.2, label="glitch")

    # Impacts where sections land, and the riser through the last four bars.
    fx = s.track()
    for bar in ARENA_IMPACTS:
        mix_loop(fx, sub_impact(R, rng), s.at(bar))
    start = s.at(21)
    n = L - start
    riser = svf(noise(n, rng), lambda i: 250.0 * (6500.0 / 250.0) ** (i / n), 2.0, R, "bp")
    riser = fade(shaped(norm(riser), [(i / n) ** 2.5 for i in range(n)]), R, 0.0, 0.012)
    mix_loop(fx, riser, start, 0.4)
    s.add(fx, -7.0, send=0.3, label="impacts/riser")

    s.add_reverb(rt60=2.6, damp_hz=2500.0, size=1.2, gain_db=-6.0)
    s.finish("Content/Music/place-your-hand.wav", ARENA_RMS_DB)


# --------------------------------------------------------------------------- #
# Sound-effect helpers
# --------------------------------------------------------------------------- #

R = SFX_RATE


def sec(t):
    return int(round(t * R))


def place(dst, src, t, gain=1.0):
    mix(dst, src, sec(t), gain)


def burst(dur, tau, rng, kind=None, fc=1000.0, q=0.7, attack=0.0004):
    """A noise burst: optional biquad colour, attack, exponential decay."""
    n = sec(dur)
    x = noise(n, rng)
    if kind:
        x = biquad(x, kind, fc, q, R)
    return shaped(norm(x), envelope(n, R, attack=attack, tau=tau))


def strike(partials, f0, dur, rng, decay_scale=1.0, click=0.3, click_hp=2000.0):
    """A struck object: decaying partials plus a click of noise for the moment of contact."""
    out = norm(modes([(r, g, t * decay_scale) for r, g, t in partials], sec(dur), R, f0))
    if click:
        mix(out, burst(0.005, 0.0008, rng, "hp", click_hp), 0, click)
    return out


def thump(f0, f1, dur, tau, attack=0.001, sweep_tau=0.03):
    n = sec(dur)
    return shaped(glide(f0, f1, sweep_tau, n, R), envelope(n, R, attack=attack, tau=tau))


def room(x, rt60, damp_hz, wet, size=0.7, tail=None):
    """Adds reverb to a dry effect. The wet signal is scaled to the dry peak, so `wet` is how
    loud the tail is compared to the hit."""
    x = x + silence(sec(rt60 if tail is None else tail))
    wl, wr = reverb(x, R, rt60, damp_hz, size)
    w = norm(list(map(add, wl, wr)), max(map(abs, x)))
    return list(map(add, x, scale(w, wet)))


def finish_sfx(rel, x, peak_db):
    x = highpass(x, 20.0, R)                 # DC block
    floor = max(map(abs, x)) * 5e-4          # 66 dB under the peak counts as silence
    end = len(x)
    while end > 1 and abs(x[end - 1]) < floor:
        end -= 1
    x = x[:min(len(x), end + sec(0.005))]
    fade(x, R, 0.0005, min(0.03, 0.1 * len(x) / R))
    write_wav(rel, [norm(x, undb(peak_db))], R)


# Struck-object recipes, (ratio, gain, tau).
TAG = ((1.0, 1.0, 0.25), (2.71, 0.6, 0.15), (5.13, 0.35, 0.08), (8.3, 0.2, 0.04))
BRASS = ((1.0, 1.0, 0.35), (1.62, 0.5, 0.25), (2.37, 0.45, 0.18), (3.9, 0.2, 0.1))
COIN = ((1.0, 1.0, 0.6), (2.33, 0.6, 0.4), (3.8, 0.4, 0.25), (5.6, 0.2, 0.12))
GLASS = ((1.0, 1.0, 0.9), (2.32, 0.5, 0.5), (4.25, 0.3, 0.3), (6.63, 0.15, 0.15))
BELL = ((0.5, 0.35, 2.4), (1.0, 1.0, 1.6), (1.19, 0.45, 1.1), (1.5, 0.3, 0.9), (2.0, 0.55, 0.8),
        (2.5, 0.2, 0.45), (2.97, 0.18, 0.35), (4.09, 0.1, 0.2))
SMALL_BELL = ((1.0, 1.0, 0.9), (2.0, 0.25, 0.5), (2.76, 0.35, 0.35), (5.4, 0.15, 0.12), (8.93, 0.06, 0.05))
BOTTLE_A = ((1.0, 1.0, 0.5), (2.08, 0.5, 0.3), (3.27, 0.35, 0.2), (5.1, 0.15, 0.1))
BOTTLE_B = ((1.0, 1.0, 0.45), (2.21, 0.45, 0.28), (3.48, 0.3, 0.18), (5.6, 0.12, 0.09))


# --------------------------------------------------------------------------- #
# UI sounds
# --------------------------------------------------------------------------- #

def sfx_menu_move(rng):
    """A muted tick off a stone plate."""
    x = silence(sec(0.09))
    plate = modes(((1.0, 1.0, 0.018), (2.31, 0.5, 0.010), (3.9, 0.25, 0.006)), sec(0.09), R, 1150.0)
    place(x, norm(plate), 0, 0.6)
    place(x, burst(0.006, 0.0015, rng, "bp", 3000.0, 0.8), 0, 0.5)
    place(x, thump(320.0, 180.0, 0.09, 0.025, 0.0005, 0.02), 0, 0.4)
    return x


def sfx_menu_confirm(rng):
    """A deep thunk with a small ember whoosh after it."""
    x = silence(sec(0.75))
    place(x, thump(150.0, 62.0, 0.6, 0.12, 0.001, 0.05), 0, 1.0)
    place(x, strike(((1.0, 1.0, 0.05), (2.4, 0.4, 0.03)), 260.0, 0.3, rng, click=0.0), 0, 0.35)
    place(x, burst(0.1, 0.03, rng, "lp", 900.0), 0, 0.45)
    n = sec(0.5)
    whoosh = svf(noise(n, rng), lambda i: 700.0 * (3200.0 / 700.0) ** (i / n), 1.2, R, "bp")
    whoosh = shaped(norm(whoosh), [v * v for v in lines(((0, 0), (0.15, 1), (0.5, 0)), n, R)])
    place(x, whoosh, 0.03, 0.3)
    for _ in range(6):
        place(x, burst(0.003, 0.0007, rng, "hp", 3000.0), rng.uniform(0.1, 0.4), rng.uniform(0.08, 0.18))
    return x


def sfx_menu_back(rng):
    """A thump played backwards: it swells up and stops, then a very soft tap."""
    t = list(map(add, thump(140.0, 55.0, 0.22, 0.07, 0.001, 0.04), scale(burst(0.22, 0.04, rng, "lp", 700.0), 0.4)))
    x = t[::-1]
    fade(x, R, 0.002, 0.015)
    x += silence(sec(0.1))
    place(x, thump(110.0, 80.0, 0.08, 0.02), 0.215, 0.25)
    return x


def sfx_option_change(rng):
    """Two tiny clicks, the second a fifth up."""
    x = silence(sec(0.12))
    for t, f in ((0.0, 1250.0), (0.05, 1870.0)):
        place(x, norm(modes(((1.0, 1.0, 0.012), (2.7, 0.3, 0.006)), sec(0.06), R, f)), t, 0.8)
        place(x, burst(0.003, 0.0006, rng, "hp", 2500.0), t, 0.3)
    return x


def sfx_type(rng):
    """Typewriter key: a sharp click, the type bar's little ring, the key bottoming out."""
    x = silence(sec(0.09))
    place(x, burst(0.004, 0.0008, rng, "hp", 2500.0), 0, 0.7)
    type_bar = modes(((1.0, 1.0, 0.03), (1.52, 0.6, 0.02), (2.66, 0.4, 0.012)), sec(0.08), R, 2350.0)
    place(x, norm(type_bar), 0, 0.35)
    place(x, thump(260.0, 170.0, 0.06, 0.025, 0.0005, 0.01), 0.006, 0.6)
    return x


def sfx_pause(rng):
    """A soft low clunk, and the latch settling just after."""
    x = silence(sec(0.3))
    for t, g in ((0.0, 1.0), (0.06, 0.35)):
        place(x, thump(170.0, 95.0, 0.24, 0.08, 0.002, 0.03), t, g)
        place(x, strike(((1.0, 1.0, 0.04), (2.2, 0.4, 0.025)), 380.0, 0.15, rng, click=0.0), t, 0.4 * g)
        place(x, burst(0.04, 0.015, rng, "lp", 1200.0), t, 0.3 * g)
    return x


# --------------------------------------------------------------------------- #
# World sounds
# --------------------------------------------------------------------------- #

def sfx_text_blip(rng):
    """35 ms rounded blip for dialogue. The game re-pitches it per speaker."""
    n = sec(0.035)
    x = list(map(add, glide(600.0, 540.0, 0.02, n, R), glide(1200.0, 1080.0, 0.02, n, R, 0.18)))
    a = sec(0.006)
    e = [0.5 - 0.5 * math.cos(math.pi * i / a) if i < a else math.cos(0.5 * math.pi * (i - a) / (n - a)) ** 2
         for i in range(n)]
    return shaped(x, e)


def sfx_footstep(rng):
    """One step on concrete: heel, a low thump, a little grit, then the toe."""
    x = silence(sec(0.13))
    heel = lowpass(burst(0.05, 0.012, rng, "lp", 1400.0), 1400.0, R)
    place(x, norm(heel), 0, 0.8)
    place(x, thump(110.0, 60.0, 0.11, 0.03, 0.001, 0.02), 0, 0.7)
    place(x, burst(0.03, 0.008, rng, "bp", 3000.0, 0.8), 0.002, 0.15)
    place(x, norm(lowpass(burst(0.04, 0.01, rng, "lp", 1100.0), 1100.0, R)), 0.04, 0.35)
    place(x, burst(0.02, 0.005, rng, "bp", 3500.0, 0.8), 0.041, 0.08)
    return x


def sfx_door_locked(rng):
    """Handle rattling, two latch clicks, then a thud (the door doesn't open)."""
    x = silence(sec(1.0))
    for t, g in ((0.0, 1.0), (0.07, 0.6), (0.115, 0.8), (0.19, 0.5), (0.24, 0.7)):
        place(x, strike(STEEL, rng.uniform(1700.0, 2100.0), 0.2, rng, decay_scale=0.09, click=0.5), t, 0.5 * g)
    for t in (0.30, 0.33):
        place(x, strike(TAG, 2600.0, 0.08, rng, decay_scale=0.15, click=0.6, click_hp=3000.0), t, 0.35)
    place(x, thump(95.0, 60.0, 0.5, 0.12, 0.002, 0.04), 0.36, 1.0)
    place(x, burst(0.2, 0.05, rng, "lp", 500.0), 0.36, 0.5)
    place(x, strike(STEEL, 110.0, 0.5, rng, decay_scale=0.4, click=0.0), 0.36, 0.25)
    return room(x, 0.6, 3500.0, 0.25)


def sfx_door_open(rng):
    """Heavy bolt: two clacks as it slides back, then a long low creak of the door."""
    x = silence(sec(2.2))
    place(x, strike(STEEL, 620.0, 0.4, rng, decay_scale=0.35, click=0.8), 0, 0.8)
    place(x, burst(0.06, 0.03, rng, "bp", 1500.0, 1.0), 0.03, 0.25)
    place(x, strike(STEEL, 540.0, 0.5, rng, decay_scale=0.45, click=0.9), 0.09, 1.0)
    place(x, thump(120.0, 70.0, 0.3, 0.08), 0.09, 0.5)
    c = creak(1.45, R, rng, ((0.0, 22.0), (0.3, 14.0), (0.7, 26.0), (1.0, 11.0)),
              ((190.0, 1.0), (410.0, 0.7), (733.0, 0.5), (1170.0, 0.3)), q=25.0)
    c = shaped(norm(c), lines(((0, 0), (0.15, 0.8), (0.6, 1.0), (1.2, 0.6), (1.45, 0)), len(c), R))
    place(x, c, 0.25, 0.55)
    place(x, shaped(sine(55.0, sec(1.4), R), lines(((0, 0), (0.4, 1), (1.4, 0)), sec(1.4), R)), 0.25, 0.12)
    return room(x, 1.2, 3000.0, 0.3)


def sfx_enter_arena(rng):
    """Steel door slam: boom, impact, the door ringing, a sub drop and a long reverb tail."""
    x = silence(sec(3.0))
    place(x, saturate(thump(75.0, 32.0, 2.4, 0.6, 0.001, 0.25), 2.0), 0, 1.0)
    place(x, burst(0.4, 0.05, rng, "lp", 1800.0), 0, 0.8)
    place(x, burst(0.05, 0.008, rng, "hp", 2500.0), 0, 0.3)
    place(x, strike(STEEL, 92.0, 2.4, rng, decay_scale=3.0, click=0.0), 0, 0.45)
    place(x, strike(STEEL, 263.0, 1.5, rng, decay_scale=1.5, click=0.0), 0.004, 0.2)
    n = sec(2.2)
    place(x, shaped(glide(65.0, 26.0, 0.6, n, R), envelope(n, R, attack=0.05, tau=1.0)), 0.05, 0.6)
    x = room(x, 3.0, 1500.0, 0.5, size=1.4, tail=0.0)
    x = x[:sec(3.0)]
    return fade(x, R, 0.0005, 0.5)


def sfx_pockets_full(rng):
    """A dull double knock, the second one lower (like a 'no')."""
    x = silence(sec(0.35))
    for t, f, g in ((0.0, 210.0, 1.0), (0.11, 175.0, 0.85)):
        place(x, strike(((1.0, 1.0, 0.05), (2.3, 0.35, 0.03), (3.9, 0.12, 0.015)), f, 0.2, rng, click=0.0), t, g)
        place(x, burst(0.03, 0.008, rng, "lp", 1500.0), t, 0.5 * g)
        place(x, thump(f * 0.6, f * 0.45, 0.15, 0.04), t, 0.6 * g)
    return room(x, 0.4, 3000.0, 0.15)


# --------------------------------------------------------------------------- #
# Table sounds
# --------------------------------------------------------------------------- #

def sfx_box_open(rng):
    """Stone grinding as the box's jaws open, with a low hum under it."""
    n = sec(1.7)
    raw = noise(n, rng)
    grind = list(map(add, biquad(raw, "bp", 450.0, 0.9, R), scale(biquad(raw, "bp", 1100.0, 1.5, R), 0.6)))
    rough = norm(lowpass([abs(v) for v in noise(n, rng)], 45.0, R))
    for _ in range(14):                    # where the stone catches
        i = rng.randrange(sec(0.05), sec(1.2))
        for k in range(sec(0.012)):
            rough[i + k] += 0.8 * math.sin(math.pi * k / sec(0.012))
    shape = lines(((0, 0), (0.15, 0.8), (0.9, 1.0), (1.25, 0.6), (1.35, 0)), n, R)
    grind = shaped(shaped(norm(grind), rough), shape)
    x = scale(norm(grind), 0.8)
    hum = silence(n)
    for f, g in ((55.0, 1.0), (110.0, 0.6), (165.5, 0.3)):
        mix(hum, sine(f, n, R, g))
    trem = [0.8 + 0.2 * math.sin(TAU * 3.0 * i / R) for i in range(n)]
    hum = shaped(shaped(hum, trem), lines(((0, 0), (0.45, 1.0), (1.2, 0.8), (1.7, 0)), n, R))
    mix(x, norm(hum), 0, 0.55)
    place(x, thump(120.0, 60.0, 0.3, 0.08), 1.3, 0.6)
    place(x, burst(0.1, 0.03, rng, "lp", 900.0), 1.3, 0.3)
    return room(x, 0.9, 2500.0, 0.2)


def sfx_hand_in(rng):
    """A reversed whoosh into a low wet thump, for the box taking the hand."""
    x = silence(sec(1.2))
    n = sec(0.5)
    w = svf(noise(n, rng), lambda i: 250.0 * (1600.0 / 250.0) ** (i / n), 2.0, R, "bp")
    w = shaped(norm(w), [(i / n) ** 3 for i in range(n)])
    w = fade(w, R, 0.0, 0.01)
    place(x, w, 0.0, 0.6)
    place(x, saturate(thump(95.0, 38.0, 0.6, 0.18, 0.002, 0.06), 1.8), 0.5, 1.0)
    m = sec(0.25)
    sq = svf(noise(m, rng), lambda i: 1100.0 * (260.0 / 1100.0) ** (i / m), 4.0, R, "bp")
    sq = shaped(norm(sq), envelope(m, R, attack=0.002, tau=0.07))
    place(x, sq, 0.5, 0.45)
    place(x, burst(0.15, 0.03, rng, "lp", 600.0), 0.5, 0.4)
    return room(x, 1.0, 1800.0, 0.25)


def sfx_payout(rng):
    """A metal tag slides out of the slot and rings."""
    x = silence(sec(0.8))
    n = sec(0.2)
    raw = noise(n, rng)
    slide = list(map(add, biquad(raw, "bp", 2600.0, 1.5, R), scale(biquad(raw, "bp", 5200.0, 3.0, R), 0.5)))
    slide = shaped(norm(slide), lines(((0, 0), (0.03, 0.5), (0.18, 1.0), (0.2, 0)), n, R))
    place(x, slide, 0, 0.35)
    place(x, shaped(sweep(900.0, 1300.0, n, R), lines(((0, 0), (0.1, 1), (0.2, 0)), n, R)), 0, 0.04)
    place(x, strike(TAG, 1850.0, 0.6, rng, click=0.4, click_hp=3000.0), 0.2, 1.0)
    return room(x, 0.5, 5000.0, 0.15)


def sfx_catch(rng):
    """Catching the tag: a soft slap and a short clink (the hand mutes it)."""
    x = silence(sec(0.35))
    place(x, burst(0.06, 0.012, rng, "lp", 2200.0), 0, 0.9)
    place(x, thump(180.0, 120.0, 0.1, 0.025), 0, 0.6)
    place(x, strike(TAG, 1850.0, 0.25, rng, decay_scale=0.3, click=0.0), 0.008, 0.4)
    return x


def sfx_drop(rng):
    """The tag falling away: bounces that get closer together, quieter and darker."""
    x = silence(sec(1.2))
    times = (0.0, 0.30, 0.52, 0.68, 0.79, 0.87, 0.925)
    for k, t in enumerate(times):
        g = 0.62 ** k
        hit = strike(TAG, 1850.0 * rng.uniform(0.98, 1.02), 0.4, rng, decay_scale=0.7, click=0.3)
        cut = 9000.0 * (1500.0 / 9000.0) ** (k / (len(times) - 1))
        place(x, lowpass(lowpass(hit, cut, R), cut, R), t, g)
    return room(x, 2.2, 1200.0, 0.45, size=1.1)


def sfx_check_good(rng):
    """Two bells rising a fifth, D then A."""
    x = silence(sec(1.5))
    place(x, strike(SMALL_BELL, 587.3, 1.4, rng, click=0.05), 0, 0.85)
    place(x, strike(SMALL_BELL, 880.0, 1.3, rng, click=0.05), 0.13, 1.0)
    return room(x, 1.0, 5000.0, 0.2)


def sfx_check_bad(rng):
    """A dull buzz dropping a few semitones, with a second slightly detuned voice so it sounds rough."""
    n = sec(0.45)
    x = list(map(add, osc_sweep("saw", 150.0, 100.0, n, R), osc_sweep("saw", 159.0, 105.0, n, R, 0.7)))
    x = lowpass(lowpass(x, 900.0, R), 900.0, R)
    x = shaped(x, envelope(n, R, attack=0.005, gate=0.38, release=0.02))
    return room(x, 0.4, 3000.0, 0.1)


def sfx_hit(rng):
    """The opponent loses a life: punch, snap and a bit-crushed crunch."""
    x = silence(sec(0.6))
    place(x, thump(140.0, 52.0, 0.5, 0.12, 0.001, 0.03), 0, 1.0)
    place(x, burst(0.06, 0.02, rng, "bp", 2500.0, 0.7), 0, 0.6)
    crunch = noise(sec(0.12), rng)
    held = 0.0
    for i in range(len(crunch)):          # sample-and-hold at a sixth of the rate
        if i % 6 == 0:
            held = crunch[i]
        crunch[i] = held
    crunch = shaped(saturate(crunch, 4.0), envelope(len(crunch), R, attack=0.001, tau=0.04))
    place(x, lowpass(crunch, 5000.0, R), 0.003, 0.45)
    return room(saturate(x, 1.3), 0.7, 3000.0, 0.15)


def sfx_damage(rng):
    """The player loses a life: a heavy distorted low hit, a heartbeat and a high ring that fades out."""
    x = silence(sec(2.4))
    place(x, saturate(thump(110.0, 34.0, 0.9, 0.3, 0.001, 0.06), 3.0), 0, 1.0)
    place(x, burst(0.2, 0.04, rng, "lp", 1200.0), 0, 0.6)
    place(x, heartbeat(R, rng), 0.55, 0.55)
    n = sec(2.2)
    ring = [v * (0.85 + 0.15 * math.sin(TAU * 4.0 * i / R)) for i, v in enumerate(sine(5800.0, n, R))]
    place(x, shaped(ring, lines(((0, 0), (0.25, 1.0), (2.2, 0)), n, R)), 0.05, 0.05)
    return room(x, 1.2, 1800.0, 0.2, tail=0.2)


def sfx_heal(rng):
    """A warm swell rising into D major."""
    n = sec(1.6)
    x = silence(n)
    for m in (62, 66, 69, 74):
        f = midi_hz(m)
        tone = list(map(add, glide(f * 0.94, f, 0.25, n, R), glide(f * 1.88, f * 2.0, 0.25, n, R, 0.12)))
        mix(x, tone, 0, 0.25)
    x = shaped(x, [v * v for v in lines(((0, 0), (0.55, 1.0), (1.6, 0)), n, R)])
    x = svf(x, lambda i: 500.0 + 2500.0 * min(1.0, i / sec(0.6)), 0.7, R)
    mix(x, shaped(norm(lowpass(noise(n, rng), 1500.0, R)), lines(((0, 0), (0.5, 0.15), (1.4, 0)), n, R)), 0, 0.3)
    return room(x, 1.4, 4000.0, 0.3)


def sfx_guard(rng):
    """Guard: a short whoosh, a soft hit, then a glassy shimmer."""
    x = silence(sec(1.3))
    n = sec(0.12)
    w = svf(noise(n, rng), lambda i: 1500.0 * 2.0 ** (i / n), 1.5, R, "bp")
    place(x, fade(shaped(norm(w), [(i / n) ** 2 for i in range(n)]), R, 0.0, 0.004), 0, 0.4)
    place(x, thump(220.0, 140.0, 0.15, 0.03), 0.12, 0.5)
    m = sec(1.1)
    sh = silence(m)
    for f, g, tau in ((2093.0, 1.0, 0.9), (2117.0, 0.9, 0.85), (3136.0, 0.6, 0.7), (3170.0, 0.55, 0.65),
                      (4186.0, 0.4, 0.5), (4230.0, 0.35, 0.5), (5274.0, 0.25, 0.4), (6272.0, 0.18, 0.3)):
        mix(sh, sine(f, m, R, g, tau))
    sh = shaped(norm(sh), [0.7 + 0.3 * math.sin(TAU * 11.0 * i / R) for i in range(m)])
    place(x, fade(sh, R, 0.003, 0.05), 0.12, 0.7)
    return room(x, 1.2, 7000.0, 0.3)


def sfx_win(rng):
    """Win stinger: a D minor music box phrase that resolves to D major over a swelling pad."""
    x = silence(sec(3.9))
    phrase = ((0.0, 74), (0.18, 77), (0.36, 81), (0.54, 79), (0.72, 77), (0.90, 76))
    for t, m in phrase:
        place(x, music_box_note(midi_hz(m, -8.0), R, rng), t, 0.55)
    for k, m in enumerate((74, 78, 81, 86)):
        place(x, music_box_note(midi_hz(m, -8.0), R, rng), 1.15 + 0.04 * k, 0.6)
    n = sec(3.0)
    pad = silence(n)
    for m, g in ((50, 1.0), (57, 0.7), (62, 0.6), (66, 0.5)):
        f = midi_hz(m)
        mix(pad, list(map(add, osc("saw", f * 2 ** (-6 / 1200), n, R, 0.5, rng.random()),
                          osc("saw", f * 2 ** (6 / 1200), n, R, 0.5, rng.random()))), 0, g)
    pad = svf(pad, lambda i: 300.0 + 1200.0 * min(1.0, i / sec(1.2)), 0.7, R)
    pad = shaped(norm(pad), [v * v for v in lines(((0, 0), (0.8, 1.0), (1.8, 0.7), (3.0, 0)), n, R)])
    place(x, pad, 0.7, 0.35)
    x = room(x, 2.0, 4000.0, 0.35, tail=0.0)
    return fade(x[:sec(3.95)], R, 0.0005, 0.6)


def sfx_lose(rng):
    """Stinger: a deep boom, then a dissonant cluster sliding down into a low drone."""
    x = silence(sec(3.9))
    place(x, saturate(thump(70.0, 30.0, 1.8, 0.7, 0.001, 0.3), 1.8), 0, 1.0)
    place(x, burst(0.4, 0.06, rng, "lp", 1200.0), 0, 0.6)
    n = sec(3.6)
    cl = silence(n)
    for m in (50, 51, 56, 57):              # D, Eb, Ab, A, all clashing
        f = midi_hz(m)
        mix(cl, osc_sweep("saw", f, f * 2 ** (-7 / 12), n, R, 0.25))
    cl = svf(cl, lambda i: 1200.0 * (400.0 / 1200.0) ** (i / n), 0.9, R)
    cl = shaped(norm(cl), lines(((0, 0), (0.3, 1.0), (2.6, 0.7), (3.6, 0)), n, R))
    place(x, cl, 0.25, 0.3)
    m = sec(2.4)
    drone = list(map(add, sine(36.7, m, R), sine(73.4, m, R, 0.5)))
    place(x, shaped(drone, lines(((0, 0), (0.6, 1.0), (2.4, 0)), m, R)), 1.5, 0.5)
    x = room(x, 2.5, 1500.0, 0.35, tail=0.0)
    return fade(x[:sec(3.95)], R, 0.0005, 0.5)


# --------------------------------------------------------------------------- #
# Item sounds
# --------------------------------------------------------------------------- #

def sfx_cinder(rng):
    """Ash crumbling and a soft ember crackle."""
    n = sec(1.0)
    x = silence(n)
    for _ in range(140):
        t = 0.6 * rng.random() ** 1.7
        dur = rng.uniform(0.0015, 0.006)
        g = (1.0 - t / 0.6) * rng.uniform(0.2, 1.0)
        place(x, burst(dur, dur / 3, rng, "bp", rng.uniform(900.0, 3500.0), 1.2), t, g * 0.8)
    for _ in range(14):
        t = rng.uniform(0.05, 0.95)
        place(x, burst(0.004, 0.0006, rng, "bp", 4000.0, 3.0), t, rng.uniform(0.3, 0.8))
    mix(x, shaped(norm(lowpass(noise(n, rng), 2500.0, R)), lines(((0, 0), (0.1, 0.25), (0.9, 0)), n, R)), 0, 0.15)
    place(x, thump(90.0, 60.0, 0.2, 0.1), 0.02, 0.1)
    return x


def sfx_spent_shell(rng):
    """A brass casing bouncing on concrete, the bounces getting closer together."""
    x = silence(sec(1.1))
    for t, g in ((0.0, 1.0), (0.21, 0.55), (0.36, 0.32), (0.46, 0.18), (0.52, 0.1)):
        place(x, strike(BRASS, 3150.0 * rng.uniform(0.98, 1.02), 0.6, rng, decay_scale=0.5, click=0.5,
                        click_hp=4000.0), t, g)
    for k in range(5):
        place(x, burst(0.003, 0.0007, rng, "hp", 4000.0), 0.56 + 0.025 * k, 0.06 * (1 - k / 5))
    return room(x, 0.6, 5000.0, 0.12)


def sfx_revolver(rng):
    """Hammer and cylinder clicks, then the shot with a short room reverb."""
    x = silence(sec(1.5))
    place(x, strike(STEEL, 2200.0, 0.1, rng, decay_scale=0.06, click=0.8, click_hp=3000.0), 0, 0.3)
    place(x, burst(0.02, 0.006, rng, "bp", 4000.0, 1.0), 0.01, 0.08)
    place(x, strike(STEEL, 3300.0, 0.08, rng, decay_scale=0.05, click=0.7, click_hp=3000.0), 0.045, 0.22)
    t = 0.38
    place(x, burst(0.12, 0.018, rng, "lp", 9000.0, attack=0.0003), t, 1.0)
    place(x, burst(0.25, 0.06, rng, "lp", 1200.0), t, 0.6)
    place(x, thump(120.0, 45.0, 0.4, 0.09, 0.0005, 0.03), t, 0.7)
    place(x, burst(0.01, 0.004, rng, "hp", 3000.0, attack=0.0001), t, 0.9)
    return room(saturate(x, 2.0), 0.9, 3500.0, 0.35)


def sfx_pact(rng):
    """A low tritone swell with two heartbeats in it."""
    n = sec(2.4)
    x = silence(n)
    for m, g in ((38, 1.0), (44, 0.8), (50, 0.5)):    # D2, Ab2, D3
        f = midi_hz(m)
        mix(x, osc("saw", f, n, R, 0.4 * g, rng.random()))
        mix(x, sine(f, n, R, 0.6 * g))
    x = svf(x, 500.0, 0.8, R)
    x = shaped(norm(x), lines(((0, 0), (1.0, 1.0), (1.8, 0.6), (2.4, 0)), n, R))
    x = scale(x, 0.6)
    place(x, heartbeat(R, rng), 0.7, 0.8)
    place(x, heartbeat(R, rng), 1.45, 0.9)
    return room(x, 1.5, 1800.0, 0.3)


def sfx_wager(rng):
    """Coin flip: the flick, the spinning wobble, the landing and a few small bounces."""
    x = silence(sec(1.2))
    place(x, strike(COIN, 2400.0, 0.1, rng, decay_scale=0.15, click=0.5, click_hp=3000.0), 0, 0.6)
    n = sec(0.55)
    spin = modes(((1.0, 1.0, 1.0), (2.33, 0.6, 0.6)), n, R, 2400.0)
    wobble = sweep(9.0, 16.0, n, R)      # the wobble speeds up as it falls
    spin = [v * (0.55 + 0.45 * w) for v, w in zip(spin, wobble)]
    spin = shaped(norm(spin), lines(((0, 0), (0.02, 1.0), (0.5, 0.5), (0.55, 0)), n, R))
    place(x, spin, 0.02, 0.35)
    place(x, strike(COIN, 2470.0, 0.5, rng, click=0.6, click_hp=2500.0), 0.6, 1.0)
    for k, t in enumerate((0.68, 0.74, 0.785, 0.82, 0.845, 0.865, 0.88)):
        place(x, strike(COIN, 2470.0, 0.15, rng, decay_scale=0.2, click=0.4), t, 0.35 * 0.8 ** k)
    return room(x, 0.5, 5000.0, 0.12)


def riffle(x, start, count, first_gap, last_gap, rng, gain=1.0):
    """Card riffle: `count` short paper ticks, with the gap between them going from first_gap
    to last_gap. Returns the time of the last one."""
    t = start
    for k in range(count):
        u = k / max(1, count - 1)
        place(x, burst(0.004, 0.0018, rng, "bp", 4000.0, 0.8), t, gain * rng.uniform(0.6, 1.0))
        place(x, burst(0.006, 0.003, rng, "bp", 900.0, 1.0), t, gain * 0.25)
        t += first_gap + (last_gap - first_gap) * u
    return t


def sfx_high_card(rng):
    """Card riffle that speeds up, then the deck snapping together."""
    x = silence(sec(0.75))
    end = riffle(x, 0.0, 25, 0.032, 0.009, rng)
    n = sec(end)
    mix(x, shaped(norm(biquad(noise(n, rng), "bp", 2000.0, 0.7, R)), lines(((0, 0), (end, 1.0)), n, R)), 0, 0.15)
    place(x, burst(0.01, 0.006, rng, "hp", 1500.0, attack=0.0001), end + 0.025, 1.0)
    place(x, thump(250.0, 150.0, 0.06, 0.02), end + 0.025, 0.5)
    return x


def sfx_tourniquet(rng):
    """Cloth pulled tight: a rising swish and a creak at the end."""
    x = silence(sec(0.8))
    n = sec(0.36)
    sw = svf(noise(n, rng), lambda i: 500.0 * (2600.0 / 500.0) ** (i / n), 1.4, R, "bp")
    place(x, shaped(norm(sw), lines(((0, 0), (0.3, 1.0), (0.36, 0)), n, R)), 0, 0.7)
    c = creak(0.24, R, rng, ((0.0, 140.0), (1.0, 90.0)), ((780.0, 1.0), (1340.0, 0.6), (2300.0, 0.3)), q=18.0)
    c = shaped(norm(c), lines(((0, 0), (0.02, 1.0), (0.18, 0.7), (0.24, 0)), len(c), R))
    place(x, c, 0.33, 0.6)
    place(x, burst(0.01, 0.003, rng, "bp", 2000.0), 0.33, 0.3)
    return x


def sfx_ash_veil(rng):
    """A soft whoosh, a breathy tail and a few faint high pings."""
    n = sec(1.2)
    w = svf(noise(n, rng), lambda i: 400.0 + 1800.0 * math.sin(math.pi * min(1.0, i / n)), 0.9, R)
    x = shaped(norm(w), lines(((0, 0), (0.25, 1.0), (0.6, 0.35), (1.2, 0)), n, R))
    raw = noise(n, rng)
    breath = list(map(add, biquad(raw, "bp", 700.0, 3.0, R), biquad(raw, "bp", 1200.0, 4.0, R)))
    mix(x, shaped(norm(breath), lines(((0.0, 0), (0.2, 0), (0.5, 0.5), (1.2, 0)), n, R)), 0, 0.4)
    for _ in range(7):
        place(x, strike(GLASS, rng.uniform(6000.0, 9000.0), 0.1, rng, decay_scale=0.06, click=0.0),
              rng.uniform(0.1, 0.8), rng.uniform(0.03, 0.07))
    return room(x, 1.0, 4000.0, 0.3)


def sfx_mirror(rng):
    """Glass shimmer: a bright ting plus pairs of slightly detuned high partials so they beat."""
    x = silence(sec(1.6))
    place(x, strike(GLASS, 2200.0, 1.2, rng, click=0.2, click_hp=5000.0), 0, 0.7)
    n = sec(1.5)
    sh = silence(n)
    for f1, f2, g, tau in ((3520.0, 3531.0, 0.5, 1.2), (4699.0, 4716.0, 0.35, 0.9),
                           (5920.0, 5900.0, 0.25, 0.7), (7040.0, 7068.0, 0.15, 0.6)):
        mix(sh, sine(f1, n, R, g, tau))
        mix(sh, sine(f2, n, R, g, tau))
    sh = shaped(norm(sh), lines(((0, 0), (0.05, 1.0)), n, R))
    place(x, sh, 0.01, 0.6)
    return room(x, 1.2, 8000.0, 0.2)


def sfx_lens(rng):
    """Glass ting, the focus ring clicks, a second ting a fifth higher."""
    x = silence(sec(1.3))
    place(x, strike(GLASS, 1318.5, 1.0, rng, decay_scale=0.8, click=0.15), 0, 0.8)
    place(x, strike(STEEL, 3000.0, 0.05, rng, decay_scale=0.03, click=0.8, click_hp=3500.0), 0.17, 0.35)
    place(x, strike(GLASS, 1975.5, 1.0, rng, decay_scale=0.8, click=0.15), 0.22, 0.85)
    return room(x, 0.9, 7000.0, 0.2)


def chalk_stroke(dur, rng, f_lo, f_hi):
    n = sec(dur)
    raw = noise(n, rng)
    band = list(map(add, svf(raw, lambda i: f_lo * (f_hi / f_lo) ** (i / n), 1.2, R, "bp"),
                    scale(biquad(raw, "bp", 5600.0, 2.0, R), 0.5)))
    grit = [0.4 + 0.6 * abs(math.sin(TAU * 170.0 * i / R + 3.0 * math.sin(TAU * 23.0 * i / R))) for i in range(n)]
    return shaped(shaped(norm(band), grit), lines(((0, 0), (0.008, 1.0), (dur * 0.8, 0.7), (dur, 0)), n, R))


def sfx_tally(rng):
    """Chalk on a wall: four short strokes, then the long one across them."""
    x = silence(sec(1.15))
    for t in (0.0, 0.17, 0.34, 0.51):
        place(x, thump(220.0, 180.0, 0.05, 0.015), t, 0.3)
        place(x, chalk_stroke(0.08, rng, 3000.0, 3400.0), t, 0.8)
    place(x, thump(220.0, 180.0, 0.05, 0.015), 0.75, 0.3)
    place(x, chalk_stroke(0.24, rng, 2500.0, 4200.0), 0.75, 1.0)
    return room(x, 0.5, 5000.0, 0.1)


def sfx_marked_deck(rng):
    """A long shuffle (speeding up, then settling) and one card flicked out."""
    x = silence(sec(1.45))
    mid = riffle(x, 0.0, 26, 0.025, 0.011, rng)
    end = riffle(x, mid, 20, 0.011, 0.022, rng)
    n = sec(end)
    mix(x, shaped(norm(biquad(noise(n, rng), "bp", 2000.0, 0.7, R)),
                  lines(((0, 0), (mid, 1.0), (end, 0)), n, R)), 0, 0.15)
    place(x, burst(0.01, 0.006, rng, "hp", 1500.0, attack=0.0001), end + 0.03, 0.5)
    t = 1.05
    place(x, burst(0.008, 0.003, rng, "hp", 1500.0, attack=0.0001), t, 1.0)
    for k in range(5):
        place(x, burst(0.004, 0.0015, rng, "bp", 3500.0, 1.0), t + 0.012 + 0.007 * k, 0.4 * 0.75 ** k)
    m = sec(0.2)
    place(x, shaped(norm(biquad(noise(m, rng), "bp", 2500.0, 0.8, R)), envelope(m, R, 0.005, 0.05)), t, 0.2)
    return x


def sfx_confession(rng):
    """Whispered breath through vowel formants that slide from 'ah' to 'oh', over one low note."""
    n = sec(2.0)
    raw = noise(n, rng)
    x = silence(n)
    for lo, hi, q, g in ((800.0, 450.0, 5.0, 1.0), (1250.0, 850.0, 8.0, 0.7), (2600.0, 2300.0, 10.0, 0.35)):
        mix(x, svf(raw, lambda i, a=lo, b=hi: a * (b / a) ** (i / n), q, R, "bp"), 0, g)
    mix(x, lowpass(raw, 3500.0, R), 0, 0.12)
    x = shaped(norm(x), lines(((0, 0), (0.35, 1.0), (1.2, 0.7), (1.9, 0)), n, R))
    m = sec(2.1)
    note = saturate(list(map(add, sine(73.4, m, R), sine(146.8, m, R, 0.25))), 1.2)
    note = shaped(note, lines(((0, 0), (0.5, 1.0), (1.6, 0.8), (2.1, 0)), m, R))
    out = scale(x, 0.7)
    out += silence(m - n)
    mix(out, note, 0, 0.3)
    return room(out, 1.6, 2000.0, 0.35)


def sfx_levy(rng):
    """Ignition: a striker click, the gas catching with a fwoomp, then the flame crackling."""
    x = silence(sec(1.7))
    place(x, strike(STEEL, 3600.0, 0.05, rng, decay_scale=0.04, click=0.9, click_hp=3000.0), 0, 0.2)
    n = sec(0.6)
    f = svf(noise(n, rng), lambda i: 150.0 * (2800.0 / 150.0) ** min(1.0, i / sec(0.12)) if i < sec(0.12)
            else 2800.0 * (900.0 / 2800.0) ** ((i - sec(0.12)) / (n - sec(0.12))), 0.9, R)
    place(x, shaped(norm(f), envelope(n, R, attack=0.04, tau=0.2)), 0.03, 0.9)
    place(x, thump(70.0, 45.0, 0.4, 0.15, 0.01, 0.05), 0.03, 0.9)
    m = sec(1.5)
    raw = noise(m, rng)
    roar = list(map(add, lowpass(raw, 600.0, R), scale(biquad(raw, "bp", 1200.0, 1.0, R), 0.4)))
    flicker = norm(lowpass(noise(m, rng), 8.0, R))
    roar = shaped(norm(roar), [0.75 + 0.25 * v for v in flicker])
    place(x, shaped(roar, lines(((0, 0), (0.1, 0.0), (0.25, 0.6), (1.5, 0)), m, R)), 0.1, 0.5)
    for _ in range(25):
        place(x, burst(0.003, 0.0006, rng, "hp", 2000.0), rng.uniform(0.15, 1.4), rng.uniform(0.1, 0.35))
    return room(x, 0.6, 4000.0, 0.15)


def sfx_second_hand(rng):
    """Four ticks closing up, then a small bell."""
    x = silence(sec(2.2))
    for k, t in enumerate((0.0, 0.36, 0.64, 0.84)):
        f = 2900.0 if k % 2 == 0 else 2400.0
        tick = strike(((1.0, 1.0, 0.008), (1.73, 0.5, 0.005), (2.9, 0.3, 0.003)), f, 0.05, rng, click=0.4)
        place(x, tick, t, 0.5)
        place(x, thump(700.0, 500.0, 0.03, 0.006), t, 0.2)
    place(x, strike(SMALL_BELL, 1760.0, 1.2, rng, click=0.05), 1.0, 1.0)
    return room(x, 0.8, 6000.0, 0.15)


def fm_bell(freq, dur):
    """FM bell with an inharmonic modulator (3.5:1) and an index that dies off quickly."""
    n = sec(dur)
    w = TAU * freq / R
    wm = w * 3.5
    sin = math.sin
    x = [sin(w * i + 4.0 * d * sin(wm * i)) for i, d in enumerate(decay(n, 0.15, R))]
    return shaped(x, envelope(n, R, attack=0.001, tau=0.5))


def sfx_wild_card(rng):
    """A reverse swell into a fast shimmering D minor arpeggio of FM bells."""
    x = silence(sec(1.8))
    first = list(map(add, fm_bell(587.3, 0.4), scale(fm_bell(587.3 * 1.004, 0.4), 0.7)))
    place(x, fade(first[::-1], R, 0.01, 0.002), 0.0, 0.5)
    n = sec(0.4)
    rush = shaped(norm(biquad(noise(n, rng), "hp", 3000.0, 0.7, R)), [(i / n) ** 3 for i in range(n)])
    place(x, rush, 0.0, 0.15)
    for k, m in enumerate((74, 77, 81, 86, 89, 93)):
        f = midi_hz(m)
        b = list(map(add, fm_bell(f, 1.0), scale(fm_bell(f * 1.0035, 1.0), 0.7)))
        place(x, b, 0.4 + 0.045 * k, 0.45 * (1.0 - 0.06 * k))
    return room(x, 1.4, 6000.0, 0.35)


def sfx_rotgut(rng):
    """Liquid glugs out of the neck, then a gulp."""
    x = silence(sec(1.3))
    for t in (0.0, 0.17, 0.33, 0.48, 0.62):
        n = sec(0.08)
        glug = svf(noise(n, rng), lambda i: 280.0 * (850.0 / 280.0) ** (i / n), 6.0, R, "bp")
        place(x, shaped(norm(glug), envelope(n, R, attack=0.005, tau=0.025)), t, 0.7)
        place(x, shaped(sweep(300.0, 700.0, n, R), envelope(n, R, attack=0.002, tau=0.03)), t, 0.35)
    t = 0.9
    place(x, thump(160.0, 70.0, 0.25, 0.08, 0.003, 0.05), t, 0.8)
    m = sec(0.15)
    sq = svf(noise(m, rng), lambda i: 600.0 * (230.0 / 600.0) ** (i / m), 5.0, R, "bp")
    place(x, shaped(norm(sq), envelope(m, R, attack=0.003, tau=0.05)), t, 0.5)
    place(x, burst(0.006, 0.0015, rng, "bp", 1500.0), t - 0.01, 0.3)
    return room(x, 0.4, 3000.0, 0.1)


def sfx_last_call(rng):
    """Two bottles clink, and a low bell tolls once."""
    x = silence(sec(3.0))
    place(x, strike(BOTTLE_A, 1650.0, 0.6, rng, click=0.4, click_hp=3000.0), 0, 0.8)
    place(x, strike(BOTTLE_B, 1910.0, 0.6, rng, click=0.4, click_hp=3000.0), 0.075, 0.6)
    place(x, strike([(r, g, t * 1.2) for r, g, t in BELL], 146.8, 2.6, rng, click=0.05), 0.32, 1.0)
    return room(x, 1.5, 3000.0, 0.3, tail=0.3)


# --------------------------------------------------------------------------- #
# Build
# --------------------------------------------------------------------------- #

SONGS = {
    "it-is-watching": build_title,
    "holding": build_holding,
    "place-your-hand": build_arena,
}

SFX = {   # name: (folder, peak dBFS, builder)
    "menu-move": ("Content/Sfx", PEAK_CLICK, sfx_menu_move),
    "menu-confirm": ("Content/Sfx", PEAK_MENU, sfx_menu_confirm),
    "menu-back": ("Content/Sfx", PEAK_MENU, sfx_menu_back),
    "option-change": ("Content/Sfx", PEAK_CLICK, sfx_option_change),
    "type": ("Content/Sfx", PEAK_CLICK, sfx_type),
    "pause": ("Content/Sfx", PEAK_CLICK, sfx_pause),
    "text-blip": ("Content/Sfx", PEAK_REPEAT, sfx_text_blip),
    "footstep": ("Content/Sfx", PEAK_REPEAT, sfx_footstep),
    "door-locked": ("Content/Sfx", PEAK_ITEM, sfx_door_locked),
    "door-open": ("Content/Sfx", PEAK_BIG, sfx_door_open),
    "enter-arena": ("Content/Sfx", PEAK_BIG, sfx_enter_arena),
    "pockets-full": ("Content/Sfx", PEAK_ITEM, sfx_pockets_full),
    "box-open": ("Content/Sfx", PEAK_ITEM, sfx_box_open),
    "hand-in": ("Content/Sfx", PEAK_ITEM, sfx_hand_in),
    "payout": ("Content/Sfx", PEAK_ITEM, sfx_payout),
    "catch": ("Content/Sfx", PEAK_ITEM, sfx_catch),
    "drop": ("Content/Sfx", PEAK_ITEM, sfx_drop),
    "check-good": ("Content/Sfx", PEAK_ITEM, sfx_check_good),
    "check-bad": ("Content/Sfx", PEAK_ITEM - 3.0, sfx_check_bad),   # a held buzz reads louder than a ping
    "hit": ("Content/Sfx", PEAK_BIG, sfx_hit),
    "damage": ("Content/Sfx", PEAK_BIG, sfx_damage),
    "heal": ("Content/Sfx", PEAK_ITEM, sfx_heal),
    "guard": ("Content/Sfx", PEAK_ITEM, sfx_guard),
    "win": ("Content/Sfx", PEAK_BIG, sfx_win),
    "lose": ("Content/Sfx", PEAK_BIG, sfx_lose),
    "cinder": ("Content/Sfx/Items", PEAK_ITEM, sfx_cinder),
    "spent-shell": ("Content/Sfx/Items", PEAK_ITEM, sfx_spent_shell),
    "revolver": ("Content/Sfx/Items", PEAK_BIG, sfx_revolver),
    "pact": ("Content/Sfx/Items", PEAK_ITEM, sfx_pact),
    "wager": ("Content/Sfx/Items", PEAK_ITEM, sfx_wager),
    "high-card": ("Content/Sfx/Items", PEAK_ITEM, sfx_high_card),
    "tourniquet": ("Content/Sfx/Items", PEAK_ITEM, sfx_tourniquet),
    "ash-veil": ("Content/Sfx/Items", PEAK_ITEM, sfx_ash_veil),
    "mirror": ("Content/Sfx/Items", PEAK_ITEM, sfx_mirror),
    "lens": ("Content/Sfx/Items", PEAK_ITEM, sfx_lens),
    "tally": ("Content/Sfx/Items", PEAK_ITEM, sfx_tally),
    "marked-deck": ("Content/Sfx/Items", PEAK_ITEM, sfx_marked_deck),
    "confession": ("Content/Sfx/Items", PEAK_ITEM, sfx_confession),
    "levy": ("Content/Sfx/Items", PEAK_BIG, sfx_levy),
    "second-hand": ("Content/Sfx/Items", PEAK_ITEM, sfx_second_hand),
    "wild-card": ("Content/Sfx/Items", PEAK_ITEM, sfx_wild_card),
    "rotgut": ("Content/Sfx/Items", PEAK_ITEM, sfx_rotgut),
    "last-call": ("Content/Sfx/Items", PEAK_ITEM, sfx_last_call),
}


def build_sfx(name):
    folder, peak_db, builder = SFX[name]
    # crc32 instead of hash(), because str hashes change every run and these seeds can't.
    rng = random.Random(zlib.crc32(name.encode()))
    finish_sfx(folder + "/" + name + ".wav", builder(rng), peak_db)


if __name__ == "__main__":
    args = [a for a in sys.argv[1:] if a != "-v"]
    VERBOSE = len(args) != len(sys.argv) - 1
    wanted = []
    for a in args or ["music", "sfx"]:
        a = os.path.splitext(os.path.basename(a))[0]
        if a == "music":
            wanted += list(SONGS)
        elif a == "sfx":
            wanted += list(SFX)
        elif a in SONGS or a in SFX:
            wanted.append(a)
        else:
            sys.exit("No song or sound called '%s'." % a)
    started = time.perf_counter()
    print("Generating audio for The Black Box...")
    for name in dict.fromkeys(wanted):
        if name in SONGS:
            SONGS[name]()
        else:
            build_sfx(name)
    print("Done in %.1f s." % (time.perf_counter() - started))
