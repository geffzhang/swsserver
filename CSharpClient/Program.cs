namespace CSharpClient
{
    using AppEvents;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using WebSocket4Net;
    using WebSocketService.Client;
    using WebSocketService.Mqtt;
    using WebSocketService.Server;

    public static class Program
    {
       

        [STAThread]
        public static void Main(string[] args)
        {
            using (var busBroker = new EventBusClientBroker("localhost", 8181, "abcdefg", new TestProcessor()))
            {
                if (busBroker.State)
                {
                    Console.WriteLine("Press 'q' to quit");
                    

                    while (true)
                    {
                        string choice = Console.ReadLine();
                        if (choice == "1")
                        {
                            NewUserRegisteredEvent evt = new NewUserRegisteredEvent();
                            evt.RegisterDate = DateTime.Now;
                            evt.UserName = "aaron";
                            busBroker.Publish<NewUserRegisteredEvent>(evt, "NewUserRegister", 1);
                        }
                        else if (choice == "2")
                        {
                            UserProfileUpdatedEvent evt = new UserProfileUpdatedEvent();
                            evt.UserID = 100;
                            busBroker.Publish<UserProfileUpdatedEvent>(evt, "UserProfileUpdated", 2);
                        }
                    }
                }
           }           
        }

       
    }

    class TestProcessor : IConnectionProcessor
    {
        private WebSocketClient client;

        public void Error(Exception ex)
        {
            Console.WriteLine(ex);
        }

        public void Opened()
        {
            client.Send(Helper.GenerateSubscribeCommand("NewUserRegister"));
            client.Send(Helper.GenerateSubscribeCommand("UserProfileUpdated"));
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

        public WebSocketClient Client
        {
            get
            {
                return client;
            }
            set
            {
                this.client = value;
            }
        }

        public void MessageReceived(byte[] message)
        {
            try
            {
                MqttMessage incoming = MqttMessage.CreateFrom(message);
                switch (incoming.Header.MessageType)
                {
                    case MqttMessageType.SubscribeAck:
                        break;
                    default:
                        break;
                }
            }
            catch(InvalidMessageException ex)
            {

            }
        }
    }

    public class PubSubWebSocketClient
    {
        public WebSocket Websoket { get; set; }
        protected AutoResetEvent OpenedEvent = new AutoResetEvent(false);
        public PubSubWebSocketClient(string ip, bool autoConnect)
        {

            this.Websoket = new WebSocket(ip, "basic", WebSocketVersion.Rfc6455);
            this.Websoket.Opened += Websoket_Opened;

            if (autoConnect)
            {
                this.Websoket.Open();

                if (!OpenedEvent.WaitOne(7000))
                    throw new Exception("Cannot Connect to websocket server");
            }


        }


        public void Publish(string topic, string msg, List<Constrain> constrains, bool closeConn)
        {

            if (this.Websoket.State != WebSocketState.Open)
            {
                this.Websoket.Open();
                if (!OpenedEvent.WaitOne(7000))
                    throw new Exception("Cannot Connect to websocket server");
            }



            ClientRequest request = new ClientRequest();
            request.RequestType = RequestType.PUBLISH;
            request.Topic = topic;
            request.Msg = msg;
            request.Constrains = constrains;

            string requestString = JsonConvert.SerializeObject(request);

            this.Websoket.Send(requestString);

            if (closeConn && this.Websoket.State == WebSocketState.Open)
                this.Websoket.Close();

        }

        public void Publish(string topic, string msg, bool closeConn)
        {

            if (this.Websoket.State != WebSocketState.Open)
            {
                this.Websoket.Open();
                if (!OpenedEvent.WaitOne(7000))
                    throw new Exception("Cannot Connect to websocket server");
            }



            ClientRequest request = new ClientRequest();
            request.RequestType = RequestType.PUBLISH;
            request.Topic = topic;
            request.Msg = msg;
            request.Constrains = null;

            string requestString = JsonConvert.SerializeObject(request);

            this.Websoket.Send(requestString);


            if (closeConn && this.Websoket.State == WebSocketState.Open)
                this.Websoket.Close();

        }





        void Websoket_Opened(object sender, EventArgs e)
        {
            OpenedEvent.Set();
        }
    }

}