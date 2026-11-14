const { fly } = require('./simport.js');
let best = null, tried = 0;
for (const kick of [1, 2, 3, 5]) for (const turnSpan of [44000, 80000, 120000]) for (const turnEnd of [45000, 70000, 100000])
  for (const turnDeg of [60, 75, 88]) for (const vrGain of [0, 0.02, 0.05]) for (const maxClimb of [0.6, 0.8, 0.95]) {
    const r = fly('saturn_v', { lat: 28.5, lon: -80.6, targetKm: 185, kick, turnSpan, turnEnd, turnDeg, altHold: true, vrGain, maxClimb, maxT: 1500 });
    tried++;
    const ok = r.phase === 'Orbit' || (r.phase === 'Coast' && r.peKm > 100);
    const score = (r.phase === 'Orbit' ? 1e6 : 0) + (r.phase === 'Failed' ? -1e5 : 0) + Math.min(r.peKm, 185) * 100 + r.dvLeft + (ok ? 1e5 : 0);
    if (!best || score > best.score) best = { score, kick, turnSpan, turnEnd, turnDeg, vrGain, maxClimb, r };
  }
console.log('tried', tried, 'programs with altitude-hold guidance; best:');
console.log(JSON.stringify({ kick: best.kick, turnSpan: best.turnSpan, turnEnd: best.turnEnd, turnDeg: best.turnDeg, vrGain: best.vrGain, maxClimb: best.maxClimb, phase: best.r.phase, t: best.r.t, ap: best.r.apKm, pe: best.r.peKm, maxQ: best.r.maxQkPa, dvLeft: best.r.dvLeft, log: best.r.log }));
