namespace CSharpClient
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Linq;
    using WebSocketService.Client;
    using AppEvents;
    using WebSocketService.Mqtt;

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
}