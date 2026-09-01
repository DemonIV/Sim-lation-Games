using UnityEngine;

namespace Sim.Runtime
{
    /// <summary>
    /// Builds recognisable vehicle silhouettes out of Unity primitives and parents them under a single
    /// child named "Model" of the unit root. Purely cosmetic:
    /// <list type="bullet">
    ///   <item>every generated part has its Collider destroyed, so models never add physics;</item>
    ///   <item>the unit root keeps its own collider and transform — gameplay is untouched;</item>
    ///   <item>local +Z is forward, matching how the controllers orient the root.</item>
    /// </list>
    /// </summary>
    public static class VehicleModelBuilder
    {
        // ---------------------------------------------------------------- public API

        /// <summary>
        /// Unarmed recon UAV (Keşif İHA): a glider-like, high-aspect-ratio airframe — ~7.1 m span on
        /// a 0.66..0.78 m chord — with a satcom dome, a chin sensor ball, an up-canted V-tail and a
        /// pusher propeller. Returns the "Model" root transform.
        ///
        /// <para>Family cues shared with the SİHA and the jet: "Fuselage" is a DIRECT child of
        /// "Model" (<c>CameraRig.TryReadBodyColor</c> uses a non-recursive <c>Find</c>), the sensor
        /// ball hangs off the unscaled "Turret" pivot and the satcom dome is the "Radar" part. Its
        /// livery is the LIGHTEST of the three: the wing skin uses the accent tint rather than the
        /// darker trim, which is what makes the recon bird read as the unarmed one at a distance.</para>
        /// </summary>
        public static Transform BuildReconUav(Transform root, Color primary)
        {
            Transform model = CreateModelRoot(root);
            if (model == null) return null;

            Material body = Body(primary);
            Material trim = Trim(primary);
            Material accent = Accent(primary);
            Material dark = DarkMetal();

            // ---- slender fuselage pod with a bulbous avionics nose.
            Part(model, PrimitiveType.Cylinder, "Fuselage", new Vector3(0f, 0f, 0.10f), new Vector3(0.34f, 1.50f, 0.34f), new Vector3(90f, 0f, 0f), body);
            Part(model, PrimitiveType.Sphere, "NoseCone", new Vector3(0f, 0f, 1.60f), new Vector3(0.44f, 0.40f, 0.62f), Vector3.zero, body);
            // "Radar" is the satcom dome. Nothing spins it on an aircraft — the name only matters
            // where a TurretVisual is attached — but it keeps the naming convention intact.
            Part(model, PrimitiveType.Sphere, "Radar", new Vector3(0f, 0.24f, 0.70f), new Vector3(0.46f, 0.40f, 0.46f), Vector3.zero, accent);

            // ---- chin sensor ball on the unscaled pivot convention (see the SAM site).
            Transform turret = Pivot(model, "Turret", new Vector3(0f, -0.28f, 1.15f));
            Part(turret, PrimitiveType.Sphere, "TurretBody", Vector3.zero, new Vector3(0.40f, 0.40f, 0.40f), Vector3.zero, dark);

            // ---- three-piece wing. The outer panels take a few degrees of dihedral: a +Z rotation
            // lifts the RIGHT panel's tip, so the left one mirrors it with the opposite sign.
            Part(model, PrimitiveType.Cube, "WingCenter", new Vector3(0f, 0.18f, -0.05f), new Vector3(2.20f, 0.10f, 0.78f), Vector3.zero, accent);
            Part(model, PrimitiveType.Cube, "WingL", new Vector3(-2.30f, 0.24f, -0.05f), new Vector3(2.50f, 0.09f, 0.66f), new Vector3(0f, 0f, -5f), accent);
            Part(model, PrimitiveType.Cube, "WingR", new Vector3(2.30f, 0.24f, -0.05f), new Vector3(2.50f, 0.09f, 0.66f), new Vector3(0f, 0f, 5f), accent);
            Part(model, PrimitiveType.Cube, "AileronL", new Vector3(-2.35f, 0.24f, -0.44f), new Vector3(2.20f, 0.05f, 0.20f), new Vector3(0f, 0f, -5f), trim);
            Part(model, PrimitiveType.Cube, "AileronR", new Vector3(2.35f, 0.24f, -0.44f), new Vector3(2.20f, 0.05f, 0.20f), new Vector3(0f, 0f, 5f), trim);
            Part(model, PrimitiveType.Cube, "WingtipFinL", new Vector3(-3.50f, 0.55f, -0.05f), new Vector3(0.08f, 0.45f, 0.50f), new Vector3(0f, 0f, -5f), trim);
            Part(model, PrimitiveType.Cube, "WingtipFinR", new Vector3(3.50f, 0.55f, -0.05f), new Vector3(0.08f, 0.45f, 0.50f), new Vector3(0f, 0f, 5f), trim);

            // ---- tail boom, comms blade and the up-canted V-tail.
            Part(model, PrimitiveType.Cylinder, "TailBoom", new Vector3(0f, 0.02f, -2.00f), new Vector3(0.16f, 0.62f, 0.16f), new Vector3(90f, 0f, 0f), body);
            Part(model, PrimitiveType.Cube, "CommsAntenna", new Vector3(0f, 0.32f, -1.55f), new Vector3(0.04f, 0.34f, 0.04f), Vector3.zero, dark);
            Part(model, PrimitiveType.Cube, "VTailL", new Vector3(-0.33f, 0.50f, -2.32f), new Vector3(0.08f, 1.05f, 0.55f), new Vector3(0f, 0f, 35f), trim);
            Part(model, PrimitiveType.Cube, "VTailR", new Vector3(0.33f, 0.50f, -2.32f), new Vector3(0.08f, 1.05f, 0.55f), new Vector3(0f, 0f, -35f), trim);

            // ---- pusher propeller. "Propeller" is the part PropellerSpinner rotates about local Z,
            // so it stays a single blade-pair cube: a child would be sheared by its own scale.
            Part(model, PrimitiveType.Cylinder, "PropHub", new Vector3(0f, 0.02f, -2.62f), new Vector3(0.18f, 0.08f, 0.18f), new Vector3(90f, 0f, 0f), dark);
            Part(model, PrimitiveType.Cube, "Propeller", new Vector3(0f, 0.02f, -2.68f), new Vector3(0.10f, 1.90f, 0.04f), Vector3.zero, dark);
            Part(model, PrimitiveType.Sphere, "SpinnerCone", new Vector3(0f, 0.02f, -2.76f), new Vector3(0.16f, 0.16f, 0.24f), Vector3.zero, trim);

            return model;
        }

