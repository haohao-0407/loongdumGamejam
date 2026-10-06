using System;
using UnityEngine;

namespace Loongdum.Levels
{
    [Serializable]
    public sealed class LevelTwoLayout
    {
        public string title;
        public string[] rows;
    }

    /// <summary>
    /// Second-level rules, independent of rendering and input. Coordinates are (column, row),
    /// zero-based, with row zero at the north edge of the supplied diagram.
    /// </summary>
    public sealed class LevelTwoModel
    {
        public const int LightRadius = 3;
        public const int MaxPortalHops = 3;
        private const string BlocksBoth = "#GApPB";
        private const string Doors = "aXYZ";
        private readonly string[] rows;
        private readonly bool[,] lit;
        private readonly Vector2Int portalHome;
        private readonly Vector2Int portalWall;
        public int Width => rows[0].Length;
        public int Height => rows.Length;
        public Vector2Int Lower { get; private set; }
        public Vector2Int Upper { get; }
        public Vector2Int Lever { get; }
        public Vector2Int LeverB { get; }
        public Vector2Int Plate { get; }
        /// <summary>The blue mirror's cell. It never moves.</summary>
        public Vector2Int PortalEntry { get; }
        /// <summary>The orange mirror's own cell, which it gives up for the wall above the far lever.</summary>
        public Vector2Int PortalHome => portalHome;
        /// <summary>The wall above the far lever that the orange mirror trades places with.</summary>
        public Vector2Int PortalWall => portalWall;
        /// <summary>Where the orange mirror stands right now: its own cell, or — once the near lever
        /// has been thrown — the cell above the far lever, where its glow points that lever out.</summary>
        public Vector2Int PortalExit => LeverThrown ? portalWall : portalHome;
        public bool LeverThrown { get; private set; }
        public bool LeverBThrown { get; private set; }
        public bool PlatePressed => Lower == Plate;
        public bool Won => Lower == Upper;
        public int MoveCount { get; private set; }
        public string Feedback { get; private set; }

        public LevelTwoModel(string[] layout)
        {
            if (layout == null || layout.Length != 11)
                throw new ArgumentException("Level 02 must contain 11 rows.");
            rows = (string[])layout.Clone();
            foreach (string row in rows)
            {
                if (row == null || row.Length != 11)
                    throw new ArgumentException("Level 02 rows must contain 11 cells.");
                foreach (char cell in row)
                    if ("#.GLUA1aXYpPBZ".IndexOf(cell) < 0)
                        throw new ArgumentException("Unknown Level 02 tile: " + cell);
            }
            Upper = FindUnique('U');
            Lever = FindUnique('A');
            LeverB = FindUnique('B');
            Plate = FindUnique('1');
            PortalEntry = FindUnique('p');
            portalHome = FindUnique('P');
            // The cell the orange mirror is swapped onto: straight north of the far lever (row zero
            // is north, so north is one row down), and a plain wall, so that swapping the two leaves
            // the board's reachability untouched.
            portalWall = LeverB + Vector2Int.down;
            if (!Contains(portalWall) || rows[portalWall.y][portalWall.x] != '#')
                throw new ArgumentException("The far lever needs a wall directly north of it.");
            FindUnique('a');
            FindUnique('X');
            FindUnique('Y');
            FindUnique('Z');
            lit = new bool[Width, Height];
            Reset();
        }

        private Vector2Int FindUnique(char symbol)
        {
            Vector2Int found = new Vector2Int(-1, -1);
            for (int r = 0; r < Height; r++)
                for (int c = 0; c < Width; c++)
                    if (rows[r][c] == symbol)
                    {
                        if (found.x >= 0) throw new ArgumentException("Duplicate tile: " + symbol);
                        found = new Vector2Int(c, r);
                    }
            if (found.x < 0) throw new ArgumentException("Missing tile: " + symbol);
            return found;
        }

        public void Reset()
        {
            Lower = FindUnique('L');
            LeverThrown = false;
            LeverBThrown = false;
            MoveCount = 0;
            Feedback = "沿着脚边能摸到的地方，去找光。";
            RecalculateLight();
        }

        public bool Contains(Vector2Int cell) => cell.x >= 0 && cell.y >= 0
            && cell.x < Width && cell.y < Height;

        /// <summary>The tile actually standing on a cell. The near lever trades the orange mirror and
        /// the wall above the far lever: the picture shows the two cells at rest, and the swap only
        /// takes effect once that lever is thrown.</summary>
        public char Tile(Vector2Int cell)
        {
            if (!Contains(cell)) return '#';
            if (LeverThrown)
            {
                if (cell == portalWall) return 'P';
                if (cell == portalHome) return '#';
            }
            return rows[cell.y][cell.x];
        }

        public bool IsOpen(char tile) => tile == 'X' ? LeverThrown
            : tile == 'Y' ? !LeverThrown
            : tile == 'Z' ? LeverBThrown
            : tile == 'a' && PlatePressed;

