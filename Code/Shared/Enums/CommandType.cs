using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Enums
{
    public enum CommandType
    {
        // ===== Auth =====
        LOGIN,
        LOGOUT,

        // ===== Lobby =====
        GET_PLAYER_LIST,
        PLAYER_LIST,
        PLAYER_JOINED,
        PLAYER_LEFT,
        CHALLENGE,
        CHALLENGE_ACCEPT,
        CHALLENGE_DECLINE,
        LOBBY_UPDATE,
        INVITE,
        INVITE_RESPONSE,

        // ===== Room =====
        CREATE_ROOM,
        JOIN_ROOM,
        LEAVE_ROOM,
        GET_ROOM_LIST,
        ROOM_LIST,
        ROOM_CREATED,
        ROOM_JOINED,
        ROOM_FULL,

        // ===== Game =====
        GAME_START,
        MAKE_MOVE,
        MOVE_RESULT,
        GAME_WIN,
        GAME_LOSE,
        GAME_DRAW,
        GAME_END,
        TIMER_UPDATE,
        OPPONENT_DISCONNECTED,
        GAME_MOVE,
        GAME_CHAT,
        DRAW_REQUEST,
        DRAW_RESPONSE,
        SURRENDER,
        START_BOT_GAME,
        GAME_RESULT,
        // ===== History =====
        GET_HISTORY,
        HISTORY_DATA,

        // ===== System =====
        PING,
        ERROR,
        SUCCESS
    }
}
