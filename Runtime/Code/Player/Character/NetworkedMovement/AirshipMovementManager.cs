using System;
using System.Collections.Generic;
using System.Linq;
using Code.Network.Simulation;
using Code.Player.Character.Net;
using Mirror;
using RSG.Promises;
using UnityEngine;

namespace Code.Player.Character.NetworkedMovement
{
    [RequireComponent(typeof(NetworkIdentity))]
    public abstract class AirshipMovementManager<MovementSystem, State, Input> : NetworkBehaviour
        where State : StateSnapshot where Input : InputCommand where MovementSystem : NetworkedMovement<State, Input>
    {
        #region Inspector Settings

        // Inspector settings
        public MovementSystem movementSystem;

        // Size of the command buffer on the server
        // TODO: we might want to make this a function of command rate
        [Tooltip(
            "Amount of commands the server will store for later processing. Increasing this value will help players with a poor network connection, but will result in delayed inputs.")]
        [Range(1, 10)]
        public int maxServerCommandBuffer = 4;

        // Todo: consider making a negative number drop that many frames per tick in order to correct instead of processing them.
        [Tooltip(
            "Amount of extra commands that can be processed per tick to catch up when there is a backup of commands in the command buffer. Increasing this value will help players with a poor network connection, but will result in irregular movement for observers.")]
        [Range(1, 10)]
        public int maxServerCommandCatchup = 1;

        // Amount of times in a row the server will fill in a missing command
        public int maxServerCommandPrediction = 2;

        // The size of the interpolation buffer for observers. Higher means smoother in bad network conditions, but more delayed visuals
        public int interpolationBufferSize = 3;

        // The number of times per second the client sends it's input commands to the server
        public int clientInputRate = 30;

        // The number of times per second the server sends snapshots of client positions to observers
        public int serverSnapshotRate = 20;

        // Determines if the server has authority over the character
        public bool serverAuth = false;

        #endregion

        #region Internal State

        // Command number tracking for server and client
        private int serverLastProcessedCommandNumber = 0;
        private int clientCommandNumber = 0;

        // interpolation implementation fields.
        private State[] interpolationBuffer;
        private double lastReceivedSnapshotTime = 0;

        // Send rate tracking fieldsP 
        private double lastClientSend = 0;
        private double lastServerSend = 0;

        // Server processing for commands
        private Input lastProcessedCommand;

        // This may advance faster than last processed command if we predicted command inputs.
        // The data in lastProcessedCommand will be the command used for ticking even if the number
        // does not match.
        private int lastProcessedCommandNumber;
        private int serverPredictedCommandCount = 0;
        private SortedList<int, Input> serverCommandBuffer = new SortedList<int, Input>();

        // Client processing for input prediction
        // private SortedList<double, Input> clientInputHistory = new SortedList<double, Input>();
        // private SortedList<double, State> clientPredictedHistory = new SortedList<double, State>();
        private State clientLastConfirmedState;
        // Flag for if we are the resimulation requestor.
        // For predicted objects, we don't overwrite the predicted state if we didn't request resimulation
        // TODO: in the future, we might get better resims in cases where there is more than one predicted
        // component by resimulating both, but it would require us to ensure that we have accurate intermediate
        // data and don't overwrite authoritative snapshots in our prediction history.
        private bool clientPredictionResimRequestor = false;
        // When the client doesn't recieve confirmation of it's commands for an extended time, we pause
        // predictions until we receive more commands from the server.
        private bool clientPausePrediction = false;

        // Fields for managing re-simulations
        // We store a max of 1 second of history
        // Note: The server does not store input history. We don't perform simulation rollbacks on the
        // server because the server is the authority (so it never rolls back) or just an observer (so it
        // doesn't process inputs).
        private History<Input> inputHistory;
        private History<State> stateHistory;

        #endregion

        #region Event Definitions

        // Networking actions to be used by subclass;
        protected Action<State> OnClientReceiveSnapshot;
        protected Action<State> OnServerReceiveSnapshot;
        protected Action<Input[]> OnServerReceiveInput;

        // Functions to be implemented by subclass that perform networking actions
        public abstract void SendClientInputToServer(Input[] input);
        public abstract void SendClientSnapshotToServer(State snapshot);
        public abstract void SendServerSnapshotToClients(State snapshot);

        #endregion

        #region Lifecycle Functions

        private void Awake()
        {
            AirshipSimulationManager.instance.ActivateSimulationManager();
            AirshipSimulationManager.OnPerformTick += this.OnPerformTick;
            AirshipSimulationManager.OnSetSnapshot += this.OnSetSnapshot;
            AirshipSimulationManager.OnCaptureSnapshot += this.OnCaptureSnapshot;

            this.interpolationBuffer = new State[this.interpolationBufferSize + 1];
            this.inputHistory = new((int)Math.Ceiling(1f / Time.fixedDeltaTime));
            this.stateHistory = new((int)Math.Ceiling(1f / Time.fixedDeltaTime));

            this.OnClientReceiveSnapshot += ClientReceiveSnapshot;
            this.OnServerReceiveSnapshot += ServerReceiveSnapshot;
            this.OnServerReceiveInput += ServerReceiveInputCommand;
        }

        public void Start()
        {
            print(("Gravity is " + Physics.gravity));
            
            // We are a shared client and server
            if (isClient && isServer)
            {
                this.movementSystem.OnSetMode(MovementMode.Authority);
            }
            // We are an authoritative client
            else if (isClient && isOwned && !serverAuth)
            {
                this.movementSystem.OnSetMode(MovementMode.Authority);
            }
            // We are a non-authoritative client
            else if (isClient && isOwned && serverAuth)
            {
                this.movementSystem.OnSetMode(MovementMode.Input);
            }
            // We are an observing client
            else if (isClient && !isOwned)
            {
                this.movementSystem.OnSetMode(MovementMode.Observer);
            }
            // We are an authoritative server
            else if (isServer && serverAuth)
            {
                this.movementSystem.OnSetMode(MovementMode.Authority);
            }
            // We are a non-authoritative server
            else if (isServer && !serverAuth)
            {
                this.movementSystem.OnSetMode(MovementMode.Observer);
            }
            else
            {
                Debug.LogWarning("Unable to determine networked movement mode. Did we miss a case? " + isServer + " " +
                                 isClient + " " + isOwned + " " + serverAuth);
            }
        }

        public void OnDestroy()
        {
            AirshipSimulationManager.OnPerformTick -= this.OnPerformTick;
            AirshipSimulationManager.OnSetSnapshot -= this.OnSetSnapshot;
            AirshipSimulationManager.OnCaptureSnapshot -= this.OnCaptureSnapshot;
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
        }

        private void OnCaptureSnapshot(double time, bool replay)
        {
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
        }

        private void OnSetSnapshot(double time)
        {
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

            var commandsProcessed = 0;
            do
            {
                commandsProcessed++;
                if (commandsProcessed > 1)
                {
                    Debug.Log("Processing additional tick for catchup.");
                }

                // Attempt to get a new command out of the buffer.
                var commandEntry = this.serverCommandBuffer.Count > 0 ? this.serverCommandBuffer.Values[0] : null;

                // If we have a new command to process
                if (commandEntry != null)
                {
                    // Get the command to process
                    var command = this.serverCommandBuffer.Values[0];
                    // Get the expected next command number
                    var expectedNextCommandNumber = this.lastProcessedCommandNumber + 1;

                    // Check if that command is in sequence. If we have a gap of commands, we will fill it with the last
                    // processed command up to the maxServerCommandPrediction size.
                    if (this.lastProcessedCommand != null && command.commandNumber != expectedNextCommandNumber &&
                        this.serverPredictedCommandCount < this.maxServerCommandPrediction)
                    {
                        Debug.LogWarning("Missing command " + expectedNextCommandNumber +
                                         " in the command buffer. Next command was: " + command.commandNumber +
                                         ". Predicted " +
                                         (this.serverPredictedCommandCount + 1) + " command(s) so far.");
                        this.lastProcessedCommandNumber = expectedNextCommandNumber;
                        command = this.lastProcessedCommand;
                        this.serverPredictedCommandCount++;
                    }
                    // We have a valid command that is in sequence, or we reached our max fill. Remove the next
                    // valid command from the buffer and process it.
                    else
                    {
                        Debug.Log("Ticking next command in sequence: " + command.commandNumber);
                        this.serverPredictedCommandCount = 0;
                        this.serverCommandBuffer.RemoveAt(0);
                        this.lastProcessedCommand = command;
                        this.lastProcessedCommandNumber = command.commandNumber;
                    }

                    // tick command
                    this.movementSystem.Tick(command, false);
                }
                else
                {
                    Debug.LogWarning("No commands left. Last command processed: " + this.lastProcessedCommand);
                    // Ensure that we always tick the system even if there's no command to process.
                    this.movementSystem.Tick(null, false);
                }
            } while (this.serverCommandBuffer.Count >
                     Math.Ceiling((1f / this.clientInputRate / Time.fixedDeltaTime) * 1.5) &&
                     commandsProcessed < 1 + this.maxServerCommandCatchup);
            // ^ we process up to maxServerCommandCatchup commands per tick if our buffer has more than 1.5 snapshots worth of additional commands.
        }

        public void AuthServerCaptureSnapshot(double time, bool replay)
        {
            if (replay)
            {
                // Log in AuthServerTick will notify if this case occurs.
                return;
            }

            // get snapshot to send to clients
            var state = this.movementSystem.GetCurrentState(this.lastProcessedCommandNumber,
                time);
            // Todo: expose state history for lag compensation use
            this.stateHistory.Add(time, state);
            Debug.Log("Processing commands up to " + this.lastProcessedCommandNumber + " resulted in " + state);

            // Send snapshot to clients if we need to.
            if (this.lastServerSend <= NetworkTime.time - (1f / this.serverSnapshotRate))
            {
                this.lastServerSend = NetworkTime.time;
                this.SendServerSnapshotToClients(state);
            }
        }

        public void AuthServerSetSnapshot(double time)
        {
            // This function shouldn't run since an authoritative server should never run
            // resimulations.
            // Log in AuthServerTick will notify if this case occurs.
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
                // If we can't find a command, return since that means we just won't process an input.
                if (command == null) return;
                // Process the command again like before, but pass replay into the network system so that
                // it doesn't replay effects or animations, etc.
                this.movementSystem.Tick(command, replay);
                return;
            }
            
            // Before we start our tick update, make sure our current predicted state is the most correct it can be.
            if (this.clientLastConfirmedState != null)
            {
                this.ReconcileInputHistory(this.clientLastConfirmedState);
                // We set this to null so that we don't re-reconcile on ticks where there's no new information
                // to base our prediction off of.
                this.clientLastConfirmedState = null;
            }
            
            // If the prediction is paused, we still tick the movement system, but we include no command
            // for the tick. We want to wait for our current set of commands to be confirmed before
            // we start predicting again, so it's important that we do not add anything else to
            // inputHistory.
            if (this.clientPausePrediction)
            {
                this.movementSystem.GetCommand(this.clientCommandNumber, time); // We tick GetCommand to clear any input, but we don't use it
                this.movementSystem.Tick(null, false);
                return;
            }

            // Update our command number and process the next tick.
            clientCommandNumber++;
            var input = this.movementSystem.GetCommand(this.clientCommandNumber, time);
            input = this.inputHistory.Add(time, input);
            this.movementSystem.Tick(input, false);
            
            var sendIntervalSec = (1f / this.clientInputRate);
            if (this.lastClientSend <= NetworkTime.time - sendIntervalSec)
            {
                this.lastClientSend = NetworkTime.time;
                // We will sometimes resend unconfirmed commands. The server should ignore these if
                // it has them already.
                this.SendClientInputToServer(this.inputHistory.GetAllAfter(NetworkTime.time - (sendIntervalSec * 2)));
            }
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

                // If we did request this resimulation, we will process the new state history
                if (this.clientPredictionResimRequestor)
                {
                    // Capture the state and overwrite our history for this tick since it may be different
                    // due to corrected collisions or positions of other objects.
                    var replayState = this.movementSystem.GetCurrentState(input.commandNumber, time);
                    var oldState = this.stateHistory.GetExact(time);
                    Debug.Log(("Replayed command " + input.commandNumber + " resulted in " + replayState + " Old state: " + oldState));
                    this.stateHistory.Overwrite(time, replayState);
                    return;
                }
                // If we didn't request this resimulation, reset our state to the one we previously captured for this tick.
                else
                {
                    var oldState = this.stateHistory.GetExact(time);
                    if (oldState == null) return;
                    this.movementSystem.SetCurrentState(oldState);
                }
            }

