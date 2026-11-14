# The physics and maths in T-MINUS

Every formula below is the one the game runs. Each one names the file where it lives, so you can check it. Units are SI (metres, seconds, kilograms, newtons, pascals) unless marked.

Teaching note: each formula is simplified on purpose. T-MINUS is a 2D game, not a flight simulator. Where a number is a game value rather than a measured one, the text says so.

## 1. Constants

| Symbol | Value | Meaning | File |
|---|---|---|---|
| μ | 3.986004418 × 10¹⁴ m³/s² | Earth's gravitational parameter (G × M) | `Flight/FlightConstants.cs` |
| R⊕ | 6 371 000 m | Earth's radius | `Flight/FlightConstants.cs` |
| ω⊕ | 7.2921159 × 10⁻⁵ rad/s | Earth's rotation rate | `Flight/FlightConstants.cs` |
| g₀ | 9.80665 m/s² | Standard gravity | `Flight/FlightConstants.cs` |
| p₀ | 101 325 Pa | Sea-level air pressure | `Flight/FlightConstants.cs` |
| H | 8 500 m | Atmosphere scale height | `Flight/FlightConstants.cs` |
| — | 150 km | Top of the atmosphere in the model | `Flight/FlightConstants.cs` |
| a_sound | 340 m/s | Speed of sound (used for Mach number) | `Flight/FlightConstants.cs` |
| Δt | 0.05 s | Fixed integration step | `Flight/FlightConstants.cs` |

## 2. Flight: the two-dimensional solver

The rocket moves in a plane around a round, rotating Earth. Position **p** and velocity **v** are vectors (x, y), with the origin at Earth's centre.

### 2.1 Equations of motion

Newton's second law, with the forces summed per unit mass:

**a** = **g** + **a**_drag + **T̂** · (T / m)

