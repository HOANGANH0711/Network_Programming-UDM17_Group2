namespace Server.GameLogic
{
    public enum CellValue
    {
        Empty = 0,
        X = 1,
        O = 2
    }

    public static class CellValueExtensions
    {
        public static CellValue FromSymbol(string symbol) =>
            symbol == "X" ? CellValue.X : CellValue.O;

        public static string ToSymbol(this CellValue value) =>
            value == CellValue.X ? "X" : value == CellValue.O ? "O" : "";

        public static CellValue Opponent(this CellValue value) =>
            value == CellValue.X ? CellValue.O : CellValue.X;
    }
}