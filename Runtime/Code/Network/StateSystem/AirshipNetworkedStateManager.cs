using System;
using System.Collections.Generic;
using System.Linq;
using Code.Network.Simulation;
using Code.Network.StateSystem.Structures;
using Code.Player.Character.Net;
using Mirror;
using RSG.Promises;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;

namespace Code.Network.StateSystem
{
    [LuauAPI]
    [RequireComponent(typeof(NetworkIdentity))]
    public abstract class AirshipNetworkedStateManager<StateSystem, State, Input> : NetworkBehaviour
        where State : StateSnapshot where Input : InputCommand where StateSystem : NetworkedStateSystem<StateSystem, State, Input>
    {
        #region Inspector Settings

        // Inspector settings
        public StateSystem stateSystem;

        // Todo: consider making a negative number drop that many frames per tick in order to correct instead of processing them.
        [Tooltip(
            "Amount of extra commands that can be processed per tick to catch up when there is a backup of commands in the command buffer. Increasing this value will help players with a poor network connection, but will result in irregular movement for observers.")]
        [Range(1, 10)]
        public int maxServerCommandCatchup = 1;

        // Amount of times in a row the server will fill in a missing command. This is calculated to be a function of client input rate,
        // so a lower input rate will have a higher number of actual commands predicted. A value of 1 is "1 input packets worth of commands".
        [Tooltip(
            "The number of inputs the server will predict if it doesn't receive any for a time. This helps smooth movement in connections with loss, but may cause undesired movement. It's best to keep this value low.")]
        [Range(0, 3)]
        public uint maxServerCommandPrediction = 1;

        // Determines if the server has authority over the character
        public bool serverAuth = false;

        [Tooltip("Determines if the server will process inputs from a client, or if it will create it's own input commands.")]
        public bool serverGeneratesCommands = false;

        #endregion

        #region Internal State

        // Command number tracking
        private int clientCommandNumber = 0;

        // Send rate tracking fields
        private double lastClientSend = 0;
        private double lastServerSend = 0;
        // The local time contained in the last data sent. This is used to know which data we have already sent to the server
        private double clientLastSentLocalTime = 0;

        // Server processing for commands
        private Input lastProcessedCommand;

        // This may advance faster than last processed command if we predicted command inputs.
        // The data in lastProcessedCommand will be the command used for ticking even if the number
        // does not match.
        private int serverLastProcessedCommandNumber;
        private int serverPredictedCommandCount = 0;
        private SortedList<int, Input> serverCommandBuffer = new SortedList<int, Input>();
        private int serverCommandBufferMaxSize = 0;
        private int serverCommandBufferTargetSize = 0;

        // Non-auth server command tracking
        // Note: we also re-use some of the above command buffer fields
        private SortedList<int, State> serverRecievedStateBuffer = new SortedList<int, State>();

        // Client processing for input prediction
        private State clientLastConfirmedState;

        // When the client doesn't recieve confirmation of it's commands for an extended time, we pause
        // predictions until we receive more commands from the server.
        private bool clientPausePrediction = false;

        // Fields for managing re-simulations
        /**
         * We store a max of 1 second of history
         * Note: The server does not store input history. We don't perform simulation rollbacks on the
         * server because the server is either the authority (it never rolls back) or an observer (it
         * doesn't process inputs).
         */
        public History<Input> inputHistory;

        public History<State> stateHistory;

        // Observer history stores authoritative state and uses the server's times. This data can be interpolated
        // with NetworkTime.time. It is converted into state history on the local clients physics timeline
        // in the observer snapshot function.
        private History<State> observerHistory;
        private double lastReceivedSnapshotTime = 0;

        // Client interpolation fields
        private double clientLastInterpolatedStateTime = 0;

        #endregion

        #region Event Definitions

        // Networking actions to be used by subclass;
        protected Action<State> OnClientReceiveSnapshot;
        protected Action<State> OnServerReceiveSnapshot;
        protected Action<Input> OnServerReceiveInput;

        // Functions to be implemented by subclass that perform networking actions
        public abstract void SendClientInputToServer(Input input);
        public abstract void SendClientSnapshotToServer(State snapshot);
        public abstract void SendServerSnapshotToClients(State snapshot);

        #endregion

        #region Lifecycle Functions

        private void Start()
        {
            // We are a shared client and server
            if (isClient && isServer)
            {
                this.stateSystem.mode = NetworkedStateSystemMode.Authority;
                this.stateSystem.SetMode(NetworkedStateSystemMode.Authority);
            }
            // We are an authoritative client
            else if (isClient && isOwned && !serverAuth)
            {
                this.stateSystem.mode = NetworkedStateSystemMode.Authority;
                this.stateSystem.SetMode(NetworkedStateSystemMode.Authority);
            }
            // We are a non-authoritative client
            else if (isClient && isOwned && serverAuth)
            {
                this.stateSystem.mode = NetworkedStateSystemMode.Input;
                this.stateSystem.SetMode(NetworkedStateSystemMode.Input);
            }
            // We are an observing client
            else if (isClient && !isOwned)
            {
                this.stateSystem.mode = NetworkedStateSystemMode.Observer;
                this.stateSystem.SetMode(NetworkedStateSystemMode.Observer);
            }
            // We are an authoritative server
            else if (isServer && serverAuth)
            {
                this.stateSystem.mode = NetworkedStateSystemMode.Authority;
                this.stateSystem.SetMode(NetworkedStateSystemMode.Authority);
            }
            // We are a non-authoritative server
            else if (isServer && !serverAuth)
            {
                this.stateSystem.mode = NetworkedStateSystemMode.Observer;
                this.stateSystem.SetMode(NetworkedStateSystemMode.Observer);
            }
            else
            {
                Debug.LogWarning("Unable to determine networked state system mode. Did we miss a case? " + isServer +
                                 " " +
                                 isClient + " " + isOwned + " " + serverAuth);
            }
        }

        private void Awake()
        {
            AirshipSimulationManager.Instance.ActivateSimulationManager();
            AirshipSimulationManager.Instance.OnPerformTick += this.OnPerformTick;
            AirshipSimulationManager.Instance.OnSetSnapshot += this.OnSetSnapshot;
            AirshipSimulationManager.Instance.OnCaptureSnapshot += this.OnCaptureSnapshot;
            AirshipSimulationManager.Instance.OnLagCompensationCheck += this.OnLagCompensationCheck;

            // We will keep up to 1 second of commands in the buffer. After that, we will start dropping new commands.
            // The client should also stop sending commands after 1 second's worth of unconfirmed commands.
            this.serverCommandBufferMaxSize = NetworkClient.sendRate;
            // Use 2 times the command buffer size as the target size for the buffer. We will process additional ticks when
            // the buffer is larger than this number. A multiple of 2 means that we may end up delaying command processing
            // by up to two sendIntervals. A higher multiple would cause more obvious delay, but result in smoother movement
            // in poor network conditions.
            this.serverCommandBufferTargetSize =
                Math.Min(this.serverCommandBufferMaxSize,
                    ((int)Math.Ceiling(NetworkClient.sendInterval / Time.fixedDeltaTime)) * 3);
            print("Command buffer max size is " + this.serverCommandBufferMaxSize + ". Target size: " + this.serverCommandBufferTargetSize);

            this.inputHistory = new((int)Math.Ceiling(1f / Time.fixedDeltaTime));
            this.stateHistory = new((int)Math.Ceiling(1f / Time.fixedDeltaTime));
            this.observerHistory = new((int)Math.Ceiling(1f / Time.fixedDeltaTime));

            this.OnClientReceiveSnapshot += ClientReceiveSnapshot;
            this.OnServerReceiveSnapshot += ServerReceiveSnapshot;
            this.OnServerReceiveInput += ServerReceiveInputCommand;

            this.stateSystem.manager = this;
        }

        public void OnDestroy()
        {
            AirshipSimulationManager.Instance.OnPerformTick -= this.OnPerformTick;
            AirshipSimulationManager.Instance.OnSetSnapshot -= this.OnSetSnapshot;
            AirshipSimulationManager.Instance.OnCaptureSnapshot -= this.OnCaptureSnapshot;
            AirshipSimulationManager.Instance.OnLagCompensationCheck -= this.OnLagCompensationCheck;
        }

        private void Update()
        {
            if (isClient && isServer)
            {
                // no networking required in shared mode.
                return;
            }
            
            // We are operating as a client
            if (isClient &&
                AccurateInterval.Elapsed(NetworkTime.localTime, NetworkClient.sendInterval, ref lastClientSend))
            {
                // We are a non-authoritative client and should send all of our latest commands.
                if (isClient && isOwned && serverAuth)
                {
                    // We will sometimes resend unconfirmed commands. The server should ignore these if
                    // it has them already.
                    var commands =
                        this.inputHistory.GetAllAfter(clientLastSentLocalTime - NetworkClient.sendInterval);
                    if (commands.Length > 0)
                    {
                        // Debug.Log($"Sending {commands.Length} commands. Last command: " + commands[^1].commandNumber + $" T: {Time.unscaledTimeAsDouble}");
                        this.clientLastSentLocalTime = this.inputHistory.Keys[^1];
                    }
                    else
                    {
                        // Debug.LogWarning("Sending no commands on interval");
                    }

                    // We make multiple calls so that Mirror can batch the commands efficiently
                    foreach (var command in commands)
                    {
                        this.SendClientInputToServer(command);
                    }
                }

                // We are an authoritative client and should send our latest state
                if (isClient && isOwned && !serverAuth)
                {
                    if (this.stateHistory.Keys.Count == 0) return;
                    var states = this.stateHistory.GetAllAfter(this.clientLastSentLocalTime);
                    if (states.Length > 0)
                    {
                        this.clientLastSentLocalTime = this.stateHistory.Keys[^1];
                    }

                    // We make multiple calls so that Mirror can batch the snapshots efficiently
                    foreach (var state in states)
                    {
                        this.SendClientSnapshotToServer(state);
                    }
                }
            }

            // We are operating as a server
            if (isServer &&
                AccurateInterval.Elapsed(NetworkTime.localTime, NetworkClient.sendInterval, ref lastServerSend))
            {
                // No matter what mode the server is operating in, we send our latest state to clients.
                // If we have no state yet, don't send
                if (this.stateHistory.Keys.Count == 0) return;
                var state = this.stateHistory.Values[^1];
                // If we have no state yet, don't send (this shouldn't be possible)
                if (state == null) return;
                this.SendServerSnapshotToClients(state);
            }
        }

        private void LateUpdate()
        {
            // Unowned clients should interpolate the observed character
            if (isClient && !isOwned)
            {
                this.Interpolate();
            }
        }

        #endregion

        #region Top Level Event Functions

        private void OnPerformTick(double time, bool replay)
        {
            // We are in shared mode
            if (isServer && isClient)
            {
                this.AuthClientTick(time, replay);
                return;
            }

            // We are the client and we are authoritative. Report our state to the server.
            if (isClient && isOwned && !serverAuth)
            {
                this.AuthClientTick(time, replay);
            }

            // We are the server, and we are the authority. Expect commands to come from owning client.
            // Process those commands and distribute and authoritative answer.
            if (isServer && serverAuth)
            {
                this.AuthServerTick(time, replay);
            }

            // We are the client and the server is authoritative. Send our input commands and handle prediction
            if (isClient && isOwned && serverAuth)
            {
                this.NonAuthClientTick(time, replay);
            }

            // We are the server and we are not authoritative.
            if (isServer && !serverAuth)
            {
                this.NonAuthServerTick(time, replay);
            }

            // We are an observing client
            if (isClient && !isOwned)
            {
                this.ObservingClientTick(time, replay);
            }
        }

        private void OnCaptureSnapshot(double time, bool replay)
        {
            // We are in shared mode
            if (isClient && isServer)
            {
                this.AuthClientCaptureSnapshot(time, replay);
                return;
            }

            // We are the client and we are authoritative. Report our state to the server.
            if (isClient && isOwned && !serverAuth)
            {
                this.AuthClientCaptureSnapshot(time, replay);
            }

            // We are the server, and we are the authority. Expect commands to come from owning client.
            // Process those commands and distribute and authoritative answer.
            if (isServer && serverAuth)
            {
                this.AuthServerCaptureSnapshot(time, replay);
            }

            // We are the server, but the client is authoritative. Simply forward to observers.
            if (isServer && !serverAuth)
            {
                this.NonAuthServerCaptureSnapshot(time, replay);
            }

            // We are the client and the server is authoritative. Send our input commands and handle prediction
            if (isClient && isOwned && serverAuth)
            {
                this.NonAuthClientCaptureSnapshot(time, replay);
            }

            // We are an observing client
            if (isClient && !isOwned)
            {
                this.ObservingClientCaptureSnapshot(time, replay);
            }
        }

        private void OnSetSnapshot(double time)
        {
            // we are in shared mode
            if (isClient && isServer)
            {
                this.AuthClientSetSnapshot(time);
                return;
            }

            // We are the client and we are authoritative.
            if (isClient && isOwned && !serverAuth)
            {
                this.AuthClientSetSnapshot(time);
            }

            // We are the server, and we are the authority.
            if (isServer && serverAuth)
            {
                this.AuthServerSetSnapshot(time);
            }

            // We are the server, but the client is authoritative.
            if (isServer && !serverAuth)
            {
                this.NonAuthServerSetSnapshot(time);
            }

            // We are the client and the server is authoritative.
            if (isClient && isOwned && serverAuth)
            {
                this.NonAuthClientSetSnapshot(time);
            }

            // We are an observing client
            if (isClient && !isOwned)
            {
                this.ObservingClientSetSnapshot(time);
            }
        }

        private void OnLagCompensationCheck(int clientId, double currentTime, double ping)
        {
            // Only the server can perform lag compensation checks.
            if (!isServer) return;
            
            // If we are viewing the world as the client who is predicting this system,
            // we don't want to include any rollback on their player since they predicted
            // all of the commands up to the one that triggered the check. They
            // saw themselves where the server sees them at the current time.
            if (this.netIdentity.connectionToClient != null && clientId == this.netIdentity.connectionToClient.connectionId)
            {
                this.OnSetSnapshot(currentTime);
                return;
            }

            // If we are viewing the world as a client who is an observer of this object,
            // calculate the position that was being rendered at the time by subtracting.
            // out their estimated latency and the interpolation buffer time. This
            // ensures that we are rolling back to the time the user actually saw on their
            // client when they issued the command.
            
            // TODO: We treat estimatedCommandDelay as a constant, but we should determine this by calculating how long the command that triggered the lag comp was buffered
            // we would need to pass that into lag comp event as an additional parameter since it would need to be calculated in the predicted character component that generated the command.
            // That may prove difficult, so we use a constant for now.
            var estimatedCommandDelay = this.serverCommandBufferTargetSize * Time.fixedDeltaTime; 
            var clientBufferTime  = NetworkServer.connections[clientId].bufferTime;
            // Debug.Log("Calculated rollback time for " + this.gameObject.name + " as ping: " + ping + " buffer time: " + clientBufferTime + " command delay: " + estimatedCommandDelay + " for a result of: " + (- ping -
            //     clientBufferTime - estimatedCommandDelay));
            var lagCompensatedTickTime =
                AirshipSimulationManager.Instance.GetLastSimulationTime(currentTime - ping -
                                                                        clientBufferTime - estimatedCommandDelay);
            this.OnSetSnapshot(lagCompensatedTickTime);
        }

        #endregion

        #region Authoritative Server Functions

        public void AuthServerTick(double time, bool replay)
        {
            if (replay)
            {
                // An authoritative server will never have a reason to replay a tick since it is the authority on what
                // happened during that tick.
                Debug.LogWarning("A replay was triggered on an authoritative server. This shouldn't happen.");
                return;
            }

            if (this.serverGeneratesCommands)
            {
                // Server generated commands will never be replayed or stored
                var command = this.stateSystem.GetCommand(this.serverLastProcessedCommandNumber, time);
                this.serverLastProcessedCommandNumber++;
                this.stateSystem.Tick(command, time,false);
                return;
            }

            // If we don't allow command catchup, drop commands to get to the target buffer size.
            if (this.maxServerCommandCatchup == 0)
            {
                var dropCount = 0;
                while (this.serverCommandBuffer.Count > this.serverCommandBufferTargetSize && dropCount < 3) // TODO: calculate drop number based on send rate for 1 send worth
                {
                    this.serverCommandBuffer.RemoveAt(0);
                    dropCount++;
                }
                print("Dropped " + dropCount + " command(s) from " + this.gameObject.name + " due to exceeding command buffer size.");
            }

            var commandsProcessed = 0;
            do
            {
                commandsProcessed++;

                // Attempt to get a new command out of the buffer.
                var commandEntry = this.serverCommandBuffer.Count > 0 ? this.serverCommandBuffer.Values[0] : null;

                // If we have a new command to process
                if (commandEntry != null)
                {
                    // Get the command to process
                    var command = this.serverCommandBuffer.Values[0];
                    // Get the expected next command number
                    var expectedNextCommandNumber = this.serverLastProcessedCommandNumber + 1;

                    // Check if that command is in sequence. If we have a gap of commands, we will fill it with the last
                    // processed command up to the maxServerCommandPrediction size.
                    // this.maxServerCommandPrediction * (1f / this.clientInputRate / Time.fixedDeltaTime)) is the number of
                    // commands contained in a single input message
                    if (this.lastProcessedCommand != null && command.commandNumber != expectedNextCommandNumber &&
                        this.serverPredictedCommandCount < Math.Ceiling(this.maxServerCommandPrediction *
                                                                        (NetworkServer.sendInterval /
                                                                         Time.fixedDeltaTime)))
                    {
                        Debug.LogWarning("Missing command " + expectedNextCommandNumber +
                                         " in the command buffer. Next command was: " + command.commandNumber +
                                         ". Predicted " +
                                         (this.serverPredictedCommandCount + 1) + " command(s) so far.");
                        this.serverLastProcessedCommandNumber = expectedNextCommandNumber;
                        command = this.lastProcessedCommand;
                        command.commandNumber = expectedNextCommandNumber;
                        this.serverPredictedCommandCount++;
                    }
                    // We have a valid command that is in sequence, or we reached our max fill. Remove the next
                    // valid command from the buffer and process it.
                    else
                    {
                        // Debug.Log("Ticking next command in sequence: " + command.commandNumber);
                        this.serverPredictedCommandCount = 0;
                        this.serverCommandBuffer.RemoveAt(0);
                        this.lastProcessedCommand = command;
                        this.serverLastProcessedCommandNumber = command.commandNumber;
                    }

                    // tick command
                    command.time = time; // Correct time to local timeline for ticking on the server.
                    this.stateSystem.Tick(command, time, false);
                }
                else
                {
                    // Ensure that we always tick the system even if there's no command to process.
                    // Debug.LogWarning("No commands left. Last command processed: " + this.lastProcessedCommand);
                    this.stateSystem.Tick(null, time, false);
                }
            } while (this.serverCommandBuffer.Count >
                     this.serverCommandBufferTargetSize &&
                     commandsProcessed < 1 + this.maxServerCommandCatchup);
            // ^ we process up to maxServerCommandCatchup commands per tick if our buffer has more than serverCommandBufferTargetSize worth of additional commands.

            if (commandsProcessed > 1)
            {
                print("Processed " + commandsProcessed + " additional commands for " + this.gameObject.name);
            }
        }

        public void AuthServerCaptureSnapshot(double time, bool replay)
        {
            if (replay)
            {
                // Log in AuthServerTick will notify if this case occurs.
                return;
            }

            // get snapshot to send to clients
            var state = this.stateSystem.GetCurrentState(this.serverLastProcessedCommandNumber,
                time);
            this.stateHistory.Add(time, state);
            // Debug.Log("Processing commands up to " + this.lastProcessedCommandNumber + " resulted in " + state);
        }

        public void AuthServerSetSnapshot(double time)
        {
            var state = this.stateHistory.GetExact(time);
            if (state == null)
            {
                // In the case where there's no state to roll back to, we simply leave the state system where it is. This technically means
                // that freshly spawned players will exist in rollback when they shouldn't but we won't handle that edge case for now.
                return;
            }
            this.stateSystem.SetCurrentState(state);
        }

        #endregion

        #region Non-Authoritative Client Functions

        public void NonAuthClientTick(double time, bool replay)
        {
            // If it's a replay, we will take inputs from our input history for this tick
            if (replay)
            {
                // Get the command that we used at this tick previously
                var command = this.inputHistory.GetExact(time);
                // Process the command again like before, but pass replay into the network system so that
                // it doesn't replay effects or animations, etc.
                this.stateSystem.Tick(command, time, true);
                return;
            }

            // If the prediction is paused, we still tick the system, but we include no command
            // for the tick. We want to wait for our current set of commands to be confirmed before
            // we start predicting again, so it's important that we do not continue processing new commands
            // and incrementing our commandNumber.
            if (this.clientPausePrediction)
            {
                this.stateSystem.GetCommand(this
                    .clientCommandNumber, time); // We tick GetCommand to clear any input, but we don't use it
                this.stateSystem.Tick(null, time, false);
                this.inputHistory.Add(time, null);
                return;
            }

            // Update our command number and process the next tick.
            clientCommandNumber++;
            var input = this.stateSystem.GetCommand(this.clientCommandNumber, time);
            input = this.inputHistory.Add(time, input);
            this.stateSystem.Tick(input, time, false);
        }

        public void NonAuthClientCaptureSnapshot(double time, bool replay)
        {
            // We are replaying a previously captured tick
            if (replay)
            {
                // Check if we had input for that tick. If we did, we will want to use the command number for that
                // input to capture our snapshot
                var input = this.inputHistory.GetExact(time);
                // If we didn't have any input, just skip capturing the tick since we won't expect
                // to see it in our state history either.
                if (input == null) return;

                if (this.stateHistory.IsAuthoritativeEntry(time))
                {
                    var oldState = this.stateHistory.GetExact(time);
                    if (oldState == null) return;
                    this.stateSystem.SetCurrentState(oldState);
                }
                else
                {
                    // Capture the state and overwrite our history for this tick since it may be different
                    // due to corrected collisions or positions of other objects.
                    var replayState = this.stateSystem.GetCurrentState(input.commandNumber, time);
                    // Debug.Log(("Replayed command " + input.commandNumber + " resulted in " + replayState + " Old state: " + oldState));
                    this.stateHistory.Overwrite(time, replayState);
                }
                return;
            }

            // Store the current physics state for prediction
            var state = this.stateSystem.GetCurrentState(clientCommandNumber, time);
            this.stateHistory.Add(time, state);
            // Debug.Log("Processing command " + state.lastProcessedCommand + " resulted in " + state);
        }

        public void NonAuthClientSetSnapshot(double time)
        {
            // Use Get() so that we use the oldest state possible
            // if our history does not include the time
            var state = this.stateHistory.Get(time);
            this.stateSystem.SetCurrentState(state);
        }

        #endregion

        #region Non-Authoritative Server Functions

        public void NonAuthServerTick(double time, bool replay)
        {
            // Non auth server ticks have no functionality
        }

        public void NonAuthServerCaptureSnapshot(double time, bool replay)
        {
            if (replay)
            {
                Debug.LogWarning(
                    "Non-authoritative server should not replay ticks. It should only observe and report observed snapshots.");
                return;
            }

            // Process the buffer of states that we've gotten from the authoritative client
            State latestState = null;
            var statesProcessed = 0;
            do
            {
                statesProcessed++;
                if (statesProcessed > 1)
                {
                    Debug.Log("Processing additional client auth state for catchup.");
                }

                // Attempt to get a new state out of the buffer.
                latestState = this.serverRecievedStateBuffer.Count > 0
                    ? this.serverRecievedStateBuffer.Values[0]
                    : null;

                // If we have a new state to process, update our last processed command and then remove it.
                if (latestState != null)
                {
                    // mark this as our latest state and remove it. We will do the real processing on the final
                    // state retrieved during this loop later.
                    this.serverLastProcessedCommandNumber = latestState.lastProcessedCommand;
                    this.serverRecievedStateBuffer.RemoveAt(0);
                }

                // If we don't have a new state to process, that's ok. It just means that the client hasn't sent us
                // their updated state yet.
            } while (this.serverRecievedStateBuffer.Count >
                     this.serverCommandBufferTargetSize &&
                     statesProcessed < 1 + this.maxServerCommandCatchup);
            // ^ we process up to maxServerCommandCatchup states per tick if our buffer has more than serverCommandBufferTargetSize worth of additional commands.
            // We re-use the command buffer settings since they are calculated to smooth out command processing in the same
            // way we are attempting to smooth out state processing.

            // Commit the last processed snapshot to our state history
            if (latestState != null)
            {
                // Set the snapshots time to be the same as the server to make it effectively
                // the current authoritative state for all observers
                // Remember that state snapshots use the local simulation time where they were created,
                // not a shared timeline.
                latestState.time = time;
                // Use this as the current state for the server
                this.stateSystem.SetCurrentState(latestState);
                // Since it's new, update our server interpolation functions
                this.stateSystem.InterpolateReachedState(latestState);
                // Add the state to our history as we would in a authoritative setup
                this.stateHistory.Add(time, latestState);
            }
        }

        public void NonAuthServerSetSnapshot(double time)
        {
            var state = this.stateHistory.GetExact(time);
            if (state == null)
            {
                // In the case where there's no state to roll back to, we simply leave the state system where it is. This technically means
                // that freshly spawned players will exist in rollback when they shouldn't but we won't handle that edge case for now.
                return;
            }
            this.stateSystem.SetCurrentState(state);
        }

        #endregion

        #region Authoritative Client Functions

        public void AuthClientTick(double time, bool replay)
        {
            // Auth clients may experience a replay when a non-auth networked system experiences a
            // mis-predict. In that case, we will want to roll back our authoritative objects as well
            // to make sure that the new simulation is correct.
            if (replay)
            {
                // In the case of ticks, we simply replay the tick as if it was a predicted object.
                var input = this.inputHistory.GetExact(time);
                // Ignore if we don't have any input history for this tick. The object will remain as
                // it was on it's last snapshot (we will also set the authoritative position on snapshot capture
                // later)
                if (input == null) return;
                this.stateSystem.Tick(input, time, true);
                return;
            }

            // Update our command number and process the next tick
            clientCommandNumber++;
            // Read input from system
            var command = this.stateSystem.GetCommand(clientCommandNumber, time);
            this.inputHistory.Add(time, command);
            // Tick system
            this.stateSystem.Tick(command, time, false);
        }

        public void AuthClientCaptureSnapshot(double time, bool replay)
        {
            // We don't want a predicted history resim to change an authoritative history,
            // so in the case of a replay, we don't actually capture the new snapshot
            // when in an authoritative context. We will actually reset the authoritative
            // object to be where it was in our snapshot history.
            // This means that client auth objects will properly affect resims, but resims
            // will not affect client history that has already been reported to the server.
            if (replay)
            {
                var authoritativeState = this.stateHistory.Get(time);
                this.stateSystem.SetCurrentState(authoritativeState);
                return;
            }

            var state = this.stateSystem.GetCurrentState(clientCommandNumber, time);
            this.stateHistory.Add(time, state);
        }

        public void AuthClientSetSnapshot(double time)
        {
            // Clients rolling back for resims on predicted objects will want to
            // have those resims properly affected by client auth objects.
            var state = this.stateHistory.Get(time);
            this.stateSystem.SetCurrentState(state);
        }

        #endregion

        #region Observing Client Functions

        public void ObservingClientCaptureSnapshot(double time, bool replay)
        {
            // No matter what, an observing client should always have the state
            // in the authoritative state from the server during resims. We use our
            // local timeline record for this.
            if (replay)
            {
                var authoritativeState = this.stateHistory.Get(time);
                if (authoritativeState == null) return;
                this.stateSystem.SetCurrentState(authoritativeState);
                return;
            }

            // Get the authoritative state received just before the current observer time. (remember interpolation is buffered by bufferTime)
            // Note: if we get multiple fixed update calls per frame, we will use the same state for all fixedUpdate calls
            // because NetworkTime.time is only advanced on Update(). This might cause resim issues with low framerates...
            var observedState = this.observerHistory.Get(NetworkTime.time - NetworkClient.bufferTime);
            if (observedState == null)
            {
                // We don't have state to use for interping or we haven't reached a new state yet. Don't add any
                // data to our state history.
                return;
            }
            
            // Clone the observed state and update it to be on the local physics timeline.
            var state = (State) observedState.Clone();
            state.time = time;
            // Store the state in our state history for re-simulation later if needed.
            this.stateHistory.Add(time, state);

            // Handle observer interpolation as well. We use observedState for this since we want the interp to be able
            // to use NetworkTime.time with this state.
            // Update our last state time so we don't call InterpolateReachedState more than once on the same state.
            this.clientLastInterpolatedStateTime = observedState.time;
            // Notify the system of a new reached state
            this.stateSystem.InterpolateReachedState(observedState);
        }

        public void ObservingClientTick(double time, bool replay)
        {
            // No actions on observing tick.
        }

        public void ObservingClientSetSnapshot(double time)
        {
            var authoritativeState = this.stateHistory.Get(time);
            if (authoritativeState == null) return;
            this.stateSystem.SetCurrentState(authoritativeState);
        }

        #endregion

        #region Utility Functions

        /**
         * Handles triggering interpolation in the system for observers
         */
        private void Interpolate()
        {
            if (this.observerHistory.Values.Count == 0) return;

            // Get the time we should render on the client.
            var clientTime = (float)NetworkTime.time - NetworkClient.bufferTime;

            // Get the state history around the time that's currently being rendered.
            if (!this.observerHistory.GetAround(clientTime, out State prevState, out State nextState))
            {
                Debug.LogWarning("Not enough state history for rendering. " + this.observerHistory.Keys.Count +
                                 " entries. First " + this.observerHistory.Keys[0] + " Last " +
                                 this.observerHistory.Keys[^1] + " Target " + clientTime);
                return;
            }

            // How far along are we in this interp?
            var timeDelta = (clientTime - prevState.time) / (nextState.time - prevState.time);

            // Call interp on the networked state system so it can place things properly for the render.
            this.stateSystem.Interpolate((float)timeDelta, prevState, nextState);
        }

        /**
        * Uses a server snapshot to reconcile client prediction with the actual authoritative
        * server history.
        */
        private void ReconcileInputHistory(PerformResimulate resimulate, State state)
        {
            if (this.stateHistory.Values.Count == 0)
            {
                // We have no predicted state history. Occurs only in a situation where
                // we have somehow cleared this history (and not added more), or we haven't started predicting yet and the server
                // is sending us snapshots.
                // Since we now know where we should be authoritatively, set the current state. Since this isn't a prediction,
                // we don't add it to the stateHistory.
                this.stateSystem.SetCurrentState(state);
                Physics.SyncTransforms();
                return;
            }

            if (this.stateHistory.Values[0].lastProcessedCommand > state.lastProcessedCommand)
            {
                // In this situation, our oldest saved command is actually higher than the last confirmed
                // command by the server. That means that the server took more time to process the command
                // than we store in our client state history. In this case, we can't confirm our prediction
                // was correct, because we literally don't have the prediction anymore.
                // If we hit this case, we want to stop predicting new commands and wait for the server to confirm
                // one of our previous predictions. Once one is confirmed, we can start predicting again.
                // On extremely high ping, this will mean that the local player will freeze in place while we wait for
                // confirmation from the server.
                this.clientPausePrediction = true;
                Debug.LogWarning(
                    "Prediction paused due to a large number of unconfirmed commands to the server. Is there something wrong with the network?");
                return;
            }

            // Find our client predicted state in our state history so we can compare it with what the server
            // provided us.
            // Our predicted state history will only have frame that matches the lastProcessedCommand, but
            // keep in mind that the server may send us more than one frame with the same lastProcessedCommand
            // due to packet loss/delay. We have to re-simulate in those cases too if the state has changed
            State clientPredictedState = null;
            foreach (var predictedState in this.stateHistory.Values)
            {
                if (predictedState.lastProcessedCommand == state.lastProcessedCommand)
                {
                    clientPredictedState = predictedState;
                    break;
                }
            }

            if (clientPredictedState == null)
            {
                // If we can't find a predicted state entry, we will consider this prediction to be correct since
                // we wouldn't be able to reconcile it with an actual prediction.
                // This may happen from time to time if the client clock gets out of sync with the server for a moment.
                // Generally a situation like this is recoverable by processing an additional state snapshot from the
                // server.
                Debug.LogWarning("Couldn't find client predicted state for command " + state.lastProcessedCommand);
                return;
            }

            // No matter what, if we get here, we are safe to send commands since we have a command that has been
            // confirmed by the server that we have locally in our state history.
            this.clientPausePrediction = false;

            // Check if our predicted state matches up with our new authoritative state.
            if (clientPredictedState.Compare(this.stateSystem, state))
            {
                // If it does, we can just return since our predictions have all been correct so far.
                this.stateHistory.SetAuthoritativeEntry(clientPredictedState.time, true);
                return;
            }

            Debug.LogWarning("Authoritative result for " + this.name + " on cmd# " + state.lastProcessedCommand + " was mispredicted. Requesting resimulation.");

            // Correct our networked system state to match the authoritative answer from the server
            this.stateSystem.SetCurrentState(state);
            Physics.SyncTransforms();
            // Build an updated state entry for the command that was processed by the server.
            // We use the client prediction time so we can act like we got this right in our history
            var updatedState = this.stateSystem.GetCurrentState(state.lastProcessedCommand,
                clientPredictedState.time);
            // Debug.Log("Generated updated state for prediction history: " + updatedState + " based on authed state: " + state);
            // Overwrite the time we should have predicted this on the client so it
            // appears from the client perspective that we predicted the command correctly.
            this.stateHistory.Overwrite(clientPredictedState.time, updatedState);
            this.stateHistory.SetAuthoritativeEntry(clientPredictedState.time, true);
            // Resimulate all commands performed after the incorrect prediction so that
            // our future predictions are (hopefully) correct.
            resimulate(clientPredictedState.time);
        }

        #endregion

        #region Networking

        // TODO: consider sending all tick data from the last snapshot time to the new time so
        // we can rebuild a completely accurate history for other re-simulations
        private void ClientReceiveSnapshot(State state)
        {
            if (state == null) return;

            if (lastReceivedSnapshotTime >= state.time)
            {
                // print("Ignoring out of order snapshot");
                return;
            }

            lastReceivedSnapshotTime = state.time;

            // Debug.Log("Client receive snapshot" + state);
            // The client is a non-authoritative owner and should update
            // their local state with the authoritative state from the server.
            if (isOwned && serverAuth)
            {
                // We just got a new confirmed state and we have already processed
                // our most recent reconcile.
                if (this.clientLastConfirmedState == null)
                {
                    // Store the state so we can use it when the callback is fired.
                    this.clientLastConfirmedState = state;
                    // Schedule the resimulation with the simulation manager.
                    AirshipSimulationManager.Instance.ScheduleResimulation((resimulate) =>
                    {
                        // Reconcile when this callback is executed. Use the last confirmed state received,
                        // (since more could come in from the network while we are waiting for the callback)
                        this.ReconcileInputHistory(resimulate, this.clientLastConfirmedState);
                        this.clientLastConfirmedState = null;
                    });
                }
                // We received a new state update before we were able to reconcile, just update the stored
                // latest state so we use the latest in our scheduled resimulation.
                else
                {
                    clientLastConfirmedState = state;
                }

                return;
            }

            // This client is an authoritative owner and should ignore the snapshot,
            // it is only meant for observers
            if (isOwned && !serverAuth)
            {
                return;
            }

            // This client is an observer and should store and interpolate the snapshot.
            if (!isOwned)
            {
                // Observers will render using the server's timeline via NetworkTime
                this.observerHistory.Add(state.time, state);
            }
        }

        private void ServerReceiveInputCommand(Input command)
        {
            // Debug.Log("Server received command " + command.commandNumber + " for " + this.gameObject.name);
            // This should only occur if the server is authoritative.
            if (!serverAuth) return;

            // We may get null commands when the client pauses command sending because of unconfirmed commands.
            // We basically ignore these as this is the client telling us "nothing". We could consider filtering
            // these on the client before sending.
            if (command == null) return;

            if (this.serverCommandBuffer.TryGetValue(command.commandNumber, out Input existingInput))
            {
                // Debug.Log("Received duplicate command number from client. Ignoring");
                return;
            }

            // Reject commands if our buffer is full.
            if (this.serverCommandBuffer.Count > this.serverCommandBufferMaxSize)
            {
                Debug.LogWarning("Dropping command " + command.commandNumber +
                                 " due to exceeding command buffer size. First command in buffer is " + this.serverCommandBuffer.Values[0]);
                return;
            }

            if (this.serverLastProcessedCommandNumber >= command.commandNumber)
            {
                // Debug.Log(("Command " + command.commandNumber + " arrived too late to be processed. Ignoring."));
                return;
            }

            // Queue the command for processing
            this.serverCommandBuffer.Add(command.commandNumber, command);
        }

        private void ServerReceiveSnapshot(State snapshot)
        {
            // Debug.Log("Server receive snapshot" + snapshot.lastProcessedCommand + " data: " + snapshot.ToString());
            // This should only occur if the server is not authoritative.
            if (serverAuth) return;

            if (snapshot == null) return;
                
            if (this.serverRecievedStateBuffer.TryGetValue(snapshot.lastProcessedCommand, out State existingInput))
            {
                // Debug.Log("Received duplicate state from client. Ignoring. " + snapshot.lastProcessedCommand);
                return;
            }
               
            // Update our state so we can distribute it to all observers.
            if (serverLastProcessedCommandNumber >= snapshot.lastProcessedCommand)
            {
                // We already have this state, so this should be ignored.
                return;
            }

            // Reject new states if our buffer is full
            if (serverRecievedStateBuffer.Count > this.serverCommandBufferMaxSize)
            {
                Debug.LogWarning("Dropping state " + snapshot.lastProcessedCommand +
                                 " due to exceeding state buffer size.");
                return;
            }

            // We order by clients last processed command. This is effectively the same as ordering
            // by time, but avoids the confusion around client vs server timelines.
            this.serverRecievedStateBuffer.Add(snapshot.lastProcessedCommand, snapshot);
        }

        #endregion
    }
}