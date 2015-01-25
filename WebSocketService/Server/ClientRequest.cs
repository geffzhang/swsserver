using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebSocketService.Server
{
    public  class ClientRequest
    {
 
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger
(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
 
 
        public string RequestType {get;set;}
        public string Topic { get; set; }
        public List<Constrain> Constrains { get; set; }
        public string Msg { get; set; }
 
 
        public ClientRequest(){
 
            this.Constrains=new List<Constrain>();
 
        }




        public static ClientRequest TryParse(string jsonString)
        {
            ClientRequest clientRequest = null;
            try
            {
                clientRequest = JsonConvert.DeserializeObject<ClientRequest>(jsonString);

            }
            catch (Exception ex)
            {
                log.Error("Error parsing client request", ex);

            }
            return clientRequest;


        }
 
 
 
    }
 
        public class RequestType{
 
             public const  string SUBSCRIBE="SUBSCRIBE";
             public const string UNSUBSCRIBE = "UNSUBSCRIBE";
             public const string PUBLISH = "PUBLISH";  
         
        }


        public class Constrain : System.Object
        {

            public string Operand { get; set; }
            public string Variable { get; set; }
            public string Value { get; set; }



            public bool Equals(Constrain c1)
            {

                if (c1.Value == this.Value && c1.Operand == this.Operand && c1.Variable == this.Variable)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            public int GetHashCode(Constrain obj)
            {
                return 1;
            }
        }

}
