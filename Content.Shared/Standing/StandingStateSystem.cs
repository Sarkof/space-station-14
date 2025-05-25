using System.Numerics;
using Content.Shared.Hands.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Rotation;
using Content.Shared.Climbing.Components;
using Content.Shared.Climbing.Systems;
using Robust.Shared.Audio.Systems;
using Content.Shared.Climbing.Events;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.Standing;

public sealed class StandingStateSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    // Система, отвечающая за модификатор скорости передвижения
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ClimbSystem _climb = default!;

    // If StandingCollisionLayer value is ever changed to more than one layer, the logic needs to be edited.
    private const int StandingCollisionLayer = (int) CollisionGroup.MidImpassable;
    // Множитель скорости при лежачем состоянии
    private const float StandingSpeedMultiplier = 0.2f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StandingStateComponent, AttemptMobCollideEvent>(OnMobCollide);
        SubscribeLocalEvent<StandingStateComponent, AttemptMobTargetCollideEvent>(OnMobTargetCollide);
        // Обновление скорости при запросе
        SubscribeLocalEvent<StandingStateComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMove);
        SubscribeLocalEvent<StandingStateComponent, EndClimbEvent>(OnEndClimb);
    }

    private void OnMobTargetCollide(Entity<StandingStateComponent> ent, ref AttemptMobTargetCollideEvent args)
    {
        if (!ent.Comp.Standing)
        {
            args.Cancelled = true;
        }
    }

    private void OnMobCollide(Entity<StandingStateComponent> ent, ref AttemptMobCollideEvent args)
    {
        if (!ent.Comp.Standing)
        {
            args.Cancelled = true;
        }
    }

    private void OnRefreshMove(EntityUid uid, StandingStateComponent comp, ref RefreshMovementSpeedModifiersEvent args)
    {
        // Если персонаж лежит, уменьшаем его скорость
        if (!comp.Standing)
            args.ModifySpeed(StandingSpeedMultiplier);
    }

    // [BUG]
    private void OnEndClimb(EntityUid uid, StandingStateComponent component, ref EndClimbEvent args)
    {
        // Log.Debug("End climb");
        if (component.ChangedFixtures.Count == 0)
            return;

        if (!TryComp(uid, out FixturesComponent? fixtures))
            return;

        foreach (var key in component.ChangedFixtures)
        {
            if (!fixtures.Fixtures.TryGetValue(key, out var fixture))
                continue;

            _physics.SetCollisionMask(uid, key, fixture, fixture.CollisionMask | StandingCollisionLayer, fixtures);
        }

        component.ChangedFixtures.Clear();
    }

    public bool IsDown(EntityUid uid, StandingStateComponent? standingState = null)
    {
        if (!Resolve(uid, ref standingState, false))
            return false;

        return !standingState.Standing;
    }

    public bool Down(EntityUid uid,
        bool playSound = true,
        bool dropHeldItems = true,
        bool force = false,
        StandingStateComponent? standingState = null,
        AppearanceComponent? appearance = null,
        HandsComponent? hands = null)
    {
        // TODO: This should actually log missing comps...
        if (!Resolve(uid, ref standingState, false))
            return false;

        // Optional component.
        Resolve(uid, ref appearance, ref hands, false);

        if (!standingState.Standing)
            return true;

        // This is just to avoid most callers doing this manually saving boilerplate
        // 99% of the time you'll want to drop items but in some scenarios (e.g. buckling) you don't want to.
        // We do this BEFORE downing because something like buckle may be blocking downing but we want to drop hand items anyway
        // and ultimately this is just to avoid boilerplate in Down callers + keep their behavior consistent.
        if (dropHeldItems && hands != null)
        {
            var ev = new DropHandItemsEvent();
            RaiseLocalEvent(uid, ref ev, false);
        }

        if (!force)
        {
            var msg = new DownAttemptEvent();
            RaiseLocalEvent(uid, msg, false);

            if (msg.Cancelled)
                return false;
        }

        standingState.Standing = false;
        Dirty(uid, standingState);
        RaiseLocalEvent(uid, new DownedEvent(), false);
        // После падения пересчитываем скорость
        _movement.RefreshMovementSpeedModifiers(uid);

        // Seemed like the best place to put it
        _appearance.SetData(uid, RotationVisuals.RotationState, RotationState.Horizontal, appearance);

        // Change collision masks to allow going under certain entities like flaps and tables
        if (TryComp(uid, out FixturesComponent? fixtureComponent))
        {
            foreach (var (key, fixture) in fixtureComponent.Fixtures)
            {
                if ((fixture.CollisionMask & StandingCollisionLayer) == 0)
                    continue;

                standingState.ChangedFixtures.Add(key);
                _physics.SetCollisionMask(uid, key, fixture, fixture.CollisionMask & ~StandingCollisionLayer, manager: fixtureComponent);
            }
        }

        // check if component was just added or streamed to client
        // if true, no need to play sound - mob was down before player could seen that
        if (standingState.LifeStage <= ComponentLifeStage.Starting)
            return true;

        if (playSound)
        {
            _audio.PlayPredicted(standingState.DownSound, uid, uid);
        }

        return true;
    }

    public bool Stand(EntityUid uid,
        StandingStateComponent? standingState = null,
        AppearanceComponent? appearance = null,
        bool force = false)
    {
        // TODO: This should actually log missing comps...
        if (!Resolve(uid, ref standingState, false))
            return false;

        // Optional component.
        Resolve(uid, ref appearance, false);

        if (standingState.Standing)
            return true;

        if (!force)
        {
            var msg = new StandAttemptEvent();
            RaiseLocalEvent(uid, msg, false);

            if (msg.Cancelled)
                return false;
        }

        standingState.Standing = true;
        Dirty(uid, standingState);
        RaiseLocalEvent(uid, new StoodEvent(), false);
        // Возвращаем обычную скорость
        _movement.RefreshMovementSpeedModifiers(uid);

        _appearance.SetData(uid, RotationVisuals.RotationState, RotationState.Vertical, appearance);

        var xform = Transform(uid);
        var worldPos = _transform.GetWorldPosition(xform);
        var centerBounds = new Box2(worldPos - Vector2.One * 0.1f,
            worldPos + Vector2.One * 0.1f);
        var isClimbed = false;

        // Проверяем пересечение себя с другими объектами
        foreach (var other in _lookup.GetEntitiesIntersecting(xform.MapID, centerBounds, LookupFlags.Static))
        {
            // Пропускаем себя или объект без компонента climbable
            if (other == uid || !HasComp<ClimbableComponent>(other))
            {
                continue;
            }

            // Если центр существа пересекается с climbable-объектом — начинаем подъем
            if (!TryComp(uid, out ClimbingComponent? climbing) || !climbing.IsClimbing)
            {
                _climb.ForciblySetClimbing(uid, other);
                // _climb.Climb(uid, uid, other, false, climbing);
                // _climb.TryClimb(uid, uid, other, out _);

                isClimbed = true;
                break;
            }
        }

        // Если этапом ранее существо взобралось на объект - пропускаем блок
        if (!isClimbed && TryComp(uid, out FixturesComponent? fixtureComponent))
        {
            foreach (var key in standingState.ChangedFixtures)
            {
                if (fixtureComponent.Fixtures.TryGetValue(key, out var fixture))
                {
                    // Log.Debug("collision");
                    _physics.SetCollisionMask(uid, key, fixture, fixture.CollisionMask | StandingCollisionLayer, fixtureComponent);
                }
            }

            standingState.ChangedFixtures.Clear();
        }

        return true;
    }
}

[ByRefEvent]
public record struct DropHandItemsEvent();

/// <summary>
/// Subscribe if you can potentially block a down attempt.
/// </summary>
public sealed class DownAttemptEvent : CancellableEntityEventArgs
{
}

/// <summary>
/// Subscribe if you can potentially block a stand attempt.
/// </summary>
public sealed class StandAttemptEvent : CancellableEntityEventArgs
{
}

/// <summary>
/// Raised when an entity becomes standing
/// </summary>
public sealed class StoodEvent : EntityEventArgs
{
}

/// <summary>
/// Raised when an entity is not standing
/// </summary>
public sealed class DownedEvent : EntityEventArgs
{
}

/// <summary>
/// Raised after an entity falls down.
/// </summary>
public sealed class FellDownEvent : EntityEventArgs
{
    public EntityUid Uid { get; }

    public FellDownEvent(EntityUid uid)
    {
        Uid = uid;
    }
}

/// <summary>
/// Raised on the entity being thrown due to the holder falling down.
/// </summary>
[ByRefEvent]
public record struct FellDownThrowAttemptEvent(EntityUid Thrower, bool Cancelled = false);


