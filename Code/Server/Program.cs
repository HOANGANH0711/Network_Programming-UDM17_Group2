using Server.GameLogic;

class Program
{
    static void Main(string[] args)
    {
        var game = new GameService();
        game.StartGame();
        Console.WriteLine("Game bắt đầu! X đi trước.\n");

        // Giả lập vài nước đi
        var moves = new[]
        {
            new Move(7, 7, CellState.X),
            new Move(7, 8, CellState.O),
            new Move(8, 7, CellState.X),
            new Move(8, 8, CellState.O),
            new Move(9, 7, CellState.X),
            new Move(9, 8, CellState.O),
            new Move(10, 7, CellState.X),
            new Move(10, 8, CellState.O),
            new Move(11, 7, CellState.X), // X thắng!
        };

        foreach (var move in moves)
        {
            string result = game.MakeMove(move);
            Console.WriteLine($"[{move.Player}] ({move.Row},{move.Col}) → {result}");
            if (result.StartsWith("WIN"))
            {
                Console.WriteLine($"\n🏆 {game.Winner} thắng!");
                break;
            }
        }
    }
}