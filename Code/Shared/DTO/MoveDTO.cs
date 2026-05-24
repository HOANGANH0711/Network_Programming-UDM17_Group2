using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DTO
{
    public class MoveDTO {
        public string GameID { get; set; }
        public string PlayerID { get; set; }
        public int Row {  get; set; }
        public int Col { get; set; }
    }

}