            // If prediction is disabled, we are waiting for our current stateHistory to be confirmed
            // by the server, so we tick the networked command system, but we don't store the state.
            if (this.clientPausePrediction)
            {
                this.movementSystem.GetCurrentState(clientCommandNumber, time);
                return;
            }

            // Store the current physics state for prediction
            var state = this.movementSystem.GetCurrentState(clientCommandNumber, time);
            this.stateHistory.Add(time, state);
            Debug.Log("Processing command " + state.lastProcessedCommand + " resulted in " + state);
        }

        public void NonAuthClientSetSnapshot(double time)
        {
            // Use Get() so that we use the oldest state possible
            // if our history does not include the time
            var state = this.stateHistory.Get(time);
            this.movementSystem.SetCurrentState(state);
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
                    "Non-authoritative server should not replay ticks. It should only observer and report observed snapshots.");
                return;
            }

            if (this.lastServerSend <= NetworkTime.time - (1f / this.serverSnapshotRate))
            {
                // read current state
                var state = this.movementSystem.GetCurrentState(serverLastProcessedCommandNumber,
                    time);
                this.stateHistory.Add(time, state);
                this.lastServerSend = NetworkTime.time;
                this.SendServerSnapshotToClients(state);
            }
        }

        public void NonAuthServerSetSnapshot(double time)
        {
            // Non auth set snapshots should not occur since no re-simulations should take ploce on the server.
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
                // it was on it's last snapshot
                // TODO: do we need to make sure of that by making it kinematic? Or ensuring that we always
                // reset the object to the last snapshot somehow after the tick is over?
                if (input == null) return;
                this.movementSystem.Tick(input, true);
                return;
            }

            // Update our command number and process the next tick
            clientCommandNumber++;
            // Read input from movement system
            var command = this.movementSystem.GetCommand(clientCommandNumber, time);
            this.inputHistory.Add(time, command);
            // Tick movement system
            this.movementSystem.Tick(command, false);
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
                this.movementSystem.SetCurrentState(authoritativeState);
                return;
            }

            var state = this.movementSystem.GetCurrentState(clientCommandNumber, time);
            this.stateHistory.Add(time, state);

            // Report snapshot from movement system to server.
            if (this.lastClientSend <= NetworkTime.time - (1f / this.clientInputRate))
            {
                this.stateHistory.Add(time, state);
                this.lastClientSend = NetworkTime.time;
                this.SendClientSnapshotToServer(state);
            }
        }

        public void AuthClientSetSnapshot(double time)
        {
            // Clients rolling back for resims on predicted objects will want to
            // have those resims properly affected by client auth objects.
            var state = this.stateHistory.Get(time);
            this.movementSystem.SetCurrentState(state);
        }

        #endregion

        #region Utility Functions

        /**
         * Handles triggering interpolation in the movement system for observers
         */
        private void Interpolate()
        {
            //Don't process unless we have received data
            if (this.interpolationBuffer[0] == null || this.interpolationBuffer[1] == null)
            {
                print("Not enough observer states");
                return;
            }

            // var timePerSnapshot = 1f / this.serverSnapshotRate;
            // var ticksPerSnapshot = timePerSnapshot / Time.fixedDeltaTime;
            // var tickBuffer = (this.interpolationBufferSize - 1) * ticksPerSnapshot;
            // var clientTick = AirshipSimulationManager.instance.tick - tickBuffer;

            var renderBuffer = (float)(this.interpolationBufferSize - 1) / this.serverSnapshotRate;
            var clientTime = (float)NetworkTime.time - renderBuffer;
            State prevState = null;
            State nextState = null;

            //Find the states to lerp to based on our current time
            for (int i = 0; i < this.interpolationBuffer.Length; i++)
            {
                var state = this.interpolationBuffer[i];
                if (state == null) break;

                // print("At time: " + clientTime + " Checkign state: " + i + " time: " + state.time);
                if (state.time < clientTime)
                {
                    prevState = state;
                }
                else if (state.time > clientTime)
                {
                    nextState = state;
                    break;
                }
            }

            // No state in the past
            if (prevState == null || nextState == null)
            {
                print("no prev or next state");
                return;
            }

            // How far along are we in this interp?
            var timeDelta = (clientTime - prevState.time) / (nextState.time - prevState.time);

            // var ticksBetweenSnapshots = (nextState.tick - prevState.tick);
            // var tickProgress = (clientTick - prevState.tick) / ticksBetweenSnapshots;
            // var currentTickProgress = (Time.time - Time.fixedTime) / Time.fixedDeltaTime;
            // var timeDelta = tickProgress + (currentTickProgress / ticksBetweenSnapshots);
            this.movementSystem.Interpolate((float)timeDelta, prevState, nextState);
        }

        /**
        * Uses a server snapshot to reconcile client prediction with the actual authoritative
        * server history.
        */
        private void ReconcileInputHistory(State state)
        {
            if (this.stateHistory.Values.Count == 0)
            {
                // We have no predicted state history. Occurs only in a situation where
                // we have somehow cleared this history (and not added more), or we haven't started predicting yet and the server
                // is sending us snapshots.
                // Since we now know where we should be authoritatively, set the current state. Since this isn't a prediction,
                // we don't add it to the stateHistory.
                this.movementSystem.SetCurrentState(state);
                Physics.SyncTransforms();
                return;
            }

            if (this.stateHistory.Values[0].lastProcessedCommand > state.lastProcessedCommand)
            {
                // In this situation, our oldest saved command is actually higher than the last confirmed
                // command by the server. That means that the server took more time to process the command
                // than we store in our client state history. In this case, we can't confirm our prediction
                // was correct, because we literally don't have the prediction anymore.
                // TODO: Perhaps we should stop predicting until we receive one we can confirm one of our
                // predictions?
                this.clientPausePrediction = true;
                Debug.LogWarning("Prediction paused due to a large number of unconfirmed commands to the server. Is there something wrong with the network?");
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

            // TODO: we somehow hit this case early on. It checked lastProcesssed = 5 against lastProcessed = 2. Weird.
            if (clientPredictedState == null)
            {
                // This should never happen since our checks above confirm that our command should be in our history.
                // If it's not, that means someone cleared the history or we've got something weird going on with our commands.
                // The server should never send us a lastProcessedCommand larger than what we've predicted since the
                // client controls the command number.
                Debug.LogWarning(
                    "Unable to find predicted command result in snapshot history. This shouldn't happen. Command number: " +
                    state.lastProcessedCommand + ". Last predicted command: " + this.stateHistory.Values[^1] + " Oldest predicted command: " + this.stateHistory.Values[0]);
                return;
            }

            // No matter what, if we get here, we are safe to send commands since we have a command that has been
            // confirmed by the server that we have locally in our state history.
            this.clientPausePrediction = false;

            // Check if our predicted state matches up with our new authoritative state.
            if (clientPredictedState.CompareWithMargin(0f, state))
            {
                // If it does, we can just return since our predictions have all been correct so far.
                return;
            }
            
            // Correct our networked system state to match the authoritative answer from the server
            this.movementSystem.SetCurrentState(state);
            Physics.SyncTransforms();
            // Build an updated state entry for the command that was processed by the server.
            // We use the client prediction time so we can act like we got this right in our history
            var updatedState = this.movementSystem.GetCurrentState(state.lastProcessedCommand,
                clientPredictedState.time);
            Debug.Log("Generated updated state for prediction history: " + updatedState + " based on authed state: " + state);
            // Overwrite the time we should have predicted this on the client so it
            // appears from the client perspective that we predicted the command correctly.
            this.stateHistory.Overwrite(clientPredictedState.time, updatedState);
            // Resimulate all commands performed after the incorrect prediction so that
            // our future predictions are (hopefully) correct.
            this.clientPredictionResimRequestor = true;
            AirshipSimulationManager.instance.PerformResimulation(clientPredictedState.time);
            this.clientPredictionResimRequestor = false;
        }

        #endregion

        #region Networking

        // TODO: consider sending all tick data from the last snapshot time to the new time so
        // we can rebuild a completely accurate history for other re-simulations
        private void ClientReceiveSnapshot(State state)
        {
            if (lastReceivedSnapshotTime > state.time)
            {
                print("Ignoring out of order snapshot");
                return;
            }

            lastReceivedSnapshotTime = state.time;

            // Debug.Log("Client receive snapshot" + state);
            // The client is a non-authoritative owner and should update
            // their local state with the authoritative state from the server.
            if (isOwned && serverAuth)
            {
                // We store the most up to date state to be reconciled on the next client tick.
                clientLastConfirmedState = state;
                return;
            }

            // This client is an authoritative owner and should ignore the snapshot,
            // it is only meant for observers (todo, ignore owner in this case?)
            if (isOwned && !serverAuth)
            {
                return;
            }

            // This client is an observer and should store and interpolate the snapshot.
            if (!isOwned)
            {
                // Shift the buffer and put in the new snapshot.
                for (int i = 0; i < this.interpolationBuffer.Length - 1; i++)
                {
                    interpolationBuffer[i] = interpolationBuffer[i + 1];
                }

                interpolationBuffer[this.interpolationBuffer.Length - 1] = state;
            }
        }

        private void ServerReceiveInputCommand(Input[] commands)
        {
            foreach (var command in commands)
            {
                Debug.Log("Server received command " + command.commandNumber);
                // This should only occur if the server is authoritative.
                if (!serverAuth) continue;

                if (this.serverCommandBuffer.TryGetValue(command.commandNumber, out Input existingInput))
                {
                    Debug.Log("Received duplicate command number from client. Ignoring");
                    continue;
                }

                // Reject commands if our buffer is full.
                var maxCommandBuffer = Math.Ceiling((1f / this.clientInputRate / Time.fixedDeltaTime) * 2);
                if (this.serverCommandBuffer.Count > maxCommandBuffer)
                {
                    Debug.Log("Dropping command " + command.commandNumber + " due to exceeding command buffer size.");
                    continue;
                }

                if (this.lastProcessedCommandNumber >= command.commandNumber)
                {
                    Debug.Log(("Command " + command.commandNumber + " arrived too late to be processed. Ignoring."));
                    continue;
                }

                // Queue the command for processing
                this.serverCommandBuffer.Add(command.commandNumber, command);
            }
        }

        private void ServerReceiveSnapshot(State snapshot)
        {
            // Debug.Log("Server receive snapshot" + snapshot.lastProcessedCommand + " data: " + snapshot.ToString());
            // This should only occur if the server is not authoritative.
            if (serverAuth) return;

            // The server is receiving an update from the authoritative client owner.
            // Update our state so we can distribute it to all observers.
            if (serverLastProcessedCommandNumber >= snapshot.lastProcessedCommand)
            {
                // We already have newer state, so this should be ignored.
                return;
            }

            this.serverLastProcessedCommandNumber = snapshot.lastProcessedCommand;
            this.movementSystem.SetCurrentState(snapshot);

            // TODO: consider interpolating on the server as well...
            // I think client authed characters are currently freezing between snapshots
            // which would make things like server hit registration be a bit behind.
            // Consider implementing interpolation in the fixed update of the server using
            // lag compensation history so that we have some reasonable in-between frames
            // Or maybe just only do that when lag compensating? It doesn't matter to observers
            // since they do their own interpolation of the snapshots forwarded to them by the server.
        }

        #endregion
    }
}