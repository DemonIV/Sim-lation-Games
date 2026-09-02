using UnityEngine;

namespace Sim.Runtime
{
    /// <summary>
    /// The hostile faction's livery, in one place so the four enemy archetypes read as ONE faction
    /// rather than four unrelated colours.
    ///
    /// <para>Design constraints this palette answers:</para>
    /// <list type="bullet">
    ///   <item>the old colours were too dark to read at range — the ground target was literally
    ///   mid-grey (0.50, 0.50, 0.50) and the SAM battery a near-black maroon
    ///   (0.50, 0.05, 0.05, luminance 0.19), both of which sank into the terrain and the greyish
    ///   buildings;</item>
    ///   <item>the HUD already codes hostiles red (<see cref="HudTheme.Critical"/>), so the world
    ///   colours must not fight that language — the family sits in a narrow warm band, hue ~350°
    ///   to ~22°;</item>
    ///   <item>every member is kept DESATURATED (S ≈ 0.45…0.56) and LIGHT (V ≈ 0.70…0.86), which is
    ///   what separates it from the friendly SİHA's fully saturated orange livery
    ///   (1.00, 0.35, 0.20 — S 0.80, V 1.00) and from the near-neutral scenery: buildings and
    ///   concrete sit at S ≈ 0.16, the terrain and foliage are green;</item>
    ///   <item>members differ by lightness and a few degrees of hue only, so the faction stays
    ///   coherent while the individual archetypes remain tellable apart.</item>
    /// </list>
    ///
    /// <para>Dark structural parts (barrels, radar array face, canister mouths, wheels) deliberately
    /// stay dark in <see cref="VehicleModelBuilder"/>: with the body masses lifted, the dark details
    /// are what keeps each silhouette readable.</para>
    ///
    /// Purely cosmetic — no gameplay value is derived from anything here.
    /// </summary>
    public static class HostilePalette
    {
        /// <summary>
        /// Plain objective vehicle (the old mid-grey utility truck): the lightest member, a warm
        /// clay so a soft target never reads as scenery. HSV ≈ (10°, 0.45, 0.80).
        /// </summary>
        public static readonly Color GroundTarget = new Color(0.80f, 0.50f, 0.44f);

        /// <summary>
        /// SAM battery: the heavy of the family, a brick red. Still the darkest hostile so the
        /// battery keeps its "serious" weight, but 2.3× the luminance of the old near-black maroon.
        /// HSV ≈ (2°, 0.54, 0.70).
        /// </summary>
        public static readonly Color SamSite = new Color(0.70f, 0.33f, 0.32f);

        /// <summary>
        /// AAA gun: the warmest and lightest member — it keeps a hint of the old orange so the gun
        /// still separates from the missile battery at a glance. HSV ≈ (22°, 0.55, 0.85).
        /// </summary>
        public static readonly Color AaaSite = new Color(0.85f, 0.55f, 0.38f);

        /// <summary>
        /// Hostile fighter: the cool end of the band, a rose/wine that keeps a memory of the old
        /// magenta while joining the faction. Sits well clear of the friendly cyan/blue drones and
        /// the light blue-grey jet. HSV ≈ (350°, 0.51, 0.78).
        /// </summary>
        public static readonly Color Fighter = new Color(0.78f, 0.38f, 0.45f);
    }
}