        /// <summary>
        /// Armed UAV (SİHA): the same family as the recon bird, but heavier — a longer, fatter
        /// fuselage with a stepped nose, a dorsal satcom bulge, a bigger chin sensor ball, a straight
        /// wing with flaps and tip fins, an INVERTED-V tail on the boom, a pusher propeller and four
        /// underwing munitions.
        ///
        /// <para>Built standalone (it no longer decorates the recon airframe) so the two silhouettes
        /// can differ where a real armed UAV does: tail geometry, nose section and stores.</para>
        /// </summary>
        public static Transform BuildArmedUav(Transform root, Color primary)
        {
            Transform model = CreateModelRoot(root);
            if (model == null) return null;

            Material body = Body(primary);
            Material trim = Trim(primary);
            Material accent = Accent(primary);
            Material dark = DarkMetal();

            // ---- fuselage: main pod, stepped forward section, rounded nose.
            Part(model, PrimitiveType.Cylinder, "Fuselage", new Vector3(0f, 0f, 0.15f), new Vector3(0.42f, 1.45f, 0.44f), new Vector3(90f, 0f, 0f), body);
            Part(model, PrimitiveType.Cylinder, "NoseSection", new Vector3(0f, 0.02f, 1.80f), new Vector3(0.38f, 0.32f, 0.40f), new Vector3(90f, 0f, 0f), body);
            Part(model, PrimitiveType.Sphere, "NoseCone", new Vector3(0f, 0.02f, 2.20f), new Vector3(0.38f, 0.34f, 0.48f), Vector3.zero, body);
            Part(model, PrimitiveType.Sphere, "Radar", new Vector3(0f, 0.26f, 0.95f), new Vector3(0.42f, 0.36f, 0.42f), Vector3.zero, accent);

            Transform turret = Pivot(model, "Turret", new Vector3(0f, -0.32f, 1.35f));
            Part(turret, PrimitiveType.Sphere, "TurretBody", Vector3.zero, new Vector3(0.44f, 0.44f, 0.44f), Vector3.zero, dark);

            // ---- straight wing with a few degrees of dihedral, flaps and upturned tip fins.
            Part(model, PrimitiveType.Cube, "WingCenter", new Vector3(0f, 0.16f, -0.10f), new Vector3(2.00f, 0.11f, 0.95f), Vector3.zero, trim);
            Part(model, PrimitiveType.Cube, "WingL", new Vector3(-2.25f, 0.20f, -0.12f), new Vector3(2.60f, 0.10f, 0.80f), new Vector3(0f, 0f, -4f), trim);
            Part(model, PrimitiveType.Cube, "WingR", new Vector3(2.25f, 0.20f, -0.12f), new Vector3(2.60f, 0.10f, 0.80f), new Vector3(0f, 0f, 4f), trim);
            Part(model, PrimitiveType.Cube, "FlapL", new Vector3(-2.30f, 0.20f, -0.60f), new Vector3(2.40f, 0.06f, 0.22f), new Vector3(0f, 0f, -4f), accent);
            Part(model, PrimitiveType.Cube, "FlapR", new Vector3(2.30f, 0.20f, -0.60f), new Vector3(2.40f, 0.06f, 0.22f), new Vector3(0f, 0f, 4f), accent);
            Part(model, PrimitiveType.Cube, "WingtipFinL", new Vector3(-3.50f, 0.45f, -0.12f), new Vector3(0.09f, 0.50f, 0.50f), new Vector3(0f, 0f, -4f), trim);
            Part(model, PrimitiveType.Cube, "WingtipFinR", new Vector3(3.50f, 0.45f, -0.12f), new Vector3(0.09f, 0.50f, 0.50f), new Vector3(0f, 0f, 4f), trim);

            // ---- INVERTED V-tail: each fin runs down AND outward from the boom, so the pair forms
            // a V pointing down. That takes the mirror image of the recon V-tail's cant (a -35° Z
            // rotation puts the left fin's lower end out at -X) with the fin centred BELOW the
            // boom; its upper end then lands on the boom's surface at x = ±0.09.
            Part(model, PrimitiveType.Cylinder, "TailBoom", new Vector3(0f, 0f, -1.90f), new Vector3(0.20f, 0.65f, 0.20f), new Vector3(90f, 0f, 0f), body);
            Part(model, PrimitiveType.Cube, "VTailL", new Vector3(-0.38f, -0.41f, -2.30f), new Vector3(0.09f, 1.00f, 0.60f), new Vector3(0f, 0f, -35f), trim);
            Part(model, PrimitiveType.Cube, "VTailR", new Vector3(0.38f, -0.41f, -2.30f), new Vector3(0.09f, 1.00f, 0.60f), new Vector3(0f, 0f, 35f), trim);

            // ---- pusher propeller (same convention as the recon airframe).
            Part(model, PrimitiveType.Cylinder, "PropHub", new Vector3(0f, 0f, -2.62f), new Vector3(0.20f, 0.08f, 0.20f), new Vector3(90f, 0f, 0f), dark);
            Part(model, PrimitiveType.Cube, "Propeller", new Vector3(0f, 0f, -2.68f), new Vector3(0.11f, 1.80f, 0.045f), Vector3.zero, dark);
            Part(model, PrimitiveType.Sphere, "SpinnerCone", new Vector3(0f, 0f, -2.78f), new Vector3(0.18f, 0.18f, 0.26f), Vector3.zero, trim);

            // ---- four underwing stores: heavier inboard, lighter outboard.
            Part(model, PrimitiveType.Cube, "PylonL", new Vector3(-1.30f, -0.05f, -0.12f), new Vector3(0.10f, 0.30f, 0.50f), Vector3.zero, trim);
            Part(model, PrimitiveType.Cube, "PylonR", new Vector3(1.30f, -0.05f, -0.12f), new Vector3(0.10f, 0.30f, 0.50f), Vector3.zero, trim);
            Part(model, PrimitiveType.Capsule, "MunitionL", new Vector3(-1.30f, -0.30f, -0.08f), new Vector3(0.18f, 0.55f, 0.18f), new Vector3(90f, 0f, 0f), dark);
            Part(model, PrimitiveType.Capsule, "MunitionR", new Vector3(1.30f, -0.30f, -0.08f), new Vector3(0.18f, 0.55f, 0.18f), new Vector3(90f, 0f, 0f), dark);
            Part(model, PrimitiveType.Cube, "PylonOuterL", new Vector3(-2.35f, 0.02f, -0.12f), new Vector3(0.09f, 0.26f, 0.42f), Vector3.zero, trim);
            Part(model, PrimitiveType.Cube, "PylonOuterR", new Vector3(2.35f, 0.02f, -0.12f), new Vector3(0.09f, 0.26f, 0.42f), Vector3.zero, trim);
            Part(model, PrimitiveType.Capsule, "MunitionOuterL", new Vector3(-2.35f, -0.19f, -0.08f), new Vector3(0.15f, 0.45f, 0.15f), new Vector3(90f, 0f, 0f), dark);
            Part(model, PrimitiveType.Capsule, "MunitionOuterR", new Vector3(2.35f, -0.19f, -0.08f), new Vector3(0.15f, 0.45f, 0.15f), new Vector3(90f, 0f, 0f), dark);

            return model;
        }

