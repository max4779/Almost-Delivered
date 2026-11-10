// Original synthesized sounds: no recordings, sample libraries or external audio.
// Run `node Tools/generate_weapon_sounds.js` to reproduce the three combat WAV files.
const fs = require('fs');
const path = require('path');

const rate = 44100;
const output = path.join(__dirname, '..', 'Assets', 'AlmostDelivered', 'Art', 'Audio', 'Weapons');
fs.mkdirSync(output, { recursive: true });

function generate(name, duration, seed, revolver) {
  let state = seed >>> 0;
  const random = () => {
    state = (Math.imul(state, 1664525) + 1013904223) >>> 0;
    return state / 0x100000000 * 2 - 1;
  };
  const count = Math.floor(duration * rate);
  const samples = new Float64Array(count);
  let filtered = 0;
  let peak = 0;
  for (let i = 0; i < count; i++) {
    const t = i / rate;
    const noise = random();
    filtered = filtered * 0.72 + noise * 0.28;
    const crack = (revolver ? noise * 0.42 + filtered * 0.58 : noise)
      * Math.exp(-t * (revolver ? 75 : 140));
    const frequency = (revolver ? 90 : 155) * Math.exp(-t * (revolver ? 11 : 15))
      + (revolver ? 45 : 55);
    const thump = Math.sin(2 * Math.PI * frequency * t) * Math.exp(-t * (revolver ? 10 : 25));
    const sub = Math.sin(2 * Math.PI * 62 * t) * Math.exp(-t * 9);
    const tail = filtered * Math.exp(-t * (revolver ? 9 : 29));
    const click = Math.sin(2 * Math.PI * 1800 * t) * Math.exp(-t * 160);
    const value = revolver
      ? 0.38 * crack + 0.82 * thump + 0.48 * sub + 0.2 * tail + 0.06 * click
      : 0.55 * crack + 0.31 * thump + 0.12 * tail + 0.13 * click;
    samples[i] = value;
    peak = Math.max(peak, Math.abs(value));
  }

  const wav = Buffer.alloc(44 + count * 2);
  wav.write('RIFF', 0);
  wav.writeUInt32LE(wav.length - 8, 4);
  wav.write('WAVEfmt ', 8);
  wav.writeUInt32LE(16, 16);
  wav.writeUInt16LE(1, 20);
  wav.writeUInt16LE(1, 22);
  wav.writeUInt32LE(rate, 24);
  wav.writeUInt32LE(rate * 2, 28);
  wav.writeUInt16LE(2, 32);
  wav.writeUInt16LE(16, 34);
  wav.write('data', 36);
  wav.writeUInt32LE(count * 2, 40);
  for (let i = 0; i < count; i++) {
    wav.writeInt16LE(Math.round(samples[i] / peak * 0.9 * 32767), 44 + i * 2);
  }
  fs.writeFileSync(path.join(output, name), wav);
}

function generateSlash() {
  let state = 4821;
  const random = () => {
    state = (Math.imul(state, 1664525) + 1013904223) >>> 0;
    return state / 0x100000000 * 2 - 1;
  };
  const duration = 0.42;
  const count = Math.floor(duration * rate);
  const samples = new Float64Array(count);
  let upperBand = 0;
  let lowerBand = 0;
  let sweepPhase = 0;
  let shimmerPhase = 0;
  let peak = 0;
  for (let i = 0; i < count; i++) {
    const t = i / rate;
    const noise = random();
    upperBand = upperBand * 0.42 + noise * 0.58;
    lowerBand = lowerBand * 0.91 + noise * 0.09;
    const swish = Math.exp(-Math.pow((t - 0.075) / 0.049, 2))
      + 0.48 * Math.exp(-Math.pow((t - 0.17) / 0.075, 2));
    const airCut = (upperBand - lowerBand) * swish * 0.38;
    const ringTime = Math.max(0, t - 0.035);
    const ringEnvelope = t < 0.035 ? t / 0.035 : Math.exp(-ringTime * 7.8);
    sweepPhase += 2 * Math.PI * (2350 + 750 * Math.min(t / 0.18, 1)) / rate;
    shimmerPhase += 2 * Math.PI * (4050 - 450 * Math.min(t / 0.32, 1)) / rate;
    const metal = (Math.sin(sweepPhase) * 0.16
      + Math.sin(shimmerPhase + 0.7) * 0.085
      + Math.sin(2 * Math.PI * 5020 * t + 1.3) * 0.035) * ringEnvelope;
    const edge = (noise - lowerBand) * Math.exp(-t * 85) * 0.07;
    const tail = (upperBand - lowerBand) * Math.exp(-Math.max(0, t - 0.16) * 16)
      * Math.min(1, t / 0.16) * 0.035;
    const value = airCut + metal + edge + tail;
    samples[i] = value;
    peak = Math.max(peak, Math.abs(value));
  }

  const wav = Buffer.alloc(44 + count * 2);
  wav.write('RIFF', 0);
  wav.writeUInt32LE(wav.length - 8, 4);
  wav.write('WAVEfmt ', 8);
  wav.writeUInt32LE(16, 16);
  wav.writeUInt16LE(1, 20);
  wav.writeUInt16LE(1, 22);
  wav.writeUInt32LE(rate, 24);
  wav.writeUInt32LE(rate * 2, 28);
  wav.writeUInt16LE(2, 32);
  wav.writeUInt16LE(16, 34);
  wav.write('data', 36);
  wav.writeUInt32LE(count * 2, 40);
  for (let i = 0; i < count; i++)
    wav.writeInt16LE(Math.round(samples[i] / peak * 0.72 * 32767), 44 + i * 2);
  fs.writeFileSync(path.join(output, 'MeleeSlash.wav'), wav);
}

generate('AutomaticRifleShot.wav', 0.18, 4779, false);
generate('RevolverShot.wav', 0.38, 4780, true);
generateSlash();
