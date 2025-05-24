using System.Linq;
using System.Numerics;
using Content.Shared.Humanoid.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Shared.Humanoid.Systems;

/// <summary>
/// Система для работы с определением частей тела гуманоида
/// </summary>
public sealed class HumanoidBodyPartSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;

    /// <summary>
    /// Получает часть тела в указанной точке относительно сущности
    /// </summary>
    /// <param name="uid">ID сущности гуманоида</param>
    /// <param name="examinerPos">Позиция наблюдателя в мировых координатах</param>
    /// <param name="targetPos">Позиция цели в мировых координатах</param>
    /// <param name="clickPos">Позиция клика в мировых координатах</param>
    /// <param name="component">Компонент зон частей тела</param>
    /// <returns>Найденная часть тела или null, если не найдено</returns>
    public HumanoidBodyPart? GetBodyPartAtPosition(
        EntityUid uid,
        Vector2 examinerPos,
        Vector2 targetPos,
        Vector2 clickPos,
        HumanoidBodyPartZonesComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return null;

        // Получение локальной позиции клика относительно целевой сущности
        var targetRotation = _transformSystem.GetWorldRotation(uid);
        var localClickPos = (clickPos - targetPos);

        // Корректировка с учетом поворота сущности
        localClickPos = RotateBy(localClickPos, -targetRotation);

        // Нормализация координат относительно размера сущности (предполагаем размер 1x2)
        var size = new Vector2(1f, 2f);
        var normalizedClickPos = new Vector2(
            localClickPos.X / (size.X / 2),
            localClickPos.Y / (size.Y / 2)
        );

        // Ограничение диапазона до [-1, 1]
        normalizedClickPos = Vector2.Clamp(normalizedClickPos, new Vector2(-1, -1), new Vector2(1, 1));

        // Находим ближайшую зону к точке клика
        return FindNearestBodyPart(normalizedClickPos, component.PartZones);
    }

    /// <summary>
    /// Находит ближайшую часть тела к указанной точке
    /// </summary>
    private HumanoidBodyPart? FindNearestBodyPart(Vector2 point, Dictionary<Vector2, HumanoidBodyPart> zones)
    {
        if (zones.Count == 0)
            return null;

        HumanoidBodyPart? nearestPart = null;
        float minDistance = float.MaxValue;

        foreach (var (zonePoint, bodyPart) in zones)
        {
            var distance = Vector2.DistanceSquared(point, zonePoint);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearestPart = bodyPart;
            }
        }

        // Если точка слишком далеко, возвращаем null
        return minDistance <= 1.5f ? nearestPart : null;
    }

    /// <summary>
    /// Поворачивает вектор на указанный угол
    /// </summary>
    private Vector2 RotateBy(Vector2 vec, Angle angle)
    {
        var sin = (float)Math.Sin(angle.Theta);
        var cos = (float)Math.Cos(angle.Theta);

        return new Vector2(
            vec.X * cos - vec.Y * sin,
            vec.X * sin + vec.Y * cos
        );
    }
}
