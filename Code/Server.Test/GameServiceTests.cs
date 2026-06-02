using Microsoft.VisualStudio.TestTools.UnitTesting;
using Server.GameLogic;

namespace Server.Tests
{
    [TestClass]
    public class GameServiceTests
    {
        [TestMethod]
        public void WrongTurn_ShouldBeRejected()
        {
            GameService game = new GameService();

            game.StartGame();

            Move move = new Move(0, 0, CellState.O);

            string result = game.MakeMove(move);

            Assert.AreEqual(
                "Không phải lượt của bạn!",
                result);
        }
    }
}