        /// <summary>
        /// The player's fighter jet (Savaş Uçağı): an area-ruled single-engine airframe with a
        /// pointed radome, a raked bubble canopy, cranked-delta wings with leading-edge root
        /// extensions, twin canted fins, side intakes, four underwing stores and a ventral sensor
        /// ball.
        ///
        /// <para>Layout notes (model space, +Z forward, +Y up, ~6.8 long by ~5.2 span):</para>
        /// <list type="bullet">
        ///   <item>the fuselage is FIVE stacked cylinders whose cross-sections pinch in over the
        ///   wing and swell again over the engine bay — a readable stand-in for area ruling that a
        ///   single box cannot give;</item>
        ///   <item>the pilot's eye in cockpit view sits at roughly (0, 0.5, 1.6) — see
        ///   <c>CameraRig.cockpitForward/cockpitUp</c> — so the canopy is built around that point
        ///   and the radome runs on ahead of it, which is exactly what
        ///   <see cref="CockpitFrame"/> draws from the inside;</item>
        ///   <item>"Fuselage" stays a DIRECT child of "Model": <c>CameraRig.TryReadBodyColor</c>
        ///   looks it up with <c>Find</c>, which does not recurse;</item>
        ///   <item>there is no "Propeller" — a jet has none, and
        ///   <see cref="PropellerSpinner"/> simply finds nothing and does nothing.</item>
        /// </list>
        /// </summary>
        public static Transform BuildFighterJet(Transform root, Color primary)
        {
            Transform model = CreateModelRoot(root);
            if (model == null) return null;

            Material body = Body(primary);
            Material trim = Trim(primary);
            Material accent = Accent(primary);
            Material dark = DarkMetal();

            // ---- fuselage: nose 0.48 wide → 0.64 at the intakes → 0.58 waist → 0.62 engine bay.
            Part(model, PrimitiveType.Capsule, "Radome", new Vector3(0f, 0.02f, 2.85f), new Vector3(0.36f, 0.55f, 0.36f), new Vector3(90f, 0f, 0f), dark);
            Part(model, PrimitiveType.Cylinder, "PitotBoom", new Vector3(0f, 0.02f, 3.55f), new Vector3(0.05f, 0.20f, 0.05f), new Vector3(90f, 0f, 0f), dark);
            Part(model, PrimitiveType.Cylinder, "NoseSection", new Vector3(0f, 0.02f, 1.95f), new Vector3(0.48f, 0.45f, 0.50f), new Vector3(90f, 0f, 0f), body);
            Part(model, PrimitiveType.Cylinder, "Fuselage", new Vector3(0f, 0f, 0.65f), new Vector3(0.64f, 0.85f, 0.68f), new Vector3(90f, 0f, 0f), body);
            Part(model, PrimitiveType.Cylinder, "MidFuselage", new Vector3(0f, 0f, -0.95f), new Vector3(0.58f, 0.75f, 0.62f), new Vector3(90f, 0f, 0f), body);
            Part(model, PrimitiveType.Cylinder, "AftFuselage", new Vector3(0f, 0f, -2.30f), new Vector3(0.62f, 0.60f, 0.58f), new Vector3(90f, 0f, 0f), body);
            Part(model, PrimitiveType.Cube, "Spine", new Vector3(0f, 0.34f, -0.75f), new Vector3(0.30f, 0.24f, 2.30f), Vector3.zero, accent);

            // ---- cockpit. The glass is the one part that may be missing: CreateTransparent can
            // return null when no shader resolves, and an opaque canopy would be worse than none.
            Part(model, PrimitiveType.Cube, "CanopySill", new Vector3(0f, 0.32f, 1.55f), new Vector3(0.52f, 0.14f, 1.35f), Vector3.zero, trim);
            Material glass = CanopyGlass();
            if (glass != null)
                Part(model, PrimitiveType.Capsule, "Canopy", new Vector3(0f, 0.46f, 1.60f), new Vector3(0.44f, 0.52f, 0.44f), new Vector3(86f, 0f, 0f), glass);

            // ---- cranked delta. A Y rotation of -a sweeps the LEFT panel back (its outboard end
            // runs along (-cos a, 0, -sin a)), so the mirrored panel takes +a — same sign
            // convention the enemy fighter's swept panels already use.
            Part(model, PrimitiveType.Cube, "LerxL", new Vector3(-0.42f, -0.02f, 0.85f), new Vector3(0.55f, 0.08f, 2.00f), new Vector3(0f, -6f, 0f), trim);
            Part(model, PrimitiveType.Cube, "LerxR", new Vector3(0.42f, -0.02f, 0.85f), new Vector3(0.55f, 0.08f, 2.00f), new Vector3(0f, 6f, 0f), trim);

            Part(model, PrimitiveType.Cube, "WingL", new Vector3(-1.05f, -0.06f, -0.55f), new Vector3(2.00f, 0.10f, 1.70f), new Vector3(0f, -30f, 0f), trim);
            Part(model, PrimitiveType.Cube, "WingR", new Vector3(1.05f, -0.06f, -0.55f), new Vector3(2.00f, 0.10f, 1.70f), new Vector3(0f, 30f, 0f), trim);
            // Outer panels pick up extra sweep at the crank, starting where the inner panel tips out.
            Part(model, PrimitiveType.Cube, "WingOuterL", new Vector3(-2.27f, -0.06f, -1.40f), new Vector3(1.00f, 0.09f, 1.00f), new Vector3(0f, -45f, 0f), trim);
            Part(model, PrimitiveType.Cube, "WingOuterR", new Vector3(2.27f, -0.06f, -1.40f), new Vector3(1.00f, 0.09f, 1.00f), new Vector3(0f, 45f, 0f), trim);
            // Flaperons ride the inner panel's trailing edge (0.95 back along its own local -Z).
            Part(model, PrimitiveType.Cube, "FlaperonL", new Vector3(-0.58f, -0.06f, -1.37f), new Vector3(1.90f, 0.07f, 0.30f), new Vector3(0f, -30f, 0f), accent);
            Part(model, PrimitiveType.Cube, "FlaperonR", new Vector3(0.58f, -0.06f, -1.37f), new Vector3(1.90f, 0.07f, 0.30f), new Vector3(0f, 30f, 0f), accent);

            // ---- tail: swept tailplanes plus twin fins canted OUTWARD (a +Z rotation tips the
            // left fin's top toward -X, so the pair takes opposite signs).
            Part(model, PrimitiveType.Cube, "StabL", new Vector3(-0.95f, 0f, -2.35f), new Vector3(1.30f, 0.08f, 0.85f), new Vector3(0f, -28f, 0f), trim);
            Part(model, PrimitiveType.Cube, "StabR", new Vector3(0.95f, 0f, -2.35f), new Vector3(1.30f, 0.08f, 0.85f), new Vector3(0f, 28f, 0f), trim);
            Part(model, PrimitiveType.Cube, "FinL", new Vector3(-0.50f, 0.62f, -2.05f), new Vector3(0.08f, 1.10f, 0.95f), new Vector3(0f, 0f, 20f), trim);
            Part(model, PrimitiveType.Cube, "FinR", new Vector3(0.50f, 0.62f, -2.05f), new Vector3(0.08f, 1.10f, 0.95f), new Vector3(0f, 0f, -20f), trim);

            // ---- side intakes. The lips are dark so the duct mouths read as holes, not as caps.
            Part(model, PrimitiveType.Cube, "IntakeL", new Vector3(-0.46f, -0.14f, 0.75f), new Vector3(0.34f, 0.42f, 1.60f), new Vector3(0f, -4f, 0f), trim);
            Part(model, PrimitiveType.Cube, "IntakeR", new Vector3(0.46f, -0.14f, 0.75f), new Vector3(0.34f, 0.42f, 1.60f), new Vector3(0f, 4f, 0f), trim);
            Part(model, PrimitiveType.Cube, "IntakeLipL", new Vector3(-0.46f, -0.14f, 1.56f), new Vector3(0.38f, 0.46f, 0.14f), Vector3.zero, dark);
            Part(model, PrimitiveType.Cube, "IntakeLipR", new Vector3(0.46f, -0.14f, 1.56f), new Vector3(0.38f, 0.46f, 0.14f), Vector3.zero, dark);

            // ---- single centreline nozzle carrying the conventional "EngineGlow" part.
            Part(model, PrimitiveType.Cylinder, "NozzleShroud", new Vector3(0f, 0f, -3.02f), new Vector3(0.50f, 0.28f, 0.50f), new Vector3(90f, 0f, 0f), dark);
            Part(model, PrimitiveType.Sphere, "EngineGlow", new Vector3(0f, 0f, -3.20f), new Vector3(0.34f, 0.34f, 0.28f), Vector3.zero, Glow());

            // ---- four pylons with slim stores, sitting under the inner wing panel.
            Part(model, PrimitiveType.Cube, "PylonL", new Vector3(-0.95f, -0.22f, -0.50f), new Vector3(0.09f, 0.26f, 0.50f), Vector3.zero, trim);
            Part(model, PrimitiveType.Cube, "PylonR", new Vector3(0.95f, -0.22f, -0.50f), new Vector3(0.09f, 0.26f, 0.50f), Vector3.zero, trim);
            Part(model, PrimitiveType.Capsule, "MissileL", new Vector3(-0.95f, -0.44f, -0.40f), new Vector3(0.16f, 0.50f, 0.16f), new Vector3(90f, 0f, 0f), dark);
            Part(model, PrimitiveType.Capsule, "MissileR", new Vector3(0.95f, -0.44f, -0.40f), new Vector3(0.16f, 0.50f, 0.16f), new Vector3(90f, 0f, 0f), dark);
            Part(model, PrimitiveType.Cube, "PylonOuterL", new Vector3(-1.75f, -0.20f, -0.95f), new Vector3(0.08f, 0.22f, 0.42f), Vector3.zero, trim);
            Part(model, PrimitiveType.Cube, "PylonOuterR", new Vector3(1.75f, -0.20f, -0.95f), new Vector3(0.08f, 0.22f, 0.42f), Vector3.zero, trim);
            Part(model, PrimitiveType.Capsule, "MissileOuterL", new Vector3(-1.75f, -0.40f, -0.90f), new Vector3(0.13f, 0.42f, 0.13f), new Vector3(90f, 0f, 0f), dark);
            Part(model, PrimitiveType.Capsule, "MissileOuterR", new Vector3(1.75f, -0.40f, -0.90f), new Vector3(0.13f, 0.42f, 0.13f), new Vector3(90f, 0f, 0f), dark);

            // ---- ventral sensor ball on the same unscaled-pivot convention the SAM/AAA turrets
            // use, so a TurretVisual attached here would slew it without shearing anything.
            Transform turret = Pivot(model, "Turret", new Vector3(0f, -0.34f, 1.30f));
            Part(turret, PrimitiveType.Sphere, "TurretBody", Vector3.zero, new Vector3(0.34f, 0.34f, 0.34f), Vector3.zero, dark);

            return model;
        }

