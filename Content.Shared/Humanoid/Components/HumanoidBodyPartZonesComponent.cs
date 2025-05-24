using System.Collections.Generic;
using System.Numerics;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Humanoid.Components;

/// <summary>
/// Компонент, содержащий информацию о зонах частей тела гуманоида
/// </summary>
[RegisterComponent]
public sealed partial class HumanoidBodyPartZonesComponent : Component
{
    /// <summary>
    /// Словарь, связывающий относительные координаты с частями тела
    /// </summary>
    [DataField("partZones")]
    public Dictionary<Vector2, HumanoidBodyPart> PartZones = new()
    {
        // Относительные координаты для разных частей тела
        // Значения в диапазоне от -1 до 1 для каждой оси
        { new Vector2(0, -0.8f), HumanoidBodyPart.Head },
        { new Vector2(0, -0.3f), HumanoidBodyPart.Torso },
        { new Vector2(-0.5f, -0.3f), HumanoidBodyPart.LeftArm },
        { new Vector2(0.5f, -0.3f), HumanoidBodyPart.RightArm },
        { new Vector2(-0.3f, 0.5f), HumanoidBodyPart.LeftLeg },
        { new Vector2(0.3f, 0.5f), HumanoidBodyPart.RightLeg }
    };
}
