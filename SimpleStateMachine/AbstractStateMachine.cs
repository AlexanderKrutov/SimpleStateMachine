using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SimpleStateMachine
{
    /// <summary>
    /// Base class for all state machine implementations.
    /// </summary>
    /// <typeparam name="TState">Machine state type.</typeparam>
    public abstract class AbstractStateMachine<TState> : IDisposable
    {
        /// <summary>
        /// Stores possible transitions between machine states.
        /// </summary>
        private ICollection<ITransition> transitions = new List<ITransition>();

        /// <summary>
        /// Stores handlers for entering state machine states.
        /// </summary>
        private IDictionary<TState, Action<TState>> enteringHandlers = new Dictionary<TState, Action<TState>>();

        /// <summary>
        /// Stores handlers for leaving state machine states.
        /// </summary>
        private IDictionary<TState, Action<TState>> leavingHandlers = new Dictionary<TState, Action<TState>>();

        /// <summary>
        /// Contains unprocessed messages.
        /// </summary>
        private BlockingCollection<object> messages = new BlockingCollection<object>();

        /// <summary>
        /// Thread to process incoming messages.
        /// </summary>
        private Thread workerThread = null;

        /// <summary>
        /// Cancel token source to interrupt the message processing when the state machine is disposed.
        /// </summary>
        private CancellationTokenSource cancelTokenSource = new CancellationTokenSource();

        /// <summary>
        /// Gets current machine state.
        /// </summary>
        public TState State { get; private set; }

        /// <summary>
        /// Raised when the machine changes its state.
        /// </summary>
        public event Action<TState, TState> StateChanged;

        /// <summary>
        /// Starts the state maching with initial state.
        /// </summary>
        /// <param name="state">Intital state of the machine.</param>
        public void Start(TState state)
        {
            State = state;
            workerThread = new Thread(ProcessMessages) { IsBackground = true };
            workerThread.Start();
        }

        /// <summary>
        /// Proceed the machine to handle the message.
        /// </summary>
        /// <typeparam name="TMessage">Message type.</typeparam>
        /// <param name="message">Message that can contain optional payload.</param>
        public virtual void HandleMessage<TMessage>(TMessage message)
        {
            messages.Add(message);
        }

        /// <summary>
        /// Adds the transition to the state machine.
        /// </summary>
        /// <typeparam name="TMessage">Message type (optionally can contain a payload).</typeparam>
        /// <param name="from">Source machine state.</param>
        /// <param name="to">Destination machine state.</param>
        /// <param name="handler">Optional handler to be invoked when state changed.</param>
        /// <param name="condition">Optional condition to be checked before the state change. If true the state will be changed, otherwise the machine does not change its state.</param>
        protected void AddTransition<TMessage>(TState from, TState to, Action<TMessage> handler = null, Func<TMessage, bool> condition = null) where TMessage : class
        {
            // Check the transition alredy exists (avoid ambiguity)
            if (transitions.Any(t => t.From.Equals(from) && t.MessageType == typeof(TMessage)))
            {
                throw new ArgumentException("State machine already has a transition from same state and message type.");
            }

            transitions.Add(new Transition<TMessage>()
            {
                From = from,
                To = to,
                Handler = handler,
                Condition = condition,
                MessageType = typeof(TMessage)
            });
        }

        /// <summary>
        /// Adds new handler for entering machine state.
        /// </summary>
        /// <param name="state">State</param>
        /// <param name="handler">Handler method. Parameter is a previous machine state.</param>
        protected void AddEnteringStateHandler(TState state, Action<TState> handler)
        {
            if (enteringHandlers.ContainsKey(state))
                throw new ArgumentException($"The state machine already contains entering handler for state {state}. Consider using single handler if you need more than one action.");

            enteringHandlers[state] = handler;
        }

        /// <summary>
        ///  Adds new handler for leaving machine state.
        /// </summary>
        /// <param name="state">State</param>
        /// <param name="handler">Handler method. Parameter is a next machine state.</param>
        protected void AddLeavingStateHandler(TState state, Action<TState> handler)
        {
            if (leavingHandlers.ContainsKey(state))
                throw new ArgumentException($"The state machine already contains leaving handler for state {state}. Consider using single handler if you need more than one action.");

            leavingHandlers[state] = handler;
        }

        /// <summary>
        /// Loop function to process messages, if any.
        /// </summary>
        private void ProcessMessages()
        {
            while (true)
            {
                try
                {
                    var message = messages.Take(cancelTokenSource.Token);
                    var transition = transitions.FirstOrDefault(t => t.From.Equals(State) && t.MessageType == message.GetType());
                    if (transition != null)
                    {
                        if (transition.InvokeCondition(message))
                        {
                            if (leavingHandlers.ContainsKey(transition.From))
                            {
                                leavingHandlers[transition.From].Invoke(transition.To);
                            }

                            transition.InvokeHandler(message);
                            State = transition.To;

                            if (enteringHandlers.ContainsKey(transition.To))
                            {
                                enteringHandlers[transition.To].Invoke(transition.From);
                            }

                            StateChanged?.Invoke(transition.From, transition.To);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            cancelTokenSource.Cancel();
        }

        /// <summary>
        /// Internal class to store transition info.
        /// </summary>
        /// <typeparam name="TMessage"></typeparam>
        private class Transition<TMessage> : ITransition where TMessage : class
        {
            /// <inheritdoc/>
            public TState From { get; set; }

            /// <inheritdoc/>
            public TState To { get; set; }

            /// <inheritdoc/>
            public Type MessageType { get; set; }

            /// <summary>
            /// Message handler.
            /// </summary>
            public Action<TMessage> Handler { get; set; }

            /// <summary>
            /// Condition to execute the transition.
            /// </summary>
            public Func<TMessage, bool> Condition { get; set; }

            /// <inheritdoc/>
            public bool InvokeCondition(object message)
            {
                return Condition != null ? Condition.Invoke((TMessage)message) : true;
            }

            /// <inheritdoc/>
            public void InvokeHandler(object message)
            {
                Handler?.Invoke((TMessage)message);
            }
        }

        /// <summary>
        /// Auxillary interface to unify state transitions.
        /// </summary>
        private interface ITransition
        {
            /// <summary>
            /// Source state.
            /// </summary>
            TState From { get; }

            /// <summary>
            /// Destinations state.
            /// </summary>
            TState To { get; }

            /// <summary>
            /// Type of message.
            /// </summary>
            Type MessageType { get; }

            /// <summary>
            /// Invokes the transition handler.
            /// </summary>
            /// <param name="message">Message.</param>
            void InvokeHandler(object message);

            /// <summary>
            /// Invokes the condition.
            /// </summary>
            /// <param name="message">Message.</param>
            /// <returns>True if transition should be executed.</returns>
            bool InvokeCondition(object message);
        }
    }
}
