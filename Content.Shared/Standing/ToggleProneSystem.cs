using Content.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;

namespace Content.Shared.Standing;

public sealed class ToggleProneSystem : EntitySystem
{
    [Dependency] private readonly StandingStateSystem _standing = default!;

    public override void Initialize()
    {
        base.Initialize();

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.ToggleProne, InputCmdHandler.FromDelegate(HandleToggle, handle: false))
            .Register<ToggleProneSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<ToggleProneSystem>();
    }

    private void HandleToggle(ICommonSession? session)
    {
        if (session?.AttachedEntity is not { Valid: true } uid || !Exists(uid))
            return;

        if (!TryComp<StandingStateComponent>(uid, out var standing))
            return;

        if (standing.Standing)
            _standing.Down(uid, playSound: false, dropHeldItems: false);
        else
            _standing.Stand(uid);
    }
}
