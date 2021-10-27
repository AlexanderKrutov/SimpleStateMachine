using System;

namespace SimpleStateMachine
{
    class Program
    {
        static void Main(string[] args)
        {
            var stateMachine = new MyStateMachine();

            stateMachine.StateChanged += StateMachine_StateChanged;

            stateMachine.Start(State.Idle);

            Console.WriteLine($"Send messages:");
            Console.WriteLine($"  start");
            Console.WriteLine($"  abort");
            Console.WriteLine($"  connected");
            Console.WriteLine($"  disconnected");
            Console.WriteLine($"  interrupt");
            Console.WriteLine($"  movement <parameter>");
            Console.WriteLine($"");
            Console.WriteLine($"exit = exit program");
            Console.WriteLine($"");

            while (true) 
            {
                var line = Console.ReadLine();

                if (line.Equals("start"))
                {
                    stateMachine.HandleMessage(new StartMessage());
                }
                else if (line.Equals("abort"))
                {
                    stateMachine.HandleMessage(new AbortMessage());
                }
                else if (line.Equals("connected"))
                {
                    stateMachine.HandleMessage(new ConnectedMessage());
                }
                else if (line.Equals("disconnected"))
                {
                    stateMachine.HandleMessage(new DisconnectedMessage());
                }
                else if (line.Equals("interrupt"))
                {
                    stateMachine.HandleMessage(new InterruptMessage());
                }
                else if (line.StartsWith("movement"))
                {
                    int parameter = int.Parse(line.Split(' ')[1]);
                    stateMachine.HandleMessage(new MovementMessage(parameter));
                }

                if (line.Equals("exit"))
                {
                    Environment.Exit(0);
                }
            }
        }

        private static void StateMachine_StateChanged(State from, State to)
        {
            Console.WriteLine($"Machine state has been changed from {from} to {to}");
        }
    }
}
