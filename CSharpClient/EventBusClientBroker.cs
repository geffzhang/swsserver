using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Xml.Serialization;
using System.IO;
using WebSocketService.Client;
using WebSocket4Net;


namespace CSharpClient
{
    public delegate void EventReceived(string evtClass, object evt);
   
    public class EventBusClientBroker : IDisposable
    {
       class TestProcessor : IConnectionProcessor
       {

        public void Error(Exception ex)
        {
            Console.WriteLine(ex);
        }

        public void Opened()
        {
            Console.WriteLine("Opened");
        }

        public void Closed()
        {
            Console.WriteLine("Closed");
        }

        public void MessageReceived(string message)
        {
            Console.WriteLine(message);
        }
       }

        private WebSocketClient client;
        public string ServerIdentity { get; set; }

        public EventBusClientBroker(string remoteServer, int remotePort, string serverIdentity)
        {
            client = new WebSocketClient(string.Format("ws://{0}:{1}/",remoteServer,remotePort), new TestProcessor(), new SystemCredential(serverIdentity));
            this.ServerIdentity = serverIdentity;             
        }

        public bool State
        {
            get
            {
                return (client.State == WebSocketState.Connecting) || (client.State == WebSocketState.Open);
            }
        }
        public void Subscribe<T>()
        {
            client.Send(Helper.GenerateSubscribeCommand<T>());
        }

        public void Publish<T>(T evt)
        {
            client.Send(Helper.GeneratePublishCommand<T>(evt));
        } 

        public void Dispose()
        {
           if(this.client != null)
           {
               this.client.Dispose();
           }
        }
    }
}
