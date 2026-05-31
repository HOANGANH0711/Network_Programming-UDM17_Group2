using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shared.Enums;

namespace Shared.Models
{
    public  class Packet
    {
        public CommandType Command {  get; set; }
        public string Data {  get; set; }
        public string SenderID {  get; set; }
        public DateTime Timestamp { get; set; }
        public Packet()
        {
            Timestamp = DateTime.Now;
        }
    }
}
