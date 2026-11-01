// Neon Brawl sound effects generator.
// Builds every gameplay sound as a layered, physically inspired recipe (no samples, no downloads)
// and writes 44.1 kHz 16-bit mono WAV files into Assets/_Project/Resources/Sfx.
//
//   node Tools/Sfx/gen-sfx.js [outDir] [previewDir]
//
// Guns  = supersonic crack + muzzle blast (noise through a falling low-pass) + body punch
//         + sub "boom" (falling sine) + mechanical action clicks + outdoor slap echoes + reverb tail.
// Steps = heel thud + toe thud + surface grains (grass/sand/snow) or heel click + scuff (hard floors).
// Every file name ends in _N: the game picks a random variant each time so repeats never sound robotic.
'use strict';
const fs = require('fs');
const path = require('path');
const zlib = require('zlib');

const SR = 44100;
const OUT = process.argv[2] || path.join(__dirname, '../../Assets/_Project/Resources/Sfx');
const PREVIEW = process.argv[3] || '';

// ------------------------------------------------------------------ random

let R = mulberry(1);
function mulberry(seed) {
  let a = seed >>> 0;
  return () => {
    a = (a + 0x6D2B79F5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}
const seed = (s) => { R = mulberry(s * 7919 + 13); };
const rnd = (a = 0, b = 1) => a + (b - a) * R();
const jit = (v, amt = 0.08) => v * (1 + rnd(-amt, amt));
const noise = () => R() * 2 - 1;

// ------------------------------------------------------------------ buffers

const buf = (sec) => new Float32Array(Math.max(1, Math.round(sec * SR)));

function mix(dst, src, at = 0, gain = 1) {
  const o = Math.round(at * SR);
  for (let i = 0; i < src.length; i++) {
    const j = i + o;
    if (j >= 0 && j < dst.length) dst[j] += src[i] * gain;
  }
  return dst;
}

function env(b, fn) {
  for (let i = 0; i < b.length; i++) b[i] *= fn(i / SR);
  return b;
}

const expd = (tau) => (t) => Math.exp(-t / tau);
/** Linear attack, then exponential decay. */
const att = (a, tau) => (t) => (t < a ? t / a : Math.exp(-(t - a) / tau));
/** Smooth hump that peaks at `peak` seconds and is gone by `len`. */
const hump = (peak, len) => (t) => {
  if (t <= 0 || t >= len) return 0;
  return t < peak ? Math.sin((t / peak) * Math.PI / 2) : Math.pow(Math.cos(((t - peak) / (len - peak)) * Math.PI / 2), 1.6);
};

function peak(b) {
  let p = 0;
  for (let i = 0; i < b.length; i++) p = Math.max(p, Math.abs(b[i]));
  return p;
}

function normalize(b, to = 1) {
  const p = peak(b) || 1;
  for (let i = 0; i < b.length; i++) b[i] *= to / p;
  return b;
}

function sat(b, drive) {
  const n = Math.tanh(drive);
  for (let i = 0; i < b.length; i++) b[i] = Math.tanh(b[i] * drive) / n;
  return b;
}

// ------------------------------------------------------------------ sources

function white(sec) {
  const b = buf(sec);
  for (let i = 0; i < b.length; i++) b[i] = noise();
  return b;
}

function brown(sec) {
  const b = buf(sec);
  let l = 0;
  for (let i = 0; i < b.length; i++) {
    l = (l + 0.02 * noise()) / 1.02;
    b[i] = l * 3.5;
  }
  return b;
}

/** Sine whose pitch falls from f0 to f1 (time constant tauF) and fades with tauA: the "boom" of a blast. */
function thump(sec, f0, f1, tauF, tauA, attack = 0.001) {
  const b = buf(sec);
  let ph = 0;
  for (let i = 0; i < b.length; i++) {
    const t = i / SR;
    const f = f1 + (f0 - f1) * Math.exp(-t / tauF);
    ph += (2 * Math.PI * f) / SR;
    b[i] = Math.sin(ph) * (t < attack ? t / attack : 1) * Math.exp(-t / tauA);
  }
  return b;
}

/** Sum of damped sines: struck metal, wood, brass. */
function modal(sec, modes) {
  const b = buf(sec);
  for (const m of modes) {
    const w = (2 * Math.PI * m.f) / SR;
    const ph = rnd(0, Math.PI * 2);
    for (let i = 0; i < b.length; i++) {
      const t = i / SR;
      const a = m.a * Math.exp(-t / m.tau) * Math.min(1, t / 0.0002);
      if (a < 1e-5 && t > 0.01) break;
      b[i] += a * Math.sin(w * i + ph);
    }
  }
  return b;
}

/** A hard metal click: inharmonic modes around `base` plus a tiny noise snap. */
function clack(base, tau = 0.012, bright = 1) {
  const d = Math.min(0.25, tau * 8 + 0.01);
  const ratios = [1, 1.47, 2.09, 2.56, 3.31, 4.15, 5.4];
  // gun parts are heavy and damped: short rings plus a gritty noise body ("chk", not "ting")
  const modes = ratios.map((r) => ({ f: base * r * rnd(0.95, 1.05), tau: (tau * 0.6 * rnd(0.6, 1.2)) / Math.sqrt(r), a: (rnd(0.4, 1) / Math.pow(r, 1.1 - bright * 0.4)) }));
  const b = modal(d, modes);
  mix(b, env(filt(white(d), 'bp', base * 1.6, 0.9), att(0.0002, tau * 0.45)), 0, 1.6);
  mix(b, env(filt(white(0.006), 'hp', 2500, 0.7), expd(0.0006)), 0, 1.2 * bright);
  return normalize(b);
}

// ------------------------------------------------------------------ filters

class Biquad {
  constructor(type, f, q = 0.707, gainDb = 0) {
    this.type = type;
    this.x1 = this.x2 = this.y1 = this.y2 = 0;
    this.set(f, q, gainDb);
  }
  set(f, q = this.q, gainDb = this.g) {
    this.q = q;
    this.g = gainDb;
    f = Math.min(Math.max(f, 10), SR * 0.45);
    const w = (2 * Math.PI * f) / SR, c = Math.cos(w), s = Math.sin(w), al = s / (2 * q), A = Math.pow(10, gainDb / 40);
    let b0, b1, b2, a0, a1, a2;
    switch (this.type) {
      case 'lp': b0 = (1 - c) / 2; b1 = 1 - c; b2 = (1 - c) / 2; a0 = 1 + al; a1 = -2 * c; a2 = 1 - al; break;
      case 'hp': b0 = (1 + c) / 2; b1 = -(1 + c); b2 = (1 + c) / 2; a0 = 1 + al; a1 = -2 * c; a2 = 1 - al; break;
      case 'bp': b0 = al; b1 = 0; b2 = -al; a0 = 1 + al; a1 = -2 * c; a2 = 1 - al; break;
      case 'peak': b0 = 1 + al * A; b1 = -2 * c; b2 = 1 - al * A; a0 = 1 + al / A; a1 = -2 * c; a2 = 1 - al / A; break;
      default: throw new Error('filter ' + this.type);
    }
    this.b0 = b0 / a0; this.b1 = b1 / a0; this.b2 = b2 / a0; this.a1 = a1 / a0; this.a2 = a2 / a0;
  }
  run(x) {
    const y = this.b0 * x + this.b1 * this.x1 + this.b2 * this.x2 - this.a1 * this.y1 - this.a2 * this.y2;
    this.x2 = this.x1; this.x1 = x; this.y2 = this.y1; this.y1 = y;
    return y;
  }
}

/** Filters a buffer; `f` may be a function of time for sweeps. */
function filt(b, type, f, q = 0.707, gainDb = 0) {
  const dyn = typeof f === 'function';
  const bq = new Biquad(type, dyn ? f(0) : f, q, gainDb);
  const out = new Float32Array(b.length);
  for (let i = 0; i < b.length; i++) {
    if (dyn && (i & 15) === 0) bq.set(f(i / SR), q, gainDb);
    out[i] = bq.run(b[i]);
  }
  return out;
}

/** Freeverb (8 combs + 4 all-passes), wet only. */
function reverb(input, room = 0.8, damp = 0.45, extra = 0) {
  const scale = SR / 44100;
  const combs = [1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617].map((n) => ({ b: new Float32Array(Math.round(n * scale)), i: 0, f: 0 }));
  const aps = [556, 441, 341, 225].map((n) => ({ b: new Float32Array(Math.round(n * scale)), i: 0 }));
  const out = new Float32Array(input.length + Math.round(extra * SR));
  for (let n = 0; n < out.length; n++) {
    const x = n < input.length ? input[n] * 0.015 : 0;
    let s = 0;
    for (const c of combs) {
      const y = c.b[c.i];
      c.f = y * (1 - damp) + c.f * damp;
      c.b[c.i] = x + c.f * room;
      c.i = (c.i + 1) % c.b.length;
      s += y;
    }
    for (const a of aps) {
      const bo = a.b[a.i];
      const y = -s + bo;
      a.b[a.i] = s + bo * 0.5;
      a.i = (a.i + 1) % a.b.length;
      s = y;
    }
    out[n] = s;
  }
  return out;
}

/** Adds an outdoor space to a dry sound: a few dark slap-back echoes and a decaying reverb tail. */
function space(dry, o) {
  const out = Float32Array.from(dry);
  const pk = peak(dry);
  for (const e of o.echoes || []) mix(out, filt(filt(dry, 'lp', e.lp || 1800, 0.6), 'hp', 120, 0.6), jit(e.t, 0.1), e.g);
  if (o.tail > 0) {
    const src = filt(dry, 'lp', o.tailLp || 2600, 0.6);
    const wet = reverb(src, o.room || 0.8, o.damp || 0.5);
    env(wet, (t) => Math.min(1, t / 0.01) * Math.exp(-t / (o.tailTau || 0.3)));
    normalize(wet, pk * o.tail);
    mix(out, wet, o.preDelay || 0.008, 1);
  }
  return out;
}

/** Grainy texture: many tiny filtered noise grains (gravel, grass, snow, debris). */
function grains(dst, count, t0, spread, lo, hi, q, amp, len = [0.0004, 0.0016], shape = 2) {
  for (let k = 0; k < count; k++) {
    const u = Math.pow(R(), shape);
    const t = t0 + u * spread;
    const g = filt(white(rnd(len[0], len[1]) + 0.002), 'bp', rnd(lo, hi), q);
    env(g, (x) => Math.exp(-x / 0.0009));
    mix(dst, g, t, amp * rnd(0.3, 1) * (1 - u * 0.7));
  }
  return dst;
}

/** Band-pass noise whose center moves through three points: whooshes, scrapes, slides. */
function sweepNoise(dur, f0, fMid, f1, mid, q, shapeFn) {
  const fc = (t) => (t < mid ? f0 + (fMid - f0) * (t / mid) : fMid + (f1 - fMid) * Math.min(1, (t - mid) / Math.max(0.001, dur - mid)));
  return env(filt(white(dur), 'bp', fc, q), shapeFn || hump(mid, dur));
}

// ------------------------------------------------------------------ finishing

function finish(b, o = {}) {
  // remove rumble below hearing, trim silence, fade the very end, and peak at -1 dBFS
  let x = filt(b, 'hp', o.hp || 28, 0.7);
  normalize(x, 1);
  let end = x.length;
  const floor = Math.pow(10, -64 / 20);
  while (end > 1 && Math.abs(x[end - 1]) < floor) end--;
  end = Math.min(x.length, end + Math.round(0.01 * SR));
  x = x.slice(0, end);
  const fade = Math.min(Math.round(0.03 * SR), Math.floor(x.length / 4));
  for (let i = 0; i < fade; i++) x[x.length - 1 - i] *= i / fade;
  return normalize(x, o.level || 0.89);
}

// ------------------------------------------------------------------ guns

/**
 * One gunshot. All times in seconds, frequencies in Hz.
 * crack: supersonic snap; blast: muzzle noise with a falling low-pass (lp0 -> lp1); punch: chest thump band;
 * boom: falling sine; mech: action clicks; then outdoor echoes + tail.
 */
function gun(o) {
  const d = o.dur;
  const dry = buf(d);
  // 1 · crack
  const crack = env(filt(white(0.03), 'hp', jit(o.crackHp || 2600), 0.7), att(0.0001, jit(o.crackTau || 0.0012)));
  mix(dry, crack, 0, o.crack || 0.9);
  // 2 · blast
  const lp0 = jit(o.lp0), lp1 = jit(o.lp1), lpTau = jit(o.lpTau);
  const blast = env(filt(white(d), 'lp', (t) => lp1 + (lp0 - lp1) * Math.exp(-t / lpTau), 0.8), att(0.0003, jit(o.blastTau)));
  mix(dry, blast, 0, o.blast || 1);
  // 3 · punch (low-mid body)
  const punch = env(filt(white(d), 'bp', jit(o.punchF, 0.1), 1.1), att(0.0006, jit(o.punchTau)));
  mix(dry, punch, 0, o.punch || 1.4);
  // 4 · boom
  mix(dry, thump(d, jit(o.f0), jit(o.f1), o.fTau || 0.03, jit(o.boomTau), 0.0006), 0, o.boom || 0.9);
  // 5 · mechanical action
  for (const m of o.mech || []) mix(dry, clack(jit(m.f, 0.06), m.tau || 0.01, m.bright || 1), jit(m.t, 0.15), m.g);
  normalize(dry);
  sat(dry, o.drive || 2.5);
  // 6 · the world answers
  const wet = space(dry, o);
  return finish(wet);
}

const GUNS = {
  // 9 mm pistol: crisp snap, short tail
  shot_pistol: {
    dur: 0.8, crack: 0.85, crackHp: 3000, lp0: 9000, lp1: 700, lpTau: 0.012, blastTau: 0.02, punchF: 260, punchTau: 0.024, punch: 1.4,
    f0: 170, f1: 70, boomTau: 0.05, boom: 0.75, drive: 2.3,
    mech: [{ t: 0.004, f: 2700, g: 0.14 }, { t: 0.032, f: 2300, g: 0.2 }],
    echoes: [{ t: 0.07, g: 0.22 }, { t: 0.16, g: 0.12, lp: 1200 }], tail: 0.32, tailTau: 0.18, room: 0.74,
  },
  // .50 hand cannon: big bark, long rolling tail
  shot_cannon: {
    dur: 1.3, crack: 1.0, crackHp: 2400, lp0: 8000, lp1: 480, lpTau: 0.022, blastTau: 0.042, punchF: 175, punchTau: 0.045, punch: 1.8,
    f0: 140, f1: 46, boomTau: 0.11, boom: 1.15, drive: 3.1,
    mech: [{ t: 0.006, f: 2100, g: 0.1 }],
    echoes: [{ t: 0.09, g: 0.3 }, { t: 0.21, g: 0.18, lp: 1300 }, { t: 0.37, g: 0.1, lp: 900 }], tail: 0.45, tailTau: 0.38, room: 0.82,
  },
  // SMG: short, tight, higher pitched (fires 12 times a second)
  shot_smg: {
    dur: 0.5, crack: 0.75, crackHp: 3400, lp0: 10000, lp1: 950, lpTau: 0.008, blastTau: 0.014, punchF: 310, punchTau: 0.016, punch: 1.2,
    f0: 190, f1: 85, boomTau: 0.03, boom: 0.6, drive: 2.4,
    mech: [{ t: 0.003, f: 3300, g: 0.2, tau: 0.007 }],
    echoes: [{ t: 0.06, g: 0.16 }], tail: 0.24, tailTau: 0.12, room: 0.7,
  },
  // 5.56 assault rifle: sharp supersonic crack and a solid body
  shot_rifle: {
    dur: 0.95, crack: 1.1, crackHp: 2500, crackTau: 0.001, lp0: 10000, lp1: 780, lpTau: 0.012, blastTau: 0.026, punchF: 230, punchTau: 0.03, punch: 1.5,
    f0: 155, f1: 60, boomTau: 0.07, boom: 0.9, drive: 2.8,
    mech: [{ t: 0.003, f: 2300, g: 0.16, tau: 0.008 }],
    echoes: [{ t: 0.08, g: 0.25 }, { t: 0.18, g: 0.14, lp: 1300 }, { t: 0.31, g: 0.08, lp: 900 }], tail: 0.4, tailTau: 0.26, room: 0.8,
  },
  // machine gun: heavier and deeper than the rifle
  shot_lmg: {
    dur: 1.0, crack: 1.0, crackHp: 2200, lp0: 9000, lp1: 600, lpTau: 0.014, blastTau: 0.03, punchF: 190, punchTau: 0.036, punch: 1.6,
    f0: 135, f1: 52, boomTau: 0.08, boom: 1.0, drive: 3.0,
    mech: [{ t: 0.004, f: 1800, g: 0.18, tau: 0.01 }],
    echoes: [{ t: 0.085, g: 0.26 }, { t: 0.19, g: 0.15, lp: 1300 }, { t: 0.33, g: 0.08, lp: 900 }], tail: 0.42, tailTau: 0.28, room: 0.8,
  },
  // 12-gauge: wide, chesty blast
  shot_shotgun: {
    dur: 1.4, crack: 0.9, crackHp: 2000, crackTau: 0.002, lp0: 7000, lp1: 420, lpTau: 0.03, blastTau: 0.06, blast: 1.2, punchF: 150, punchTau: 0.06, punch: 1.8,
    f0: 115, f1: 40, fTau: 0.04, boomTau: 0.14, boom: 1.3, drive: 3.4,
    echoes: [{ t: 0.1, g: 0.32 }, { t: 0.22, g: 0.2, lp: 1300 }, { t: 0.4, g: 0.12, lp: 900 }], tail: 0.5, tailTau: 0.42, room: 0.84,
  },
  // sniper: huge crack and a long echo down the valley
  shot_sniper: {
    dur: 2.2, crack: 1.3, crackHp: 2000, crackTau: 0.0015, lp0: 9000, lp1: 450, lpTau: 0.025, blastTau: 0.05, punchF: 160, punchTau: 0.055, punch: 1.7,
    f0: 125, f1: 38, fTau: 0.04, boomTau: 0.16, boom: 1.3, drive: 3.5,
    echoes: [{ t: 0.12, g: 0.35 }, { t: 0.3, g: 0.25, lp: 1200 }, { t: 0.55, g: 0.16, lp: 900 }, { t: 0.9, g: 0.08, lp: 700 }], tail: 0.6, tailTau: 0.7, room: 0.88,
  },
};

function rocket() {
  const d = 1.3;
  const dry = buf(d);
  mix(dry, thump(0.4, jit(110), 45, 0.03, 0.07, 0.001), 0, 1.0);
  mix(dry, env(filt(white(0.2), 'lp', 2500, 0.7), att(0.0005, 0.025)), 0, 0.9);
  // motor: band-passed roar that climbs, with a ragged crackle
  let rag = 1;
  const motor = env(filt(white(d), 'bp', (t) => 500 + 700 * Math.min(1, t / 0.5), 0.7), (t) => {
    if ((Math.round(t * SR) & 127) === 0) rag = 0.65 + 0.35 * R();
    return Math.min(1, t / 0.05) * Math.exp(-t / 0.38) * rag;
  });
  mix(dry, motor, 0.01, 1.3);
  mix(dry, env(filt(white(d), 'hp', 3000, 0.7), (t) => Math.min(1, t / 0.03) * Math.exp(-t / 0.2)), 0.01, 0.35);
  normalize(dry);
  sat(dry, 2);
  return finish(space(dry, { echoes: [{ t: 0.12, g: 0.2 }], tail: 0.35, tailTau: 0.3 }));
}

function plasma() {
  const d = 0.6;
  const b = buf(d);
  let ph = 0, mph = 0;
  for (let i = 0; i < b.length; i++) {
    const t = i / SR;
    const f = 240 + jit(1500) * Math.exp(-t / 0.05);
    mph += (2 * Math.PI * f * 1.5) / SR;
    const idx = 0.5 + 3.5 * Math.exp(-t / 0.04);
    ph += (2 * Math.PI * f) / SR;
    b[i] = Math.sin(ph + idx * Math.sin(mph)) * att(0.001, 0.07)(t);
  }
  mix(b, thump(0.3, 150, 60, 0.02, 0.05), 0, 0.6);
  mix(b, env(filt(white(0.05), 'hp', 4000, 0.7), expd(0.008)), 0, 0.35);
  normalize(b);
  sat(b, 1.6);
  return finish(space(b, { echoes: [{ t: 0.07, g: 0.15, lp: 2500 }], tail: 0.3, tailTau: 0.15, tailLp: 4000 }));
}

function tesla() {
  const d = 0.32;
  const b = buf(d);
  let gate = 1, ph = 0;
  for (let i = 0; i < b.length; i++) {
    const t = i / SR;
    if (i % Math.round(SR * 0.004) === 0) gate = R() < 0.7 ? rnd(0.4, 1) : 0.05;
    ph += (2 * Math.PI * jit(118, 0.02)) / SR;
    const saw = ((ph / (2 * Math.PI)) % 1) * 2 - 1;
    b[i] = saw * gate * att(0.003, 0.12)(t);
  }
  const arcs = buf(d);
  grains(arcs, 40, 0, 0.25, 2500, 9000, 0.8, 1, [0.0005, 0.003], 1.3);
  mix(b, filt(arcs, 'hp', 1500, 0.7), 0, 0.8);
  return finish(filt(b, 'hp', 250, 0.7));
}

function flame() {
  const d = 0.42;
  const b = filt(brown(d), 'lp', 900, 0.7);
  mix(b, filt(white(d), 'bp', 700, 0.6), 0, 0.5);
  let turb = 1, target = 1;
  env(b, (t) => {
    if ((Math.round(t * SR) % 600) === 0) target = rnd(0.45, 1);
    turb += (target - turb) * 0.004;
    return turb * Math.min(1, t / 0.03) * (t < 0.2 ? 1 : Math.exp(-(t - 0.2) / 0.06));
  });
  const pops = buf(d);
  grains(pops, 10, 0.02, 0.25, 1500, 5000, 1, 0.6, [0.001, 0.004], 1);
  mix(b, pops, 0, 0.4);
  sat(normalize(b), 1.5);
  return finish(b);
}

function explosion() {
  const d = 2.8;
  const dry = buf(d);
  mix(dry, env(filt(white(0.04), 'hp', 1500, 0.7), expd(0.003)), 0, 1.0);
  mix(dry, env(filt(white(d), 'lp', (t) => 160 + 6500 * Math.exp(-t / 0.06), 0.8), att(0.002, 0.18)), 0, 1.4);
  mix(dry, thump(d, jit(95), 30, 0.08, 0.36, 0.004), 0, 1.7);
  const rumble = filt(brown(d), 'lp', 320, 0.7);
  const ph = rnd(0, 6);
  env(rumble, (t) => Math.min(1, t / 0.06) * Math.exp(-t / 0.65) * (0.72 + 0.28 * Math.sin(2 * Math.PI * 3.1 * t + ph)));
  mix(dry, rumble, 0, 1.1);
  // falling debris: dirt grains and a few stones / bits of metal
  grains(dry, 160, 0.1, 1.5, 1200, 6000, 0.9, 0.18, [0.0006, 0.003], 2.2);
  for (let k = 0; k < 9; k++) mix(dry, clack(rnd(700, 2600), rnd(0.006, 0.02), 0.6), 0.15 + Math.pow(R(), 1.6) * 1.2, rnd(0.03, 0.08));
  normalize(dry);
  sat(dry, 2.4);
  return finish(space(dry, { echoes: [{ t: 0.15, g: 0.3, lp: 1000 }, { t: 0.4, g: 0.18, lp: 700 }], tail: 0.5, tailTau: 0.8, room: 0.86, tailLp: 1500 }));
}

// ------------------------------------------------------------------ weapon handling

function scrape(dur, f, g = 1) {
  return env(sweepNoise(dur, f * 0.8, f * 1.25, f, dur * 0.4, 1.4), (t) => g * (0.7 + 0.3 * Math.sin(t * 900)));
}

function reload() {
  const b = buf(1.05);
  mix(b, clack(jit(3000), 0.008), 0, 0.45);                 // mag release
  mix(b, scrape(0.16, jit(2200), 1), 0.03, 0.3);            // mag slides out
  mix(b, scrape(0.07, jit(1800), 1), 0.4, 0.28);            // new mag slides in…
  mix(b, clack(jit(900), 0.025, 0.7), 0.47, 1.0);           // …and seats
  mix(b, env(filt(white(0.05), 'lp', 900, 0.7), att(0.0008, 0.012)), 0.47, 0.5);
  mix(b, scrape(0.06, jit(2600), 1), 0.68, 0.25);           // charging handle back
  mix(b, clack(jit(2400), 0.01), 0.7, 0.45);
  mix(b, clack(jit(1500), 0.03, 0.9), 0.8, 1.0);            // bolt slams home
  return finish(space(b, { tail: 0.12, tailTau: 0.08, room: 0.5 }));
}

function pump() {
  const b = buf(0.5);
  mix(b, scrape(0.05, jit(1700), 1), 0, 0.35);
  mix(b, clack(jit(1100), 0.025, 0.8), 0.045, 0.9);
  mix(b, scrape(0.05, jit(1900), 1), 0.16, 0.35);
  mix(b, clack(jit(1300), 0.028, 0.9), 0.2, 1.0);
  mix(b, env(filt(white(0.04), 'lp', 800, 0.7), att(0.0008, 0.01)), 0.2, 0.45);
  return finish(space(b, { tail: 0.12, tailTau: 0.08, room: 0.5 }));
}

function bolt() {
  const b = buf(0.8);
  mix(b, clack(jit(2800), 0.008), 0, 0.5);                  // bolt up
  mix(b, scrape(0.09, jit(2000), 1), 0.09, 0.35);           // back
  mix(b, clack(jit(1600), 0.02, 0.8), 0.18, 0.8);
  mix(b, scrape(0.08, jit(2100), 1), 0.36, 0.35);           // forward
  mix(b, clack(jit(1400), 0.025, 0.9), 0.45, 1.0);
  mix(b, clack(jit(2400), 0.01), 0.6, 0.55);                // down
  return finish(space(b, { tail: 0.1, tailTau: 0.07, room: 0.5 }));
}

function empty() {
  const b = buf(0.15);
  mix(b, clack(jit(3200), 0.006), 0, 0.8);                  // trigger
  mix(b, clack(jit(2600), 0.008), 0.012, 1.0);              // hammer falls on nothing
  mix(b, thump(0.05, 230, 180, 0.01, 0.01), 0.012, 0.25);
  return finish(b);
}

function handling() {
  const b = buf(0.3);
  mix(b, env(filt(white(0.12), 'bp', 1800, 0.8), hump(0.04, 0.12)), 0, 0.25); // cloth
  mix(b, clack(jit(1700), 0.02, 0.8), 0.06, 1.0);
  mix(b, clack(jit(3100), 0.007), 0.1, 0.4);
  return finish(b);
}

function pickup() {
  const b = buf(0.45);
  mix(b, handling(), 0, 0.7);
  const bell = modal(0.4, [{ f: 1318, tau: 0.12, a: 1 }, { f: 1976, tau: 0.09, a: 0.6 }, { f: 2637, tau: 0.05, a: 0.3 }]);
  mix(b, bell, 0.03, 0.35);
  return finish(b);
}

function shell(brass) {
  const b = buf(0.5);
  const base = brass ? jit(4300, 0.1) : jit(1100, 0.1);
  const t1 = rnd(0.06, 0.09), t2 = t1 + rnd(0.04, 0.06);
  const hits = [[0, 1], [t1, 0.55], [t2, 0.25]];
  for (const [t, g] of hits) {
    const modes = brass
      ? [1, 1.62, 2.31, 2.96].map((r, i) => ({ f: base * r * rnd(0.98, 1.02), tau: [0.07, 0.04, 0.025, 0.015][i], a: 1 / (i + 1) }))
      : [1, 1.9, 3.1].map((r, i) => ({ f: base * r, tau: 0.012 / (i + 1), a: 1 / (i + 1) }));
    mix(b, modal(0.3, modes), t, g);
    mix(b, env(filt(white(0.004), 'hp', 3000, 0.7), expd(0.0006)), t, g * 0.5);
  }
  return finish(b);
}

// ------------------------------------------------------------------ bodies and impacts

function hitFlesh() {
  const b = buf(0.3);
  mix(b, env(filt(white(0.2), 'lp', jit(1800), 0.7), att(0.0005, 0.012)), 0, 1.0);
  mix(b, thump(0.2, jit(170), 75, 0.015, 0.035), 0, 1.0);
  mix(b, env(filt(white(0.15), 'bp', jit(650, 0.15), 2.5), att(0.002, 0.03)), 0.002, 1.1);
  mix(b, env(filt(white(0.01), 'hp', 3000, 0.7), expd(0.001)), 0, 0.35);
  normalize(b);
  sat(b, 2);
  return finish(space(b, { tail: 0.12, tailTau: 0.06, room: 0.5 }));
}

function hitWall(ricochet) {
  const b = buf(ricochet ? 0.6 : 0.35);
  mix(b, env(filt(white(0.02), 'hp', 1500, 0.7), expd(0.003)), 0, 1.0);
  mix(b, env(filt(white(0.15), 'bp', jit(2500), 1), att(0.001, 0.02)), 0, 0.6);
  mix(b, thump(0.06, 230, 120, 0.01, 0.015), 0, 0.4);
  grains(b, 18, 0.015, 0.12, 2500, 8000, 1, 0.3);
  if (ricochet) {
    const w = buf(0.5);
    let ph = 0;
    const f0 = jit(4200, 0.15), wob = rnd(25, 45);
    for (let i = 0; i < w.length; i++) {
      const t = i / SR;
      const f = f0 * (0.55 + 0.45 * Math.exp(-t / 0.25)) * (1 + 0.012 * Math.sin(2 * Math.PI * wob * t));
      ph += (2 * Math.PI * f) / SR;
      w[i] = Math.sin(ph) * att(0.008, 0.11)(t);
    }
    mix(b, w, 0.012, 0.32);
  }
  normalize(b);
  sat(b, 1.6);
  return finish(space(b, { echoes: [{ t: 0.06, g: 0.12, lp: 2500 }], tail: 0.18, tailTau: 0.12, room: 0.6 }));
}

function grenadeBounce() {
  const b = buf(0.35);
  const base = jit(380, 0.1);
  mix(b, modal(0.3, [1, 2.43, 3.91, 5.6].map((r, i) => ({ f: base * r, tau: [0.05, 0.03, 0.02, 0.012][i], a: 1 / (i + 1) }))), 0, 1);
  mix(b, env(filt(white(0.05), 'lp', 1200, 0.7), att(0.0005, 0.01)), 0, 0.8);
  mix(b, thump(0.1, 150, 90, 0.01, 0.02), 0, 0.5);
  return finish(b);
}

function death() {
  const b = buf(0.7);
  mix(b, thump(0.3, jit(95), 55, 0.03, 0.07), 0, 1.0);
  mix(b, env(filt(white(0.15), 'lp', 700, 0.7), att(0.001, 0.03)), 0, 0.8);
  mix(b, thump(0.3, jit(110), 60, 0.02, 0.05), 0.12, 0.55);
  mix(b, env(filt(white(0.1), 'lp', 800, 0.7), att(0.001, 0.02)), 0.12, 0.45);
  for (let k = 0; k < 6; k++) mix(b, clack(rnd(1500, 3800), rnd(0.005, 0.012)), rnd(0.02, 0.28), rnd(0.06, 0.16));
  mix(b, env(filt(white(0.3), 'bp', 2200, 0.8), hump(0.06, 0.25)), 0.02, 0.18);
  return finish(space(b, { tail: 0.15, tailTau: 0.1, room: 0.6 }));
}

function heartbeat() {
  const b = buf(0.8);
  mix(b, thump(0.3, 70, 45, 0.03, 0.06, 0.004), 0, 1.0);
  mix(b, env(filt(white(0.1), 'lp', 200, 0.7), att(0.003, 0.03)), 0, 0.5);
  mix(b, thump(0.3, 85, 55, 0.03, 0.045, 0.004), 0.2, 0.7);
  return finish(b, { hp: 20 });
}

function whoosh(dur, f0, fPeak, f1, peakAt, q) {
  return sweepNoise(dur, f0, fPeak, f1, peakAt, q);
}

function swing() {
  const b = buf(0.3);
  mix(b, whoosh(0.24, jit(700), jit(4200), 1500, 0.07, 1.4), 0, 1);
  mix(b, modal(0.15, [{ f: jit(5200), tau: 0.05, a: 1 }, { f: jit(7600), tau: 0.03, a: 0.6 }]), 0.01, 0.12);
  return finish(b);
}

function throwSound() {
  const b = buf(0.32);
  mix(b, whoosh(0.28, jit(350), jit(1800), 600, 0.1, 1.0), 0, 1);
  mix(b, env(filt(white(0.15), 'lp', 900, 0.7), hump(0.03, 0.12)), 0, 0.4);
  return finish(b);
}

function dash() {
  const b = buf(0.4);
  mix(b, whoosh(0.32, jit(250), jit(1500), 400, 0.08, 0.9), 0, 1);
  mix(b, scrape(0.12, 2000, 1), 0.02, 0.35);
  mix(b, thump(0.1, 110, 70, 0.02, 0.03), 0, 0.45);
  return finish(b);
}

function whiz() {
  const b = buf(0.2);
  mix(b, sweepNoise(0.16, jit(5000), jit(3500), 1400, 0.03, 2), 0, 1);
  mix(b, env(filt(white(0.01), 'hp', 4000, 0.7), expd(0.0008)), 0.02, 0.5);
  return finish(b);
}

// ------------------------------------------------------------------ footsteps

const SURFACES = {
  grass: { thud: 0.7, thudLp: 900, thudNoise: 0.8, grains: 70, lo: 1500, hi: 6000, q: 1.5, amp: 0.32, spread: 0.07, rustle: 0.22 },
  sand: { thud: 0.45, thudLp: 650, thudNoise: 0.6, grains: 150, lo: 2500, hi: 9000, q: 0.9, amp: 0.22, spread: 0.09, rustle: 0.3 },
  snow: { thud: 0.5, thudLp: 750, thudNoise: 0.7, grains: 90, lo: 900, hi: 3500, q: 4, amp: 0.42, spread: 0.08, rustle: 0.05 },
  hard: { thud: 0.65, thudLp: 1400, thudNoise: 0.5, grains: 6, lo: 3000, hi: 8000, q: 1, amp: 0.15, spread: 0.03, rustle: 0, click: 0.7, scuff: 0.25 },
};

function step(kind) {
  const S = SURFACES[kind];
  const b = buf(0.3);
  const toe = rnd(0.035, 0.06);
  for (const [t0, g] of [[0, 1], [toe, rnd(0.45, 0.65)]]) {
    mix(b, thump(0.1, jit(115), 62, 0.02, 0.022, 0.002), t0, g * S.thud);
    mix(b, env(filt(white(0.08), 'lp', jit(S.thudLp), 0.7), att(0.001, 0.012)), t0, g * S.thudNoise);
    if (S.click) mix(b, env(filt(white(0.01), 'bp', jit(2600), 1.2), expd(0.0018)), t0, g * S.click);
    grains(b, Math.round(S.grains * g), t0, S.spread, S.lo, S.hi, S.q, S.amp * g);
  }
  if (S.rustle) mix(b, env(filt(white(0.2), 'bp', jit(3200), 0.8), hump(0.03, 0.14)), 0.005, S.rustle);
  if (S.scuff) mix(b, scrape(0.07, jit(3200), 1), toe + 0.01, S.scuff);
  return finish(b);
}

// ------------------------------------------------------------------ output

function wav(file, data) {
  const n = data.length;
  const b = Buffer.alloc(44 + n * 2);
  b.write('RIFF', 0); b.writeUInt32LE(36 + n * 2, 4); b.write('WAVE', 8);
  b.write('fmt ', 12); b.writeUInt32LE(16, 16); b.writeUInt16LE(1, 20); b.writeUInt16LE(1, 22);
  b.writeUInt32LE(SR, 24); b.writeUInt32LE(SR * 2, 28); b.writeUInt16LE(2, 32); b.writeUInt16LE(16, 34);
  b.write('data', 36); b.writeUInt32LE(n * 2, 40);
  for (let i = 0; i < n; i++) b.writeInt16LE(Math.round(Math.max(-1, Math.min(1, data[i])) * 32767), 44 + i * 2);
  fs.writeFileSync(file, b);
}

// --- preview: waveform + spectrogram PNG (for checking a sound by eye)
const CRC = (() => { const t = new Uint32Array(256); for (let n = 0; n < 256; n++) { let c = n; for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1; t[n] = c >>> 0; } return t; })();
function crc32(buf) { let c = 0xffffffff; for (let i = 0; i < buf.length; i++) c = CRC[(c ^ buf[i]) & 0xff] ^ (c >>> 8); return (c ^ 0xffffffff) >>> 0; }
function png(file, w, h, rgb) {
  const raw = Buffer.alloc((w * 3 + 1) * h);
  for (let y = 0; y < h; y++) { raw[y * (w * 3 + 1)] = 0; rgb.copy(raw, y * (w * 3 + 1) + 1, y * w * 3, (y + 1) * w * 3); }
  const chunk = (type, data) => { const len = Buffer.alloc(4); len.writeUInt32BE(data.length); const td = Buffer.concat([Buffer.from(type), data]); const c = Buffer.alloc(4); c.writeUInt32BE(crc32(td)); return Buffer.concat([len, td, c]); };
  const ihdr = Buffer.alloc(13); ihdr.writeUInt32BE(w, 0); ihdr.writeUInt32BE(h, 4); ihdr[8] = 8; ihdr[9] = 2;
  fs.writeFileSync(file, Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk('IHDR', ihdr), chunk('IDAT', zlib.deflateSync(raw)), chunk('IEND', Buffer.alloc(0))]));
}
function fftMag(frame) {
  const n = frame.length, re = Float64Array.from(frame), im = new Float64Array(n);
  for (let i = 1, j = 0; i < n; i++) { let bit = n >> 1; for (; j & bit; bit >>= 1) j ^= bit; j ^= bit; if (i < j) { [re[i], re[j]] = [re[j], re[i]]; } }
  for (let len = 2; len <= n; len <<= 1) { const ang = (-2 * Math.PI) / len; for (let i = 0; i < n; i += len) for (let k = 0; k < len / 2; k++) { const wr = Math.cos(ang * k), wi = Math.sin(ang * k); const ur = re[i + k], ui = im[i + k]; const vr = re[i + k + len / 2] * wr - im[i + k + len / 2] * wi, vi = re[i + k + len / 2] * wi + im[i + k + len / 2] * wr; re[i + k] = ur + vr; im[i + k] = ui + vi; re[i + k + len / 2] = ur - vr; im[i + k + len / 2] = ui - vi; } }
  const m = new Float64Array(n / 2); for (let i = 0; i < n / 2; i++) m[i] = Math.hypot(re[i], im[i]); return m;
}
function preview(file, data) {
  const w = 600, specH = 200, waveH = 80, h = specH + waveH, N = 1024;
  const rgb = Buffer.alloc(w * h * 3);
  for (let x = 0; x < w; x++) {
    const c = Math.floor((x / w) * Math.max(0, data.length - N));
    const frame = new Float64Array(N);
    for (let i = 0; i < N; i++) frame[i] = (data[c + i] || 0) * (0.5 - 0.5 * Math.cos((2 * Math.PI * i) / N));
    const m = fftMag(frame);
    for (let y = 0; y < specH; y++) {
      // log frequency axis 40 Hz .. 16 kHz
      const f = 40 * Math.pow(16000 / 40, 1 - y / specH);
      const bin = Math.min(N / 2 - 1, Math.round((f * N) / SR));
      const db = 20 * Math.log10(m[bin] / (N / 4) + 1e-9);
      const v = Math.max(0, Math.min(1, (db + 80) / 80));
      const o = (y * w + x) * 3;
      rgb[o] = Math.round(255 * Math.min(1, v * 1.6)); rgb[o + 1] = Math.round(255 * Math.max(0, v * 1.6 - 0.6)); rgb[o + 2] = Math.round(255 * Math.max(0, 0.4 - v) * 1.5);
    }
    // waveform column (peak of this slice)
    const s0 = Math.floor((x / w) * data.length), s1 = Math.floor(((x + 1) / w) * data.length);
    let pk = 0; for (let i = s0; i < s1; i++) pk = Math.max(pk, Math.abs(data[i]));
    const half = waveH / 2, hgt = Math.round(pk * half);
    for (let y = 0; y < waveH; y++) { const o = ((specH + y) * w + x) * 3; const on = Math.abs(y - half) <= hgt; rgb[o] = on ? 120 : 20; rgb[o + 1] = on ? 220 : 20; rgb[o + 2] = on ? 255 : 30; }
  }
  png(file, w, h, rgb);
}

