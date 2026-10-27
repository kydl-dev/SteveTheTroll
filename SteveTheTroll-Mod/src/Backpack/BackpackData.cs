namespace SteveTheTroll.Backpack
{
    /// <summary>
    /// T-008b (D-053, D-057): the user's backpack construction from stt_dumpbuild (2026-10-27 20:51, 43 pieces, radius 10 m; the 2 stone_floor_2x2
    /// the user only placed so it would not crumble are left out -> 41 pieces: 19 woodiron_beam, 6 iron_wall_2x2, 6 woodiron_pole, 4 piece_chest, 4 iron_floor_2x2, 2 woodiron_beam_45).
    /// D-057: the cage pieces are snapped to their 1 m grid (x = n + 0.02, z = n + 0.325; the game placed them up to 1.3 cm off); the 4 reinforced
    /// chests are mirrored about the cage centre line x 0.02 (each pair gets the average of the user's two placements; side chests on the floor, y 0.502).
    /// Two 45-degree struts (+ the beams on top) are the "shoulder straps" that lie against the troll's back. Numbers are relative to the centre
    /// between the struts (the pivot that sits on the troll's back); local -Z = towards the troll, +Y = up. Pure data (unit-tested: BackpackDataTests).
    /// </summary>
    internal static class BackpackData
    {
        /// Dump origin -> pivot (the cage centre line = the struts' centre, x 0.02, y 1.5, z -2.675 in the dump).
        internal const float PivotX = 0.02f, PivotY = 1.5f, PivotZ = -2.675f;

        /// prefab, x, y, z (metres, dump frame), yaw (degrees). All pieces were placed upright (x / z rotation 0), scale 1.
        internal static readonly Piece[] Pieces =
        {
            new Piece("woodiron_beam", -1.98f, 0.5f, 0.325f, 90f),
            new Piece("iron_wall_2x2", 1.02f, 1.5f, 1.325f, 0f),
            new Piece("woodiron_beam", -0.98f, 0.5f, 1.325f, 0f),
            new Piece("woodiron_beam", 2.02f, 0.5f, 0.325f, 90f),
            new Piece("woodiron_beam", 2.02f, 2.5f, 0.325f, 90f),
            new Piece("woodiron_pole", 2.02f, 1.5f, -0.675f, 270f),
            new Piece("woodiron_beam", 1.02f, 0.5f, 1.325f, 0f),
            new Piece("iron_wall_2x2", 1.02f, 1.5f, -0.675f, 180f),
            new Piece("woodiron_pole", 0.02f, 1.5f, 1.325f, 90f),
            new Piece("iron_wall_2x2", -1.98f, 1.5f, 0.325f, 270f),
            new Piece("woodiron_beam", 1.02f, 2.5f, 1.325f, 0f),
            new Piece("piece_chest", 1.813f, 0.502f, 0.2045f, 90f),
            new Piece("woodiron_pole", -1.98f, 1.5f, -0.675f, 270f),
            new Piece("woodiron_beam", -0.98f, 2.5f, 1.325f, 0f),
            new Piece("piece_chest", -0.835f, 0.652f, 1.4025f, 0f),
            new Piece("woodiron_beam", 2.02f, 0.5f, -0.675f, 90f),
            new Piece("iron_floor_2x2", 0.02f, 0.5f, 0.325f, 0f),
            new Piece("woodiron_beam", 2.02f, 2.5f, -2.675f, 90f),
            new Piece("woodiron_beam", 2.02f, 2.5f, -0.675f, 90f),
            new Piece("woodiron_pole", -1.98f, 1.5f, 1.325f, 270f),
            new Piece("piece_chest", -1.773f, 0.502f, 0.2045f, 270f),
            new Piece("iron_floor_2x2", 1.02f, 2.5f, 0.325f, 270f),
            new Piece("woodiron_beam", -1.98f, 2.5f, -0.675f, 90f),
            new Piece("iron_wall_2x2", -0.98f, 1.5f, -0.675f, 180f),
            new Piece("iron_wall_2x2", 2.02f, 1.5f, 0.325f, 90f),
            new Piece("woodiron_beam", -0.98f, 0.5f, -0.675f, 0f),
            new Piece("woodiron_beam", -1.98f, 2.5f, 0.325f, 90f),
            new Piece("iron_wall_2x2", -0.98f, 1.5f, 1.325f, 0f),
            new Piece("woodiron_beam_45", -1.98f, 1.5f, -2.675f, 90f),
            new Piece("woodiron_beam", -0.98f, 2.5f, -0.675f, 0f),
            new Piece("woodiron_beam_45", 2.02f, 1.5f, -2.675f, 90f),
            new Piece("woodiron_pole", 2.02f, 1.5f, 1.325f, 270f),
            new Piece("woodiron_beam", 1.02f, 2.5f, -0.675f, 0f),
            new Piece("woodiron_beam", 0.02f, 2.5f, 0.325f, 90f),
            new Piece("woodiron_pole", 0.02f, 1.5f, -0.675f, 90f),
            new Piece("piece_chest", 0.875f, 0.652f, 1.4025f, 0f),
            new Piece("iron_floor_2x2", -0.98f, 0.5f, 0.325f, 0f),
            new Piece("woodiron_beam", -1.98f, 2.5f, -2.675f, 90f),
            new Piece("woodiron_beam", 1.02f, 0.5f, -0.675f, 0f),
            new Piece("woodiron_beam", -1.98f, 0.5f, -0.675f, 90f),
            new Piece("iron_floor_2x2", -0.98f, 2.5f, 0.325f, 270f),
        };

        /// The four reinforced chests = 4 inventories (T-008c storage, D-055 / D-056): indices into Pieces.
        internal static readonly int[] Chests = { 11, 14, 20, 35 };

        internal struct Piece
        {
            internal readonly string Prefab;
            internal readonly float X, Y, Z, Yaw;
            internal Piece(string prefab, float x, float y, float z, float yaw) { Prefab = prefab; X = x - PivotX; Y = y - PivotY; Z = z - PivotZ; Yaw = yaw; }
        }
    }
}
