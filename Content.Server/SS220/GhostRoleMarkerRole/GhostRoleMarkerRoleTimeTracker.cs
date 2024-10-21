// © SS220, An EULA/CLA with a hosting restriction, full text: https://raw.githubusercontent.com/SerbiaStrong-220/space-station-14/master/CLA.txt

using Content.Server.Ghost.Roles;
using Content.Shared.Roles;

namespace Content.Server.SS220.GhostRoleMarkerRole;

public sealed class GhostRoleMarkerRoleTimeTracker : EntitySystem
{
    private const string UnknownRoleName = "game-ticker-unknown-role";
    private const string GhostRoleTracker = "JobGhostRole";
    private const string GhostRolePrototype = "GhostRole";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GhostRoleMarkerRoleComponent, MindGetAllRoleInfoEvent>(OnMindGetAllRoles);
    }

    private void OnMindGetAllRoles(EntityUid uid, GhostRoleMarkerRoleComponent component, ref MindGetAllRoleInfoEvent args)
    {
        string name = component.Name == null ? UnknownRoleName : component.Name;
        args.Roles.Add(new RoleInfo(name, false, GhostRoleTracker, GhostRolePrototype));
    }
}
