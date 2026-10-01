namespace PickUpAndHaul.Planning;

/// <summary>
/// One place an item can be stored: either a cell of a slot group (<see cref="cell"/>) or a container Thing
/// (<see cref="container"/>) such as a building with its own ThingOwner. Moved out of WorkGiver_HaulToInventory unchanged.
/// </summary>
public struct StoreTarget : IEquatable<StoreTarget>
{
    public IntVec3 cell;
    public Thing container;
    public IntVec3 Position => container?.Position ?? cell;

    public StoreTarget(IntVec3 cell)
    {
        this.cell = cell;
        container = null;
    }
    public StoreTarget(Thing container)
    {
        cell = default;
        this.container = container;
    }

    public bool Equals(StoreTarget other) => container is null ? other.container is null && cell == other.cell : container == other.container;
    public override int GetHashCode() => container?.GetHashCode() ?? cell.GetHashCode();
    public override string ToString() => container?.ToString() ?? cell.ToString();
    public override bool Equals(object obj) => obj is StoreTarget target ? Equals(target) : obj is Thing thing ? container == thing : obj is IntVec3 intVec && cell == intVec;
    public static bool operator ==(StoreTarget left, StoreTarget right) => left.Equals(right);
    public static bool operator !=(StoreTarget left, StoreTarget right) => !left.Equals(right);
    public static implicit operator LocalTargetInfo(StoreTarget target) => target.container != null ? target.container : target.cell;
}
