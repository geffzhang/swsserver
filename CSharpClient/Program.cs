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

    public static class Program
    {
       

        [STAThread]
        public static void Main(string[] args)
        {
            using (var busBroker = new EventBusClientBroker("localhost", 8181, "abcdefg"))
            {
                if (busBroker.State)
                {
                    Console.WriteLine("Press 'q' to quit");
                    busBroker.Subscribe<NewUserRegisteredEvent>();

                    while (true)
                    {
                        string choice = Console.ReadLine();
                        if (choice == "1")
                        {
                            NewUserRegisteredEvent evt = new NewUserRegisteredEvent();
                            evt.RegisterDate = DateTime.Now;
                            evt.UserName = "aaron";
                            busBroker.Publish<NewUserRegisteredEvent>(evt);
                        }
                        else if (choice == "2")
                        {
                            UserProfileUpdatedEvent evt = new UserProfileUpdatedEvent();
                            evt.UserID = 100;
                            busBroker.Publish<UserProfileUpdatedEvent>(evt);
                        }
                    }
                }
           }           
        }

       
    }
}