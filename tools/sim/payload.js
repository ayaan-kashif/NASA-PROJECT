const { fly } = require('./simport.js');
// How much does the CubeSat's real mass (a few kg) change the Pathfinder flight compared with the fixed 150 kg the game used?
for (const payload of [150, 6, 2.5, 1.5]) {
  const r = fly('pathfinder', { lat: 25.2, lon: 66.75, targetKm: 550, kick: 3, payload, maxT: 8000 });
  console.log('payload', payload, 'kg ->', r.phase, 'orbit', r.apKm + 'x' + r.peKm, 'km, max-Q', r.maxQkPa, 'kPa, t', r.t);
}
