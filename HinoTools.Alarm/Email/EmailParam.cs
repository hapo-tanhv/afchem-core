using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HinoTools.Alarm.Email
{
    public class EmailParam
    {
        public string Host { get; set; }
       
        public int Port { get; set; }
      
        public int TimeOut { get; set; }
        
        public bool EnableSSL { get; set; }
       
        public string CredentialEmail { get; set; }
       
        public string CredentialPassword { get; set; }        

    }
}
