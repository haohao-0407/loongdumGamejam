using UnityEngine;

/// <summary>Window Return: five enclosed areas and a return corridor.</summary>
public static class Level04Layout
{
    public const float CellSize = 2f;
    public static readonly string[] Rows = CreateMap();
    public static Vector3 Position(int row, int column, float y = 0f) =>
        new Vector3((column - 12) * CellSize, y, (10 - row) * CellSize);
    public static char At(int row, int column) => row < 0 || row >= Rows.Length
        || column < 0 || column >= Rows[row].Length ? ' ' : Rows[row][column];
    public static Vector3 Find(char symbol, float y = 0f)
    {
        for (int r = 0; r < Rows.Length; r++)
        {
            int c = Rows[r].IndexOf(symbol);
            if (c >= 0) return Position(r, c, y);
        }
        throw new System.ArgumentException("Missing map symbol " + symbol);
    }
    public static Vector3 FirstTile => Find('S');
    public static Vector3 SecondTile => Find('T');
    public static Vector3 PortalEntry => Position(14, 21, 1.5f) + Vector3.right * 2.8f;
    public static Vector3 PortalExit => Position(14, 2, 1.5f);

    private static string[] CreateMap()
    {
        var cells = new char[20, 26];
        for (int r = 0; r < 20; r++) for (int c = 0; c < 26; c++) cells[r, c] = ' ';
        System.Action<int, int, int, int> room = (north, west, south, east) =>
        {
            for (int r = north; r <= south; r++) for (int c = west; c <= east; c++)
                cells[r, c] = r == north || r == south || c == west || c == east ? '#' : '.';
        };
        room(1, 1, 7, 7);   // West control room.
        room(8, 1, 17, 7);  // West passage.
        room(11, 9, 18, 18); // Starting courtyard.
        room(4, 9, 10, 17); // Central observation room.
        room(4, 19, 17, 23); // East observation corridor.
        cells[7, 4] = 'Y'; cells[8, 4] = '.';
        cells[14, 7] = '.'; cells[14, 8] = 'X'; cells[14, 9] = '.';
        cells[13, 8] = cells[15, 8] = '#';
        cells[7, 17] = '.'; cells[7, 18] = 'D'; cells[7, 19] = '.';
        cells[6, 18] = cells[8, 18] = '#';
        for (int c = 7; c <= 21; c++) cells[2, c] = '.';
        for (int c = 8; c <= 22; c++) cells[1, c] = '#';
        for (int c = 8; c <= 20; c++) cells[3, c] = '#';
        cells[2, 7] = 'Z'; cells[2, 22] = cells[3, 22] = '#';
        cells[3, 21] = cells[4, 21] = '.';
        for (int c = 12; c <= 14; c++) cells[10, c] = cells[11, c] = 'G';
        cells[8, 13] = 'U'; cells[16, 15] = 'L';
        cells[6, 15] = 'A'; cells[14, 4] = 'B'; cells[4, 4] = 'C';
        cells[10, 4] = '1'; cells[13, 13] = 'S'; cells[14, 21] = 'T';
        cells[14, 22] = 'a';
        var result = new string[20];
        for (int r = 0; r < result.Length; r++)
        {
            var line = new char[26];
            for (int c = 0; c < line.Length; c++) line[c] = cells[r, c];
            result[r] = new string(line);
        }
        return result;
    }
}