- **Gravity:** **g** = −μ **p** / |**p**|³ (inverse-square law, pointing at Earth's centre).
- **Drag:** **a**_drag = −(½ ρ |**v**_rel|² C_d A / m) · **v̂**_rel, where **v**_rel = **v** − **v**_air.
- **Thrust:** T̂ is the thrust direction (set by guidance), and T / m is the acceleration it gives.

`Flight/FlightSim.cs`, method `Derivative`.

The state is integrated with classical fourth-order Runge–Kutta (RK4), using the fixed step Δt = 0.05 s:

- k₁ = f(y), k₂ = f(y + ½Δt k₁), k₃ = f(y + ½Δt k₂), k₄ = f(y + Δt k₃)
- y(t + Δt) = y + (Δt / 6)(k₁ + 2k₂ + 2k₃ + k₄)

`FlightSim.cs`, method `Integrate`. Any leftover time from a frame is carried to the next one (`_advanceRemainder`), so a fast warp setting still steps by exactly 0.05 s.

### 2.2 Air

- **Air density:** ρ(h) = 1.225 · e^(−h / H) for 0 ≤ h < 150 km, otherwise 0.
- **Air pressure:** p(h) = p₀ · e^(−h / H).
- **Air velocity:** the air moves with the Earth. **v**_air = ω⊕ cos(φ) · (−y, x), where φ is the launch latitude.
- **Dynamic pressure (max-Q):** q = ½ ρ |**v**_rel|². This is the number that matters most for the rocket: the highest value during ascent is called max-Q.

`Flight/FlightPhysics.cs`.

### 2.3 Drag coefficient

The drag coefficient depends on the Mach number M = |**v**_rel| / a_sound:

- C_d = 0.6 if 0.8 ≤ M ≤ 1.2 (transonic), otherwise C_d = 0.4.

This is a simple step, not a measured curve. The real drag rises sharply through the speed of sound.

The reference area is the cross-section of the rocket: A = π D² / 4, where D is the widest diameter.

### 2.4 Engines and the rocket equation

- **Mass flow:** ṁ = min(T / (g₀ · I_sp), propellant left / Δt).
- **Thrust:** T = ṁ · g₀ · I_sp, where I_sp (specific impulse) is itself altitude-dependent:
  I_sp(h) = I_vac − (I_vac − I_sea) · p(h) / p₀.
- **Ideal velocity gain (the rocket equation):** Δv = I_sp · g₀ · ln(m₀ / m_f).
  Here m₀ is the starting mass and m_f the mass left at burnout. This is the number the game shows as "delta-v left".

`Flight/FlightSim.cs` (`Derivative`), `Flight/FlightPhysics.cs` (`RocketEquation`).

### 2.5 Orbit from position and velocity

From the state vectors the game computes the orbit's shape:

- **Specific orbital energy:** ε = ½ v² − μ / r.
- **Semi-major axis:** a = −μ / (2ε). The orbit is bound (an ellipse) when ε < 0.
- **Specific angular momentum:** h = x vᵧ − y vₓ.
- **Eccentricity:** e = √(1 + 2 ε h² / μ²).
- **Apoapsis** (highest point) = a (1 + e). **Periapsis** (lowest point) = a (1 − e).

`Flight/FlightModels.cs` (`OrbitElements`). A periapsis below the surface means the path still hits the ground; the game shows this as "below ground (suborbital)".

### 2.6 Guidance (the autopilot)

- **Pitch programme:** the rocket starts straight up, then tilts over as it climbs. The tilt is a function of altitude h:
  - the turn fraction is turn = clamp((h − 1 000 m) / turn span, 0, 1);
  - the pitch from vertical is θ = (pitch kick + turn · turn angle), in radians.
  - Thrust direction: **T̂** = cos θ · **r̂** + sin θ · **ê** (radial and east unit vectors). Above the turn end, the rocket points east.
- **Burn-out to coast:** the engines switch off when the apoapsis reaches the target radius.
- **Circularise:** the rocket burns sideways (along the velocity) at the apoapsis until the periapsis is at the target orbit.

The pitch kick (a few degrees, set in the Pitch kick slider) is the sideways push that starts the turn. Too little and the rocket climbs straight up and wastes fuel; too much and it dives into thick air, which raises max-Q.

`Flight/FlightGuidance.cs`.

### 2.7 Joint failure at max-Q

Every bolted joint has a limit set in `rules.json`:

- **Joint strength:** maxQ_limit × integrity, where the integrity (0 to 1) depends on the bolts (see section 5).
- **Failure:** if the dynamic pressure exceeds the joint's limit, the joint fails.

This is why a joint with missing or badly tightened bolts can break at max-Q, which is one of the main lessons in Judge Mode.

`Core/BoltMath.cs` (`FailureQkPa`), `Flight/FlightSim.cs` (`CheckJointFailure`).

## 3. Satellite power (the budget model)

### 3.1 Orbit period and eclipse

- **Orbital period:** T = 2π √(r³ / μ), where r = R⊕ + altitude. Converted to minutes.
- **Eclipse fraction** (worst case, the satellite is in Earth's shadow): f_eclipse = asin(R⊕ / r) / π.
- **Sunlit fraction:** 1 − f_eclipse.

`Core/SpacecraftBudget.cs` (`PeriodMinutes`, `EclipseFraction`).

Example: at 550 km, r ≈ 6 921 km, so the period is about 95 minutes and about 37% of it is in eclipse.

### 3.2 Power

- **Generation:** P_gen = Σ (peak panel power × sunlit fraction × pointing factor).
- **Consumption:** P_use = always-on load + imaging + transmitting.
- **Margin:** P_gen − P_use. The power light is yellow below 10% of consumption and red below zero.

### 3.3 Battery

- **Eclipse energy:** E_eclipse = P_use × (eclipse minutes / 60).
- **Battery needed:** E_eclipse / 0.3. The 0.3 is the maximum depth of discharge (30%), so the battery is never drained below 70%.

`SpacecraftBudget.cs`, lines around 115–120.

### 3.4 State of charge in operations

Each orbit, the battery changes by the net energy of that orbit:

- SoC(next) = clamp(SoC + (P_gen − P_use) · T / (60 · capacity), 0, 1).

The satellite takes an image only if SoC > 40%, the computer has not rebooted, and the pointing quality is above zero. `Mission/OpsSimulation.cs`, `StepStateOfCharge` and `CanImage`.

## 4. Data and imaging

- **Image size:** from the camera part's data (`imageMB`).
- **Storage and downlink:** data per day = images per day × image size. Images that do not fit in storage are lost. Downlink per day = rate × contacts × minutes per contact ÷ 8 ÷ 1000 (kbps to MB).
- **Ground sample distance (GSD):** the width of ground one pixel sees. GSD = GSD at 550 km × altitude / 550. Higher orbit means a coarser picture.
- **Pointing quality:** 1.0 with reaction wheels. With magnetorquers only, it drops to 0.3, so the image is blurred.

`Core/SpacecraftBudget.cs`, `Mission/OpsSimulation.cs`.

## 5. Bolts and joint integrity

Each joint has N bolts (for example 12 for the first-stage joint), each with a target torque T (in N·m) and a tolerance of ±10%.

- **Pass targets:** the torque is applied in three passes: 30%, 70% and 100% of the target.
- **Star pattern:** bolts are tightened across the circle, not round it, so the load spreads evenly. The order is built by `BoltMath.StarOrder`.
- **Bolt states:**
  - Missing, Inserted (finger-tight), Torqued (within ±10%), UnderTorqued (below), OverTorqued (above, which strips the thread).
- **Warping:** tightening a bolt fully while its neighbours are still loose warps the joint.
- **Integrity:** the share of bolts that are properly torqued, and the joint's strength falls with it.

`Core/BoltMath.cs`.

## 6. Scoring

The score is out of 100, with weights set per mission in `missions.json`. For example, FloodWatch-1 uses:

| Part | Weight | How it is measured |
|---|---|---|
| Mission success | 40 | 1 if deployed and the satellite survives 30 days |
| Science | 25 | images downlinked ÷ the goal (capped at 1) |
| Budget | 10 | 1 if the design is within budget |
| Design margins | 10 | share of the five budget checks that are not red |
| Decision quality | 10 | falls with known failures, NO-GO polls and hints used |
| Team roles | 5 | distinct crew roles used ÷ 4 |

Stars: 3 stars at 85 or more, 2 at 60 or more, 1 at 35 or more.

`Mission/Scoring.cs`.

## 7. The Failure Lab maths

- **Apollo 12 lightning job:** the score is the reward minus 60 points per wrong call, and half the reward is lost if the 20-second decision runs out.
- **Broken Pathfinder job:** the score is the reward scaled by faults fixed ÷ faults seeded, minus 10 points per HOUSTON hint, minus 50 points per fault left in.

`Mission/Apollo12Console.cs`, `Mission/FailureJobTracker.cs`.

## 8. What is a game value, not a measured one

Be honest about these when you present:

- The air model is a single exponential with a fixed scale height, and drag uses a two-value step for C_d.
- Engine data, part masses, battery capacities, and the joint strengths are illustrative values chosen to make the game teach something. They are not real rocket or satellite specifications. The rules say so in the data files and in the Space Guide.
- Solar generation uses a pointing factor (default 0.6), not a measured attitude model.
- The flight is in a plane, so there is no out-of-plane motion and no real wind or weather.

Real-world anchors used: Earth's μ, radius and rotation rate, standard gravity, the rocket equation, Kepler's laws (through the orbital period and orbit elements), and the Landsat and Blue Marble data for the images.

## 9. Where to look in the code

| Topic | File |
|---|---|
| Constants | `Scripts/Flight/FlightConstants.cs` |
| Air and drag | `Scripts/Flight/FlightPhysics.cs` |
| Equations of motion, RK4, staging, joint failure | `Scripts/Flight/FlightSim.cs` |
| Autopilot and pitch programme | `Scripts/Flight/FlightGuidance.cs` |
| Orbit from state vectors | `Scripts/Flight/FlightModels.cs` |
| Power, eclipse, budget | `Scripts/Core/SpacecraftBudget.cs` |
| Operations over 30 days | `Scripts/Mission/OpsSimulation.cs` |
| Bolts, star pattern, integrity | `Scripts/Core/BoltMath.cs` |
| Score | `Scripts/Mission/Scoring.cs` |
| Tests that pin these numbers | `Tests/EditMode/Flight/FlightPhysicsTests.cs`, `BudgetTests.cs`, `OpsTests.cs`, `BoltMathTests.cs` |
