const { fly } = require('./simport.js');
// Saturn V with the profile stored in rockets.json: what does each pitch kick (the player's one choice) give?
for (const kick of [1, 2, 3, 4, 5, 6]) {
  const r = fly('saturn_v', { lat: 28.5, lon: -80.6, targetKm: 185, kick, turnSpan: 30000, turnEnd: 130000, turnDeg: 60, altHold: true, vrGain: 0.01, maxClimb: 0.4, maxT: 1500 });
  console.log('kick', kick, '->', r.phase, 'orbit', r.apKm + 'x' + r.peKm, 'km, dv left', r.dvLeft, 'm/s, max-Q', r.maxQkPa, 'kPa, t', r.t);
}