        /// <summary>Aggressive delta-wing enemy jet with a glowing exhaust.</summary>
        public static Transform BuildEnemyFighter(Transform root, Color primary)
        {
            Transform model = CreateModelRoot(root);
            if (model == null) return null;

            Material body = Body(primary);
            Material trim = Trim(primary);

            Part(model, PrimitiveType.Capsule, "Fuselage", new Vector3(0f, 0f, 0f), new Vector3(0.45f, 1.3f, 0.45f), new Vector3(90f, 0f, 0f), body);
            Part(model, PrimitiveType.Sphere, "NoseCone", new Vector3(0f, 0f, 1.5f), new Vector3(0.5f, 0.45f, 0.9f), Vector3.zero, body);

            Part(model, PrimitiveType.Cube, "DeltaWing", new Vector3(0f, -0.05f, -0.35f), new Vector3(4.2f, 0.09f, 1.7f), new Vector3(0f, 0f, 0f), trim);
            Part(model, PrimitiveType.Cube, "SweepL", new Vector3(-1.5f, -0.05f, 0.35f), new Vector3(1.6f, 0.09f, 1.0f), new Vector3(0f, -25f, 0f), trim);
            Part(model, PrimitiveType.Cube, "SweepR", new Vector3(1.5f, -0.05f, 0.35f), new Vector3(1.6f, 0.09f, 1.0f), new Vector3(0f, 25f, 0f), trim);

            Part(model, PrimitiveType.Cube, "TailFinL", new Vector3(-0.6f, 0.5f, -1.5f), new Vector3(0.08f, 0.9f, 0.6f), Vector3.zero, trim);
            Part(model, PrimitiveType.Cube, "TailFinR", new Vector3(0.6f, 0.5f, -1.5f), new Vector3(0.08f, 0.9f, 0.6f), Vector3.zero, trim);

            Part(model, PrimitiveType.Sphere, "EngineGlow", new Vector3(0f, 0f, -1.8f), new Vector3(0.35f, 0.35f, 0.35f), Vector3.zero, Glow());

            return model;
        }

