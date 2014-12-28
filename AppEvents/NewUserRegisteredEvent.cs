using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AppEvents
{
    public class NewUserRegisteredEvent
    {
        public string UserName { get; set; }
        public DateTime RegisterDate { get; set; }
    }
}
