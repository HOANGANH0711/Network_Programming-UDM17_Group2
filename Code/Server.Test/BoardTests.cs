using Microsoft.VisualStudio.TestTools.UnitTesting;
using Server.GameLogic;

namespace Server.Tests
{
    [TestClass]
    public class BoardTests
    {
        [TestMethod]
        public void PlaceMove_OnOccupiedCell_ShouldReturnFalse()
        {
            Board board = new Board();

            board.PlaceMove(1, 1, CellState.X);

            bool result =
                board.PlaceMove(1, 1, CellState.O);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void InvalidPosition_ShouldReturnFalse()
        {
            Board board = new Board();

            bool result =
                board.PlaceMove(-1, 20, CellState.X);

            Assert.IsFalse(result);
        }
    }
}