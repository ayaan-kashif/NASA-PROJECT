const { fly } = require('./simport.js');
let best = null, tried = 0, orbits = 0;
for (const kick of [2, 3, 5, 7]) for (const turnSpan of [30000, 44000, 60000]) for (const turnEnd of [60000, 80000, 100000, 130000])
  for (const turnDeg of [50, 60, 70]) for (const vrGain of [0, 0.01, 0.03]) for (const maxClimb of [0.4, 0.5, 0.6, 0.7]) {
    const r = fly('saturn_v', { lat: 28.5, lon: -80.6, targetKm: 185, kick, turnSpan, turnEnd, turnDeg, altHold: true, vrGain, maxClimb, maxT: 1400 });
    tried++;
    if (r.phase !== 'Orbit') continue;
    orbits++;
    if (!best || r.dvLeft > best.r.dvLeft) best = { kick, turnSpan, turnEnd, turnDeg, vrGain, maxClimb, r };
  }
console.log('tried', tried, '; reached orbit in', orbits);
console.log(JSON.stringify(best && { kick: best.kick, turnSpan: best.turnSpan, turnEnd: best.turnEnd, turnDeg: best.turnDeg, vrGain: best.vrGain, maxClimb: best.maxClimb, ap: best.r.apKm, pe: best.r.peKm, maxQ: best.r.maxQkPa, dvLeft: best.r.dvLeft, t: best.r.t, log: best.r.log }));
