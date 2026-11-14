// A faithful port of FlightSim / FlightGuidance / FlightPhysics (Assets/_TMinus/Scripts/Flight) to check whether a rocket reaches orbit.
const fs = require('fs');
const C = { Mu: 3.986004418e14, R: 6371000, W: 7.2921159e-5, G0: 9.80665, P0: 101325, H: 8500, ATM: 150000, CUT: 100000, DT: 0.05, SOUND: 340, CD: 0.4, CDT: 0.6 };
const V = {
  add: (a, b) => ({ x: a.x + b.x, y: a.y + b.y }), sub: (a, b) => ({ x: a.x - b.x, y: a.y - b.y }), mul: (a, s) => ({ x: a.x * s, y: a.y * s }),
  mag: (a) => Math.hypot(a.x, a.y), dot: (a, b) => a.x * b.x + a.y * b.y,
  norm: (a) => { const m = Math.hypot(a.x, a.y); return m > 0 ? { x: a.x / m, y: a.y / m } : { x: 0, y: 0 }; },
};
const alt = (p) => V.mag(p) - C.R;
const density = (h) => (h >= C.ATM || h < 0 ? 0 : 1.225 * Math.exp(-h / C.H));
const pressure = (h) => (h >= C.ATM || h < 0 ? 0 : C.P0 * Math.exp(-h / C.H));
const airVel = (p, lat) => { const w = C.W * Math.cos(lat * Math.PI / 180); return { x: -w * p.y, y: w * p.x }; };
const cd = (v) => { const m = v / C.SOUND; return m >= 0.8 && m <= 1.2 ? C.CDT : C.CD; };
function orbitEl(p, v) {
  const r = V.mag(p), e = V.dot(v, v) * 0.5 - C.Mu / r;
  const a = Math.abs(e) < 1e-12 ? Infinity : -C.Mu / (2 * e);
  const h = p.x * v.y - p.y * v.x;
  const ecc = Math.sqrt(Math.max(0, 1 + 2 * e * h * h / (C.Mu * C.Mu)));
  const bound = e < 0 && isFinite(a);
  return { bound, ap: bound ? a * (1 + ecc) : Infinity, pe: bound ? a * (1 - ecc) : -Infinity };
}

