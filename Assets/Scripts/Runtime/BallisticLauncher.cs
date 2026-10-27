using System.Collections.Generic;
using UnityEngine;
using Sim.Core;

namespace Sim.Runtime
{
    /// <summary>Why a balistik füze launch attempt did (or did not) happen. Drives the HUD's reason line.</summary>
    public enum BallisticFireResult
    {
        /// <summary>A round left the rail.</summary>
        Fired,
        /// <summary>This airframe has no centreline launcher at all (every archetype but the jet).</summary>
        NoLauncher,
        /// <summary>The rack is empty. Land on station to be rearmed, or buy more in the hangar.</summary>
        NoRounds,
        /// <summary>Still inside <see cref="BallisticMissile.ReloadSeconds"/> since the last shot.</summary>
        Cooling,
        /// <summary>Nothing is designated — no hostile is in the picture to number.</summary>
        NoTarget,
        /// <summary>The designated hostile is beyond <see cref="BallisticMissile.MaxRange"/>.</summary>
        OutOfRange,
        /// <summary>The geometry has no ballistic solution at this launch speed.</summary>
        NoSolution
    }

    /// <summary>
    /// The fighter jet's centreline balistik füze launcher — THIN GLUE over two pure-logic cores:
    /// <see cref="Sim.Core.TargetDesignator"/> (which numbers the visible hostiles 1, 2, 3 … and holds
    /// the pilot's selection) and <see cref="Sim.Core.WeaponSystem"/> (stock + inter-shot cooldown,
    /// the same fire-control object the guided missile and the air-defence sites already use).
    /// Everything this class owns itself is scene plumbing: the detection snapshot, the spawned
    /// <see cref="BallisticRound"/> and the launch sound.
    ///
    /// <para>
    /// JET-EXCLUSIVE BY CONSTRUCTION. <see cref="SimulationBootstrap"/> attaches this component only
    /// when <see cref="AircraftProfile.BallisticRounds"/> is above zero, which is true of the fighter
    /// jet alone. On a SİHA or a recon İHA there is no component, <see cref="IhaController.Ballistic"/>
    /// is null, and every key press is a no-op the HUD can explain.
    /// </para>
    ///
    /// <para>
    /// THE WARHEAD IS DERIVED AT LAUNCH, not configured. <see cref="Damage"/> reads the sibling
    /// <see cref="SihaController.MissileDamage"/> — the very field the jet's NORMAL missile hits with —
    /// and runs it through <see cref="BallisticMissile.Damage"/> (×2). There is no second damage number
    /// anywhere in the project to keep in step: retune the guided missile and this moves with it.
    /// </para>
    ///
    /// <para>
    /// STOCK REGENERATES EXACTLY LIKE THE MISSILE MAGAZINE and in no other way: there is no timer and
    /// no trickle. <see cref="IhaController.Resupply"/> calls <see cref="Rearm"/> when the aircraft has
    /// dwelled inside its base radius for the full <see cref="Sim.Core.ResupplyPoint"/> cycle, which is
    /// the same single call site that refills the gun belt, the flares and
    /// <see cref="SihaController"/>'s missiles.
    /// </para>
    /// </summary>
    public class BallisticLauncher : MonoBehaviour
    {
        [Header("Launcher")]
        // Rack size, set from AircraftProfile.BallisticRounds by SimulationBootstrap.
        [SerializeField] private int rounds = 2;
        // Which faction the numbering enumerates. 1 = hostile, matching every other sensor path.
        [SerializeField] private int enemyFaction = 1;

        /// <summary>
        /// How far ahead of the nose the round is spawned, so it leaves from under the fuselage rather
        /// than inside the model. Cosmetic.
        /// </summary>
        private const float MuzzleOffset = 2.2f;

        /// <summary>How long the HUD keeps showing the last launch result, in seconds.</summary>
        public const float ResultDisplaySeconds = 2.5f;

        // Pure-logic cores. Built lazily so a spawner's Configure(...) can precede Start, exactly the
        // pattern Jammer / CountermeasureDispenser / GunTurret already use.
        private WeaponSystem _weapon;
        private readonly TargetDesignator _designator = new TargetDesignator();

        // Reused snapshot buffer, so refreshing the numbering allocates nothing per frame (finding B-06).
        private readonly List<DetectableTarget> _snapshot = new List<DetectableTarget>();

        // The sibling armed controller the normal missile damage is read off. Resolved lazily.
        private SihaController _siha;

        /// <summary>
        /// The numbering + selection state. The HUD draws a number over every entry and marks
        /// <see cref="TargetDesignator.SelectedId"/>.
        /// </summary>
        public TargetDesignator Designator => _designator;

        /// <summary>
        /// True while the launcher is ARMED, i.e. the pilot is in targeting mode: the numbers are on
        /// screen and ←/→ cycle the selection. See <see cref="PlayerDroneController"/> for the keys.
        /// </summary>
        public bool Armed { get; private set; }