        public bool BlocksMovement(Vector2Int cell)
        {
            char tile = Tile(cell);
            // Mirror frames and both lever pedestals occupy their cell: each would otherwise create
            // a shortcut past the doors in the source design. The mirror is swapped for a wall rather
            // than slid onto floor, so the two cells it trades between block exactly as before.
            return BlocksBoth.IndexOf(tile) >= 0 || (Doors.IndexOf(tile) >= 0 && !IsOpen(tile));
        }

        private bool BlocksLight(char tile) =>
            tile == '#' || (Doors.IndexOf(tile) >= 0 && !IsOpen(tile));

        public bool CanReachLeverFrom(Vector2Int cell) => WithinReach(cell, Lever);

        public bool CanReachLever => WithinReach(Lower, Lever) || WithinReach(Lower, LeverB);

        private static bool WithinReach(Vector2Int from, Vector2Int to)
        {
            Vector2Int delta = from - to;
            return delta != Vector2Int.zero && Math.Abs(delta.x) <= 1 && Math.Abs(delta.y) <= 1;
        }

        /// <summary>Records the cell the lower body has walked into. The first level's own movement
        /// module does the walking, so the rules read where the body ended up rather than granting it a
        /// step; the grid is only how this map was measured out. A cell off the board or filled by an
        /// obstacle is ignored, though the walk cannot carry the body into one — the obstacle is a
        /// solid body in the way.</summary>
        public void SetLower(Vector2Int cell)
        {
            if (Won || cell == Lower || !Contains(cell) || BlocksMovement(cell)) return;
            bool wasOnPlate = PlatePressed;
            Lower = cell;
            MoveCount++;
            RecalculateLight();
            Feedback = Won ? "终于，重新走到一起。"
                : PlatePressed ? "脚下压住了机关。远处，一束光亮了。"
                : wasOnPlate ? "松开了。记住刚才亮起的路。"
                : CanReachLever ? "够得着拉杆了。按 E 拨动。" : "";
        }

        /// <summary>One step in a cardinal direction, to the rules's own grid. The scene does not walk
        /// this way any more — this is the route the checks and the solvability search travel by.</summary>
        public bool TryMove(Vector2Int direction)
        {
            if (Won || Math.Abs(direction.x) + Math.Abs(direction.y) != 1) return false;
            Vector2Int next = Lower + direction;
            if (BlocksMovement(next))
            {
                char tile = Tile(next);
                Feedback = tile == 'G' ? "玻璃：光能过去，脚过不去。"
                    : tile == 'A' ? "拉杆就在手边。按 E 拨动。"
                    : tile == 'p' || tile == 'P' ? "镜面只让视线通过。"
                    : Doors.IndexOf(tile) >= 0 ? "门关着。" : "摸到了一面墙。";
                return false;
            }
            SetLower(next);
            return true;
        }

        public bool TryInteract()
        {
            if (Won) return false;
            if (!CanReachLever)
            {
                Feedback = "这里够不到拉杆。";
                return false;
            }
            if (WithinReach(Lower, Lever))
            {
                char closingDoor = LeverThrown ? 'X' : 'Y';
                if (Tile(Lower) == closingDoor)
                {
                    Feedback = "先走出门框，再拨动拉杆。";
                    return false;
                }
                LeverThrown = !LeverThrown;
                RecalculateLight();
                Feedback = LeverThrown
                    ? "拉杆落下。一扇门开，另一扇门关。远处的橙镜换了位置。"
                    : "拉杆抬起。门换回来了，橙镜也归了位。";
                return true;
            }
            LeverBThrown = !LeverBThrown;
            RecalculateLight();
            Feedback = LeverBThrown
                ? "拉杆落下。脚边的门开了。"
                : "拉杆抬起。门又关上了。";
            return true;
        }

        public bool IsLit(Vector2Int cell) => Contains(cell) && lit[cell.x, cell.y];
        public bool IsFelt(Vector2Int cell) => Contains(cell)
            && Math.Abs(cell.x - Lower.x) + Math.Abs(cell.y - Lower.y) == 1;
        public bool IsVisible(Vector2Int cell) => IsLit(cell) || IsFelt(cell) || cell == Lower;

        private void RecalculateLight()
        {
            Array.Clear(lit, 0, lit.Length);
            lit[Upper.x, Upper.y] = true;
            for (int dr = -1; dr <= 1; dr++)
                for (int dc = -1; dc <= 1; dc++)
                {
                    if (dr == 0 && dc == 0) continue;
                    Vector2Int cursor = Upper;
                    Vector2Int direction = new Vector2Int(dc, dr);
                    int budget = LightRadius;
                    int hops = 0;
                    while (budget-- > 0)
                    {
                        cursor += direction;
                        if (!Contains(cursor)) break;
                        lit[cursor.x, cursor.y] = true;
                        char tile = Tile(cursor);
                        if (BlocksLight(tile)) break;
                        if (tile != 'p' && tile != 'P') continue;
                        if (hops++ >= MaxPortalHops) break;
                        cursor = tile == 'p' ? PortalExit : PortalEntry;
                        lit[cursor.x, cursor.y] = true;
                        budget = LightRadius;
                    }
                }
        }
    }
}