function fly(rocketId, opts) {
  const data = opts.rocketData || JSON.parse(fs.readFileSync('' + require('path').join(__dirname, '..', '..', 'Assets', '_TMinus', 'Resources', 'Data', 'rockets.json') + '', 'utf8')).rockets.find(r => r.id === rocketId);
  const stages = data.stages.map(s => ({ id: s.id, dry: s.dryMassKg, prop: s.propMassKg, thrustVac: s.thrustVacKN * 1000, ispVac: s.ispVac, ispSl: s.ispSl, d: s.diameterM }));
  const comps = (data.components || []).map(c => ({ id: c.id, mass: c.massKg, drop: c.dropWithStage, d: c.diameterM || 0 }));
  const payload = opts.payload !== undefined ? opts.payload : data.referencePayloadKg;
  const maxD = Math.max(...stages.map(s => s.d), ...comps.map(c => c.d), 0.01);
  const lat = opts.lat, lon = opts.lon * Math.PI / 180;
  const target = opts.targetKm;
  let mass = payload + stages.reduce((m, s) => m + s.dry + s.prop, 0) + comps.reduce((m, c) => m + c.mass, 0);
  const fuel = stages.map(s => s.prop), attached = stages.map(() => true), compAtt = comps.map(() => true);
  let stage = 0, t = 0, phase = 'Pad', maxQ = 0, kickDone = false, ignited = true, maxAlt = 0;
  const rot = C.W * Math.cos(lat * Math.PI / 180);
  let p = { x: C.R * Math.cos(lon), y: C.R * Math.sin(lon) };
  let v = V.mul({ x: -Math.sin(lon), y: Math.cos(lon) }, rot * C.R);
  const log = [];

  function guidance() {
    const rad = V.norm(p), east = { x: -rad.y, y: rad.x };
    const rel = V.sub(v, airVel(p, lat)), h = alt(p), relSpeed = V.mag(rel);
    const targetR = C.R + target * 1000;
    let dir = rad, thr = 1;
    if (opts.suborbital) {
      // A sounding rocket: no orbit logic. Straight up for the first 300 m, then a fixed small tilt towards the east.
      if (phase === 'Pad') { const s0 = stages[stage]; if (s0.thrustVac * s0.ispSl / s0.ispVac > mass * C.G0) phase = 'Ascent'; }
      const k = opts.kick * Math.PI / 180;
      return { thr: 1, dir: V.norm(h < 300 ? rad : V.add(V.mul(rad, Math.cos(k)), V.mul(east, Math.sin(k)))) };
    }
    if (phase === 'Pad') {
      const s = stages[stage], slT = s.thrustVac * s.ispSl / s.ispVac;
      if (slT > mass * C.G0) phase = 'Ascent';
    }
    if (phase === 'Ascent') {
      const o = orbitEl(p, v);
      if (h >= C.CUT && o.bound && o.ap >= targetR && V.dot(rel, rad) > 0) { phase = 'Coast'; thr = 0; }
      else if (relSpeed < 100 && h < 1000) dir = rad;
      else {
        const turn = Math.max(0, Math.min(1, (h - 1000) / (opts.turnSpan || 44000)));
        const pitch = ((opts.kick) + turn * (opts.turnDeg || 60)) * Math.PI / 180;
        dir = h >= (opts.turnEnd || 45000) ? east : V.add(V.mul(rad, Math.cos(pitch)), V.mul(east, Math.sin(pitch)));
        if (opts.altHold && h >= (opts.turnEnd || 45000) && stage < stages.length) {
          // Thrust-to-weight below 1: pointing the thrust level makes the vehicle sink. Tilt it up just enough to cancel the sinking.
          const s = stages[stage], isp = s.ispVac - (s.ispVac - s.ispSl) * pressure(h) / C.P0;
          const a = (s.thrustVac * isp / s.ispVac) / Math.max(1, mass);
          const r = V.mag(p), g = C.Mu / (r * r), vh = V.dot(rel, east), vr = V.dot(rel, rad);
          const need = g - vh * vh / r - (opts.vrGain || 0.02) * vr;       // upward acceleration still needed
          const sin = Math.max(0, Math.min(opts.maxClimb || 0.8, need / a));
          if (a < 1.5 * g || sin > 0.05) dir = V.add(V.mul(rad, sin), V.mul(east, Math.sqrt(1 - sin * sin)));
        }
      }
    }
    if (phase === 'Coast') {
      thr = 0;
      if (V.dot(rel, rad) <= 0) { phase = 'Circularize'; thr = 1; dir = V.norm(v); }
    } else if (phase === 'Circularize') {
      dir = V.norm(v);
      const o = orbitEl(p, v);
      if (o.bound && o.pe >= targetR - 10000) { phase = 'Orbit'; thr = 0; }
    }
    if (phase === 'Orbit') thr = 0;
    return { thr, dir: V.norm(dir) };
  }

  function deriv(st, cmd) {
    const r = Math.max(1, V.mag(st.p)), h = r - C.R;
    const g = V.mul(st.p, -C.Mu / (r * r * r));
    const rel = V.sub(st.v, airVel(st.p, lat)), rho = density(h), sp = V.mag(rel);
    const area = Math.PI * maxD * maxD / 4;
    const drag = 0.5 * rho * sp * sp * cd(sp) * area;
    const da = st.m > 0 && sp > 0 ? V.mul(rel, -drag / (sp * st.m)) : { x: 0, y: 0 };
    let thrust = 0, mdot = 0;
    if (stage < stages.length && ignited && st.f > 0 && cmd.thr > 0) {
      const s = stages[stage];
      const nominal = s.thrustVac / (C.G0 * s.ispVac) * cmd.thr;
      mdot = Math.min(nominal, st.f / C.DT);
      const isp = s.ispVac - (s.ispVac - s.ispSl) * pressure(h) / C.P0;
      thrust = mdot * C.G0 * isp;
    }
    const acc = V.add(V.add(g, da), V.mul(cmd.dir, thrust / Math.max(1, st.m)));
    return { p: st.v, v: acc, m: -mdot, f: -mdot };
  }
  const addS = (a, b) => ({ p: V.add(a.p, b.p), v: V.add(a.v, b.v), m: a.m + b.m, f: a.f + b.f });
  const mulS = (a, s) => ({ p: V.mul(a.p, s), v: V.mul(a.v, s), m: a.m * s, f: a.f * s });

  while (t < (opts.maxT || 2400) && !['Orbit', 'Failed', 'Apogee'].includes(phase)) {
    if (stage < stages.length && fuel[stage] <= 1e-9 && attached[stage]) {
      if (stage + 1 < stages.length) {
        attached[stage] = false;
        mass -= stages[stage].dry + fuel[stage];
        comps.forEach((c, i) => { if (compAtt[i] && c.drop === stages[stage].id) { mass -= c.mass; compAtt[i] = false; } });
        log.push(`T+${t.toFixed(0)} ${stages[stage].id} separated at ${(alt(p) / 1000).toFixed(0)} km`);
        stage++;
      } else { attached[stage] = false; stage = stages.length; }
    }
    const cmd = guidance();
    const y = { p, v, m: mass, f: stage < stages.length ? fuel[stage] : 0 };
    const k1 = deriv(y, cmd), k2 = deriv(addS(y, mulS(k1, C.DT / 2)), cmd), k3 = deriv(addS(y, mulS(k2, C.DT / 2)), cmd), k4 = deriv(addS(y, mulS(k3, C.DT)), cmd);
    const n = addS(y, mulS(addS(addS(k1, mulS(k2, 2)), addS(mulS(k3, 2), k4)), C.DT / 6));
    const newF = Math.max(0, Math.min(y.f, n.f)), used = y.f - newF;
    p = n.p; v = n.v; mass = Math.max(1, y.m - used);
    if (stage < stages.length) fuel[stage] = newF;
    if (phase === 'Pad') { const rad = V.norm(p); p = V.mul(rad, C.R); v = V.sub(v, V.mul(rad, V.dot(v, rad))); }
    t += C.DT;
    const q = 0.5 * density(alt(p)) * Math.pow(V.mag(V.sub(v, airVel(p, lat))), 2);
    if (q > maxQ) maxQ = q;
    if (alt(p) > maxAlt) maxAlt = alt(p);
    if (opts.suborbital && t > 3 && V.dot(v, V.norm(p)) <= 0 && alt(p) > 1000) { phase = 'Apogee'; }
    if (alt(p) < -10 && t > 2) { phase = 'Failed'; log.push('IMPACT'); }
  }
  const o = orbitEl(p, v);
  const dvLeft = (() => { let m = mass, sum = 0; for (let i = Math.max(0, stage); i < stages.length; i++) { const s = stages[i]; const f = fuel[i]; if (f <= 0 || m <= f) { m -= s.dry; continue; } sum += s.ispVac * C.G0 * Math.log(m / (m - f)); m -= f + s.dry; comps.forEach((c, j) => { if (c.drop === s.id) m -= c.mass; }); } return sum; })();
  return { phase, apogeeKm: +(maxAlt / 1000).toFixed(1), maxAccelG: 0, t: +t.toFixed(0), altKm: +(alt(p) / 1000).toFixed(0), apKm: +((o.ap - C.R) / 1000).toFixed(0), peKm: +((o.pe - C.R) / 1000).toFixed(0), maxQkPa: +(maxQ / 1000).toFixed(1), stage, dvLeft: +dvLeft.toFixed(0), log };
}
module.exports = { fly };
if (require.main === module) {
  console.log('Pathfinder (sanity: should reach orbit at 550 km, max-Q ~40.6 kPa):');
  console.log(JSON.stringify(fly('pathfinder', { lat: 25.2, lon: 66.75, targetKm: 550, kick: 3 })));
  console.log('Saturn V, game autopilot as it is:');
  console.log(JSON.stringify(fly('saturn_v', { lat: 28.5, lon: -80.6, targetKm: 185, kick: 3 })));
}
