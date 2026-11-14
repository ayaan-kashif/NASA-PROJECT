const { fly } = require('./simport.js');
// A two-stage solid sounding rocket in the spirit of Rehbar-I (Nike booster + Cajun). The numbers are game values, tuned here so that the
// flight reaches about 130 km. Run: node tools/sim/rehbar.js
function rocket(n, c, payload) {
  return { id: 'rehbar', referencePayloadKg: payload, components: [], stages: [
    { id: 'rb_nike', dryMassKg: n.dry, propMassKg: n.prop, thrustVacKN: n.thrust * 1.06, ispVac: n.isp * 1.06, ispSl: n.isp, diameterM: 0.42 },
    { id: 'rb_cajun', dryMassKg: c.dry, propMassKg: c.prop, thrustVacKN: c.thrust, ispVac: c.isp, ispSl: c.isp * 0.93, diameterM: 0.30 } ] };
}
const results = [];
for (const nikeProp of [250, 300, 350, 400]) for (const cajunProp of [90, 120, 150, 180]) for (const kick of [1, 2, 3]) {
  const r = rocket({ dry: 120, prop: nikeProp, thrust: 217, isp: 210 }, { dry: 40, prop: cajunProp, thrust: 36, isp: 225 }, 36);
  const f = fly('rehbar', { rocketData: r, lat: 25.2, lon: 66.75, targetKm: 130, kick, suborbital: true, maxT: 600 });
  results.push({ nikeProp, cajunProp, kick, phase: f.phase, apogee: f.apogeeKm, maxQ: f.maxQkPa, t: f.t });
}
results.sort((a, b) => Math.abs(a.apogee - 130) - Math.abs(b.apogee - 130));
console.log(results.slice(0, 8).map(r => JSON.stringify(r)).join('\n'));
