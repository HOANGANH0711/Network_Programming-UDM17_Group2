namespace Server.GameLogic
{
    public class Board
    {
        public const int Size = 15;

        private readonly int[][] _cells =
            Enumerable.Range(0, Size).Select(_ => new int[Size]).ToArray();

        public bool Place(int row, int col, CellValue value)
        {
            if (row < 0 || row >= Size || col < 0 || col >= Size)
                return false;
            if (_cells[row][col] != 0)
                return false;

            _cells[row][col] = (int)value;
            return true;
        }

        public CellValue Get(int row, int col) => (CellValue)_cells[row][col];

        internal int GetRaw(int row, int col) => _cells[row][col];

        internal void SetRaw(int row, int col, int value) => _cells[row][col] = value;

        public bool IsFull() => _cells.All(row => row.All(cell => cell != 0));

        public int[][] GetSnapshot() => _cells.Select(row => row.ToArray()).ToArray();
    }
}