        /// <summary>
        /// Long-range SAM battery, built to read like a real Patriot section rather than a single
        /// launcher: an M901-style lowboy launcher trailer with an elevating four-canister rack, a
        /// phased-array radar trailer parked off to one side, and an engagement-control shelter with
        /// its own generator and antenna mast.
        ///
        /// <para>Layout notes (model space, +Z forward, +Y up, ~9 m by ~7 m of ground):</para>
        /// <list type="bullet">
        ///   <item>the launcher's chassis, bogies and outrigger beams stay on the model root — only
        ///   the rack traverses;</item>
        ///   <item>"Turret" is the EMPTY, unscaled pivot <see cref="TurretVisual"/> yaws. "Rack" is a
        ///   second unscaled pivot under it carrying the +32° launch elevation, so the canisters can
        ///   be laid out in plain un-rotated coordinates;</item>
        ///   <item>the rack pivots near its REAR, the way an M901's does, so elevating swings the
        ///   canister mouths UP and FORWARD instead of driving their tails through the trailer bed.
        ///   With the geometry below the mouths end up roughly over the model origin — which is
        ///   where <c>AirDefenseSite.LaunchMunition</c> spawns the round (it uses the SITE ROOT's
        ///   position, no named muzzle transform), so the missile leaves from under the tubes;</item>
        ///   <item>"Radar" is an unscaled pivot with an IDENTITY rotation, so the sweep TurretVisual
        ///   applies to it is a clean azimuth turn about the vertical. The 30° rake lives on the
        ///   "ArrayMount" child below it (finding B-19: the old dish carried the tilt itself and
        ///   therefore wobbled instead of scanning).</item>
        /// </list>
        /// </summary>
        public static Transform BuildSamSite(Transform root, Color primary)
        {
            Transform model = CreateModelRoot(root);
            if (model == null) return null;

            Material body = Body(primary);
            Material trim = Trim(primary);
            Material accent = Accent(primary);
            Material dark = DarkMetal();

            // ---- launcher trailer (M901-style): lowboy bed, gooseneck and hitch, two bogies a side,
            // and the outrigger beams that jack the bed down for firing.
            Part(model, PrimitiveType.Cube, "TrailerBed", new Vector3(0f, 0.72f, -1.60f), new Vector3(2.30f, 0.30f, 4.40f), Vector3.zero, trim);
            Part(model, PrimitiveType.Cube, "Gooseneck", new Vector3(0f, 0.95f, 1.15f), new Vector3(1.30f, 0.26f, 1.20f), Vector3.zero, trim);
            Part(model, PrimitiveType.Cylinder, "Hitch", new Vector3(0f, 1.02f, 2.20f), new Vector3(0.12f, 0.50f, 0.12f), new Vector3(90f, 0f, 0f), dark);

            Part(model, PrimitiveType.Cylinder, "BogieLF", new Vector3(-1.20f, 0.39f, -1.70f), new Vector3(0.78f, 0.16f, 0.78f), new Vector3(0f, 0f, 90f), dark);
            Part(model, PrimitiveType.Cylinder, "BogieRF", new Vector3(1.20f, 0.39f, -1.70f), new Vector3(0.78f, 0.16f, 0.78f), new Vector3(0f, 0f, 90f), dark);
            Part(model, PrimitiveType.Cylinder, "BogieLB", new Vector3(-1.20f, 0.39f, -2.70f), new Vector3(0.78f, 0.16f, 0.78f), new Vector3(0f, 0f, 90f), dark);
            Part(model, PrimitiveType.Cylinder, "BogieRB", new Vector3(1.20f, 0.39f, -2.70f), new Vector3(0.78f, 0.16f, 0.78f), new Vector3(0f, 0f, 90f), dark);

            // The front beam is kept back to z = -0.55 so it clears the round the site spawns at the
            // model origin (a 0.6-wide sphere reaching z = +/-0.3).
            Part(model, PrimitiveType.Cube, "OutriggerFront", new Vector3(0f, 0.20f, -0.55f), new Vector3(3.40f, 0.40f, 0.40f), Vector3.zero, dark);
            Part(model, PrimitiveType.Cube, "OutriggerRear", new Vector3(0f, 0.20f, -3.30f), new Vector3(3.40f, 0.40f, 0.40f), Vector3.zero, dark);

            // ---- traverse ring + elevating canister rack. Both pivots are scale-free: the ring's
            // non-uniform (1.30, 0.14, 1.30) would otherwise flatten every canister hanging below it.
            Transform turret = Pivot(model, "Turret", new Vector3(0f, 0.88f, -1.60f));
            Part(turret, PrimitiveType.Cylinder, "TurretBody", Vector3.zero, new Vector3(1.30f, 0.14f, 1.30f), Vector3.zero, trim);
            Part(turret, PrimitiveType.Cube, "TrunnionL", new Vector3(-0.80f, 0.22f, -0.10f), new Vector3(0.18f, 0.60f, 0.36f), Vector3.zero, trim);
            Part(turret, PrimitiveType.Cube, "TrunnionR", new Vector3(0.80f, 0.22f, -0.10f), new Vector3(0.18f, 0.60f, 0.36f), Vector3.zero, trim);

            // A NEGATIVE X euler tips local +Z up (R_x(-32°) maps (0,0,1) to (0, 0.53, 0.85)), which
            // is the same sign convention the aircraft's canted surfaces use.
            Transform rack = Pivot(turret, "Rack", new Vector3(0f, 0.20f, 0f), new Vector3(-32f, 0f, 0f));

            // Four square canisters in a 2x2 block, all forward of the rack pivot so elevating never
            // buries their tails in the bed. 0.70 boxes on a 0.76 pitch leave a visible seam.
            Part(rack, PrimitiveType.Cube, "CanisterLL", new Vector3(-0.38f, 0.38f, 1.45f), new Vector3(0.70f, 0.70f, 2.80f), Vector3.zero, body);
            Part(rack, PrimitiveType.Cube, "CanisterRL", new Vector3(0.38f, 0.38f, 1.45f), new Vector3(0.70f, 0.70f, 2.80f), Vector3.zero, body);
            Part(rack, PrimitiveType.Cube, "CanisterLU", new Vector3(-0.38f, 1.14f, 1.45f), new Vector3(0.70f, 0.70f, 2.80f), Vector3.zero, body);
            Part(rack, PrimitiveType.Cube, "CanisterRU", new Vector3(0.38f, 1.14f, 1.45f), new Vector3(0.70f, 0.70f, 2.80f), Vector3.zero, body);

            // Blow-out front caps, sitting just proud of the canister mouths (z = 1.45 + 1.40).
            Part(rack, PrimitiveType.Cube, "CapLL", new Vector3(-0.38f, 0.38f, 2.88f), new Vector3(0.66f, 0.66f, 0.10f), Vector3.zero, accent);
            Part(rack, PrimitiveType.Cube, "CapRL", new Vector3(0.38f, 0.38f, 2.88f), new Vector3(0.66f, 0.66f, 0.10f), Vector3.zero, accent);
            Part(rack, PrimitiveType.Cube, "CapLU", new Vector3(-0.38f, 1.14f, 2.88f), new Vector3(0.66f, 0.66f, 0.10f), Vector3.zero, accent);
            Part(rack, PrimitiveType.Cube, "CapRU", new Vector3(0.38f, 1.14f, 2.88f), new Vector3(0.66f, 0.66f, 0.10f), Vector3.zero, accent);

            Part(rack, PrimitiveType.Cube, "RackRearPlate", new Vector3(0f, 0.76f, 0.10f), new Vector3(1.76f, 1.76f, 0.14f), Vector3.zero, trim);
            Part(rack, PrimitiveType.Cube, "RackRailL", new Vector3(-0.86f, 0.76f, 1.45f), new Vector3(0.10f, 1.70f, 2.60f), Vector3.zero, trim);
            Part(rack, PrimitiveType.Cube, "RackRailR", new Vector3(0.86f, 0.76f, 1.45f), new Vector3(0.10f, 1.70f, 2.60f), Vector3.zero, trim);

            // ---- phased-array radar trailer, parked off the launcher's left quarter. It stands
            // 3.8 m out so the array's sweep circle (~1.6 m) never crosses the rack's (~1.8 m).
            Part(model, PrimitiveType.Cube, "RadarTrailerBed", new Vector3(-3.80f, 0.62f, -2.20f), new Vector3(1.90f, 0.28f, 3.20f), Vector3.zero, trim);
            Part(model, PrimitiveType.Cylinder, "RadarBogieL", new Vector3(-4.80f, 0.31f, -2.80f), new Vector3(0.62f, 0.14f, 0.62f), new Vector3(0f, 0f, 90f), dark);
            Part(model, PrimitiveType.Cylinder, "RadarBogieR", new Vector3(-2.80f, 0.31f, -2.80f), new Vector3(0.62f, 0.14f, 0.62f), new Vector3(0f, 0f, 90f), dark);
            Part(model, PrimitiveType.Cube, "RadarJack", new Vector3(-3.80f, 0.24f, -0.95f), new Vector3(0.32f, 0.48f, 0.32f), Vector3.zero, dark);

            Transform radar = Pivot(model, "Radar", new Vector3(-3.80f, 0.76f, -2.20f));
            Transform arrayMount = Pivot(radar, "ArrayMount", new Vector3(0f, 0.10f, -0.10f), new Vector3(-30f, 0f, 0f));
            // Frame first, panel a hair in front of it, so a thin border shows all round the array.
            Part(arrayMount, PrimitiveType.Cube, "ArrayFrame", new Vector3(0f, 1.55f, -0.08f), new Vector3(2.70f, 3.10f, 0.12f), Vector3.zero, trim);
            Part(arrayMount, PrimitiveType.Cube, "ArrayPanel", new Vector3(0f, 1.55f, 0.02f), new Vector3(2.42f, 2.78f, 0.14f), Vector3.zero, dark);
            Part(arrayMount, PrimitiveType.Cube, "ArrayStrut", new Vector3(0f, 0.55f, -0.75f), new Vector3(0.24f, 0.24f, 1.40f), Vector3.zero, trim);

            // ---- engagement-control shelter, generator set and antenna mast: what turns a lone
            // launcher into a battery.
            Part(model, PrimitiveType.Cube, "ShelterSkids", new Vector3(3.20f, 0.10f, -1.60f), new Vector3(2.70f, 0.20f, 3.70f), Vector3.zero, dark);
            Part(model, PrimitiveType.Cube, "Shelter", new Vector3(3.20f, 1.25f, -1.60f), new Vector3(2.60f, 2.10f, 3.60f), Vector3.zero, body);
            Part(model, PrimitiveType.Cube, "ShelterDoor", new Vector3(1.88f, 0.95f, -0.50f), new Vector3(0.10f, 1.40f, 1.00f), Vector3.zero, dark);
            Part(model, PrimitiveType.Cube, "Generator", new Vector3(3.20f, 0.55f, 0.90f), new Vector3(1.10f, 0.90f, 1.60f), Vector3.zero, trim);
            Part(model, PrimitiveType.Cylinder, "GeneratorExhaust", new Vector3(3.60f, 1.18f, 0.90f), new Vector3(0.12f, 0.30f, 0.12f), Vector3.zero, dark);
            Part(model, PrimitiveType.Cylinder, "Mast", new Vector3(4.40f, 2.00f, -2.90f), new Vector3(0.10f, 1.90f, 0.10f), Vector3.zero, dark);
            Part(model, PrimitiveType.Cube, "MastWhip", new Vector3(4.40f, 3.86f, -2.90f), new Vector3(0.70f, 0.06f, 0.06f), Vector3.zero, accent);

            return model;
        }