// ------------------------------------------------------------------ catalogue

const JOBS = [];
const job = (name, count, make) => JOBS.push({ name, count, make });

for (const [name, o] of Object.entries(GUNS)) job(name, 3, () => gun(o));
job('rocket', 2, rocket);
job('plasma', 2, plasma);
job('tesla', 2, tesla);
job('flame', 3, flame);
job('explosion', 2, explosion);
job('reload', 2, reload);
job('pump', 2, pump);
job('bolt', 1, bolt);
job('empty', 2, empty);
job('swap', 2, handling);
job('pickup', 1, pickup);
job('shell', 4, () => shell(true));
job('shell_plastic', 3, () => shell(false));
job('hit_flesh', 4, hitFlesh);
job('hit_wall', 3, () => hitWall(false));
job('ricochet', 2, () => hitWall(true));
job('grenade_bounce', 3, grenadeBounce);
job('death', 2, death);
job('heartbeat', 1, heartbeat);
job('swing', 3, swing);
job('throw', 2, throwSound);
job('dash', 2, dash);
job('whiz', 3, whiz);
for (const s of Object.keys(SURFACES)) job('step_' + s, 6, () => step(s));

fs.mkdirSync(OUT, { recursive: true });
if (PREVIEW) fs.mkdirSync(PREVIEW, { recursive: true });
let total = 0, files = 0;
JOBS.forEach((j, ji) => {
  for (let v = 0; v < j.count; v++) {
    seed(ji * 100 + v + 1);
    const data = j.make();
    const file = path.join(OUT, `${j.name}_${v}.wav`);
    wav(file, data);
    total += data.length * 2 + 44;
    files++;
    if (PREVIEW && v === 0) preview(path.join(PREVIEW, `${j.name}.png`), data);
  }
});
console.log(`${files} files, ${(total / 1024 / 1024).toFixed(2)} MB -> ${OUT}`);
