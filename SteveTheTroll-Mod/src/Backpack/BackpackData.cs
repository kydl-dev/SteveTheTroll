namespace SteveTheTroll.Backpack
{
    /// <summary>
    /// T-008b (D-053, D-054): the user's backpack construction from stt_dumpbuild (2026-10-27 20:22, 52 pieces, radius 10 m; the 2 stone_floor_2x2
    /// the user only placed so it would not crumble are left out -> 50 pieces: 8 iron_wall_1x1, 12 iron_floor_1x1_v2, 4 woodiron_beam, 5 stave_deco_wall_2x2, 9 woodiron_pole, 6 ashwood_beam_1m, 4 piece_chest_wood, 2 woodiron_beam_45).
    /// Two 45-degree struts on one side are the "shoulder straps" that lie against the troll's back. Numbers are relative to the centre
    /// between the struts (the pivot that sits on the troll's back); local -Z = towards the troll, +Y = up. Pure data (unit-tested: BackpackDataTests).
    /// </summary>
    internal static class BackpackData
    {
        /// Dump origin -> pivot (the struts' centre, x 0.126, y 1.5665, z -1.921 in the dump).
        internal const float PivotX = 0.126f, PivotY = 1.5665f, PivotZ = -1.921f;

        /// prefab, x, y, z (metres, dump frame), yaw (degrees). All pieces were placed upright (x / z rotation 0), scale 1.
        internal static readonly Piece[] Pieces =
        {
            new Piece("iron_wall_1x1", 1.128f, 1.5f, -0.221f, 270f),
            new Piece("iron_floor_1x1_v2", 0.06f, 2.851f, 0.629f, 270f),
            new Piece("woodiron_beam", -0.872f, 0.5f, 1.279f, 0f),
            new Piece("stave_deco_wall_2x2", 0.128f, 1.5f, 0.279f, 0f),
            new Piece("woodiron_pole", -0.872f, 1.5f, 0.279f, 270f),
            new Piece("iron_wall_1x1", -1.372f, 0.5f, -0.721f, 180f),
            new Piece("woodiron_pole", 2.128f, 1.5f, 1.279f, 270f),
            new Piece("iron_floor_1x1_v2", 1.06f, 2.851f, 0.629f, 270f),
            new Piece("iron_wall_1x1", -0.872f, 0.5f, -0.221f, 270f),
            new Piece("ashwood_beam_1m", 1.56f, 2.351f, -0.871f, 180f),
            new Piece("woodiron_pole", 1.128f, 1.5f, 0.279f, 270f),
            new Piece("iron_floor_1x1_v2", 1.128f, 1f, -0.221f, 270f),
            new Piece("piece_chest_wood", -1.674f, 0.55f, 0.111f, 270f),
            new Piece("iron_wall_1x1", -0.872f, 1.5f, -0.221f, 270f),
            new Piece("woodiron_pole", 1.128f, 1.5f, -0.721f, 270f),
            new Piece("woodiron_beam_45", 1.69f, 1.557f, -1.921f, 90f),
            new Piece("woodiron_pole", -1.872f, 1.5f, 1.279f, 270f),
            new Piece("iron_floor_1x1_v2", -1.94f, 2.851f, 0.629f, 270f),
            new Piece("iron_floor_1x1_v2", -1.94f, 2.851f, -0.371f, 270f),
            new Piece("woodiron_beam", 1.69f, 2.557f, -1.921f, 90f),
            new Piece("iron_floor_1x1_v2", 1.06f, 2.851f, -0.371f, 270f),
            new Piece("woodiron_beam", -1.438f, 2.576f, -1.921f, 90f),
            new Piece("ashwood_beam_1m", 1.06f, 2.351f, -0.371f, 270f),
            new Piece("ashwood_beam_1m", -1.44f, 2.351f, -0.871f, 180f),
            new Piece("ashwood_beam_1m", 1.628f, 0.5f, -0.721f, 180f),
            new Piece("iron_wall_1x1", 1.628f, 0.5f, -0.721f, 180f),
            new Piece("piece_chest_wood", -0.923f, 0.65f, 1.197f, 0f),
            new Piece("iron_floor_1x1_v2", -1.872f, 1f, 0.779f, 270f),
            new Piece("iron_floor_1x1_v2", -0.872f, 1f, 0.779f, 270f),
            new Piece("stave_deco_wall_2x2", 2.128f, 1.5f, 0.279f, 90f),
            new Piece("stave_deco_wall_2x2", 1.128f, 1.5f, 1.279f, 0f),
            new Piece("stave_deco_wall_2x2", -0.872f, 1.5f, 1.279f, 0f),
            new Piece("iron_floor_1x1_v2", 0.128f, 1f, 0.779f, 270f),
            new Piece("iron_floor_1x1_v2", 1.128f, 1f, 0.779f, 270f),
            new Piece("iron_wall_1x1", 1.128f, 0.5f, -0.221f, 270f),
            new Piece("woodiron_beam_45", -1.438f, 1.576f, -1.921f, 90f),
            new Piece("piece_chest_wood", 2.008f, 0.55f, 0.31f, 90f),
            new Piece("ashwood_beam_1m", -1.372f, 0.5f, -0.721f, 180f),
            new Piece("woodiron_pole", 2.128f, 1.5f, -0.721f, 270f),
            new Piece("piece_chest_wood", 1.133f, 0.65f, 1.301f, 0f),
            new Piece("iron_floor_1x1_v2", -0.94f, 2.851f, 0.629f, 270f),
            new Piece("ashwood_beam_1m", -0.94f, 2.351f, -0.371f, 270f),
            new Piece("iron_wall_1x1", -1.372f, 1.5f, -0.721f, 180f),
            new Piece("woodiron_pole", -1.872f, 1.5f, -0.721f, 270f),
            new Piece("iron_floor_1x1_v2", -1.872f, 1f, -0.221f, 270f),
            new Piece("stave_deco_wall_2x2", -1.872f, 1.5f, 0.279f, 270f),
            new Piece("woodiron_pole", -0.872f, 1.5f, -0.721f, 270f),
            new Piece("woodiron_beam", 1.128f, 0.5f, 1.279f, 0f),
            new Piece("iron_wall_1x1", 1.628f, 1.5f, -0.721f, 180f),
            new Piece("woodiron_pole", 0.128f, 1.5f, 1.279f, 270f),
        };

        /// The four wood chests (T-008c storage): indices into Pieces.
        internal static readonly int[] Chests = { 12, 26, 36, 39 };

        internal struct Piece
        {
            internal readonly string Prefab;
            internal readonly float X, Y, Z, Yaw;
            internal Piece(string prefab, float x, float y, float z, float yaw) { Prefab = prefab; X = x - PivotX; Y = y - PivotY; Z = z - PivotZ; Yaw = yaw; }
        }
    }
}
