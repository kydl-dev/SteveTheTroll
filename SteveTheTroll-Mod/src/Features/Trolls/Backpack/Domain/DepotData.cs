namespace SteveTheTroll.Features.Trolls.Backpack.Domain;

/// <summary>
///     D-064: the user's troll depot (stt_dumpbuild 6, 2026-10-27 23:07, 14 pieces; the 2 stone_floor_2x2 support floors
///     left out): a
///     4 x 2 m frame of 6 wood poles (1 m) under 2 wood floors (2 x 2 m) with 4 reinforced chests below, back to back. The
///     dump was turned
///     by -45 degrees (it was built diagonally), lowered by the 0.5 m of the stone floors and centred; the chests are
///     snapped to the middle of
///     their 2 x 2 cell (x +-1, z +-0.52). +Y up, y 0 = the ground. Depots stack up to MaxStack high (the next one stands
///     on the floors).
///     Pure data (unit-tested: DepotDataTests).
/// </summary>
internal static class DepotData
{
    internal const int MaxStack = 5;

    /// Footprint half sizes (m) with a little room around it, for the free-space check.
    internal const float HalfX = 2.3f, HalfZ = 1.3f;

    internal const string ChestPrefab = "piece_chest";

    internal static readonly Part[] Frame =
    {
        new("wood_pole", -2f, 0.5f, -1f, 180f),
        new("wood_pole", 0f, 0.5f, -1f, 180f),
        new("wood_pole", 2f, 0.5f, -1f, 180f),
        new("wood_pole", -2f, 0.5f, 1f, 180f),
        new("wood_pole", 0f, 0.5f, 1f, 180f),
        new("wood_pole", 2f, 0.5f, 1f, 180f),
        new("wood_floor", -1f, 1f, 0f, 0f),
        new("wood_floor", 1f, 1f, 0f, 0f)
    };

    /// The 4 chests: two facing -Z, two facing +Z (yaw 180 / 0), standing on the ground of their level.
    internal static readonly Part[] Chests =
    {
        new(ChestPrefab, -1f, 0f, -0.52f, 180f),
        new(ChestPrefab, 1f, 0f, -0.52f, 180f),
        new(ChestPrefab, -1f, 0f, 0.52f, 0f),
        new(ChestPrefab, 1f, 0f, 0.52f, 0f)
    };

    /// D-090 / D-095: how far the floors go up so the chest lids stay `clearance` below the floors' top face (both heights in m above the
    /// depot's ground; a lid inside the floor slab is hidden). Expected outcome: 0 when they already clear it, else the missing height.
    internal static float FloorLift(float chestTop, float floorTop, float clearance)
    {
        float need = chestTop + clearance - floorTop;
        return need > 0f ? need : 0f;
    }

    internal struct Part
    {
        internal readonly string Prefab;
        internal readonly float X, Y, Z, Yaw;

        internal Part(string prefab, float x, float y, float z, float yaw)
        {
            Prefab = prefab;
            X = x;
            Y = y;
            Z = z;
            Yaw = yaw;
        }
    }
}
