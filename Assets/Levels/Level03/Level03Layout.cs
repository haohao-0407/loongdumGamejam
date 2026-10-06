using UnityEngine;

/// <summary>Coordinates transcribed from the supplied diagram; row zero is north.</summary>
public static class Level03Layout
{
    public const float CellSize = 2.4f;
    public static readonly string[] Rows = {
        "              #####  ",
        "             #.....# ",
        "             #..U..# ",
        "             #.....# ",
        "             #C..A.# ",
        "   ########## #DG##  ",
        "  #..........#.....# ",
        "  #..........#.....# ",
        "  #....B..M.MGaM...# ",
        "  #..........#.....# ",
        "  #..........Z..#..# ",
        "  #..........#..#S## ",
        "  ####X###### #####  ",
        " #.S......#          ",
        " #........#          ",
        " #........#          ",
        " #....1.L.#          "
    };
    public static Vector3 Position(int row, int column, float y = 0f) =>
        new Vector3((column - 10) * CellSize, y, (8 - row) * CellSize);
    public static char At(int row, int column) => row < 0 || row >= Rows.Length
        || column < 0 || column >= Rows[row].Length ? ' ' : Rows[row][column];
    public static Vector3 Find(char symbol, float y = 0f)
    {
        for (int r = 0; r < Rows.Length; r++)
        {
            int c = Rows[r].IndexOf(symbol);
            if (c >= 0) return Position(r, c, y);
        }
        throw new System.ArgumentException("Missing diagram symbol " + symbol);
    }
    // S remains the original non-walkable pedestal. The adjacent outlined floor
    // is the actual standing area for the project's existing F skill.
    public static Vector3 FirstTile => Position(13, 4);
    public static Vector3 SecondTile => Position(10, 17) + new Vector3(.8f, 0, .5f);
}
