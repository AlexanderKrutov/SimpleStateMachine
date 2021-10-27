using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleStateMachine
{
    public enum State
    {
        NotInitialized,
        Idle,
        Detecting,
        Detected,
        Lost,
        NotDetected,
        NotFound,
        Interrupted
    }

    public class StartMessage { }
    public class ConnectedMessage { }
    public class DisconnectedMessage { }
    public class AbortMessage { }
    public class InterruptMessage { }
    public class MovementMessage 
    { 
        public int Parameter { get; private set; }    
        public MovementMessage(int parameter)
        {
            Parameter = parameter;
        }
    }
    public class SearchTimeoutMessage { }
    public class DetectingTimeoutMessage { }

    public class MyStateMachine : AbstractStateMachine<State>
    {
        private int MovementParameter;

        private Timer connectionTimer = null;
        private Timer searchTimer = null;

        public MyStateMachine()
        {
            AddTransition<StartMessage>(State.Idle, State.Detecting);
            AddTransition<AbortMessage>(State.Idle, State.NotDetected);

            AddTransition<AbortMessage>(State.Detecting, State.NotDetected);
            AddTransition<DetectingTimeoutMessage>(State.Detecting, State.NotDetected);
            AddTransition<InterruptMessage>(State.Detecting, State.Interrupted);
            AddTransition<ConnectedMessage>(State.Detecting, State.Detected, msg => { StartSearchTimer(); });

            AddTransition<DisconnectedMessage>(State.Detected, State.Lost);
            AddTransition<ConnectedMessage>(State.Lost, State.Detected);

            AddTransition<InterruptMessage>(State.Detected, State.Interrupted, msg => { StopSearchTimer(); });
            AddTransition<SearchTimeoutMessage>(State.Detected, State.NotFound);
            AddTransition<AbortMessage>(State.Detected, State.NotFound, msg => { StopSearchTimer(); });

            AddTransition<InterruptMessage>(State.Lost, State.Interrupted);
            AddTransition<SearchTimeoutMessage>(State.Lost, State.NotFound);
            AddTransition<AbortMessage>(State.Lost, State.NotFound);

            AddEnteringStateHandler(State.NotDetected, fromState => { /*set endTime */});
            AddEnteringStateHandler(State.Interrupted, fromState => { /*set endTime */});
            AddEnteringStateHandler(State.NotFound, fromState => { /*set endTime */});

            AddEnteringStateHandler(State.Detecting, fromState => {  StartConnectionTimer(); });
            AddLeavingStateHandler(State.Detecting, toState => { StopConnectionTimer(); });

            connectionTimer = new Timer(ConnectionTimerElapsed);
            searchTimer = new Timer(SearchTimerElapsed);
        }

        private void StartConnectionTimer()
        {
            connectionTimer.Change(TimeSpan.FromMinutes(1), Timeout.InfiniteTimeSpan);
        }

        private void StartSearchTimer()
        {
            searchTimer.Change(TimeSpan.FromMinutes(5), Timeout.InfiniteTimeSpan);
        }

        private void StopSearchTimer()
        {
            searchTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private void StopConnectionTimer()
        {
            connectionTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private void ConnectionTimerElapsed(object _)
        {
            HandleMessage(new DetectingTimeoutMessage());
        }

        private void SearchTimerElapsed(object _)
        {
            HandleMessage(new SearchTimeoutMessage());
        }

        public override void HandleMessage<TMessage>(TMessage message)
        {            
            if (message is MovementMessage movementMessage)
            {
                MovementParameter = movementMessage.Parameter;
            }

            base.HandleMessage(message);
        }
    }
}
