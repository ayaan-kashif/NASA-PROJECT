const { fly } = require('./simport.js');
function rocket(nProp, cProp) {
  return { id: 'rehbar', referencePayloadKg: 36, components: [], stages: [
    { id: 'rb_nike', dryMassKg: 120, propMassKg: nProp, thrustVacKN: 217 * 1.06, ispVac: 210 * 1.06, ispSl: 210, diameterM: 0.42 },
    { id: 'rb_cajun', dryMassKg: 40, propMassKg: cProp, thrustVacKN: 36, ispVac: 225, ispSl: 225 * 0.93, diameterM: 0.30 } ] };
}
let best = null;
for (let n = 320; n <= 360; n += 5) for (let c = 140; c <= 180; c += 5) {
  const per = [1, 2, 3, 4, 5, 6].map(k => fly('rehbar', { rocketData: rocket(n, c), lat: 25.2, lon: 66.75, targetKm: 130, kick: k, suborbital: true, maxT: 600 }).apogeeKm);
  const worst = Math.min(...per), spread = Math.max(...per) - worst;
  const score = -Math.abs(per[2] - 132) - (worst < 120 ? 50 : 0);
  if (!best || score > best.score) best = { score, n, c, per };
}
console.log(JSON.stringify(best));
