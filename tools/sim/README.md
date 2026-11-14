# Flight solver check (no Unity needed)

`simport.js` is a line-by-line port of `FlightSim`, `FlightGuidance` and `FlightPhysics` to JavaScript. It reads the rockets from
`Assets/_TMinus/Resources/Data/rockets.json`, so you can ask "does this rocket reach orbit?" in a second:

    node tools/sim/simport.js     # Pathfinder and Saturn V with the game's autopilot
    node tools/sim/sweep.js       # Pathfinder sanity check, then a sweep of Saturn V pitch programs

Check it against the game: Pathfinder reaches about 550 x 548 km with max-Q 40.6 kPa, the same as the C# tests.

Finding (14 November 2026): **Saturn V cannot reach orbit with the game's autopilot, and no pitch program in the sweep fixes it.**
The S-II stage has a thrust-to-weight ratio below 1 when it lights (4,400 kN against about 640 tonnes), and the autopilot points the
thrust horizontally above 45 km, so the vehicle sinks back into the air. A real Saturn V keeps climbing during the S-II burn. The Moonshot
mission therefore needs a guidance law that holds altitude (pitch up while thrust-to-weight is below 1) before it can be offered. Try
that here first, then port it to `FlightGuidance.cs` and guard it so Pathfinder (whose stages are above 1) does not change.
