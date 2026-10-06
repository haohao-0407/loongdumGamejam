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
        private readonly string[] rows;
        private readonly bool[,] lit;
        public int Width => rows[0].Length;
        public int Height => rows.Length;
        public Vector2Int Lower { get; private set; }
        public Vector2Int Upper { get; }
        public Vector2Int Lever { get; }
        public Vector2Int Plate { get; }
        public Vector2Int PortalEntry { get; }
        public Vector2Int PortalExit { get; }
        public bool LeverThrown { get; private set; }
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
                    if ("#.GLUA1aXYpP".IndexOf(cell) < 0)
                        throw new ArgumentException("Unknown Level 02 tile: " + cell);
            }
            Upper = FindUnique('U');
            Lever = FindUnique('A');
            Plate = FindUnique('1');
            PortalEntry = FindUnique('p');
            PortalExit = FindUnique('P');
            FindUnique('a');
            FindUnique('X');
            FindUnique('Y');
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
            MoveCount = 0;
            Feedback = "沿着脚边能摸到的地方，去找光。";
            RecalculateLight();
        }

        public bool Contains(Vector2Int cell) => cell.x >= 0 && cell.y >= 0
            && cell.x < Width && cell.y < Height;
        public char Tile(Vector2Int cell) => Contains(cell) ? rows[cell.y][cell.x] : '#';
        public bool IsOpen(char tile) => tile == 'X' ? LeverThrown
            : tile == 'Y' ? !LeverThrown : tile == 'a' && PlatePressed;
        public bool BlocksMovement(Vector2Int cell)
        {
            char tile = Tile(cell);
            // Mirror frames and the lever pedestal occupy their cell. Otherwise they
            // create shortcuts that bypass the double-throw doors in the source design.
            return "#GApP".IndexOf(tile) >= 0 || ("aXY".IndexOf(tile) >= 0 && !IsOpen(tile));
        }
        public bool CanReachLeverFrom(Vector2Int cell)
        {
            Vector2Int delta = cell - Lever;
            return delta != Vector2Int.zero && Math.Abs(delta.x) <= 1 && Math.Abs(delta.y) <= 1;
        }
        public bool CanReachLever => CanReachLeverFrom(Lower) || CanReachLeverFrom(Upper);

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
                    : "aXY".IndexOf(tile) >= 0 ? "门关着。" : "摸到了一面墙。";
                return false;
            }
            bool wasOnPlate = PlatePressed;
            Lower = next;
            MoveCount++;
            RecalculateLight();
            Feedback = Won ? "终于，重新走到一起。"
                : PlatePressed ? "脚下压住了机关。远处，一束光亮了。"
                : wasOnPlate ? "松开了。记住刚才亮起的路。"
                : CanReachLever ? "够得着拉杆了。按 E 拨动。" : "";
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
            char closingDoor = LeverThrown ? 'X' : 'Y';
            if (Tile(Lower) == closingDoor)
            {
                Feedback = "先走出门框，再拨动拉杆。";
                return false;
            }
            LeverThrown = !LeverThrown;
            RecalculateLight();
            Feedback = "拉杆落下。一扇门开，另一扇门关。";
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
                        if (tile == '#' || ("aXY".IndexOf(tile) >= 0 && !IsOpen(tile))) break;
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
