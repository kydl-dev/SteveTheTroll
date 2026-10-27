namespace SteveTheTroll.Backpack
{
    /// <summary>
    /// T-008b (D-053): the user's backpack construction from stt_dumpbuild (2026-10-27 19:51, 23 pieces, radius 4 m): a 2 x 2 x 2 m iron cage
    /// (iron floors bottom / top, iron walls on 4 sides, wood-iron poles and beams) with two chests inside and two 45-degree struts on one
    /// side - the "shoulder straps" that lie against the troll's back. Numbers are relative to the centre between the struts (the pivot that
    /// sits on the troll's back); local -Z = towards the troll, +Y = up. Pure data (unit-tested: BackpackDataTests).
    /// </summary>
    internal static class BackpackData
    {
        /// Dump origin -> pivot (the struts' centre, x -0.04, y 1, z -1.735 in the dump).
        internal const float PivotX = -0.04f, PivotY = 1f, PivotZ = -1.735f;

        /// prefab, x, y, z (metres, dump frame), yaw (degrees). All pieces were placed upright (x / z rotation 0), scale 1.
        internal static readonly Piece[] Pieces =
        {
            new Piece("iron_wall_2x2", -0.04f, 1f, -0.735f, 180f),
            new Piece("woodiron_beam_45", 0.96f, 1f, -1.735f, 90f),
            new Piece("woodiron_beam", -1.04f, 0f, 0.265f, 90f),
            new Piece("iron_floor_2x2", -0.04f, 0f, 0.265f, 90f),
            new Piece("woodiron_pole", 0.96f, 1f, -0.735f, 90f),
            new Piece("iron_wall_2x2", -0.04f, 1f, 1.265f, 180f),
            new Piece("woodiron_beam", 0.96f, 2f, -1.735f, 90f),
            new Piece("woodiron_beam", -0.04f, 0f, 1.265f, 180f),
            new Piece("woodiron_beam", -1.04f, 2f, 0.265f, 90f),
            new Piece("woodiron_beam", 0.96f, 2f, 0.265f, 270f),
            new Piece("iron_floor_2x2", -0.04f, 2f, 0.265f, 180f),
            new Piece("woodiron_beam", -0.04f, 0f, -0.735f, 0f),
            new Piece("woodiron_pole", 0.96f, 1f, 1.265f, 90f),
            new Piece("woodiron_beam", -0.04f, 2f, -0.735f, 0f),
            new Piece("woodiron_pole", -1.04f, 1f, 1.265f, 90f),
            new Piece("woodiron_beam", 0.96f, 0f, 0.265f, 270f),
            new Piece("woodiron_beam", -0.04f, 2f, 1.265f, 180f),
            new Piece("iron_wall_2x2", 0.96f, 1f, 0.265f, 90f),
            new Piece("piece_chest", -0.12f, 0.052f, 0.944f, 0f),
            new Piece("piece_chest", -0.036f, 0.052f, -0.508f, 180f),
            new Piece("woodiron_beam_45", -1.04f, 1f, -1.735f, 90f),
            new Piece("iron_wall_2x2", -1.04f, 1f, 0.265f, 90f),
            new Piece("woodiron_pole", -1.04f, 1f, -0.735f, 90f),
        };

        /// The two chests (T-008c storage): indices into Pieces.
        internal static readonly int[] Chests = { 18, 19 };

        internal struct Piece
        {
            internal readonly string Prefab;
            internal readonly float X, Y, Z, Yaw;
            internal Piece(string prefab, float x, float y, float z, float yaw) { Prefab = prefab; X = x - PivotX; Y = y - PivotY; Z = z - PivotZ; Yaw = yaw; }
        }
    }
}
