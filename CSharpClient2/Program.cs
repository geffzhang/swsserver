using AppEvents;
using CSharpClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSharpClient2
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var busBroker = new EventBusClientBroker("localhost", 8181, "abcde550"))
            {
                Console.WriteLine("Press 'q' to quit");
                busBroker.Subscribe<NewUserRegisteredEvent>();
                busBroker.Subscribe<UserProfileUpdatedEvent>();

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
