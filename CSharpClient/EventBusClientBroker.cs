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

        private WebSocketClient client;
        public string ServerIdentity { get; set; }

        public EventBusClientBroker(string remoteServer, int remotePort, string serverIdentity,IConnectionProcessor processor)
        {
            client = new WebSocketClient(string.Format("ws://{0}:{1}/",remoteServer,remotePort), processor, new SystemCredential(serverIdentity));
            this.ServerIdentity = serverIdentity;             
        }

        public bool State
        {
            get
            {
                return (client.State == WebSocketState.Connecting) || (client.State == WebSocketState.Open);
            }
        }

        public void Publish<T>(T evt, string topic, short messageId)
        {
            client.Send(Helper.GeneratePublishCommand<T>(evt,topic,messageId));
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
