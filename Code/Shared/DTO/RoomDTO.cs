using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DTO
{
    public class RoomDTO
    {
        public string RoomID { get; set; }
        public string RoomName { get; set; }
        public string OwnerID { get; set; }
        public string Player1ID { get; set; }
        public string Player2ID { get; set; }
        public bool IsPlaying { get; set; }
        public bool IsFull { get; set; }
        public RoomDTO()
        {
            IsPlaying = false;
            IsFull = false;
        }


    }
}
