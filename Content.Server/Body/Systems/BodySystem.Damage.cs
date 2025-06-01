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
///     // TODO: check DamageableSystem
/// </summary>
public sealed partial class BodySystem
{
    private const int OtherDamageLimit = 50;

    /// <summary>
    /// Порог урона, при котором торс будет уничтожен при нанесении грубого повреждения.
    /// </summary>
    private const int TorsoBruteThreshold = 40;

    /// <summary>
    /// Порог урона, при котором часть тела будет отсечена от торса.
    /// </summary>
    private const int SeverThreshold = 8;

    /// <summary>
    /// Сила, с которой отлетают отсечённые конечности.
    /// </summary>
    private const float SeverImpulse = 25f;

    /// <summary>
    /// Разброс силы для отлёта конечностей.
    /// </summary>
    private const float SeverImpulseVariance = 1.5f;

    private void InitializeDamage()
    {
        // Слушаем изменение урона, чтобы распределять его по конечностям.
        SubscribeLocalEvent<BodyComponent, DamageChangedEvent>(OnBodyDamageChanged);
    }

    /// <summary>
    ///     Заставляет отсечённую часть тела отлететь в случайном направлении.
    ///     Используется при отделении конечностей от тела.
    /// </summary>
    private void FlingPart(EntityUid part)
    {
        // Случайный угол для импульса
        var angle = _random.NextAngle();
        // Рассчитываем вектор силы с небольшим разбросом
        var impulse = _random.NextAngle(angle - Angle.FromDegrees(30), angle + Angle.FromDegrees(30))
            .ToVec() * (SeverImpulse + _random.NextFloat(SeverImpulseVariance));

        // Поворачиваем и придаём импульс отсечённой части
        SharedTransform.SetWorldRotation(part, _random.NextAngle());
        _physics.ApplyLinearImpulse(part, impulse);
    }

    // /// <summary>
    // ///     Нанесение урона по конечности. Возвращает словарь с типами урона и значениями урона
    // /// </summary>
    // private Dictionary<string, int> SetDamage(DamageSpecifier delta)
    // {
    //     // Brute
    //     delta.DamageDict.TryGetValue("Slash", out var slashDamage);
    //     delta.DamageDict.TryGetValue("Piercing", out var piercingDamage);
    //     delta.DamageDict.TryGetValue("Blunt", out var bluntDamage);
    //
    //     var isDestroyBody = false;
    //     var isDestroyPart = false;
    //     var body = new Dictionary<String, int>();
    //     var totalDamage = 0;
    //
    //     totalDamage += (int) MathF.Ceiling(slashDamage.Float() + piercingDamage.Float() + bluntDamage.Float());;
    //
    //     return body;
    // }

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
        var totalDamage = (int) MathF.Ceiling(args.DamageDelta.GetTotal().Float());

        if (totalDamage <= 0)
            return;

        // Рандомом определим, какой части огребать
        var target =  _random.Pick(parts);
        // Признак грубого урона
        var isBrute = false;
        // Кол-во грубого урона
        var bruteDamage = 0;
        // Кол-во прочего урона
        var otherDamage = 0;

        // Циклом получим информацию о всех наносимых видах урона объекту
        foreach (var (damageType, value) in args.DamageDelta.DamageDict)
        {
            // Игнорируем отрицательный урон
            if (value <= FixedPoint2.Zero)
            {
                Log.Debug(damageType + " Zero damage");
                continue;
            }

            // С помощью switch проверим каждый тип урона и назначим ему модификатор
            switch (damageType)
            {
                // ГРУБЫЙ УРОН
                // Brute
                case "Slash":
                    bruteDamage += (int) MathF.Ceiling(value.Float() * 4f);
                    isBrute = true;
                    goto case "BRUTE-FINAL";
                case "Piercing":
                    bruteDamage += (int) MathF.Ceiling(value.Float() * 2f);
                    isBrute = true;
                    goto case "BRUTE-FINAL";
                case "Blunt":
                    bruteDamage += (int) MathF.Ceiling(value.Float() * 0.5f);
                    goto case "BRUTE-FINAL";
                case "BRUTE-FINAL":
                    Log.Debug("Brute damage " + damageType + " " + bruteDamage);
                    break;
                // ПРОЧИЙ УРОН
                // Burn
                case "Shock":
                case "Cold":
                case "Heat":
                case "Caustic":
                    otherDamage += (int) MathF.Ceiling(value.Float() * 0.5f);
                    Log.Debug("Other damage " + damageType + "");
                    break;
                // ИГНОРИРУЕМЫЙ УРОН
                // Airloss
                case "Asphyxiation":
                case "Bloodloss":
                // Toxin
                case "Radiation":
                case "Poison":
                // Genetic
                case "Cellular":
                    Log.Debug("Damage ignore " + damageType + "");
                    break;
                default:
                    Log.Debug("Hmm type " + damageType + " not found");
                    break;
            }
        }

        // Если получен сопутствующий урон, и конечность не достигла предела по нему
        if (otherDamage > 0 && target.Component.Health > OtherDamageLimit)
        {
            Log.Debug(
                "Other damage " +
                " " + target.Component.PartType +
                " " + target.Component.Health  +
                " -> "+ Math.Max(0, target.Component.Health - otherDamage) +
                " / " + target.Component.MaxHealth
            );
            target.Component.Health = Math.Max(OtherDamageLimit, target.Component.Health - otherDamage);
        }

        if (bruteDamage <= 0)
            return;

        // Проверяем, какой группой нанесён урон.
        // var bruteGroup = Prototypes.Index<DamageGroupPrototype>("Brute");
        // var damageType = args.DamageDelta.DamageDict.Keys;
        // Log.Debug("damage " + damageType);
        // var isBrute = args.DamageDelta.TryGetDamageInGroup(bruteGroup, out var brute) && brute > FixedPoint2.Zero;

        // Уменьшаем здоровье выбранной части тела и помечаем её для синхронизации.
        Log.Debug(
            "Brute damage " +
            " " + target.Component.PartType +
            " " + target.Component.Health  +
            " -> "+ Math.Max(0, target.Component.Health - bruteDamage) +
            " / " + target.Component.MaxHealth
        );
        target.Component.Health = Math.Max(0, target.Component.Health - bruteDamage);
        Dirty(target.Id, target.Component);

        // Если урон не относится к группе грубого урона или у конечности осталось здоровье,
        // пропускаем этап разрушения.
        if (!isBrute || target.Component.Health > 0)
            return;

        Log.Debug("Brute damage caused limb loss");

        if (target.Component.PartType == BodyPartType.Torso)
        {
            if (bruteDamage >= TorsoBruteThreshold)
            {
                Log.Debug("Gib body");
                GibBody(uid, true, component);
            }
            return;
        }

        if (Containers.TryRemoveFromContainer(target.Id))
        {
            if (bruteDamage >= SeverThreshold)
            {
                Log.Debug("Fliiing");
                FlingPart(target.Id);
            }
            return;
        }
    }
}