        /// <summary>Rounds remaining on the rack.</summary>
        public int Rounds => EnsureWeapon().Ammo;

        /// <summary>Rack size (what a full resupply restores).</summary>
        public int MaxRounds => EnsureWeapon().MagazineSize;

        /// <summary>Remaining stock as a 0..1 fraction, for a HUD bar.</summary>
        public float StockFraction =>
            MaxRounds > 0 ? (float)Rounds / MaxRounds : 0f;

        /// <summary>True when the rack holds at least one round.</summary>
        public bool HasRounds => Rounds > 0;

        /// <summary>True when this is a real launcher rather than a level-0 placeholder.</summary>
        public bool IsFitted => MaxRounds > 0;

        /// <summary>Seconds until the next shot is allowed (0 when ready).</summary>
        public float Cooldown => EnsureWeapon().Cooldown;

        /// <summary>Result of the most recent launch ATTEMPT, for the HUD's reason line.</summary>
        public BallisticFireResult LastResult { get; private set; } = BallisticFireResult.Fired;

        /// <summary>
        /// True while <see cref="LastResult"/> is still worth showing. Uses unscaled time so the
        /// message does not freeze with a paused game.
        /// </summary>
        public bool HasRecentResult =>
            _lastResultTime > 0f && Time.unscaledTime - _lastResultTime <= ResultDisplaySeconds;

        private float _lastResultTime;

        /// <summary>
        /// Damage one round delivers: the jet's own NORMAL missile warhead, doubled by
        /// <see cref="BallisticMissile.Damage"/>. Zero when there is no guided missile to derive from,
        /// which is what <see cref="TryFire"/> refuses to launch on.
        /// </summary>
        public float Damage => BallisticMissile.Damage(NormalMissileDamage);

        /// <summary>
        /// The sibling armed controller's guided-missile damage — the single source this weapon's
        /// warhead is derived from. 0 when this airframe carries no guided missile at all.
        /// </summary>
        public float NormalMissileDamage
        {
            get
            {
                // Unity's overloaded == is what reports a destroyed/absent component, so the check is
                // written out explicitly (never ?.).
                if (_siha == null) _siha = GetComponent<SihaController>();
                return _siha == null ? 0f : _siha.MissileDamage;
            }
        }

        /// <summary>
        /// Sets the rack size before <see cref="Start"/>, mirroring the <c>Configure(...)</c> pattern
        /// of the other runtime components. Called by <see cref="SimulationBootstrap"/> with
        /// <see cref="AircraftProfile.BallisticRounds"/> (stock 2 on the jet, plus one per level of the
        /// hangar's "Balistik Füze" track).
        /// </summary>
        public void Configure(int rackSize)
        {
            rounds = Mathf.Max(0, rackSize);
            // Drop any fire control already built with the old size; it is rebuilt on next access.
            _weapon = null;
        }

        /// <summary>
        /// Enters targeting mode and designates something immediately, so arming always leaves the
        /// pilot with a target under the crosshair. Refuses (and records why) on an empty rack, which
        /// is how the HUD can explain a key that "did nothing".
        /// </summary>
        public bool TryArm()
        {
            if (!IsFitted)
            {
                Record(BallisticFireResult.NoLauncher);
                return false;
            }
            if (!HasRounds)
            {
                Record(BallisticFireResult.NoRounds);
                return false;
            }

            Armed = true;
            RefreshDesignation();
            return true;
        }

        /// <summary>Leaves targeting mode and drops the numbering.</summary>
        public void Disarm()
        {
            Armed = false;
            _designator.Clear();
        }

        /// <summary>Arms the launcher when it is safed and safes it when it is armed.</summary>
        public bool Toggle()
        {
            if (Armed)
            {
                Disarm();
                return false;
            }
            return TryArm();
        }

        /// <summary>Steps the designation one number UP, wrapping. No-op while safed.</summary>
        public void CycleNext()
        {
            if (!Armed) return;
            _designator.CycleNext();
        }

        /// <summary>Steps the designation one number DOWN, wrapping. No-op while safed.</summary>
        public void CyclePrevious()
        {
            if (!Armed) return;
            _designator.CyclePrevious();
        }

        /// <summary>
        /// Advances the fire-control cooldown and, while armed, rebuilds the numbering from the current
        /// hostile picture. Called once per frame by <see cref="IhaController.Update"/>, alongside the
        /// flare dispenser and the jammer.
        /// </summary>
        public void Tick(float dt)
        {
            EnsureWeapon().Tick(dt);
            if (Armed) RefreshDesignation();
        }

