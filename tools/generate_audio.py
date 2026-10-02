"""
Procedural audio generator for The Black Box.

Every WAV in Content/Music and Content/Sfx comes out of this script. Run it from the repo root:

    python tools/generate_audio.py                     # everything
    python tools/generate_audio.py music               # the three songs
    python tools/generate_audio.py sfx                 # every sound effect
    python tools/generate_audio.py holding revolver    # just these, by file name
    python tools/generate_audio.py -v holding          # also print each layer's level

Like generate_assets.py it only needs CPython. Nothing is sampled: every sound is built from
sines, wavetables, noise and a few filters, and every random source is seeded, so a re-run
writes byte-identical files.

Music is 16-bit stereo at 32 kHz and loops seamlessly under MediaPlayer.IsRepeating. Sound
effects are 16-bit mono at 44.1 kHz and already balanced against each other, so the game can
play them all at the same volume.
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

# Loudness. Music is normalised to an RMS target and kept under the peak ceiling; sound
# effects are normalised by peak, grouped by how big the moment is and how often it fires.
MUSIC_PEAK_DB = -1.0
TITLE_RMS_DB = -21.0
LOBBY_RMS_DB = -20.0
ARENA_RMS_DB = -16.0
LIMIT_KNEE = 0.5        # the music limiter only bends samples above this (linear)

PEAK_BIG = -1.0         # slams, shots, hits, the two stingers
PEAK_ITEM = -4.0        # items and most table sounds
PEAK_MENU = -6.0        # confirm and back
PEAK_CLICK = -8.0       # UI clicks
PEAK_REPEAT = -10.0     # footsteps and text blips, which fire constantly

TITLE_BPM, TITLE_BARS = 66, 16
LOBBY_BPM, LOBBY_BARS = 84, 24
ARENA_BPM, ARENA_BARS = 124, 32

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
    """How the loop point compares with the rest. A click is a step much bigger than the steps
    around it, so the jump from the last sample back to the first is measured against the
    mean step in the 10 ms either side of it (and against the whole song's for reference).
    Then the level of the 50 ms either side."""
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
# Everything is a plain list of floats: in CPython a list fed through map() or a comprehension
# beats array('d') and explicit loops. Only finished song layers get parked in array('d'),
# because a list costs four times the memory.

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
    """Adds src into dst starting at sample `at`; whatever falls off either end is dropped."""
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
    """Like mix(), but wraps around the end of dst. Tails that run past the loop point land on
    the start, which is most of what makes the songs loop without a seam."""
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
    """A sine from zero phase, dying away with time constant tau (s) if tau > 0.

    I rotate a complex phasor with accumulate() instead of calling sin() per sample. Same
    numbers, roughly twice as fast."""
    if n <= 0:
        return []
    r = math.exp(-1.0 / (tau * rate)) if tau > 0 else 1.0
    step = cmath.rect(r, TAU * freq / rate)
    return [z.imag for z in accumulate(repeat(step, n - 1), mul, initial=complex(amp, 0.0))]


def glide(f_start, f_end, tau, n, rate, amp=1.0):
    """A sine whose pitch slides from f_start toward f_end with time constant tau. Kicks,
    thumps and swells. The phase is the closed-form integral, so no per-sample state."""
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
    """Raised-cosine attack, exponential decay (tau, 0 = hold), and if gate is given an
    exponential release after it. It always ends on a short fade to exactly zero."""
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
    """Raised-cosine fade in and out, in place, so the first and last samples are true zero."""
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
    """Sum of decaying sines, partials given as (ratio, gain, tau). It's how I ring every
    struck thing here: music box tines, bells, glass, the metal tags, steel doors."""
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
    """Wavetable oscillator. The phase is a fixed-point integer counted by range(), so the
    whole thing runs as a chain of C-level map()s with no Python per sample."""
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
    """Sample times bent by a vibrato that fades in after delay_s, like a singer's. Reading an
    oscillator at these times instead of 0, 1, 2... gives it the vibrato, and the three
    voices of a lead can share one list."""
    w = TAU * vib_hz / rate
    depth = (2.0 ** (cents / 1200.0) - 1.0) / w
    d0 = int(delay_s * rate)
    ramp = max(1, int(0.25 * rate))
    cos = math.cos
    out = list(range(n))
    for i in range(d0, n):
        out[i] = i + depth * min(1.0, (i - d0) / ramp) * (1.0 - cos(w * i))
    return out


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
    index that I read every `block` samples (sweeps, breathing pads, filter envelopes)."""
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
    """Runs a short-memory filter over a loop. I feed it the last second first, so its state
    at sample 0 is what it would be coming round from the end (a full second pass, cheaper)."""
    pre = min(len(x), preroll(rate, pre_s))
    return fn(x[-pre:] + x)[pre:]


REVERB_LINES_MS = (43.7, 53.9, 63.1, 75.3)
REVERB_DIFFUSERS_MS = (5.3, 7.7, 12.3)
REVERB_DIFFUSION = 0.62


def reverb(send, rate, rt60, damp_hz, size=1.0, loop=False):
    """Feedback-delay-network reverb: three allpass diffusers into four damped delay lines
    mixed through a Hadamard matrix. Mono in, (left, right) wet out.

    With loop=True I run the send through twice and keep the second pass, so the tail from
    the end of the song is already ringing at sample 0 and the loop point is invisible."""
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
    """Memoryless soft limiter: straight below the knee, tanh-bent above it, never past the
    ceiling. No state, so it can't disturb the loop point."""
    span = ceiling - knee
    th = math.tanh
    return [v if -knee <= v <= knee else
            (knee + span * th((v - knee) / span) if v > 0 else -knee - span * th((-v - knee) / span))
            for v in map(mul, x, repeat(gain))]


# --------------------------------------------------------------------------- #
# Shared instruments
# --------------------------------------------------------------------------- #

# (ratio, gain, tau). The 5.43 and 8.2 partials are the inharmonic tine modes that make it
# a music box rather than a sine organ.
MUSIC_BOX = ((1.0, 1.0, 1.3), (2.0, 0.2, 0.5), (3.01, 0.05, 0.28), (5.43, 0.16, 0.11),
             (8.21, 0.07, 0.045), (11.9, 0.03, 0.022))
MUSIC_BOX_LENGTH = 3.6


def music_box_note(freq, rate, rng):
    n = int(MUSIC_BOX_LENGTH * rate)
    out = modes(MUSIC_BOX, n, rate, freq)
    # The pluck: a couple of milliseconds of bright noise where the pin lets go of the tine.
    m = int(0.003 * rate)
    click = shaped(highpass(noise(m, rng), 2500.0, rate), envelope(m, rate, 0.0003, 0.0007))
    mix(out, click, 0, 0.06)
    return fade(out, rate, 0.0003, 0.4)


def heartbeat(rate, rng):
    """Lub-dub: two soft low thumps 0.27 s apart, the second a little higher and quieter."""
    n = int(0.75 * rate)
    out = silence(n)
    for t, f0, f1, g in ((0.0, 66.0, 41.0, 1.0), (0.27, 76.0, 47.0, 0.7)):
        m = int(0.42 * rate)
        e = envelope(m, rate, attack=0.012, tau=0.075)
        thump = shaped(glide(f0, f1, 0.05, m, rate), e)
        flesh = shaped(norm(lowpass(lowpass(noise(m, rng), 220.0, rate), 220.0, rate)), e)
        mix(thump, flesh, 0, 0.25)
        mix(out, thump, int(t * rate), g)
    return out


def creak(dur, rate, rng, rates, resonances, q=28.0):
    """Stick-slip friction: an impulse train whose rate wanders along `rates` (a list of
    (position 0..1, pulses per second)), rung through a few narrow resonances."""
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
        """Sums the layers into one bus (1 left, 2 right, 3 reverb send). Adding them one at a
        time was most of the run time; zip + sumprod does every layer in a single pass."""
        picked = [(layer[0], layer[which]) for layer in self.layers if layer[which]]
        if not picked:
            return self.track()
        tracks = [t for t, _ in picked]
        gains = [g for _, g in picked]
        sp = math.sumprod
        return [sp(c, gains) for c in zip(*tracks)]

    def add_reverb(self, rt60, damp_hz, size, gain_db):
        # The tail is dark anyway, so I run it at half rate: half the work, nothing audible lost.
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
# it-is-watching.wav -- title theme
# --------------------------------------------------------------------------- #
# D minor at 66 BPM, 16 bars. A slightly broken music box over a low drone that breathes,
# a heartbeat once a bar, tape hiss and a creak somewhere far off every 8 bars.

TITLE_DRONE = (        # (first bar, bars, root MIDI): D, Bb, C, D, Bb, the Phrygian Eb, D
    (15, 4, 38),       # bars 15-16 and 1-2 are one D that runs straight across the loop point
    (3, 2, 34),
    (5, 2, 36),
    (7, 4, 38),
    (11, 2, 34),
    (13, 2, 39),
)

TITLE_MELODY = (       # (bar, beat, MIDI, velocity)
    (1, 0.0, 86, 0.90), (1, 1.0, 81, 0.70), (1, 2.0, 77, 0.75), (1, 3.0, 81, 0.65),
    (2, 0.0, 79, 0.80), (2, 1.5, 77, 0.60), (2, 2.0, 76, 0.70),
    (3, 0.0, 86, 0.85), (3, 1.0, 82, 0.70), (3, 2.0, 77, 0.70), (3, 3.0, 82, 0.60),
    (4, 0.0, 81, 0.80), (4, 1.5, 79, 0.60), (4, 2.0, 77, 0.70),
    (5, 0.0, 84, 0.85), (5, 1.0, 79, 0.70), (5, 2.0, 76, 0.70), (5, 3.0, 79, 0.60),
    (6, 0.0, 77, 0.75), (6, 1.5, 76, 0.60), (6, 2.0, 74, 0.70),
    (7, 0.0, 74, 0.70), (7, 2.0, 69, 0.50), (7, 3.0, 70, 0.45),
    (8, 0.0, 69, 0.55),
    (9, 0.0, 86, 0.90), (9, 1.0, 81, 0.70), (9, 2.0, 77, 0.75), (9, 3.0, 81, 0.65),
    (10, 0.0, 79, 0.80), (10, 1.5, 77, 0.60), (10, 2.0, 75, 0.80),   # Eb over D: the first wrong note
    (11, 0.0, 86, 0.85), (11, 1.0, 82, 0.70), (11, 2.0, 77, 0.70), (11, 3.0, 86, 0.60),
    (12, 0.0, 84, 0.75), (12, 1.5, 82, 0.60), (12, 2.0, 81, 0.70),
    (13, 0.0, 87, 0.90), (13, 1.0, 82, 0.70), (13, 2.0, 79, 0.70), (13, 3.0, 82, 0.60),
    (14, 0.0, 86, 0.75), (14, 1.5, 82, 0.60), (14, 2.0, 75, 0.85),
    (15, 0.0, 74, 0.75), (15, 2.0, 77, 0.45), (15, 3.0, 76, 0.40),
    (16, 0.0, 74, 0.50), (16, 3.0, 69, 0.30),
)

TITLE_TINES = ((1, 62), (3, 58), (5, 60), (7, 62), (9, 62), (11, 58), (13, 63), (15, 62))
TITLE_DETUNE = -10.0      # cents; the whole box has sagged flat
TITLE_CROSSFADE = 2.0     # seconds between drone chords


def build_title():
    rng = random.Random(6601)
    s = Song(TITLE_BPM, TITLE_BARS)
    R, L = s.rate, s.length

    # Drone: root and fifth as detuned saw pairs, one of each pair per side, plus a sine
    # under the root so it has weight on small speakers.
    dl, dr, body = s.track(), s.track(), s.track()
    xf = int(TITLE_CROSSFADE * R)
    for first, bars, root in TITLE_DRONE:
        n = bars * s.bar + xf
        start = s.at(first) - xf // 2
        win = [1.0] * n
        for i in range(xf):
            win[i] = math.sin(0.5 * math.pi * i / (xf - 1))
            win[n - 1 - i] = math.sin(0.5 * math.pi * i / (xf - 1))
        lo, hi = midi_hz(root), midi_hz(root + 7)
        left = list(map(add, osc("saw", lo * 2 ** (-6 / 1200), n, R, 1.0, rng.random()),
                        osc("saw", hi * 2 ** (8 / 1200), n, R, 0.55, rng.random())))
        right = list(map(add, osc("saw", lo * 2 ** (6 / 1200), n, R, 1.0, rng.random()),
                         osc("saw", hi * 2 ** (-9 / 1200), n, R, 0.55, rng.random())))
        mix_loop(dl, shaped(left, win), start)
        mix_loop(dr, shaped(right, win), start)
        mix_loop(body, shaped(sine(lo, n, R), win), start)

    # The low-pass breathes once every two bars, with a slower drift on top. Both are whole
    # cycles per loop so the filter is in the same place at the end as at the start.
    def breath(i):
        u = (i % L) / L
        return 170.0 + 460.0 * (0.5 - 0.5 * math.cos(TAU * 8 * u)) + 80.0 * math.sin(TAU * u)
    pre = preroll(R)
    dl = looped(lambda x: svf(x, lambda i: breath(i - pre), 1.2, R), dl, R)
    dr = looped(lambda x: svf(x, lambda i: breath(i - pre), 1.2, R), dr, R)
    s.add_stereo(dl, dr, -14.0, send=0.12, label="drone")
    s.add(body, -21.0, label="drone sine")

    # Music box. Every tine has its own mistuning (seeded), and the whole thing is run
    # through a wobbling delay afterwards for the wow and flutter of an old mechanism.
    tine_cents = {}
    box = note_cache(lambda m: music_box_note(midi_hz(m, TITLE_DETUNE + tine_cents.setdefault(
        m, rng.uniform(-7.0, 7.0))), R, rng))
    mb = s.track()
    for bar, beat, m, vel in TITLE_MELODY:
        jitter = int(rng.gauss(0.0, 0.006) * R)
        mix_loop(mb, box(m), s.at(bar, beat) + jitter, vel * rng.uniform(0.94, 1.0))
    for bar, m in TITLE_TINES:
        mix_loop(mb, box(m), s.at(bar), 0.42)
    mb = wow(mb, R, wow_cycles=32, wow_depth=0.0028, flutter_cycles=361, flutter_depth=0.0005)
    s.add(mb, -12.0, pan=-0.05, send=0.55, label="music box")

    # Heartbeat on beat two of every bar, a little harder through the Eb bars.
    hb = s.track()
    beat_once = heartbeat(R, rng)
    for bar in range(1, TITLE_BARS + 1):
        mix_loop(hb, beat_once, s.at(bar, 1.0), 1.0 if bar in (13, 14) else 0.75)
    s.add(hb, -8.0, send=0.08, label="heartbeat")

    # Tape hiss on both sides and a low room rumble in the middle.
    s.add_stereo(hiss(L, rng), hiss(L, rng), -54.0, label="hiss")
    rumble = looped(lambda x: lowpass(lowpass(x, 90.0, R), 90.0, R), noise(L, rng), R)
    s.add(norm(rumble), -46.0, label="room")

    # A creak somewhere in the building every 8 bars, distant and drenched.
    for bar, pan in ((5, -0.6), (13, 0.55)):
        c = creak(1.7, R, rng, ((0.0, 16.0), (0.4, 34.0), (0.75, 22.0), (1.0, 12.0)),
                  ((231.0, 1.0), (517.0, 0.7), (873.0, 0.45), (1420.0, 0.25)))
        c = shaped(lowpass(c, 1600.0, R), lines(((0, 0), (0.3, 1), (1.2, 0.8), (1.7, 0)), len(c), R))
        part = s.track()
        mix_loop(part, norm(c), s.at(bar, 1.5))
        s.add(part, -18.0, pan=pan, send=0.9, label="creak")
    s.add_reverb(rt60=4.8, damp_hz=2200.0, size=1.35, gain_db=-9.5)
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
# holding.wav -- lobby theme
# --------------------------------------------------------------------------- #
# D minor at 84 BPM, 24 bars in three 8-bar passes of Dm9 - Bbmaj9 - Gm9 - A7sus(b9) - A7(b9).
# Bars 1-8: Rhodes, bass and brushes. 9-16: the vibraphone joins. 17-24: just Rhodes and bass,
# with the hats creeping back in bar 23, then a brush fill and a vibes pickup to lead into the top.

LOBBY_CHORDS = (       # one per bar of the 8-bar cycle: (bass root, Rhodes voicing)
    (38, (53, 57, 60, 64)),   # Dm9
    (38, (53, 57, 60, 64)),
    (34, (57, 60, 62, 65)),   # Bbmaj9
    (34, (57, 60, 62, 65)),
    (31, (53, 57, 58, 62)),   # Gm9
    (31, (53, 57, 58, 62)),
    (33, (55, 58, 62, 64)),   # A7sus b9
    (33, (55, 58, 61, 64)),   # A7 b9, the C# pulls back home
)

SWING = 2.0 / 3.0       # where the off-beat eighth lands, in beats

LOBBY_COMP = ((0.0, 2.4, 0.78), (2.0 + SWING, 0.5, 0.5), (4.0 + SWING, 1.6, 0.62), (6.0 + SWING, 1.1, 0.5))
LOBBY_COMP_THIN = ((0.0, 3.8, 0.7), (4.0 + SWING, 4.0 - SWING, 0.5))   # held right up to the next chord

LOBBY_BASS = (         # (beat in the 2-bar chord, interval, beats, velocity); None = approach note
    (0.0, 0, 1.4, 0.95), (1.0 + SWING, 12, 0.3, 0.45), (2.0, 7, 0.9, 0.75), (3.0 + SWING, 0, 0.3, 0.5),
    (4.0, 0, 1.4, 0.85), (5.0 + SWING, 10, 0.3, 0.45), (6.0, 7, 0.9, 0.7), (7.0, None, 1.0, 0.75),
)

LOBBY_VIBES = (        # (bar, beat, MIDI, beats)
    (9, SWING, 76, 0.33), (9, 1.0, 77, 1.0), (9, 2.0, 81, 2.0),
    (10, 0.0, 79, 1.0), (10, 1.0, 77, SWING), (10, 1.0 + SWING, 76, 2.33),
    (11, SWING, 74, 0.33), (11, 1.0, 77, 1.0), (11, 2.0, 81, 1.0), (11, 3.0, 84, 1.0),
    (12, 0.0, 82, 1.0 + SWING), (12, 1.0 + SWING, 81, 2.33),
    (13, SWING, 79, 0.33), (13, 1.0, 82, 1.0), (13, 2.0, 86, 2.0),
    (14, 0.0, 84, 1.0), (14, 1.0, 82, SWING), (14, 1.0 + SWING, 81, 2.33),
    (15, 0.0, 79, 1.0), (15, 1.0, 77, 1.0), (15, 2.0, 75, 1.0), (15, 3.0, 74, 1.0),
    (16, 0.0, 73, 2.0), (16, 2.0 + SWING, 76, 1.33),
    (24, 3.0, 76, SWING), (24, 3.0 + SWING, 73, 0.33),   # the pickup that leads back to the top
    (1, 0.0, 74, 2.0),                                   # ...and lands on D as the loop comes round
)

LOBBY_HATS = ((0.0, 0.5), (SWING, 0.3), (1.0, 0.6), (1.0 + SWING, 0.3),
              (2.0, 0.5), (2.0 + SWING, 0.3), (3.0, 0.6), (3.0 + SWING, 0.35))
LOBBY_FLICKERS = ((6, 2.3), (14, 0.7), (21, 3.1))


def ep_note(freq, dur, vel, rate):
    """FM electric piano: carrier and modulator both at the note's pitch, the index falling
    away after the strike (bright bark, then the round tone), plus a quick tine ping."""
    n = int((dur + 0.4) * rate)
    w = TAU * freq / rate
    lo, hi = 0.3 + 0.2 * vel, 1.4 * vel
    sin = math.sin
    body = [sin(w * i + (lo + hi * d) * sin(w * i)) for i, d in enumerate(decay(n, 0.22, rate))]
    ratio = 14.0 if freq * 14.0 < 0.4 * rate else 7.0
    mix(body, sine(freq * ratio, int(0.1 * rate), rate, 0.07 * vel, 0.012))
    tau = 1.6 * (220.0 / freq) ** 0.35
    return shaped(body, envelope(n, rate, attack=0.003, tau=tau, gate=dur, release=0.09))


def round_bass(freq, dur, vel, rate, rng):
    """Muted electric bass: sine plus a little octave, warmed by a tanh and rolled off."""
    n = int((dur + 0.12) * rate)
    x = list(map(add, sine(freq, n, rate), sine(2.0 * freq, n, rate, 0.22)))
    x = saturate(x, 1.0 + 0.8 * vel)
    x = shaped(x, envelope(n, rate, attack=0.006, tau=0.55, gate=dur, release=0.05))
    m = int(0.015 * rate)
    thump = shaped(lowpass(noise(m, rng), 500.0, rate), envelope(m, rate, 0.001, 0.005))
    mix(x, norm(thump), 0, 0.12 * vel)
    return lowpass(x, 1100.0, rate)


VIBES = ((1.0, 1.0, 2.2), (4.0, 0.22, 0.45), (9.92, 0.05, 0.12))


def vibes_note(freq, dur, rate, rng):
    n = int((dur + 1.2) * rate)
    x = modes(VIBES, n, rate, freq)
    m = int(0.004 * rate)
    mallet = shaped(lowpass(noise(m, rng), 2500.0, rate), envelope(m, rate, 0.0005, 0.0012))
    mix(x, mallet, 0, 0.05)
    # Pedal comes up half a second after the written length.
    return shaped(x, envelope(n, rate, attack=0.001, gate=dur + 0.5, release=0.18))


def brush_snare(rate, rng):
    n = int(0.45 * rate)
    x = biquad(noise(n, rng), "bp", 2600.0, 0.55, rate)
    x = lowpass(x, 7000.0, rate)
    x = shaped(norm(x), envelope(n, rate, attack=0.012, tau=0.09))
    mix(x, sine(205.0, int(0.2 * rate), rate, 0.12, 0.03))
    return x


def brush_swish(rate, rng, beats_s):
    """The circular brush stroke between hits: a quiet band of noise that swells and fades."""
    n = int(beats_s * rate)
    x = biquad(noise(n, rng), "bp", 3200.0, 0.5, rate)
    e = [math.sin(math.pi * i / n) ** 2 for i in range(n)]
    return shaped(norm(x), e)


def soft_hat(rate, rng):
    n = int(0.08 * rate)
    x = biquad(biquad(noise(n, rng), "hp", 7000.0, 0.7, rate), "hp", 7000.0, 0.7, rate)
    return shaped(norm(x), envelope(n, rate, attack=0.0005, tau=0.018))


def soft_kick(rate):
    n = int(0.35 * rate)
    x = shaped(glide(82.0, 50.0, 0.04, n, rate), envelope(n, rate, attack=0.004, tau=0.12))
    return lowpass(x, 300.0, rate)


def build_holding():
    rng = random.Random(8401)
    s = Song(LOBBY_BPM, LOBBY_BARS)
    R, L = s.rate, s.length

    def chord(bar):
        return LOBBY_CHORDS[(bar - 1) % 8]

    # Rhodes: chords comped on the swung eighths, rolled a few ms from the bottom up.
    ep = note_cache(lambda m, beats, v: ep_note(midi_hz(m), s.secs(beats), v, R))
    keys = s.track()
    for first in range(1, LOBBY_BARS + 1, 2):
        pattern = LOBBY_COMP_THIN if first >= 17 else LOBBY_COMP
        for beat, beats, vel in pattern:
            bar = first + int(beat // 4)
            start = s.at(first, beat) + int(rng.gauss(0.0, 0.004) * R)
            for k, m in enumerate(chord(bar)[1]):
                v = round(vel * rng.uniform(0.88, 1.0), 1)
                mix_loop(keys, ep(m, beats, v), start + int(k * 0.007 * R), v)
    # Suitcase tremolo: the two sides swap level a little over 4 times a second.
    w = TAU * 288 / L
    sin = math.sin
    trem_l = [1.0 - 0.28 * (0.5 + 0.5 * sin(w * i)) for i in range(L)]
    trem_r = [1.0 - 0.28 * (0.5 - 0.5 * sin(w * i)) for i in range(L)]
    s.add_stereo(shaped(keys, trem_l), shaped(keys, trem_r), -17.0, send=0.25, label="rhodes")

    # Bass.
    bass_note = note_cache(lambda m, beats, v: round_bass(midi_hz(m), s.secs(beats), v, R, rng))
    bass = s.track()
    for first in range(1, LOBBY_BARS + 1, 2):
        root = chord(first)[0]
        nxt = chord(first + 2)[0]
        for beat, interval, beats, vel in LOBBY_BASS:
            if first >= 17 and vel < 0.5:
                continue        # no ghost notes in the thin section
            m = (nxt - 1 if nxt > root else nxt + 1) if interval is None else root + interval
            if interval is None and nxt == root:
                m = root + 7
            at = s.at(first, beat) + int(rng.gauss(0.0, 0.003) * R)
            mix_loop(bass, bass_note(m, beats, vel), at, vel)
    s.add(bass, -12.0, send=0.04, label="bass")

    # Brushes. Out for bars 17-22, hats creep back in 23, a fill in 24.
    snare_hits = [brush_snare(R, rng) for _ in range(4)]
    hat_hits = [soft_hat(R, rng) for _ in range(4)]
    kick = soft_kick(R)
    swish = brush_swish(R, rng, s.secs(0.9))
    sn, hh, kk, sw = s.track(), s.track(), s.track(), s.track()
    for bar in range(1, LOBBY_BARS + 1):
        full = bar <= 16
        if full:
            for beat in (1.0, 3.0):
                mix_loop(sn, rng.choice(snare_hits), s.at(bar, beat) + int(rng.gauss(0, 0.003) * R),
                         rng.uniform(0.65, 0.8))
            for beat in range(4):
                mix_loop(sw, swish, s.at(bar, beat), 0.8 if beat % 2 else 0.5)
            mix_loop(kk, kick, s.at(bar), 0.8)
            mix_loop(kk, kick, s.at(bar, 2.0 + SWING), 0.45)
        if full or bar >= 23:
            fade_in = 0.5 if bar == 23 else 1.0
            for beat, vel in LOBBY_HATS:
                mix_loop(hh, rng.choice(hat_hits), s.at(bar, beat) + int(rng.gauss(0, 0.002) * R),
                         vel * fade_in * rng.uniform(0.85, 1.0))
        if bar == 24:
            # The fill: brush triplets getting louder, the swirl coming back under them.
            for k, beat in enumerate((2.0, 2.33, 2.67, 3.0, 3.33, 3.67)):
                mix_loop(sn, rng.choice(snare_hits), s.at(bar, beat), 0.3 + 0.08 * k)
            for beat in (2.0, 3.0):
                mix_loop(sw, swish, s.at(bar, beat), 0.8)
            mix_loop(kk, kick, s.at(bar, 3.0 + SWING), 0.5)
    s.add(sn, -11.0, pan=-0.2, send=0.3, label="brush snare")
    s.add(sw, -25.0, pan=-0.1, send=0.2, label="brush swish")
    s.add(hh, -16.0, pan=0.3, send=0.12, label="hats")
    s.add(kk, -12.0, label="kick")

    # Vibraphone lead, bars 9-16, with the motor's slow tremolo.
    vib = s.track()
    for bar, beat, m, beats in LOBBY_VIBES:
        note = vibes_note(midi_hz(m), s.secs(beats), R, rng)
        mix_loop(vib, note, s.at(bar, beat), rng.uniform(0.75, 0.9))
    w = TAU * 357 / L
    vib = [v * (1.0 - 0.3 * (0.5 + 0.5 * sin(w * i))) for i, v in enumerate(vib)]
    s.add(vib, -16.0, pan=0.25, send=0.4, label="vibes")

    # Fluorescent tube: 60 Hz family, a buzzy 120, and three flickers. The frequency is nudged
    # so a whole number of cycles fits in the loop.
    k = round(60.0 * L / R)
    f = k * R / L
    hum = silence(L)
    for h, g in ((1, 0.35), (2, 1.0), (3, 0.45), (4, 0.2), (6, 0.08)):
        mix(hum, sine(f * h, L, R, g))
    buzz = looped(lambda x: highpass(x, 600.0, R), saturate(sine(2 * f, L, R), 4.0), R, 0.1)
    mix(hum, buzz, 0, 0.12)
    flick = [1.0] * L
    zaps = s.track()
    for bar, beat in LOBBY_FLICKERS:
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
    s.add(shaped(hum, flick), -46.0, pan=0.1, label="hum")
    s.add(zaps, -30.0, pan=0.1, label="flicker")

    s.add_reverb(rt60=1.9, damp_hz=3500.0, size=1.0, gain_db=-6.0)
    s.finish("Content/Music/holding.wav", LOBBY_RMS_DB)


# --------------------------------------------------------------------------- #
# place-your-hand.wav -- table theme
# --------------------------------------------------------------------------- #
# D minor / Phrygian at 124 BPM, 32 bars over a D - D - Bb - A cycle.
#  1-8   full groove: kick, clap + steel clank, hats, the box's clock, rolling bass, ostinato
#  9-16  the same with the lead hook on top
#  17-24 breakdown: no kick, sustained pads, ostinato filter opening, the clock still counting
#  25-30 rebuild with a noise riser
#  31-32 snare roll into the loop point, so bar 1 lands as the full groove again

ARENA_ROOTS = (38, 38, 34, 33)          # D2, D2, Bb1, A1
ARENA_BASS_STEPS = (1, 2, 3, 5, 6, 7, 9, 10, 11, 13, 14, 15)   # every 16th the kick doesn't take
ARENA_BASS_LINES = (                   # intervals on those steps, one row per bar of the cycle
    (0, 0, 12, 0, 0, 1, 0, 0, 12, 0, 3, 1),
    (0, 0, 12, 0, 0, 1, 0, 0, 12, 0, 7, 6),
    (0, 0, 12, 0, 0, -1, 0, 0, 12, 0, -2, -1),
    (0, 0, 12, 0, 0, 1, 0, 0, 12, 0, 6, 4),
)
ARENA_OSTINATO = (62, 65, 68, 67) * 4   # D F Ab G
ARENA_PADS = (
    (50, 57, 62, 65),     # Dm
    (50, 57, 62, 65),
    (46, 53, 58, 62),     # Bb
    (45, 52, 58, 61),     # A with the b9 on top
)
ARENA_LEAD = (            # (bar, beat, MIDI, beats)
    (9, 0.0, 74, 1.5), (9, 1.5, 77, 0.5), (9, 2.0, 75, 1.0), (9, 3.0, 74, 0.5), (9, 3.5, 72, 0.5),
    (10, 0.0, 74, 2.5), (10, 3.0, 69, 1.0),
    (11, 0.0, 77, 1.5), (11, 1.5, 79, 0.5), (11, 2.0, 80, 1.0), (11, 3.0, 79, 1.0),
    (12, 0.0, 76, 1.0), (12, 1.0, 77, 0.5), (12, 1.5, 76, 0.5), (12, 2.0, 73, 2.0),
    (13, 0.0, 81, 1.5), (13, 1.5, 79, 0.5), (13, 2.0, 77, 1.0), (13, 3.0, 75, 1.0),
    (14, 0.0, 74, 1.5), (14, 1.5, 77, 0.5), (14, 2.0, 75, 2.0),
    (15, 0.0, 74, 1.0), (15, 1.0, 77, 1.0), (15, 2.0, 82, 1.5), (15, 3.5, 81, 0.5),
    (16, 0.0, 80, 1.0), (16, 1.0, 79, 0.5), (16, 1.5, 77, 0.5), (16, 2.0, 76, 2.0),
)
STEEL = ((1.0, 1.0, 0.5), (1.47, 0.75, 0.4), (2.09, 0.55, 0.3), (2.83, 0.4, 0.22),
         (3.62, 0.3, 0.15), (4.71, 0.2, 0.1))
HAT_FREQS = (205.3, 304.4, 369.6, 522.7, 540.0, 800.0)   # the 808's six square waves


def kick(rate, rng):
    n = int(0.42 * rate)
    x = shaped(glide(170.0, 47.0, 0.032, n, rate), envelope(n, rate, attack=0.0008, tau=0.16))
    m = int(0.004 * rate)
    click = shaped(highpass(noise(m, rng), 1500.0, rate), envelope(m, rate, 0.0002, 0.0012))
    mix(x, norm(click), 0, 0.25)
    return saturate(x, 1.6)


def clap(rate, rng, clank_hz, clank=0.6):
    """Industrial snare: three quick clap bursts and a tail, a snare body, and a struck piece
    of steel on top."""
    n = int(0.5 * rate)
    band = biquad(noise(n, rng), "bp", 1300.0, 0.9, rate)
    e = silence(n)
    for t in (0.0, 0.008, 0.017):
        mix(e, envelope(int(0.03 * rate), rate, 0.0003, 0.004), int(t * rate))
    mix(e, envelope(n - int(0.024 * rate), rate, 0.001, 0.09), int(0.024 * rate), 0.7)
    x = shaped(norm(band), e)
    snap = shaped(biquad(noise(n, rng), "hp", 1800.0, 0.7, rate), envelope(n, rate, 0.0005, 0.11))
    mix(x, norm(snap), 0, 0.45)
    mix(x, shaped(glide(240.0, 185.0, 0.02, n, rate), envelope(n, rate, 0.0005, 0.05)), 0, 0.35)
    if clank:
        steel = modes([(r, g, t * 0.45) for r, g, t in STEEL], n, rate, clank_hz)
        mix(x, saturate(norm(steel), 1.5), 0, clank)
    return norm(x)


def metal_hat(rate, rng, open_=False):
    n = int((0.42 if open_ else 0.07) * rate)
    metal = silence(n)
    for f in HAT_FREQS:
        mix(metal, osc("square", f * 1.7, n, rate, 1.0, rng.random()))
    x = list(map(add, scale(norm(metal), 0.7), noise(n, rng)))
    x = biquad(biquad(x, "hp", 7000.0, 0.7, rate), "hp", 7000.0, 0.7, rate)
    return norm(shaped(x, envelope(n, rate, attack=0.0004, tau=0.11 if open_ else 0.016)))


def clock_tick(rate, rng, high):
    """The box counting: a dry little escapement click, tick high and tock low."""
    n = int(0.05 * rate)
    f0 = 3200.0 if high else 2350.0
    x = modes(((1.0, 1.0, 0.006), (1.73, 0.5, 0.004), (2.9, 0.3, 0.003)), n, rate, f0)
    m = int(0.002 * rate)
    mix(x, shaped(highpass(noise(m, rng), 3000.0, rate), envelope(m, rate, 0.0002, 0.0005)), 0, 0.3)
    mix(x, sine(f0 / 4.0, n, rate, 0.25, 0.008))
    return norm(x)


def crash(rate, rng):
    n = int(2.6 * rate)
    metal = silence(n)
    for f in HAT_FREQS:
        mix(metal, osc("square", f * 2.3, n, rate, 1.0, rng.random()))
    x = list(map(add, scale(norm(metal), 0.5), noise(n, rng)))
    x = biquad(x, "hp", 4500.0, 0.6, rate)
    return norm(shaped(x, envelope(n, rate, attack=0.001, tau=0.75)))


def rolling_bass(freq, beats_s, accent, rate):
    """Two detuned saws and a sine sub through a resonant low-pass with a fast pluck on the
    cutoff. Accented notes open further."""
    n = int((beats_s + 0.02) * rate)
    x = list(map(add, osc("saw", freq * 2 ** (-7 / 1200), n, rate, 0.5),
                 osc("saw", freq * 2 ** (7 / 1200), n, rate, 0.5, 0.37)))
    top = 1300.0 if accent else 800.0
    x = svf(x, lambda i: 170.0 + top * math.exp(-i / (0.05 * rate)), 1.6, rate, block=8)
    mix(x, sine(freq, n, rate, 0.6))
    x = saturate(x, 1.8)
    return shaped(x, envelope(n, rate, attack=0.002, gate=beats_s * 0.9, release=0.012))


def pluck(freq, cutoff, rate):
    """The ostinato voice: square and saw, plucked through a low-pass that starts bright."""
    n = int(0.3 * rate)
    x = list(map(add, osc("square", freq, n, rate, 0.6), osc("saw", freq * 1.004, n, rate, 0.4)))
    x = svf(x, lambda i: cutoff * (1.0 + 2.5 * math.exp(-i / (0.025 * rate))), 1.3, rate)
    return shaped(x, envelope(n, rate, attack=0.002, tau=0.11))


def pad_chord(notes, length_s, attack, release, cutoff, rate, rng):
    n = int((length_s + release * 3) * rate)
    x = silence(n)
    for m in notes:
        f = midi_hz(m)
        for c in (-10.0, 0.0, 9.0):
            mix(x, osc("saw", f * 2 ** (c / 1200), n, rate, 0.33, rng.random()))
    x = svf(x, cutoff, 0.8, rate)
    return shaped(x, envelope(n, rate, attack=attack, gate=length_s, release=release))


def lead_note(freq, beats_s, rate, rng):
    n = int((beats_s + 0.35) * rate)
    x = silence(n)
    times = vibrato_time(n, rate, 28.0, 5.6, 0.18)
    for c in (-8.0, 0.0, 8.0):
        mix(x, osc_at("saw", freq * 2 ** (c / 1200), times, rate, 0.33, rng.random()))
    x = svf(x, 2600.0, 0.9, rate)
    return shaped(x, envelope(n, rate, attack=0.008, gate=beats_s * 0.95, release=0.1))


def build_arena():
    rng = random.Random(12401)
    s = Song(ARENA_BPM, ARENA_BARS)
    R, L = s.rate, s.length
    step = s.beat // 4

    def at_step(bar, k):
        return s.at(bar) + k * step

    def groove(bar):
        return bar <= 16 or 25 <= bar <= 31

    # Kick on every beat of the groove, only the first half of bar 32 so the roll hangs.
    kick_at = []
    for bar in range(1, ARENA_BARS + 1):
        beats = range(4) if groove(bar) else (0, 1) if bar == 32 else ()
        kick_at += [s.at(bar, b) for b in beats]
    k = kick(R, rng)
    kk = s.track()
    for p in kick_at:
        mix_loop(kk, k, p)
    s.add(kk, -5.0, send=0.02, label="kick")

    # Side-chain: everything sustained ducks under each kick and swells back.
    duck_shape = shaped(decay(int(0.4 * R), 0.075, R), envelope(int(0.4 * R), R, attack=0.003))
    reduction = s.track()
    for p in kick_at:
        mix_loop(reduction, duck_shape, p)
    duck = [1.0 - 0.75 * v for v in reduction]       # kicks are a beat apart, so dips never stack
    soft_duck = [1.0 - 0.35 * (1.0 - d) for d in duck]

    # Bass.
    bass_note = note_cache(lambda m, acc: rolling_bass(midi_hz(m), s.secs(0.25), acc, R))
    bass = s.track()
    for bar in range(1, ARENA_BARS + 1):
        if not groove(bar) and bar != 32:
            continue
        cyc = (bar - 1) % 4
        for st, iv in zip(ARENA_BASS_STEPS, ARENA_BASS_LINES[cyc]):
            if bar == 32 and st >= 8:
                break
            accent = st % 4 == 3      # the last 16th before each kick leans forward
            note = bass_note(ARENA_ROOTS[cyc] + iv, accent)
            mix_loop(bass, note, at_step(bar, st), 1.0 if accent else 0.85)
    s.add(shaped(bass, duck), -9.0, send=0.02, label="bass")

    # Ostinato. The filter sits at 2.4 kHz, drops to nothing at the breakdown and opens back up
    # over those 8 bars. Notes are cached by pitch and a semitone-wide cutoff bucket.
    def ost_cutoff(bar, st):
        if 17 <= bar <= 24:
            u = ((bar - 17) * 16 + st) / 128.0
            return 380.0 * (3600.0 / 380.0) ** u
        return 2400.0 if bar <= 16 else 3600.0
    ost_note = note_cache(lambda m, bucket: pluck(midi_hz(m), 2 ** (bucket / 12.0), R))
    ost = s.track()
    for bar in range(1, ARENA_BARS + 1):
        for st, m in enumerate(ARENA_OSTINATO):
            if st == 8:
                m = 74
            elif st == 15:
                m = 70
            bucket = round(12 * math.log2(ost_cutoff(bar, st)))
            vel = 1.0 if st % 4 == 0 else 0.72
            if 9 <= bar <= 16:
                vel *= 0.75        # sits back under the lead
            mix_loop(ost, ost_note(m, bucket), at_step(bar, st), vel)
    ost = shaped(ost, soft_duck)
    s.add(ost, -17.0, pan=-0.15, send=0.2, label="ostinato")

    # Pads: stabs pushed onto the "and" of 4 every other bar in the groove; held chords in the
    # breakdown.
    stab = note_cache(lambda i: pad_chord(ARENA_PADS[i], s.secs(0.5), 0.005, 0.2,
                                          lambda j: 500.0 + 1500.0 * math.exp(-j / (0.06 * R)), R, rng))
    pads = s.track()
    for bar in range(2, ARENA_BARS + 1, 2):
        if groove(bar) and bar != 16:
            mix_loop(pads, stab(bar % 4), s.at(bar, 3.5))
    for bar in range(17, 25):
        cyc = (bar - 1) % 4
        held = pad_chord(ARENA_PADS[cyc], s.secs(4.0), 0.5, 0.6, 700.0 + 120.0 * (bar - 17), R, rng)
        mix_loop(pads, held, s.at(bar), 0.9)
    s.add(shaped(pads, duck), -15.0, pan=0.1, send=0.35, label="pads")

    # Lead hook, bars 9-16.
    lead = s.track()
    for bar, beat, m, beats in ARENA_LEAD:
        mix_loop(lead, lead_note(midi_hz(m), s.secs(beats), R, rng), s.at(bar, beat))
    s.add(lead, -8.0, pan=0.05, send=0.3, label="lead")

    # Both go into one dotted-eighth ping-pong.
    echo_l, echo_r = pingpong(list(map(add, scale(ost, undb(-17.0)), scale(lead, undb(-9.0)))),
                              R, 3 * step, 0.35, 2800.0)
    s.add_stereo(echo_l, echo_r, -8.0, send=0.3, label="echoes")

    # Clap + steel on 2 and 4 (back from bar 27), then the roll across 31-32.
    claps = [clap(R, rng, rng.uniform(360.0, 420.0)) for _ in range(4)]
    roll_hit = clap(R, rng, 400.0, clank=0.25)
    sn = s.track()
    for bar in range(1, 31):
        if bar <= 16 or bar >= 27:
            for beat in (1, 3):
                mix_loop(sn, rng.choice(claps), s.at(bar, beat))
    roll = [(31, b * 0.5) for b in range(4)] + [(31, 2.0 + b * 0.25) for b in range(8)]
    roll += [(32, b * 0.25) for b in range(8)] + [(32, 2.0 + b * 0.125) for b in range(16)]
    for i, (bar, beat) in enumerate(roll):
        mix_loop(sn, roll_hit, s.at(bar, beat), 0.3 + 0.7 * (i / (len(roll) - 1)) ** 1.5)
    s.add(sn, -5.0, pan=-0.05, send=0.28, label="clap")

    # Hats: 16ths with open hats on the off-beats; eighths only while it rebuilds.
    closed = [metal_hat(R, rng) for _ in range(4)]
    choked = fade(metal_hat(R, rng, open_=True)[:2 * step], R, 0.0, 0.004)   # the next closed hat cuts it
    hats = s.track()
    for bar in range(1, ARENA_BARS + 1):
        full = bar <= 16 or 27 <= bar <= 31
        sparse = bar in (25, 26)
        opens = bar <= 16 or 29 <= bar <= 31
        for st in range(16):
            p = at_step(bar, st)
            if opens and st % 4 == 2:
                mix_loop(hats, choked, p, 0.55)
            elif full or (sparse and st % 4 == 2):
                vel = (0.9, 0.45, 0.7, 0.45)[st % 4] * rng.uniform(0.85, 1.0)
                mix_loop(hats, rng.choice(closed), p, vel)
    s.add(hats, -15.0, pan=0.25, send=0.06, label="hats")

    # The clock never stops.
    tick, tock = clock_tick(R, rng, True), clock_tick(R, rng, False)
    clock = s.track()
    for bar in range(1, ARENA_BARS + 1):
        for e in range(8):
            on_beat = e % 2 == 0
            mix_loop(clock, tick if on_beat else tock, s.at(bar, e * 0.5), 1.0 if on_beat else 0.8)
    s.add(clock, -15.0, pan=-0.3, send=0.15, label="clock")

    # Crashes where sections land, a sub boom for the breakdown, and the riser.
    cymbal = crash(R, rng)
    fx = s.track()
    for bar in (1, 9, 17, 25):
        mix_loop(fx, cymbal, s.at(bar), 0.8 if bar != 17 else 0.6)
    n = int(2.5 * R)
    boom = shaped(glide(90.0, 30.0, 0.3, n, R), envelope(n, R, attack=0.002, tau=0.6))
    mix_loop(fx, saturate(boom, 1.5), s.at(17), 1.4)
    s.add(fx, -11.0, send=0.25, label="crash/boom")

    start, end = s.at(25), L
    n = end - start
    riser = svf(noise(n, rng), lambda i: 250.0 * (6500.0 / 250.0) ** (i / n), 2.0, R, "bp")
    ramp = [(i / n) ** 2.5 for i in range(n)]
    riser = fade(shaped(norm(riser), ramp), R, 0.0, 0.012)
    rs = s.track()
    mix_loop(rs, riser, start)
    s.add(rs, -9.0, send=0.2, label="riser")

    s.add_reverb(rt60=2.3, damp_hz=3000.0, size=1.15, gain_db=-6.0)
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
    """Puts a dry effect in a space. The wet signal is peak-matched to the dry, so `wet` reads
    as how loud the tail is next to the hit."""
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
    """A deep thunk and a small ember whoosh off the back of it."""
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
    """The handle rattles against the latch, two clicks, and the door doesn't give."""
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
    """The steel door slams behind you: boom, impact, the door ringing, a sub drop, a long tail."""
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
    """A dull double knock, the second lower. 'No.'"""
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
    """The box's jaws grinding apart, stone on stone, with a low hum coming up out of it."""
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
    """A whoosh pulled in backwards, then a dark wet thump: the box has your hand."""
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
    """Palm closing on the tag: a soft slap and a clink the hand cuts short."""
    x = silence(sec(0.35))
    place(x, burst(0.06, 0.012, rng, "lp", 2200.0), 0, 0.9)
    place(x, thump(180.0, 120.0, 0.1, 0.025), 0, 0.6)
    place(x, strike(TAG, 1850.0, 0.25, rng, decay_scale=0.3, click=0.0), 0.008, 0.4)
    return x


def sfx_drop(rng):
    """The tag falls away: bounces that get closer, quieter and darker until there's nothing."""
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
    """A dull buzz sinking a few semitones, with a second voice just off it so it grinds."""
    n = sec(0.45)
    x = list(map(add, osc_sweep("saw", 150.0, 100.0, n, R), osc_sweep("saw", 159.0, 105.0, n, R, 0.7)))
    x = lowpass(lowpass(x, 900.0, R), 900.0, R)
    x = shaped(x, envelope(n, R, attack=0.005, gate=0.38, release=0.02))
    return room(x, 0.4, 3000.0, 0.1)


def sfx_hit(rng):
    """The opponent loses a life: punch, snap, and a crunch with the bits knocked out of it."""
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
    """You lose a life: a heavy distorted low hit, your heart, and a thin ring that fades."""
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
    """A veil catching something: a whoosh runs into it and it shimmers like thin glass."""
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
    """Stinger: a minor music-box phrase that turns to D major as a swell comes up under it."""
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
    for m in (50, 51, 56, 57):              # D, Eb, Ab, A: everything wrong at once
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
    """Ash crumbling, and the soft crackle of an ember going out."""
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
    """A brass casing bouncing on concrete, the bounces closing up."""
    x = silence(sec(1.1))
    for t, g in ((0.0, 1.0), (0.21, 0.55), (0.36, 0.32), (0.46, 0.18), (0.52, 0.1)):
        place(x, strike(BRASS, 3150.0 * rng.uniform(0.98, 1.02), 0.6, rng, decay_scale=0.5, click=0.5,
                        click_hp=4000.0), t, g)
    for k in range(5):
        place(x, burst(0.003, 0.0007, rng, "hp", 4000.0), 0.56 + 0.025 * k, 0.06 * (1 - k / 5))
    return room(x, 0.6, 5000.0, 0.12)


def sfx_revolver(rng):
    """Hammer back, the cylinder clicks round, then the shot in a small concrete room."""
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
    """A low tritone swelling up, and two heartbeats inside it."""
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
    """Coin flip: the thumb's ping, the wobble as it spins, the landing and a little settle."""
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
    """Card edges flicking past a thumb: `count` papery ticks whose spacing slides from
    first_gap to last_gap. Returns when the last one landed."""
    t = start
    for k in range(count):
        u = k / max(1, count - 1)
        place(x, burst(0.004, 0.0018, rng, "bp", 4000.0, 0.8), t, gain * rng.uniform(0.6, 1.0))
        place(x, burst(0.006, 0.003, rng, "bp", 900.0, 1.0), t, gain * 0.25)
        t += first_gap + (last_gap - first_gap) * u
    return t


def sfx_high_card(rng):
    """Card riffle, accelerating, then the deck snaps square."""
    x = silence(sec(0.75))
    end = riffle(x, 0.0, 25, 0.032, 0.009, rng)
    n = sec(end)
    mix(x, shaped(norm(biquad(noise(n, rng), "bp", 2000.0, 0.7, R)), lines(((0, 0), (end, 1.0)), n, R)), 0, 0.15)
    place(x, burst(0.01, 0.006, rng, "hp", 1500.0, attack=0.0001), end + 0.025, 1.0)
    place(x, thump(250.0, 150.0, 0.06, 0.02), end + 0.025, 0.5)
    return x


def sfx_tourniquet(rng):
    """Cloth pulled tight: a rising swish and a creak as the knot bites."""
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
    """A soft whoosh of ash, a breath behind it, a few sparks catching the light."""
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
    """Glass shimmer: a bright ting and detuned high pairs beating against each other."""
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
    """A whisper with no words, the vowel sinking from 'ah' to 'oh', over one low note."""
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
    # crc32 rather than hash(): str hashes change every run, and these seeds must not.
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
