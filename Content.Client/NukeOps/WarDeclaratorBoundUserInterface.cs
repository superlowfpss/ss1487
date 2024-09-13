using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.NukeOps;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client.NukeOps;

[UsedImplicitly]
public sealed class WarDeclaratorBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    [ViewVariables]
    private WarDeclaratorWindow? _window;

    public WarDeclaratorBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) {}

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<WarDeclaratorWindow>();
        _window.OnActivated += OnWarDeclaratorActivated;
        _window.SetMaxMessageLength(_cfg.GetCVar(CCVars.ChatMaxAnnouncementLength)); // SS220 Text Edit Limits
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (_window == null || state is not WarDeclaratorBoundUserInterfaceState cast)
            return;

        _window?.UpdateState(cast);
    }

    private void OnWarDeclaratorActivated(string message)
    {
        var maxLength = _cfg.GetCVar(CCVars.ChatMaxAnnouncementLength);
        var msg = SharedChatSystem.SanitizeAnnouncement(message, maxLength);
        SendMessage(new WarDeclaratorActivateMessage(msg));

        //ss220 SPAM Button NukeDecWar fix start
        if (_window != null)
        {
            _window.WarButton.Disabled = true;
        }
        //ss220 SPAM Button NukeDecWar fix end
    }
}
