// © SS220, An EULA/CLA with a hosting restriction, full text: https://raw.githubusercontent.com/SerbiaStrong-220/space-station-14/master/CLA.txt

using Robust.Shared.Serialization;

namespace Content.Shared.SS220.Discord;

[Serializable, NetSerializable]
public enum SponsorTier
{
    None,

    Mentor,
    MiniDeveloper,
    Developer,
    Moderator,
    Administrator,
    HeadAdministrator,
    SeniorAdministrator,
    SubHeadAdministrator,
    HeadLoroved,
    WikiHead,
    HeadModerator,
    HeadDeveloper,
    SubHeadDeveloper,
    ProjectManager,
    SeniorDeveloper,
    Mapper,

    // Тиры подписок должны идти в конце, для упрощения вычисления лучшего уровня поддержки.
    Shlopa,
    BigShlopa,
    HugeShlopa,
    GoldenShlopa,
    CriticalMassShlopa,
}