        /// <summary>
        /// Short-range AAA piece: a towed twin-barrel gun mount. Deliberately still reads as a GUN —
        /// wheels, outrigger skids, a gun shield, an ammunition box and twin muzzle brakes — so it
        /// never gets mistaken for the missile battery above.
        /// </summary>
        public static Transform BuildAaaSite(Transform root, Color primary)
        {
            Transform model = CreateModelRoot(root);
            if (model == null) return null;

            Material body = Body(primary);
            Material trim = Trim(primary);
            Material dark = DarkMetal();

            Part(model, PrimitiveType.Cube, "Base", new Vector3(0f, 0.45f, 0f), new Vector3(2.20f, 0.55f, 2.60f), Vector3.zero, body);
            Part(model, PrimitiveType.Cube, "SkidFront", new Vector3(0f, 0.15f, 0.85f), new Vector3(3.00f, 0.30f, 0.35f), Vector3.zero, dark);
            Part(model, PrimitiveType.Cube, "SkidRear", new Vector3(0f, 0.15f, -0.85f), new Vector3(3.00f, 0.30f, 0.35f), Vector3.zero, dark);
            Part(model, PrimitiveType.Cylinder, "WheelL", new Vector3(-1.25f, 0.28f, -0.95f), new Vector3(0.55f, 0.14f, 0.55f), new Vector3(0f, 0f, 90f), dark);
            Part(model, PrimitiveType.Cylinder, "WheelR", new Vector3(1.25f, 0.28f, -0.95f), new Vector3(0.55f, 0.14f, 0.55f), new Vector3(0f, 0f, 90f), dark);
            Part(model, PrimitiveType.Cube, "AmmoBox", new Vector3(0f, 0.95f, -1.20f), new Vector3(1.20f, 0.45f, 0.55f), Vector3.zero, trim);

            // Same unscaled-pivot pattern as the SAM site: the barrels must slew with the turret.
            Transform turret = Pivot(model, "Turret", new Vector3(0f, 0.9f, 0f));
            Part(turret, PrimitiveType.Cylinder, "TurretBody", Vector3.zero, new Vector3(0.85f, 0.4f, 0.85f), Vector3.zero, trim);
            Part(turret, PrimitiveType.Cube, "GunShield", new Vector3(0f, 0.55f, 0.55f), new Vector3(1.40f, 0.90f, 0.12f), new Vector3(-15f, 0f, 0f), body);
            Part(turret, PrimitiveType.Cube, "Cradle", new Vector3(0f, 0.50f, 0.30f), new Vector3(0.55f, 0.30f, 0.70f), Vector3.zero, trim);
            Part(turret, PrimitiveType.Cube, "Sight", new Vector3(0.60f, 0.45f, 0.15f), new Vector3(0.30f, 0.25f, 0.30f), Vector3.zero, dark);
            Part(turret, PrimitiveType.Cylinder, "BarrelL", new Vector3(-0.18f, 0.35f, 0.6f), new Vector3(0.1f, 0.9f, 0.1f), new Vector3(75f, 0f, 0f), dark);
            Part(turret, PrimitiveType.Cylinder, "BarrelR", new Vector3(0.18f, 0.35f, 0.6f), new Vector3(0.1f, 0.9f, 0.1f), new Vector3(75f, 0f, 0f), dark);
            // Muzzle brakes ride the barrel tips: the barrels are 1.8 long and canted 75°, so their
            // ends sit 0.9 along (0, cos75°, sin75°) = (0, 0.259, 0.966) from each barrel centre.
            Part(turret, PrimitiveType.Cylinder, "MuzzleL", new Vector3(-0.18f, 0.58f, 1.47f), new Vector3(0.16f, 0.10f, 0.16f), new Vector3(75f, 0f, 0f), trim);
            Part(turret, PrimitiveType.Cylinder, "MuzzleR", new Vector3(0.18f, 0.58f, 1.47f), new Vector3(0.16f, 0.10f, 0.16f), new Vector3(75f, 0f, 0f), trim);

            return model;
        }

