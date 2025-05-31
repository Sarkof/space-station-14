using System.Linq;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Random;

namespace Content.Server.Body.Systems;

/// <summary>
///     Система, распределяющая урон по частям тела.
/// </summary>
public sealed partial class BodySystem
{

    private void InitializeDamage()
    {
        // Слушаем изменение урона, чтобы распределять его по конечностям.
        SubscribeLocalEvent<BodyComponent, DamageChangedEvent>(OnBodyDamageChanged);
    }

    /// <summary>
    ///     При получении урона случайно выбирает часть тела, которая пострадает.
    /// </summary>
    private void OnBodyDamageChanged(EntityUid uid, BodyComponent component, DamageChangedEvent args)
    {
        // Игнорируем уменьшение урона и отсутствие данных об изменении.
        if (!args.DamageIncreased || args.DamageDelta == null)
            return;

        // Получаем корневую часть тела
        if (component.RootContainer.ContainedEntity is not { Valid: true } rootPartId ||
            !TryComp(rootPartId, out BodyPartComponent? rootPart))
            return;

        // Собираем все части тела, включая корень.
        var parts = GetBodyPartChildren(rootPartId, rootPart).ToList();
        if (parts.Count == 0)
            return;

        // Считаем нанесённый урон в целых единицах.
        // var totalDamage = 0;
        // foreach (var value in args.DamageDelta.DamageDict.Values)
        // {
        //     if (value <= FixedPoint2.Zero)
        //         continue;
        //     totalDamage += (int) MathF.Ceiling(value.Float());
        // }

        // Считаем нанесённый урон в целых единицах.
        var totalDamage = (int) MathF.Ceiling(args.DamageDelta.GetTotal().Float());
        if (totalDamage <= 0)
            return;

        // Проверяем, какой группой нанесён урон.
        var bruteGroup = Prototypes.Index<DamageGroupPrototype>("Brute");
        var isBrute = args.DamageDelta.TryGetDamageInGroup(bruteGroup, out var brute) && brute > FixedPoint2.Zero;

        // Выбираем часть тела. Шанс попадания по торсу (корню) - 50%,
        // остальные части распределяются равномерно между собой.
        // var target = parts[0];
        // if (parts.Count > 1)
        // {
        //     if (!_random.Prob(0.5f))
        //     {
        //         var index = _random.Next(1, parts.Count);
        //         target = parts[index];
        //     }
        // }
        var target =  _random.Pick(parts);

        if (target.Component.PartType == BodyPartType.Torso)
            totalDamage *= 2;
        else
            totalDamage *= 10;

        // Уменьшаем здоровье выбранной части тела и помечаем её для синхронизации.
        Log.Debug(
            // " " + target +
            " " + target.Component.PartType +
            " " + target.Component.Health  +
            " -> "+ Math.Max(0, target.Component.Health - totalDamage) +
            " / " + target.Component.MaxHealth
        );
        target.Component.Health = Math.Max(0, target.Component.Health - totalDamage);
        Dirty(target.Id, target.Component);

        if (isBrute && target.Component.Health <= 0)
        {
            Log.Debug("Brute");
            if (target.Component.PartType != BodyPartType.Torso)
            {
                Containers.TryRemoveFromContainer(target.Id);
            }
            else
            {
                Log.Debug("Gib body");
                GibBody(uid, true, component);
            }
        }
    }
}
