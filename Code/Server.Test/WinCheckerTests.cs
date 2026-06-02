using Microsoft.VisualStudio.TestTools.UnitTesting;
using Server.GameLogic;

namespace Server.Tests
{
    [TestClass]
    public class WinCheckerTests
    {
        [TestMethod]
        public void HorizontalWin_ShouldReturnTrue()
        {
            Board board = new Board();

            for (int i = 0; i < 5; i++)
            {
                board.PlaceMove(0, i, CellState.X);
            }

            bool result =
                WinChecker.CheckWin(board, 0, 4, CellState.X);

            Assert.IsTrue(result);
        }
    }
}