        /// <summary>Plain ground objective: a four-wheeled utility vehicle.</summary>
        public static Transform BuildGroundTarget(Transform root, Color primary)
        {
            Transform model = CreateModelRoot(root);
            if (model == null) return null;

            Material body = Body(primary);
            Material trim = Trim(primary);
            Material dark = DarkMetal();

            Part(model, PrimitiveType.Cube, "Hull", new Vector3(0f, 0.6f, 0f), new Vector3(2.6f, 0.8f, 1.5f), Vector3.zero, body);
            Part(model, PrimitiveType.Cube, "Cabin", new Vector3(0.4f, 1.2f, 0f), new Vector3(1.1f, 0.6f, 1.3f), Vector3.zero, trim);

            Part(model, PrimitiveType.Cylinder, "WheelFL", new Vector3(-0.9f, 0.3f, 0.75f), new Vector3(0.35f, 0.12f, 0.35f), new Vector3(0f, 0f, 90f), dark);
            Part(model, PrimitiveType.Cylinder, "WheelFR", new Vector3(0.9f, 0.3f, 0.75f), new Vector3(0.35f, 0.12f, 0.35f), new Vector3(0f, 0f, 90f), dark);
            Part(model, PrimitiveType.Cylinder, "WheelBL", new Vector3(-0.9f, 0.3f, -0.75f), new Vector3(0.35f, 0.12f, 0.35f), new Vector3(0f, 0f, 90f), dark);
            Part(model, PrimitiveType.Cylinder, "WheelBR", new Vector3(0.9f, 0.3f, -0.75f), new Vector3(0.35f, 0.12f, 0.35f), new Vector3(0f, 0f, 90f), dark);

            return model;
        }

