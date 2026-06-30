/// <summary>
/// Shared category tags for designated playfield sections and the component
/// groups that can be installed into them. A group can only be installed into a
/// section whose category matches the group's category. Mirrors the
/// <see cref="BoardComponentType"/> tagging pattern.
/// </summary>
public enum BoardSectionCategory
{
    BumperCluster,
    FlipperLane,
    TargetBank,
    PortalPair,
    LauncherLane,
    SpinnerArea,
    Mixed,
    Other
}
