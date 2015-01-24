using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebSocketService.Client
{
    public interface IConnectionProcessor
    {
        WebSocketClient Client {get;set;}
        void Error(Exception ex);

        void Opened();

        void Closed();

        void MessageReceived(string message);

        void MessageReceived(byte[] message);
    }
}
