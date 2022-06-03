using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace GameSupport.Models
{
    public class Message
    {
        public int Id { get; set; }
        public Player Player { get; set; }
        public Admin Admin { get; set; }
        [DataType(DataType.DateTime)]
        public DateTime Time { get; set; }
        public bool Read { get; set; }
        public bool From { get; set; }
        public string Text { get; set; }
    }
}
