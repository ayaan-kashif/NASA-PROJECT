const { fly } = require('./simport.js');

const path = fly('pathfinder', { lat: 25.2, lon: 66.75, targetKm: 550, kick: 3, maxT: 8000 });
console.log('Pathfinder sanity (game reports orbit about 542x545 km, max-Q 40.6 kPa):', JSON.stringify({ phase: path.phase, t: path.t, ap: path.apKm, pe: path.peKm, q: path.maxQkPa }));

let best = null, tried = 0;
for (const kick of [1, 2, 3, 5, 8]) {
  for (const turnSpan of [44000, 80000, 120000, 200000]) {
    for (const turnEnd of [45000, 90000, 140000]) {
      for (const turnDeg of [60, 75, 88]) {
        const r = fly('saturn_v', { lat: 28.5, lon: -80.6, targetKm: 185, kick, turnSpan, turnEnd, turnDeg, maxT: 1600 });
        tried++;
        const score = (r.phase === 'Orbit' ? 1e6 : 0) + (r.phase === 'Failed' ? -1e5 : 0) + Math.min(r.peKm, 185) * 100 + r.dvLeft;
        if (!best || score > best.score) best = { score, kick, turnSpan, turnEnd, turnDeg, r };
      }
    }
  }
}
console.log('tried', tried, 'Saturn V pitch programs; best:');
console.log(JSON.stringify({ kick: best.kick, turnSpan: best.turnSpan, turnEnd: best.turnEnd, turnDeg: best.turnDeg, phase: best.r.phase, t: best.r.t, ap: best.r.apKm, pe: best.r.peKm, maxQ: best.r.maxQkPa, dvLeft: best.r.dvLeft, log: best.r.log }));
