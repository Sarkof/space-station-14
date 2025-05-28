using System.Numerics;
namespace Content.Shared.Light.Components;

/// <summary>
/// Data for directional sunlight cycle steps.
/// </summary>
public sealed class SunShadowDirection
{
    /// <summary>
    /// Position along the cycle from 0 to 1.
    /// </summary>
    [DataField]
    public float Ratio;

    /// <summary>
    /// Direction vector for the sun shadow.
    /// </summary>
    [DataField]
    public Vector2 Direction;

    /// <summary>
    /// Opacity multiplier for the shadow.
    /// </summary>
    [DataField]
    public float Alpha;
}