        /// <summary>
        /// Disables the root primitive's MeshRenderer and destroys its collider. The simulation has no
        /// Rigidbody, no raycasts and no collision callbacks — every hit is a distance check — so a
        /// root collider would only cost PhysX a static-broadphase rebuild every frame the object
        /// moves.
        /// </summary>
        public static void HideRootMesh(GameObject root)
        {
            if (root == null) return;
            var renderer = root.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.enabled = false;
            StripCollider(root);
        }

        /// <summary>Destroys the GameObject's collider, if it has one. Null-safe.</summary>
        public static void StripCollider(GameObject go)
        {
            if (go == null) return;
            var c = go.GetComponent<Collider>();
            if (c != null) UnityEngine.Object.Destroy(c);
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>
        /// Creates (or reuses) the "Model" child. Its local scale cancels the root's own scale, so the
        /// silhouettes keep their designed proportions even when the root primitive was scaled by the
        /// spawner — the root's collider size is left exactly as it was.
        /// </summary>
        private static Transform CreateModelRoot(Transform root)
        {
            if (root == null) return null;

            Transform existing = root.Find("Model");
            if (existing != null) return existing;

            var go = new GameObject("Model");
            go.transform.SetParent(root, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Reciprocal(root.localScale);
            return go.transform;
        }

        /// <summary>Component-wise 1/v, guarding against degenerate zero scales.</summary>
        private static Vector3 Reciprocal(Vector3 v)
        {
            return new Vector3(
                Mathf.Abs(v.x) < 1e-4f ? 1f : 1f / v.x,
                Mathf.Abs(v.y) < 1e-4f ? 1f : 1f / v.y,
                Mathf.Abs(v.z) < 1e-4f ? 1f : 1f / v.z);
        }

        /// <summary>
        /// Creates an empty, unscaled pivot (no renderer, no collider) under <paramref name="parent"/>.
        /// Used for rotating assemblies whose visible parts carry a non-uniform scale, which would
        /// otherwise shear their children.
        /// </summary>
        private static Transform Pivot(Transform parent, string name, Vector3 localPosition)
        {
            return Pivot(parent, name, localPosition, Vector3.zero);
        }

        /// <summary>
        /// Same as <see cref="Pivot(Transform,string,Vector3)"/> but with a fixed local rotation, for
        /// sub-assemblies mounted at an angle (the SAM rack's launch elevation, the phased array's
        /// rake). Laying the angle on the PIVOT lets the parts below it be authored in plain
        /// un-rotated coordinates, and keeps the animated parent ("Turret"/"Radar") rotation-free.
        /// </summary>
        private static Transform Pivot(Transform parent, string name, Vector3 localPosition, Vector3 localEuler)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        /// <summary>Spawns one collider-less primitive part under the model root.</summary>
        private static GameObject Part(Transform parent, PrimitiveType type, string name,
            Vector3 localPosition, Vector3 localScale, Vector3 localEuler, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;

            // Models must never add physics.
            var c = part.GetComponent<Collider>();
            if (c != null) UnityEngine.Object.Destroy(c);

            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.Euler(localEuler);
            part.transform.localScale = localScale;

            MaterialLibrary.Apply(part, material);
            return part;
        }

        /// <summary>Main body material for a unit's primary colour.</summary>
        private static Material Body(Color primary)
        {
            return MaterialLibrary.Create(primary, 0.3f, 0.45f);
        }

        /// <summary>Darker, desaturated variant used for wings, tails and secondary structure.</summary>
        private static Material Trim(Color primary)
        {
            float grey = primary.r * 0.299f + primary.g * 0.587f + primary.b * 0.114f;
            var c = new Color(
                Mathf.Lerp(primary.r, grey, 0.4f) * 0.72f,
                Mathf.Lerp(primary.g, grey, 0.4f) * 0.72f,
                Mathf.Lerp(primary.b, grey, 0.4f) * 0.72f,
                primary.a);
            return MaterialLibrary.Create(c, 0.35f, 0.35f);
        }

        /// <summary>
        /// Lighter, slightly washed-out variant of the primary colour, used for panel detail that has
        /// to read against the body: spines, control surfaces and the recon airframe's wing skin.
        /// Derived from the unit's own colour, so no new palette is introduced.
        /// </summary>
        private static Material Accent(Color primary)
        {
            var c = new Color(
                Mathf.Lerp(primary.r, 1f, 0.35f),
                Mathf.Lerp(primary.g, 1f, 0.35f),
                Mathf.Lerp(primary.b, 1f, 0.35f),
                primary.a);
            return MaterialLibrary.Create(c, 0.2f, 0.4f);
        }

        /// <summary>Dark metallic material for sensors, barrels, munitions and wheels.</summary>
        private static Material DarkMetal()
        {
            return MaterialLibrary.Create(new Color(0.16f, 0.17f, 0.19f), 0.6f, 0.8f);
        }

        /// <summary>Hot emissive exhaust material, shared by every engine nozzle in the sim.</summary>
        private static Material Glow()
        {
            return MaterialLibrary.Create(new Color(1f, 0.45f, 0.15f), 0f, 0.6f, new Color(2f, 0.7f, 0.15f));
        }

        // The single canopy-glass instance. CreateTransparent hands back an UNCACHED material that
        // the caller owns, so caching it here is what keeps a rebuild from leaking one glass
        // material per aircraft. Unity's overloaded == makes the null test cover the destroyed case
        // too (leaving play mode tears runtime materials down), so it is simply rebuilt on demand.
        private static Material _canopyGlass;

        /// <summary>
        /// Tinted canopy glass, or null when no transparent material could be built — in which case
        /// callers must SKIP the canopy rather than draw it opaque.
        /// </summary>
        private static Material CanopyGlass()
        {
            if (_canopyGlass == null)
                _canopyGlass = MaterialLibrary.CreateTransparent(new Color(0.42f, 0.55f, 0.68f, 0.35f));
            return _canopyGlass;
        }
    }
}
