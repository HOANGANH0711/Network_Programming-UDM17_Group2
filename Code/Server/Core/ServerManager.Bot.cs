using Shared.DTO;
using Shared.Enums;
using Shared.Models;

namespace Server.Core
{
    public partial class ServerManager
    {
        private async Task StartBotGameAsync(ClientHandler client, string data)
        {
            var request = Serializer.Deserialize<BotGameRequestDto>(data) ?? new BotGameRequestDto();
            var playerIsX = request.PlayerSymbol != "O";
            var xId = playerIsX ? client.ClientId : ActiveGame.BotId;
            var oId = playerIsX ? ActiveGame.BotId : client.ClientId;
            var game = ActiveGame.CreateBot(
                xId,
                xId == client.ClientId ? _matchmaking.GetName(client.ClientId) : "Bot",
                oId,
                oId == client.ClientId ? _matchmaking.GetName(client.ClientId) : "Bot",
                request.TurnSeconds,
                request.Difficulty);

            AddGame(game);
            await BroadcastGameStateAsync(game, CommandType.GAME_START);
            game.StartTimer(() => _ = TickGameAsync(game.GameID));
            await MaybeBotMoveAsync(game);
            await _matchmaking.BroadcastLobbyAsync();
        }

        private async Task MaybeBotMoveAsync(ActiveGame game)
        {
            if (!game.IsBotGame || game.CurrentTurnID != ActiveGame.BotId || game.IsGameOver)
                return;

            await Task.Delay(450);
            var (row, col) = game.ChooseBotMove();
            var botSymbol = game.SymbolOf(ActiveGame.BotId);
            if (row >= 0 && game.PlaceMove(ActiveGame.BotId, botSymbol, row, col))
            {
                await BroadcastGameStateAsync(game, CommandType.GAME_STATE);
                if (game.HasWinner(row, col, botSymbol))
                    await EndGameAsync(game, ActiveGame.BotId, "Bot thang");
                else if (game.IsBoardFull())
                    await EndGameAsync(game, "", "Hoa do ban co day");
                else
                {
                    game.SwitchTurn();
                    await BroadcastGameStateAsync(game, CommandType.GAME_STATE);
                }
            }
        }
    }
}
