// © SS220, An EULA/CLA with a hosting restriction, full text: https://raw.githubusercontent.com/SerbiaStrong-220/space-station-14/master/CLA.txt

using Content.Server.Antag;
using Content.Server.Body.Components;
using Content.Server.EUI;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Server.Objectives.Components;
using Content.Server.Objectives.Systems;
using Content.Server.Popups;
using Content.Server.Roles;
using Content.Server.SS220.MindSlave.UI;
using Content.Shared.Alert;
using Content.Shared.Cloning;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Objectives.Systems;
using Content.Shared.Roles;
using Content.Shared.SS220.MindSlave;
using Content.Shared.Tag;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.SS220.MindSlave;

public sealed class MindSlaveSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly RoleSystem _role = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly AntagSelectionSystem _antagSelection = default!;
    [Dependency] private readonly SharedObjectivesSystem _objectives = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly TargetObjectiveSystem _targetObjective = default!;
    [Dependency] private readonly TraitorRuleSystem _traitorRule = default!;
    [Dependency] private readonly AlertsSystem _alert = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly EuiManager _eui = default!;
    [Dependency] private readonly SharedSubdermalImplantSystem _implant = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    [ValidatePrototypeId<EntityPrototype>]
    private const string MindSlaveAntagId = "MindRoleMindSlave";

    [ValidatePrototypeId<EntityPrototype>]
    private const string MindSlaveObjectiveId = "MindSlaveObeyObjective";

    [ValidatePrototypeId<NpcFactionPrototype>]
    private const string NanoTrasenFactionId = "NanoTrasen";

    [ValidatePrototypeId<NpcFactionPrototype>]
    private const string SyndicateFactionId = "Syndicate";

    [ValidatePrototypeId<TagPrototype>]
    private const string MindSlaveImplantTag = "MindSlave";

    private readonly SoundSpecifier GreetSoundNotification = new SoundPathSpecifier("/Audio/Ambience/Antag/traitor_start.ogg");

    [ValidatePrototypeId<AlertPrototype>]
    private const string EnslavedAlert = "MindSlaved";

    /// <summary>
    /// Dictionary, containing list of all enslaved minds (as a key), and their master (as a value).
    /// </summary>
    public Dictionary<EntityUid, EntityUid> EnslavedMinds { get; private set; } = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MindSlaveMasterComponent, MobStateChangedEvent>(OnMasterDeadOrCrit);
        SubscribeLocalEvent<MindSlaveMasterComponent, BeingGibbedEvent>(OnMasterGibbed);
        SubscribeLocalEvent<MindSlaveComponent, CloningEvent>(OnCloned);
        SubscribeLocalEvent<SubdermalImplantComponent, MindSlaveRemoved>(OnMindSlaveRemoved);
    }

    private void OnMasterGibbed(Entity<MindSlaveMasterComponent> entity, ref BeingGibbedEvent args)
    {
        if (entity.Comp.EnslavedEntities.Count <= 0)
            return;

        // This is disgusting, but I need to get rid of a reference
        RemoveSlaves(new List<EntityUid>(entity.Comp.EnslavedEntities));
    }

    private void OnMasterDeadOrCrit(Entity<MindSlaveMasterComponent> entity, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead && args.NewMobState != MobState.Critical)
            return;

        if (entity.Comp.EnslavedEntities.Count <= 0)
            return;

        var message = args.NewMobState == MobState.Dead ? Loc.GetString("mindslave-master-dead") : Loc.GetString("mindslave-master-crit");
        foreach (var slave in entity.Comp.EnslavedEntities)
        {
            _popup.PopupEntity(message, slave, slave, Shared.Popups.PopupType.LargeCaution);
            _antagSelection.SendBriefing(slave, message, Color.Red, null);
        }
    }

    private void OnCloned(Entity<MindSlaveComponent> entity, ref CloningEvent args)
    {
        TryRemoveSlave(entity);
    }

    private void OnMindSlaveRemoved(Entity<SubdermalImplantComponent> mind, ref MindSlaveRemoved args)
    {
        if (args.Slave == null || !IsEnslaved(args.Slave.Value))
            return;

        TryRemoveSlave(args.Slave.Value);
    }

    //I need all of this for the TraitorMinds list
    private void GetTraitorGamerule(out EntityUid? ruleEntity, out TraitorRuleComponent? component)
    {
        var gameRules = _gameTicker.GetActiveGameRules().GetEnumerator();
        ruleEntity = null;
        while (gameRules.MoveNext())
        {
            if (!HasComp<TraitorRuleComponent>(gameRules.Current))
                continue;

            ruleEntity = gameRules.Current;
            break;
        }

        TryComp(ruleEntity, out component);
    }

    /// <summary>
    /// Makes entity a slave, converting it into an antag.
    /// </summary>
    /// <param name="slave">Entity to be enslaved.</param>
    /// <param name="master">Master of the given entity.</param>
    /// <returns>Whether enslaving were succesfull.</returns>
    public bool TryMakeSlave(EntityUid slave, EntityUid master)
    {
        if (IsEnslaved(slave))
            return false;

        if (!_mind.TryGetMind(slave, out var mindId, out var mindComp))
            return false;

        if (!_mind.TryGetMind(master, out var masterMindId, out var masterMindComp))
            return false;

        if (HasComp<MindShieldComponent>(slave))
        {
            _popup.PopupEntity(Loc.GetString("mindslave-target-mindshielded"), slave, master);
            return false;
        }

        var objective = _objectives.TryCreateObjective(mindId, mindComp, MindSlaveObjectiveId);
        _role.MindAddRole(mindId, MindSlaveAntagId, mindComp, true);
        if (_role.MindHasRole<MindSlaveRoleComponent>(mindId, out var slaveRole))
        {
            slaveRole.Value.Comp2.masterEntity = master;
            slaveRole.Value.Comp2.objectiveEntity = objective;
        }

        var masterName = masterMindComp.CharacterName ?? Loc.GetString("mindslave-unknown-master");
        var briefing = Loc.GetString("mindslave-briefing-slave", ("master", masterName));
        _antagSelection.SendBriefing(slave, briefing, Color.Red, GreetSoundNotification);
        _popup.PopupEntity(briefing, slave, slave, Shared.Popups.PopupType.LargeCaution);

        if (mindComp.CharacterName != null)
        {
            var briefingMaster = Loc.GetString("mindslave-briefing-slave-master", ("name", mindComp.CharacterName), ("ent", slave));
            _popup.PopupEntity(briefingMaster, slave, master);
            _antagSelection.SendBriefing(master, briefingMaster, null, null);
        }

        //If we fail to add objective - try to use briefing for that...
        if (objective != null && TryComp<TargetObjectiveComponent>(objective.Value, out var targetObjective))
        {
            _targetObjective.SetTarget(objective.Value, masterMindId, targetObjective);
            _targetObjective.ResetEntityName(objective.Value, targetObjective);
            _mind.AddObjective(mindId, mindComp, objective.Value);
        }
        else if (slaveRole is { } slaveRoleVal)
        {
            EnsureComp<RoleBriefingComponent>(slaveRoleVal).Briefing = briefing;
        }

        EnsureComp<MindSlaveComponent>(slave);
        _alert.ShowAlert(slave, EnslavedAlert);

        var masterComp = EnsureComp<MindSlaveMasterComponent>(master);
        masterComp.EnslavedEntities.Add(slave);
        Dirty(master, masterComp);

        _npcFaction.RemoveFaction(slave, NanoTrasenFactionId, false);
        _npcFaction.AddFaction(slave, SyndicateFactionId);

        EnslavedMinds.Add(mindId, masterMindId);

        if (mindComp.UserId != null && _playerManager.TryGetSessionById(mindComp.UserId.Value, out var session))
            _eui.OpenEui(new MindSlaveNotificationEui(masterName, true), session);

        //Remove pacifism from slave, because if the slave is pacified they becomes pretty useless.
        if (TryComp<PacifiedComponent>(slave, out var pacified))
            RemComp(slave, pacified);

        return true;
    }

    /// <summary>
    /// Removes MindSlave from enslaved entity.
    /// </summary>
    /// <param name="slave">Enslaved entity.</param>
    /// <returns>Whether removing MindSlave were succesfull.</returns>
    public bool TryRemoveSlave(EntityUid slave)
    {
        if (!IsEnslaved(slave))
            return false;

        if (!_mind.TryGetMind(slave, out var mindId, out var mindComp))
            return false;

        if (!_role.MindHasRole<MindSlaveRoleComponent>(mindId, out var mindSlave))
            return false;

        var briefing = Loc.GetString("mindslave-removed-slave");
        _antagSelection.SendBriefing(slave, briefing, Color.Red, null);
        _popup.PopupEntity(briefing, slave, slave, Shared.Popups.PopupType.LargeCaution);

        var master = mindSlave.Value.Comp2.masterEntity;
        if (master != null && TryComp<MindSlaveMasterComponent>(master.Value, out var masterComponent))
        {
            var briefingMaster = mindComp.CharacterName != null ? Loc.GetString("mindslave-removed-slave-master", ("name", mindComp.CharacterName), ("ent", slave)) :
                Loc.GetString("mindslave-removed-slave-master-unknown");

            _antagSelection.SendBriefing(master.Value, briefingMaster, Color.Red, null);
            _popup.PopupEntity(briefingMaster, master.Value, master.Value, Shared.Popups.PopupType.MediumCaution);

            masterComponent.EnslavedEntities.Remove(slave);
            Dirty(master.Value, masterComponent);
        }
        var masterName = master != null ? Name(master.Value) : string.Empty;

        _role.MindRemoveRole<MindSlaveRoleComponent>(mindId);

        //If slave had a valid objective - remove it, otherwise - remove briefing
        var objective = mindSlave.Value.Comp2.objectiveEntity;
        if (objective != null)
            _mind.TryRemoveObjective(mindId, mindComp, objective.Value);
        else
            _role.MindTryRemoveRole<RoleBriefingComponent>(mindId);

        RemComp<MindSlaveComponent>(slave);
        _alert.ClearAlert(slave, EnslavedAlert);

        _npcFaction.RemoveFaction(slave, SyndicateFactionId, false);
        _npcFaction.AddFaction(slave, NanoTrasenFactionId);

        EnslavedMinds.Remove(mindId);

        if (mindComp.UserId != null && master != null && _playerManager.TryGetSessionById(mindComp.UserId.Value, out var session))
            _eui.OpenEui(new MindSlaveNotificationEui(masterName, false), session);

        // Remove implant if has one
        // Recursion's not happening because all the other stuff is already removed, so it will not proceed with checks.
        if (TryComp<ImplantedComponent>(slave, out var implantComp))
        {
            var implants = implantComp.ImplantContainer.ContainedEntities;

            EntityUid? mindslaveImplant = null;
            foreach (var implant in implants)
            {
                if (!_tag.HasTag(implant, MindSlaveImplantTag))
                    continue;

                mindslaveImplant = implant;
            }

            if (mindslaveImplant != null)
                _implant.ForceRemove(slave, mindslaveImplant.Value);
        }

        return true;
    }

    public void RemoveSlaves(List<EntityUid> slaves)
    {
        if (slaves.Count <= 0)
            return;

        foreach (var slave in slaves)
            TryRemoveSlave(slave);
    }

    /// <summary>
    /// Returns whether the given entity is mind-enslaved by someone.
    /// </summary>
    /// <param name="entity">Entity to be checked.</param>
    /// <returns>Whether the entity is mind-enslaved.</returns>
    public bool IsEnslaved(EntityUid entity)
    {
        if (!_mind.TryGetMind(entity, out var mindId, out var mindComp))
            return false;

        return _role.MindHasRole<MindSlaveRoleComponent>(mindId);
    }
}
