using Shared.DTO;
using Shared.Enums;
using Shared.Models;

namespace Server.Handlers
{
    public class MatchmakingService
    {
        private readonly LobbySession _lobby = new();
        private readonly Dictionary<string, InviteDto> _pendingInvites = new();

        public event Action<string, object> SendToClient;
        public event Action<object> BroadcastToLobby;
        public event Func<string, string, string> CreateGameRoom;

        public bool PlayerJoinLobby(string playerId, string playerName)
        {
            var player = new Player(playerId, playerName);
            bool added = _lobby.AddPlayer(player);
            if (added) BroadcastLobbyUpdate();
            return added;
        }

        public void PlayerLeaveLobby(string playerId)
        {
            if (!_lobby.IsOnline(playerId)) return;
            CancelInvitesOf(playerId);
            _lobby.RemovePlayer(playerId);
            BroadcastLobbyUpdate();
        }

        public List<Player> GetOnlinePlayers() => _lobby.GetOnlinePlayers();

        public void BroadcastLobbyUpdate()
        {
            var dto = new LobbyUpdateDto
            {
                OnlinePlayers = _lobby.GetOnlinePlayers().Select(p => new PlayerInfoDto
                {
                    PlayerId = p.PlayerId,
                    PlayerName = p.PlayerName,
                    Status = p.Status.ToString()
                }).ToList()
            };
            BroadcastToLobby?.Invoke(dto);
        }

        public bool SendInvite(string fromId, string toId)
        {
            var from = _lobby.GetPlayer(fromId);
            var to = _lobby.GetPlayer(toId);

            if (from == null || to == null) return false;
            if (to.Status != PlayerStatus.Online) return false;

            var invite = new InviteDto
            {
                FromPlayerId = fromId,
                FromPlayerName = from.PlayerName,
                ToPlayerId = toId
            };

            _pendingInvites[toId] = invite;
            _lobby.UpdateStatus(fromId, PlayerStatus.InQueue);
            _lobby.UpdateStatus(toId, PlayerStatus.InQueue);

            SendToClient?.Invoke(toId, invite);
            return true;
        }

        public void RespondToInvite(string responderId, bool accepted)
        {
            if (!_pendingInvites.TryGetValue(responderId, out var invite)) return;
            _pendingInvites.Remove(responderId);

            if (accepted)
            {
                StartGame(invite.FromPlayerId, invite.ToPlayerId);
            }
            else
            {
                _lobby.UpdateStatus(invite.FromPlayerId, PlayerStatus.Online);
                _lobby.UpdateStatus(invite.ToPlayerId, PlayerStatus.Online);
                SendToClient?.Invoke(invite.FromPlayerId, new { Type = "INVITE_DECLINED" });
                BroadcastLobbyUpdate();
            }
        }

        private void StartGame(string player1Id, string player2Id)
        {
            var p1 = _lobby.GetPlayer(player1Id);
            var p2 = _lobby.GetPlayer(player2Id);
            if (p1 == null || p2 == null) return;

            string roomId = CreateGameRoom?.Invoke(player1Id, player2Id) ?? Guid.NewGuid().ToString();

            _lobby.UpdateStatus(player1Id, PlayerStatus.InGame);
            _lobby.UpdateStatus(player2Id, PlayerStatus.InGame);

            var gameStart = new GameStartDto
            {
                RoomId = roomId,
                Player1Id = p1.PlayerId,
                Player1Name = p1.PlayerName,
                Player2Id = p2.PlayerId,
                Player2Name = p2.PlayerName
            };

            SendToClient?.Invoke(player1Id, gameStart);
            SendToClient?.Invoke(player2Id, gameStart);
            BroadcastLobbyUpdate();
        }

        private void CancelInvitesOf(string playerId)
        {
            if (_pendingInvites.TryGetValue(playerId, out var invite))
            {
                _lobby.UpdateStatus(invite.FromPlayerId, PlayerStatus.Online);
                SendToClient?.Invoke(invite.FromPlayerId, new { Type = "INVITE_CANCELLED" });
                _pendingInvites.Remove(playerId);
            }

            var asInviter = _pendingInvites.Where(kv => kv.Value.FromPlayerId == playerId).ToList();
            foreach (var kv in asInviter)
            {
                _lobby.UpdateStatus(kv.Value.ToPlayerId, PlayerStatus.Online);
                SendToClient?.Invoke(kv.Value.ToPlayerId, new { Type = "INVITE_CANCELLED" });
                _pendingInvites.Remove(kv.Key);
            }
        }

        public void OnGameEnded(string player1Id, string player2Id)
        {
            _lobby.UpdateStatus(player1Id, PlayerStatus.Online);
            _lobby.UpdateStatus(player2Id, PlayerStatus.Online);
            BroadcastLobbyUpdate();
        }
    }
}