        /// <summary>
        /// Launches one round at the DESIGNATED hostile along the lofted ballistic solution, and leaves
        /// targeting mode on success (so the pilot is back on the normal missile straight away).
        ///
        /// <para>
        /// Every refusal path records a <see cref="BallisticFireResult"/> and changes nothing else, so
        /// the HUD can always say why the key did nothing. The aiming solution is
        /// <see cref="Ballistics.TryLoftedLaunchVelocity"/>'s HIGH arc against the target's CURRENT
        /// position — the round is unguided, so that point is also where it detonates.
        /// </para>
        /// </summary>
        public BallisticFireResult TryFire()
        {
            if (!IsFitted) return Record(BallisticFireResult.NoLauncher);

            // No guided missile to derive a warhead from. Unreachable on the jet (it is the only
            // archetype with a launcher, and it always carries missiles) but never fire a blank round.
            if (Damage <= 0f) return Record(BallisticFireResult.NoLauncher);

            WeaponSystem weapon = EnsureWeapon();
            if (weapon.Ammo <= 0) return Record(BallisticFireResult.NoRounds);
            if (weapon.Cooldown > 0f) return Record(BallisticFireResult.Cooling);

            // Keep the numbering current even if Tick has not run since the last spawn/kill.
            RefreshDesignation();
            if (!_designator.HasSelection) return Record(BallisticFireResult.NoTarget);

            Targetable target = TargetRegistry.FindById(_designator.SelectedId);
            if (target == null) return Record(BallisticFireResult.NoTarget);

            Vector3 muzzle = transform.position + transform.forward * MuzzleOffset;
            Vector3 aimPoint = target.transform.position;

            if (!BallisticMissile.InRange(muzzle, aimPoint)) return Record(BallisticFireResult.OutOfRange);

            // The lofted (high-arc) branch: a heavy round that climbs away and falls back onto the
            // designated point. Out of reach at this launch speed reports false and is NOT fudged.
            Vector3 velocity;
            if (!Ballistics.TryLoftedLaunchVelocity(muzzle, aimPoint, BallisticMissile.LaunchSpeed,
                                                    BallisticMissile.Gravity, true, out velocity))
            {
                return Record(BallisticFireResult.NoSolution);
            }

            // Spend the round only once the shot is certain to happen.
            if (!weapon.TryFire(true)) return Record(BallisticFireResult.NoRounds);

            float flightSeconds = Ballistics.LoftedTimeOfFlight(muzzle, aimPoint, velocity);
            SpawnRound(muzzle, aimPoint, velocity, flightSeconds, target);

            // Firing leaves targeting mode: the pilot is flying again, on the normal missile.
            Disarm();
            return Record(BallisticFireResult.Fired);
        }

        /// <summary>Refills the rack. Called from <see cref="IhaController.Resupply"/> only.</summary>
        public void Rearm()
        {
            EnsureWeapon().Reload();
        }

        private void Start()
        {
            EnsureWeapon();
        }

        /// <summary>Builds the pure-logic fire control on first use, so Configure can precede Start.</summary>
        private WeaponSystem EnsureWeapon()
        {
            if (_weapon == null)
            {
                // Rounds per second is the reciprocal of the inter-shot delay the weapon definition
                // states, so the cooldown is BallisticMissile.ReloadSeconds and nothing else.
                float rps = BallisticMissile.ReloadSeconds > 0f ? 1f / BallisticMissile.ReloadSeconds : 0f;
                _weapon = new WeaponSystem(Mathf.Max(0, rounds), rps);
            }
            return _weapon;
        }

        /// <summary>
        /// Rebuilds the numbering from the current hostile snapshot. The ordering and the selection
        /// reconciliation are <see cref="TargetDesignator.Refresh"/>'s job — this only supplies the
        /// picture, into a reused buffer.
        /// </summary>
        private void RefreshDesignation()
        {
            TargetRegistry.GetSnapshot(enemyFaction, _snapshot);
            _designator.Refresh(_snapshot);
        }

        /// <summary>
        /// Spawns the round on the same path the guided missile uses — a primitive sphere carrying the
        /// weapon component — then hands it the solved velocity and the derived warhead.
        /// </summary>
        private void SpawnRound(Vector3 muzzle, Vector3 aimPoint, Vector3 velocity,
                                float flightSeconds, Targetable target)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "BallisticRound";
            go.transform.position = muzzle;
            // Visibly a heavier store than the guided munition's 0.6 sphere.
            go.transform.localScale = Vector3.one * 1.05f;

            var round = go.AddComponent<BallisticRound>();
            round.Launch(target, aimPoint, velocity, Damage, flightSeconds);

            // Launch report, spatialised at the rail: the same clip the missile uses, pitched DOWN so
            // the heavier store sounds heavier, and louder/further-carrying. Cosmetic.
            AudioDirector.PlayAt(muzzle, AudioLibrary.MissileLaunch, 0.8f, 0.62f, 8f, 220f);
        }

        /// <summary>Stores a launch result (and when it happened) for the HUD, then returns it.</summary>
        private BallisticFireResult Record(BallisticFireResult result)
        {
            LastResult = result;
            _lastResultTime = Time.unscaledTime;
            return result;
        }
    }
}
