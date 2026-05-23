using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Enums
{
    public enum CommandType
    {
        // ====== Auth =====
        LOGIN,
        LOGOUT,
        // ===== Lobby =====
        GET_PLAYER_LIST,
        PLAYER_LIST,
        CHALLENGE,
        CHALLENGE_ACCEPT,
        CHALLENGE_DECLINE,

        // ===== GAME =====
        GAME_START,
        MAKE_MOVE,
        MOVE_RESULT,
        GAME_WIN,
        GAME_LOSE,
        GAME_DRAW,
        TIMER_UPDATE,

        // ===== Room =====
        CREATE_ROOM,
        JOIN_ROOM,
        LEAVE_ROOM,
        GET_ROOM_LIST,
        ROOM_LIST,
        
        // ===== History =====
        GET_HISTORY,
        HISTORY_DATA,

        // ===== System =====
        PING,
        ERROR,
        SUCCESS
    }
}
