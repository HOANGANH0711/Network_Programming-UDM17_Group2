using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DTO
{
    public class UserDTO
    {
        public string UserID { get; set; }
        public string UserName { get; set; }
        public bool IsOnline {  get; set; }
        public bool ISInGame { get; set; }
    }
}
