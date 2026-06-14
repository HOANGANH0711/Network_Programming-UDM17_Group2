namespace Server.GameLogic
{
    public class Board
    {
        public const int Size = 15;
        public int[][] Cells { get; } = Enumerable.Range(0, Size).Select(_ => new int[Size]).ToArray();

        public bool PlaceMove(string symbol, int row, int col)
        {
            if (row < 0 || row >= Size || col < 0 || col >= Size || Cells[row][col] != 0)
                return false;

            Cells[row][col] = symbol == "X" ? 1 : 2;
            return true;
        }

        public bool IsFull() => Cells.All(row => row.All(cell => cell != 0));

        public int[][] ToArray() => Cells.Select(row => row.ToArray()).ToArray();
    }
}