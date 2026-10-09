#!/usr/bin/env python3
"""Build seven original ring-toss prototype cues with the Python standard library.

No recordings, samples, melodies, or audio data from another project are inputs.
Every cue has its own seed and fixed parameters. Generated files are candidates
for event feedback, not final music or evidence of in-game listening acceptance.
Existing different files are rejected rather than overwritten.
"""

from __future__ import annotations

import argparse
import hashlib
import io
import json
import math
from pathlib import Path
import random
import struct
import wave


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "assets" / "proto-013-ring-toss" / "audio" / "original"
SAMPLE_RATE = 48000
PEAK_LIMIT = 0.50
TAU = math.tau

# (filename, duration in seconds, independent seed, rule event)
CUES = (
    ("launch", 0.240, 13001, "throw_committed"),
    ("hit", 0.420, 13002, "new_prize_hit_confirmed"),
    ("bounce", 0.220, 13003, "rebound_contact_confirmed"),
    ("cash", 0.480, 13004, "prize_cash_committed"),
    ("retain", 0.400, 13005, "prize_retain_committed"),
    ("miss", 0.280, 13006, "throw_miss_confirmed"),
    ("ui_confirm", 0.120, 13007, "ui_action_accepted"),
)


def bell(t: float, frequency: float, decay: float, phase: float = 0.0) -> float:
    """Short modal tone; chosen ratios are timbre, not an imported sample."""
    if t < 0.0:
        return 0.0
    envelope = (1.0 - math.exp(-t * 1800.0)) * math.exp(-t * decay)
    return envelope * (
        math.sin(TAU * frequency * t + phase)
        + 0.24 * math.sin(TAU * frequency * 2.43 * t + phase * 0.5)
        + 0.10 * math.sin(TAU * frequency * 4.17 * t)
    )


def synthesize(name: str, duration: float, seed: int) -> list[float]:
    rng = random.Random(seed)
    length = round(duration * SAMPLE_RATE)
    phase = rng.uniform(-0.3, 0.3)
    filtered_noise = 0.0
    previous_noise = 0.0
    samples = []
    for index in range(length):
        t = index / SAMPLE_RATE
        x = t / duration
        noise = rng.uniform(-1.0, 1.0)
        filtered_noise += 0.14 * (noise - filtered_noise)
        high_noise = noise - previous_noise
        previous_noise = noise

        if name == "launch":
            # A cloth/air gesture with a gentle downward glide.
            phase_glide = TAU * (720.0 * t - 470.0 * t * t / (2.0 * duration))
            swell = math.sin(math.pi * x) ** 1.6
            value = swell * (0.65 * filtered_noise + 0.12 * math.sin(phase_glide))
        elif name == "hit":
            # Bright but restrained confirmation, separated from cash's cluster.
            value = 0.70 * bell(t, 1046.5, 12.0, phase)
            value += 0.25 * bell(t - 0.045, 1568.0, 15.0)
        elif name == "bounce":
            # One dry wooden contact and a short damped flex resonance.
            value = 0.65 * filtered_noise * math.exp(-t * 125.0)
            value += 0.36 * bell(t, 310.0, 31.0, phase)
            value += 0.12 * bell(t, 730.0, 47.0)
        elif name == "cash":
            # Three light coin-like strikes, not a slot-machine reward fanfare.
            value = 0.52 * bell(t, 1760.0, 18.0, phase)
            value += 0.38 * bell(t - 0.075, 2093.0, 20.0)
            value += 0.28 * bell(t - 0.140, 2637.0, 23.0)
            value += 0.035 * high_noise * math.exp(-t * 70.0)
        elif name == "retain":
            # A lower warm latch cue, intentionally distinct from money.
            value = 0.62 * bell(t, 392.0, 12.0, phase)
            value += 0.35 * bell(t - 0.080, 523.25, 13.0)
            value += 0.06 * filtered_noise * math.exp(-t * 95.0)
        elif name == "miss":
            # Quiet low contact; failure has no alarm or blaming voice.
            phase_glide = TAU * (190.0 * t - 75.0 * t * t / (2.0 * duration))
            value = 0.28 * math.sin(phase_glide) * math.exp(-t * 16.0)
            value += 0.18 * filtered_noise * math.exp(-t * 65.0)
        elif name == "ui_confirm":
            # Compact acceptance tick; no cue is played for predictions.
            phase_glide = TAU * (660.0 * t + 220.0 * t * t / (2.0 * duration))
            value = math.sin(phase_glide) * math.exp(-t * 31.0)
            value += 0.10 * bell(t, 1320.0, 52.0, phase)
        else:
            raise ValueError(f"Unknown cue: {name}")
        samples.append(value)

    # Remove DC, then smooth both ends. Fade uses actual frame indices so the
    # first and last PCM samples are zero, without relying on container padding.
    mean = math.fsum(samples) / length
    attack_frames = round(0.002 * SAMPLE_RATE)
    release_frames = round(0.015 * SAMPLE_RATE)
    for index, value in enumerate(samples):
        attack = min(1.0, index / attack_frames)
        release = min(1.0, (length - 1 - index) / release_frames)
        samples[index] = (value - mean) * attack * release
    maximum = max(abs(value) for value in samples)
    # Leave prototype mixer headroom. Miss/UI are quieter in the source itself.
    target = {"miss": 0.24, "ui_confirm": 0.32}.get(name, PEAK_LIMIT)
    gain = target / maximum if maximum else 0.0
    return [value * gain for value in samples]


def wav_bytes(samples: list[float]) -> tuple[bytes, list[int]]:
    pcm = [round(value * 32767.0) for value in samples]
    if any(abs(value) > 32767 for value in pcm):
        raise ValueError("PCM exceeds signed 16-bit headroom")
    buffer = io.BytesIO()
    with wave.open(buffer, "wb") as target:
        target.setnchannels(1)
        target.setsampwidth(2)
        target.setframerate(SAMPLE_RATE)
        target.writeframes(struct.pack(f"<{len(pcm)}h", *pcm))
    return buffer.getvalue(), pcm


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Verify existing bytes without writing")
    args = parser.parse_args()
    if not args.check:
        OUTPUT.mkdir(parents=True, exist_ok=True)
    for name, duration, seed, event in CUES:
        blob, pcm = wav_bytes(synthesize(name, duration, seed))
        destination = OUTPUT / f"{name}.wav"
        if destination.exists():
            if destination.read_bytes() != blob:
                raise FileExistsError(f"Different existing asset; review before replacement: {destination}")
            status = "verified" if args.check else "unchanged"
        elif args.check:
            raise FileNotFoundError(destination)
        else:
            destination.write_bytes(blob)
            status = "created"
        peak = max(abs(value) for value in pcm) / 32768.0
        rms = math.sqrt(math.fsum((value / 32768.0) ** 2 for value in pcm) / len(pcm))
        print(json.dumps({
            "asset_id": f"SFX-PROTOTYPE-{name.upper()}",
            "event": event,
            "path": destination.relative_to(ROOT).as_posix(),
            "seed": seed,
            "sample_rate": SAMPLE_RATE,
            "channels": 1,
            "frames": len(pcm),
            "duration_seconds": len(pcm) / SAMPLE_RATE,
            "bytes": len(blob),
            "sha256": hashlib.sha256(blob).hexdigest(),
            "peak_dbfs": round(20.0 * math.log10(peak), 3) if peak else None,
            "rms_dbfs": round(20.0 * math.log10(rms), 3) if rms else None,
            "boundary_pcm": [pcm[0], pcm[-1]],
            "status": status,
        }, sort_keys=True))


if __name__ == "__main__":
    main()
