using System;
using System.Collections.Generic;
using Code.Network.Simulation;
using Code.Network.StateSystem.Structures;
using Code.Player.Character.Net;
using Code.Util;
using Mirror;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace Code.Network.StateSystem
{
    [LuauAPI]
    [RequireComponent(typeof(NetworkIdentity))]
    public abstract class AirshipNetworkedStateManager<StateSystem, State, Diff, Input> : NetworkBehaviour
        where State : StateSnapshot
        where Diff : StateDiff
        where Input : InputCommand
        where StateSystem : NetworkedStateSystem<StateSystem, State, Diff, Input> {
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

        [Tooltip(
            "Determines if the server will process inputs from a client, or if it will create it's own input commands.")]
        public bool serverGeneratesCommands = false;

        #endregion

        #region Internal State

        // Command number tracking
        private int clientCommandNumber = 0;

        // Send rate tracking fields
        private double lastClientSend = 0;

        private double lastServerSend = 0;

        // The local time contained in the last data sent. This is used to know which data we have already sent to the server
        private uint clientLastSentLocalTick = 0;

        // Server processing for commands
        private Input lastProcessedCommand;

        // This may advance faster than last processed command if we predicted command inputs.
        // The data in lastProcessedCommand will be the command used for ticking even if the number
        // does not match.
        private int serverLastProcessedCommandNumber;
        private int serverPredictedCommandCount = 0;
        private SortedList<int, Input> serverCommandBuffer = new SortedList<int, Input>();

        private int serverCommandBufferMaxSize = 0;

        // How many commands we should generally have in the command buffer
        private int serverCommandBufferTargetSize = 0;

        // Non-auth server command tracking
        // Note: we also re-use some of the above command buffer fields
        private SortedList<int, State> serverRecievedStateBuffer = new SortedList<int, State>();

        // Client processing for input prediction
        private State clientLastConfirmedState;

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
        // in the observer snapshot function. Clients build observer history out of the state snapshots they
        // receive as well as by applying diffs received to existing state in this history.
        public ObservableHistory<State> observerHistory;
        private double lastObserverClientTime = 0;

        // Base history is the history that is used as a base for snapshot diffing. We only need to keep a few snapshots
        // around for applying diffs, so we keep a much smaller history of base states the server might use to generate diffs.
        // Both observers and local players use this history.
        public History<State> baseHistory;
        private double lastReceivedSnapshotTick = 0;

        // Client interpolation fields
        private double clientLastInterpolatedStateTick = 0;

        // A map between clientId and the last acked snapshot they received. We use this to select
        // the snapshot we use to generate diffs for the client.
        private Dictionary<int, uint> serverAckedSnapshots = new();

        #endregion

        #region Event Definitions

        // Networking actions to be used by subclass;
        protected Action<State> OnClientReceiveSnapshot;
        protected Action<Diff> OnClientReceiveDiff;
        protected Action<int> OnServerReceiveFullSnapshotRequest;
        protected Action<State> OnServerReceiveSnapshot;
        protected Action<Input> OnServerReceiveInput;

        // Functions to be implemented by subclass that perform networking actions
        public abstract void SendClientInputToServer(Input input);
        public abstract void SendClientSnapshotToServer(State snapshot);

        /// <summary>
        /// Used by the client to request the server to send a full snapshot next time it sends an update.
        /// The client can call this as often as it wants, but the server will only send a full snapshot on the
        /// next send interval.
        /// </summary>
        /// <param name="sender"></param>
        public abstract void SendRequestFullSnapshotToServer();

        public abstract void SendServerSnapshotToClient(NetworkConnection client, State snapshot);
        public abstract void SendServerDiffToClient(NetworkConnection client, Diff diff);

        #endregion

        #region Lifecycle Functions

        private void Start() {
            // We are a shared client and server
            if (isClient && isServer) {
                this.stateSystem.mode = NetworkedStateSystemMode.Authority;
                this.stateSystem.SetMode(NetworkedStateSystemMode.Authority);
            }
            // We are an authoritative client
            else if (isClient && isOwned && !serverAuth) {
                this.stateSystem.mode = NetworkedStateSystemMode.Authority;
                this.stateSystem.SetMode(NetworkedStateSystemMode.Authority);
            }
            // We are a non-authoritative client
            else if (isClient && isOwned && serverAuth) {
                this.stateSystem.mode = NetworkedStateSystemMode.Input;
                this.stateSystem.SetMode(NetworkedStateSystemMode.Input);
            }
            // We are an observing client
            else if (isClient && !isOwned) {
                this.stateSystem.mode = NetworkedStateSystemMode.Observer;
                this.stateSystem.SetMode(NetworkedStateSystemMode.Observer);
            }
            // We are an authoritative server
            else if (isServer && serverAuth) {
                this.stateSystem.mode = NetworkedStateSystemMode.Authority;
                this.stateSystem.SetMode(NetworkedStateSystemMode.Authority);
            }
            // We are a non-authoritative server
            else if (isServer && !serverAuth) {
                this.stateSystem.mode = NetworkedStateSystemMode.Observer;
                this.stateSystem.SetMode(NetworkedStateSystemMode.Observer);
            }
            else {
                Debug.LogWarning(
                    $"Unable to determine networked state system mode for {this.name}. Did we miss a case? " +
                    isServer +
                    " " +
                    isClient + " " + isOwned + " " + serverAuth);
            }
        }

        private void Awake() {
            AirshipSimulationManager.Instance.ActivateSimulationManager();
            AirshipSimulationManager.Instance.OnTick += this.OnTick;
            AirshipSimulationManager.Instance.OnSetSnapshot += this.OnSetSnapshot;
            AirshipSimulationManager.Instance.OnCaptureSnapshot += this.OnCaptureSnapshot;
            AirshipSimulationManager.Instance.OnLagCompensationCheck += this.OnLagCompensationCheck;

            // Cleanup acked snapshot data for clients that are leaving the game.
            NetworkServer.OnDisconnectedEvent += client => { this.serverAckedSnapshots.Remove(client.connectionId); };

            // We will keep up to 1 second of commands in the buffer. After that, we will start dropping new commands.
            // The client should also stop sending commands after 1 second's worth of unconfirmed commands.
            // This value is refreshed in auth server tick
            this.serverCommandBufferMaxSize = (int)(1f/ Time.fixedUnscaledDeltaTime);
            // must convert send interval to scaled time because fixedDeltaTime is scaled
            // This value is refreshed in auth server tick
            this.serverCommandBufferTargetSize = Math.Min(this.serverCommandBufferMaxSize,
                (int)Math.Ceiling(NetworkClient.bufferTime / Time.fixedUnscaledDeltaTime));

            this.inputHistory = new(1);
            this.stateHistory = new(1);
            this.observerHistory = new(1);
            // 3 is an arbitrary value, technically we only really need to store 1, but keeping more than one around
            // means diff packets arriving out of order would still be able to be applied.
            this.baseHistory = new History<State>(3);

            this.OnClientReceiveSnapshot += ClientReceiveSnapshot;
            this.OnClientReceiveDiff += ClientReceiveDiff;
            this.OnServerReceiveSnapshot += ServerReceiveSnapshot;
            this.OnServerReceiveInput += ServerReceiveInputCommand;
            this.OnServerReceiveFullSnapshotRequest += ServerReceiveFullSnapshotRequest;

            this.stateSystem.manager = this;
        }

        public void OnDestroy() {
            AirshipSimulationManager.Instance.OnTick -= this.OnTick;
            AirshipSimulationManager.Instance.OnSetSnapshot -= this.OnSetSnapshot;
            AirshipSimulationManager.Instance.OnCaptureSnapshot -= this.OnCaptureSnapshot;
            AirshipSimulationManager.Instance.OnLagCompensationCheck -= this.OnLagCompensationCheck;
        }

        private void SendNetworkMessages() {
            if (isClient && isServer) {
                // no networking required in shared mode.
                return;
            }

            // We are operating as a client
            if (isClient &&
                AccurateInterval.Elapsed(NetworkTime.localTime, NetworkClient.sendInterval, ref lastClientSend))
            {
                // We are a non-authoritative client and should send all of our latest commands.
                if (isClient && isOwned && serverAuth) {
                    // We will sometimes resend unconfirmed commands. The server should ignore these if
                    // it has them already.
                    var commands =
                        this.inputHistory.GetAllAfter((uint)Math.Max(0,
                            (clientLastSentLocalTick - (NetworkClient.bufferTime / Time.fixedUnscaledDeltaTime))));
                    if (commands.Length > 0) {
                        this.clientLastSentLocalTick = this.inputHistory.Keys[^1];
                    }
                    else {
                        Debug.LogWarning(
                            $"Sending no commands on interval. Last local tick: {clientLastSentLocalTick}. Local command history size: {this.inputHistory.Keys.Count}");
                    }
                    
                    // print($"Sending {commands.Length} cmds to the server");

                    // We make multiple calls so that Mirror can batch the commands efficiently.
                    foreach (var command in commands) {
                        this.SendClientInputToServer(command);
                    }
                }

                // We are an authoritative client and should send our latest state
                if (isClient && isOwned && !serverAuth) {
                    if (this.stateHistory.Keys.Count == 0) return;
                    var states = this.stateHistory.GetAllAfter((uint)(this.clientLastSentLocalTick -
                                                                      (NetworkClient.bufferTime /
                                                                       Time.fixedUnscaledDeltaTime)));
                    if (states.Length > 0) {
                        this.clientLastSentLocalTick = this.stateHistory.Keys[^1];
                    }

                    // We make multiple calls so that Mirror can batch the snapshots efficiently
                    foreach (var state in states) {
                        this.SendClientSnapshotToServer(state);
                    }
                }
            }

            // We are operating as a server
            if (isServer &&
                AccurateInterval.Elapsed(NetworkTime.localTime, NetworkServer.sendInterval, ref lastServerSend)) {
                // No matter what mode the server is operating in, we send our latest state to clients.
                // If we have no state yet, don't send
                if (this.stateHistory.Keys.Count == 0) return;
                var state = this.stateHistory.Values[^1];
                // If we have no state yet, don't send (this shouldn't be possible)
                if (state == null) return;

                foreach (var client in NetworkServer.connections) {
                    if (!client.Value.isAuthenticated || !client.Value.isReady) continue;
                    if (!serverAuth && client.Key == this.netIdentity.connectionToClient.connectionId) {
                        // This client is the authoritative owner and doesn't need snapshot data,
                        // it is only meant for observers
                        continue;
                    }

                    if (!this.serverAckedSnapshots.TryGetValue(client.Key, out var lastAckedTick)) {
                        // print("sending snapshot at time " + state.time + " because no lastAcked was set.");
                        this.SendServerSnapshotToClient(client.Value, state);
                        // We will expect the client to receive this and send snapshots after this
                        // as an optimization.
                        this.serverAckedSnapshots[client.Key] = state.tick;
                        continue;
                    }

                    var baseState = this.stateHistory.GetExact(lastAckedTick);
                    if (baseState == null) {
                        this.SendServerSnapshotToClient(client.Value, state);
                        // We will expect the client to receive this and send snapshots after this
                        // as an optimization.
                        this.serverAckedSnapshots[client.Key] = state.tick;
                        // print("sending snapshot at time " + state.time + " because no base state with acked time could be found.");
                        continue;
                    }

                    var diff = baseState.CreateDiff(state) as Diff;
                    if (diff == null) {
                        Debug.LogWarning("Could not generate diff for client " + client.Key + ". Report this.");
                        continue;
                    }

                    this.SendServerDiffToClient(client.Value, diff);
                }
            }
        }

        private void LateUpdate()
        {
            // Unowned clients should interpolate the observed character
            if (isClient && !isOwned)
            {
                this.Interpolate();
            }

            SendNetworkMessages();
        }

        #endregion

        #region Top Level Event Functions

        private void OnTick(object tickObj, object timeObj, object replayObj) {
            if (tickObj is not uint tick || timeObj is not double time || replayObj is not bool replay) {
                Debug.LogWarning($"OnTick: Unexpected value in tick object.");
                return;
            }
            
            // We are in shared mode
            if (isServer && isClient)
            {
                this.AuthClientTick(tick, time, replay);
                return;
            }

            // We are the client and we are authoritative. Report our state to the server.
            if (isClient && isOwned && !serverAuth)
            {
                this.AuthClientTick(tick, time, replay);
            }

            // We are the server, and we are the authority. Expect commands to come from owning client.
            // Process those commands and distribute and authoritative answer.
            if (isServer && serverAuth)
            {
                this.AuthServerTick(tick, time, replay);
            }

            // We are the client and the server is authoritative. Send our input commands and handle prediction
            if (isClient && isOwned && serverAuth)
            {
                this.NonAuthClientTick(tick, time, replay);
            }

            // We are the server and we are not authoritative.
            if (isServer && !serverAuth)
            {
                this.NonAuthServerTick(tick, time, replay);
            }

            // We are an observing client
            if (isClient && !isOwned)
            {
                this.ObservingClientTick(tick, time, replay);
            }
        }

        private void OnCaptureSnapshot(uint tick, double time, bool replay)
        {
            // We are in shared mode
            if (isClient && isServer)
            {
                this.AuthClientCaptureSnapshot(tick, time, replay);
                return;
            }

            // We are the client and we are authoritative. Report our state to the server.
            if (isClient && isOwned && !serverAuth)
            {
                this.AuthClientCaptureSnapshot(tick, time, replay);
            }

            // We are the server, and we are the authority. Expect commands to come from owning client.
            // Process those commands and distribute and authoritative answer.
            if (isServer && serverAuth)
            {
                this.AuthServerCaptureSnapshot(tick, time, replay);
            }

            // We are the server, but the client is authoritative. Simply forward to observers.
            if (isServer && !serverAuth)
            {
                this.NonAuthServerCaptureSnapshot(tick, time, replay);
            }

            // We are the client and the server is authoritative. Send our input commands and handle prediction
            if (isClient && isOwned && serverAuth)
            {
                this.NonAuthClientCaptureSnapshot(tick, time, replay);
            }

            // We are an observing client
            if (isClient && !isOwned)
            {
                this.ObservingClientCaptureSnapshot(tick, time, replay);
            }
        }

        private void OnSetSnapshot(object objTick)
        {
            if (objTick is uint tick) {
                // we are in shared mode
                if (isClient && isServer) {
                    this.AuthClientSetSnapshot(tick);
                    return;
                }

                // We are the client and we are authoritative.
                if (isClient && isOwned && !serverAuth) {
                    this.AuthClientSetSnapshot(tick);
                }

                // We are the server, and we are the authority.
                if (isServer && serverAuth) {
                    this.AuthServerSetSnapshot(tick);
                }

                // We are the server, but the client is authoritative.
                if (isServer && !serverAuth) {
                    this.NonAuthServerSetSnapshot(tick);
                }

                // We are the client and the server is authoritative.
                if (isClient && isOwned && serverAuth) {
                    this.NonAuthClientSetSnapshot(tick);
                }

                // We are an observing client
                if (isClient && !isOwned) {
                    this.ObservingClientSetSnapshot(tick);
                }
            }
            else {
                Debug.LogWarning($"OnSetSnapshot: Unexpected value in tick object.");
            }
        }

        private void OnLagCompensationCheck(int clientId, uint currentTick, double currentTime, double latency, double bufferTime)
        {
            // Only the server can perform lag compensation checks.
            if (!isServer) return;
            
            // If we are viewing the world as the client who is predicting this system,
            // we don't want to include any rollback on their player since they predicted
            // all of the commands up to the one that triggered the check. They
            // saw themselves where the server sees them at the current time.
            if (this.netIdentity.connectionToClient != null && clientId == this.netIdentity.connectionToClient.connectionId)
            {
                this.OnSetSnapshot(currentTick);
                return;
            }

            // If we are viewing the world as a client who is an observer of this object,
            // calculate the position that was being rendered at the time by subtracting.
            // out their estimated latency and the interpolation buffer time. This
            // ensures that we are rolling back to the time the user actually saw on their
            // client when they issued the command.
            
            // This buffer covers the command buffer time.
            // TODO: We could get lag comp a little more accurate if we tracked the actual time the command was buffered. It's good enough
            // to use the ideal commands in one interval for now though.
            // var tickGenerationTime = Time.fixedDeltaTime / Time.timeScale; // how long it takes to generate a single tick in real time.
            var commandsInOneInterval = NetworkClient.sendInterval / Time.fixedUnscaledDeltaTime;
            // This basically just calculates out to sendInterval...
            var commandBufferTime = Time.fixedUnscaledDeltaTime * commandsInOneInterval * 2; // how long will it take for us to process a command added to the end of the buffer
            
            var totalBuffer = (latency * 2) + bufferTime + commandBufferTime;
            var lagCompensatedTime = currentTime - totalBuffer;
            var lagCompensatedTick = AirshipSimulationManager.Instance.GetNearestTickForUnscaledTime(lagCompensatedTime);
            // It seems like we can get better results by doing a combination of adding 1 send rate and/or 1 tick time
            // This test was with .34 timescale and 1/40 send rate
            // 12.9068027064583 - (13.1350901077098 - ((0.0275639141664629 * 2) + 0.0500000007450581 + 0.025 + (0.07352941 * .34) + 0.07352941)) = 0.00036983722
            // 16.074496323404 - (16.296857560941 - ((0.0142610969939435 * 2) + 0.0500000007450581 + 0.025 + (0.07352941 * 0.34) + 0.07352941 + 0.025)) = 0.00469036659
            // 19.2368007725462 - (19.4586250708617 - ((0.0123929982200843 * 2) + 0.0500000007450581 + 0.025 + (0.07352941 * 0.34) + 0.07352941 + 0.025)) = 0.00149110826
            // 22.5390862366273 - (22.7674515306122 - ((0.0122401664944862 * 2) + 0.0500000007450581 + 0.025 + (0.07352941 * 0.34) + 0.07352941 + .025)) = -0.00535555085
            // print($"CLIENTTIME - ({currentTime} - (({latency} * 2) + {bufferTime} + {NetworkServer.sendInterval} + ({tickGenerationTime} * {commandsInOneInterval})))");
            // print($"CLIENTTIME - ({currentTime} - (({latency} * 2) + {bufferTime} + {commandBufferTime}))");
            // print(
            //     $"{currentTime} - (({latency} * 2) + {bufferTime} + ({tickGenerationTime} * {commandsInOneInterval} * 2)) = {lagCompensatedTime} ({lagCompensatedTick})");
            this.OnSetSnapshot(lagCompensatedTick);
        }

        #endregion

        #region Authoritative Server Functions

        public void AuthServerTick(uint tick, double time, bool replay)
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
                var command = this.stateSystem.GetCommand(this.serverLastProcessedCommandNumber, tick);
                this.serverLastProcessedCommandNumber++;
                this.stateSystem.Tick(command, tick, time,false);
                return;
            }
            
            // We must recalculate target size if the timescale has changed.
            this.serverCommandBufferMaxSize = (int)( 1 / Time.fixedUnscaledDeltaTime);
            this.serverCommandBufferTargetSize =
                Math.Min(this.serverCommandBufferMaxSize,
                    (int)Math.Ceiling(NetworkClient.bufferTime / Time.fixedUnscaledDeltaTime));
            // Optimal max is when we will start processing extra commands.
            // print($"{this.name} has {serverCommandBuffer.Count} entries in the buffer. Target is {this.serverCommandBufferTargetSize} {NetworkClient.bufferTime} {NetworkClient.bufferTimeMultiplier} {Time.timeScale} {NetworkServer.sendInterval}");

            // If we don't allow command catchup, drop commands to get to the target buffer size.
            if (this.maxServerCommandCatchup == 0)
            {
                var dropCount = 0;
                
                while (this.serverCommandBuffer.Count > serverCommandBufferTargetSize && dropCount < this.serverCommandBufferTargetSize)
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
                                                                         Time.fixedUnscaledDeltaTime)))
                    {
                        Debug.LogWarning("Missing command " + expectedNextCommandNumber +
                                         " in the command buffer for " + this.name + ". Next command was: " + command.commandNumber +
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
                    command.tick = tick; // Correct tick to local timeline for ticking on the server.
                    this.stateSystem.Tick(command, tick, time, false);
                }
                else
                {
                    // Ensure that we always tick the system even if there's no command to process.
                    Debug.LogWarning($"No commands left for {this.name}. Last command processed: " + this.lastProcessedCommand);
                    this.stateSystem.Tick(null, tick, time, false);
                }
            } while (this.serverCommandBuffer.Count >
                     serverCommandBufferTargetSize &&
                     commandsProcessed < 1 + this.maxServerCommandCatchup);
            // ^ we process up to maxServerCommandCatchup commands per tick if our buffer has more than serverCommandBufferTargetSize worth of additional commands.

            if (commandsProcessed > 1)
            {
                print("Processed " + commandsProcessed + " commands for " + this.gameObject.name + $". There are now {this.serverCommandBuffer.Count} commands in the buffer.");
            }
        }

        public void AuthServerCaptureSnapshot(uint tick, double time, bool replay)
        {
            if (replay)
            {
                // Log in AuthServerTick will notify if this case occurs.
                return;
            }

            // get snapshot to send to clients
            var state = this.stateSystem.GetCurrentState(this.serverLastProcessedCommandNumber,
                tick, time);
            this.stateHistory.Set(tick, state);
            // Debug.Log("Processing commands up to " + this.lastProcessedCommandNumber + " resulted in " + state);
        }

        public void AuthServerSetSnapshot(uint tick)
        {
            var state = this.stateHistory.GetExact(tick);
            if (state == null)
            {
                // In the case where there's no state to roll back to, we simply leave the state system where it is. This technically means
                // that freshly spawned players will exist in rollback when they shouldn't but we won't handle that edge case for now.
                Debug.LogWarning($"Set snapshot to {tick} resulted in null state for {this.name}. State history size is {this.stateHistory.Keys.Count}");
                return;
            }
            this.stateSystem.SetCurrentState(state);
        }

        #endregion

        #region Non-Authoritative Client Functions

        public void NonAuthClientTick(uint tick, double time, bool replay)
        {
            // If it's a replay, we will take inputs from our input history for this tick
            if (replay)
            {
                // Get the command that we used at this tick previously
                var command = this.inputHistory.GetExact(tick);
                // Process the command again like before, but pass replay into the network system so that
                // it doesn't replay effects or animations, etc.
                this.stateSystem.Tick(command, tick, time, true);
                return;
            }

            // Update our command number and process the next tick.
            clientCommandNumber++;
            var input = this.stateSystem.GetCommand(this.clientCommandNumber, tick);
            input = this.inputHistory.Add(tick, input);
            this.stateSystem.Tick(input, tick, time, false);
        }

        public void NonAuthClientCaptureSnapshot(uint tick, double time, bool replay)
        {
            // We are replaying a previously captured tick
            if (replay)
            {
                // Check if we had input for that tick. If we did, we will want to use the command number for that
                // input to capture our snapshot
                var input = this.inputHistory.GetExact(tick);
                // If we didn't have any input, just skip capturing the tick since we won't expect
                // to see it in our state history either.
                if (input == null) return;

                if (this.stateHistory.IsAuthoritativeEntry(tick))
                {
                    var oldState = this.stateHistory.GetExact(tick);
                    // Debug.Log(("Replayed command " + input.commandNumber + " authoritatively resulted in " + oldState));
                    if (oldState == null) return;
                    this.stateSystem.SetCurrentState(oldState);
                }
                else
                {
                    // Capture the state and overwrite our history for this tick since it may be different
                    // due to corrected collisions or positions of other objects.
                    var replayState = this.stateSystem.GetCurrentState(input.commandNumber, tick, time);
                    // var oldState = this.stateHistory.GetExact(tick);
                    // Debug.Log(("Replayed command " + input.commandNumber + " resulted in " + replayState + " Old state: " + oldState));
                    this.stateHistory.Overwrite(tick, replayState);
                }
                return;
            }

            // Store the current physics state for prediction
            var state = this.stateSystem.GetCurrentState(clientCommandNumber, tick, time);
            this.stateHistory.Add(tick, state);
            // Debug.Log("Processing command " + state.lastProcessedCommand + " resulted in " + state);
        }

        public void NonAuthClientSetSnapshot(uint tick)
        {
            // Use Get() so that we use the oldest state possible
            // if our history does not include the time
            var state = this.stateHistory.Get(tick);
            this.stateSystem.SetCurrentState(state);
        }

        #endregion

        #region Non-Authoritative Server Functions

        public void NonAuthServerTick(uint tick, double time, bool replay)
        {
            if (replay)
            {
                Debug.LogError(
                    "Non-authoritative server should not replay ticks. Report this.");
                return;
            }
            
            this.serverCommandBufferMaxSize = (int)( 1 / Time.fixedUnscaledDeltaTime);
            this.serverCommandBufferTargetSize =
                Math.Min(this.serverCommandBufferMaxSize,
                    (int)Math.Ceiling(NetworkClient.bufferTime / Time.fixedUnscaledDeltaTime));
            // print($"{this.name} {serverRecievedStateBuffer.Count}/{serverCommandBufferMaxSize} target {serverCommandBufferTargetSize}");

            // Process the buffer of states that we've gotten from the authoritative client
            State latestState = null;
            var statesProcessed = 0;
            do
            {
                statesProcessed++;
                if (statesProcessed > 1)
                {
                    Debug.Log($"Processing additional client auth state for {this.name}. The server needs to catch up.");
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
                     serverCommandBufferTargetSize &&
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
                latestState.tick = tick;
                latestState.time = time;
                // Use this as the current state for the server
                this.stateSystem.SetCurrentState(latestState);
                // Since it's new, update our server interpolation functions
                this.stateSystem.InterpolateReachedState(latestState); 
                // Add the state to our history as we would in a authoritative setup
                this.stateHistory.Set(tick, latestState);
            }
        }

        public void NonAuthServerCaptureSnapshot(uint tick, double time, bool replay)
        {
            // Non server auth will accept the client auth as the official possition. No need to capture
            // the state after the physics tick as the position that was pulled from the buffer in OnTick
            // was already added to the state timeline as the official position.
        }

        public void NonAuthServerSetSnapshot(uint tick)
        {
            var state = this.stateHistory.GetExact(tick);
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

        public void AuthClientTick(uint tick, double time, bool replay)
        {
            // Auth clients may experience a replay when a non-auth networked system experiences a
            // mis-predict. In that case, we will want to roll back our authoritative objects as well
            // to make sure that the new simulation is correct.
            if (replay)
            {
                // In the case of ticks, we simply replay the tick as if it was a predicted object.
                var input = this.inputHistory.GetExact(tick);
                // Ignore if we don't have any input history for this tick. The object will remain as
                // it was on it's last snapshot (we will also set the authoritative position on snapshot capture
                // later)
                if (input == null) return;
                this.stateSystem.Tick(input, tick, time, true);
                return;
            }

            // Update our command number and process the next tick
            clientCommandNumber++;
            // Read input from system
            var command = this.stateSystem.GetCommand(clientCommandNumber, tick);
            this.inputHistory.Add(tick, command);
            // Tick system
            this.stateSystem.Tick(command, tick, time, false);
        }

        public void AuthClientCaptureSnapshot(uint tick, double time, bool replay)
        {
            // We don't want a predicted history resim to change an authoritative history,
            // so in the case of a replay, we don't actually capture the new snapshot
            // when in an authoritative context. We will actually reset the authoritative
            // object to be where it was in our snapshot history.
            // This means that client auth objects will properly affect resims, but resims
            // will not affect client history that has already been reported to the server.
            if (replay)
            {
                var authoritativeState = this.stateHistory.Get(tick);
                this.stateSystem.SetCurrentState(authoritativeState);
                return;
            }

            var state = this.stateSystem.GetCurrentState(clientCommandNumber, tick, time);
            this.stateHistory.Add(tick, state);
        }

        public void AuthClientSetSnapshot(uint tick)
        {
            // Clients rolling back for resims on predicted objects will want to
            // have those resims properly affected by client auth objects.
            var state = this.stateHistory.Get(tick);
            this.stateSystem.SetCurrentState(state);
        }

        #endregion

        #region Observing Client Functions

        public void ObservingClientCaptureSnapshot(uint tick, double time, bool replay)
        {
            // Snapshots are authoritative for observed players. We just use whatever
            // we pulled out of the observer timeline. Nothing to do here.
        }

        public void ObservingClientTick(uint tick, double time, bool replay)
        {
            // No actions on observing tick.
            if (replay) {
                var authoritativeState = this.stateHistory.Get(tick);
                if (authoritativeState == null) return;
                
                this.stateSystem.SetCurrentState(authoritativeState);
                return;
            }
            
            // Get the authoritative state received just before the current observer time. (remember interpolation is buffered by bufferTime)
            // Note: if we get multiple fixed update calls per frame, we will use the same state for all fixedUpdate calls
            // because NetworkTime.time is only advanced on Update(). This might cause resim issues with low framerates...
            var observedState = this.observerHistory.Get(NetworkTime.time);
            if (observedState == null)
            {
                // We don't have state to use for interping or we haven't reached a new state yet. Don't add any
                // data to our state history.
                return;
            }
            
            // Clone the observed state and update it to be on the local physics timeline.
            var state = (State) observedState.Clone();
            state.tick = tick;
            state.time = time;
            
            // Set the current state to be what we observed at this time and store it to our
            // local timeline for resims.
            this.stateHistory.Add(tick, state);
            
            // We don't call SetCurrentState because we control the actual position of the character
            // in the LateUpdate Interpolate() call. The currentSnapshot will be update by InterpolateReachedState()
            // below once we hit a new snapshot.
            
            // Handle observer interpolation
            if (clientLastInterpolatedStateTick >= observedState.tick) return;
            // Update our last state tick so we don't call InterpolateReachedState more than once on the same state.
            this.clientLastInterpolatedStateTick = observedState.tick;
            // Notify the system of a new reached state.
            this.stateSystem.InterpolateReachedState(state);
        }

        public void ObservingClientSetSnapshot(uint tick)
        {
            var authoritativeState = this.stateHistory.Get(tick);
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
            // -- Notes on Interpolation
            // Interpolation functions by selecting two snapshots from the server timeline. We can use network.time to access
            // this timeline since Network.time is unscaled time synchronized with the servers unscaled time minus the
            // the configured Mirror buffer time. Snapshots include unscaled time specifically for the purpose of clients
            // interpolating over the server timeline.
            // --
            if (this.observerHistory.Values.Count == 0) return;

            // Get the time we should render on the client.
            // Get the state history around the time that's currently being rendered.
            if (!this.observerHistory.GetAround(NetworkTime.time, out State prevState, out State nextState))
            {
                // if (clientTime < this.observerHistory.Keys[0]) return; // Our local time hasn't advanced enough to render the positions reported. No need to log debug
                // Debug.LogWarning("Frame " + Time.frameCount + " not enough state history for rendering. " + this.observerHistory.Keys.Count +
                //                  " entries. First " + this.observerHistory.Keys[0] + " Last " +
                //                  this.observerHistory.Keys[^1] + " Target " + clientTime + " Buffer is: " +  NetworkClient.bufferTime + " tick gen time is: " + tickGenerationTime + " Estimated Latency (1 way): " +
                //                  (NetworkTime.rtt / 2) + " Network Time: " + NetworkTime.time + " TScale: " + Time.timeScale);
                return;
            }
            
            var timeDelta = (NetworkTime.time - prevState.time) / (nextState.time - prevState.time);
            // Call interp on the networked state system so it can place things properly for the render.
            this.stateSystem.Interpolate(timeDelta, prevState, nextState);
            // print($"Viewing {this.name} at {NetworkTime.time} <{timeDelta}> lat: {NetworkTime.rtt / 2f} {this.transform.position} {this.GetComponent<Rigidbody>().position} {prevState.tick} {nextState.tick} {observerHistory.Values[^1].tick}");
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

            // Check if it's even worth iterating over the state history
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
                // this.clientPausePrediction = true; // disabled for now
                Debug.LogWarning(
                    "We have a large number of unconfirmed commands to the server. Is there something wrong with the network or is the server lagging?");
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

            // Check if our predicted state matches up with our new authoritative state.
            if (clientPredictedState.Compare(this.stateSystem, state))
            {
                // If it does, we can just return since our predictions have all been correct so far.
                this.stateHistory.SetAuthoritativeEntry(clientPredictedState.tick, true);
                return;
            }

            Debug.LogWarning("Misprediction for " + this.name + " on cmd#" + state.lastProcessedCommand + ". Requesting resimulation.");
            
            // We use the client prediction time so we can act like we got this right in our history. Server gives us
            // a time value in its local timeline so the provided time is not useful to us.
            var newState = state.Clone() as State;
            newState.tick = clientPredictedState.tick;
            newState.time = clientPredictedState.time;
            
            // Overwrite the time we should have predicted this on the client so it
            // appears from the client perspective that we predicted the command correctly.
            this.stateHistory.Overwrite(clientPredictedState.tick, newState);
            this.stateHistory.SetAuthoritativeEntry(clientPredictedState.tick, true);
            
            // Resimulate all commands performed after the incorrect prediction so that
            // our future predictions are (hopefully) correct.
            resimulate(clientPredictedState.tick);
        }

        #endregion

        #region Networking

        private void ProcessNewStateOnClient(State state) {
            if (state == null) return;
            
            // Observers record all snapshots received, even if they are the same tick values. This allows us to
            // interpolate over unscaledTime accurately. The remote timestamp is what the server was rendering
            // at that time, which may be the same tick twice (especially with modified timescales)
            if (!isOwned) {
                this.observerHistory.Set(state.time, state);
            }
             
            // If we get a snapshot out of order, we don't need to do reconcile processing since the later
            // snapshot we already received will result in fewer resimulations.
            if (lastReceivedSnapshotTick >= state.tick)
            {
                // print("Ignoring out of order snapshot");
                return;
            }
            
            lastReceivedSnapshotTick = state.tick;
            
            // The client is a non-authoritative owner and should update
            // their local state with the authoritative state from the server.
            if (isOwned && serverAuth) {
                // We just got a new confirmed state and we have already processed
                // our most recent reconcile.
                if (this.clientLastConfirmedState == null) {
                    // Store the state so we can use it when the callback is fired.
                    this.clientLastConfirmedState = state;
                    // Schedule the resimulation with the simulation manager.
                    AirshipSimulationManager.Instance.ScheduleResimulation((resimulate) => {
                        // Reconcile when this callback is executed. Use the last confirmed state received,
                        // (since more could come in from the network while we are waiting for the callback)
                        this.ReconcileInputHistory(resimulate, this.clientLastConfirmedState);
                        this.clientLastConfirmedState = null;
                    });
                }
                // We received a new state update before we were able to reconcile, just update the stored
                // latest state so we use the latest in our scheduled resimulation.
                else {
                    clientLastConfirmedState = state;
                }

                return;
            }

            
        }

        private void ClientReceiveSnapshot(State state) {
            // Clients store all received snapshots so they can correctly generate new snapshots
            // from diffs received from the server. Observers will render observerHistory using the
            // server's timeline via NetworkTime.
            this.baseHistory.Set(state.tick, state);
            
            ProcessNewStateOnClient(state);
        }

        private void ClientReceiveDiff(StateDiff diff) {
            var baseState = this.baseHistory.GetExact(diff.baseTick);
            if (baseState == null) {
                // TODO: We could reduce network by throttling this so we only call it once
                // per round trip time + a little buffer for the send interval. Right now, we
                // will call this until our request reaches the server and the snapshot gets sent
                // back.
                // This might be ok though because it means in situations with high loss, the server
                // will continue to send full snapshots all the time to this client instead of just diffs.
                // It will make for smoother gameplay at the cost of higher network usage.
                // print("No base state for " + diff.baseTick + ". Requesting new snapshot.");
                SendRequestFullSnapshotToServer();
                return;
            }

            var snapshot = baseState.ApplyDiff(diff);
            if (snapshot == null) {
                SendRequestFullSnapshotToServer();
                return;
            }
            
            // Call the standard receive snapshot logic, since we've build the new snapshot received from the server.
            // This will also add the generated snapshot to the observerHistory so that it can be used as a base for new
            // diffs if required.
            this.ProcessNewStateOnClient(snapshot as State);
        }

        private void ProcessClientInputOnServer(Input command) {
            // Debug.Log("Server received command " + command.commandNumber + " for " + this.gameObject.name);
            // This should only occur if the server is authoritative.
            if (!serverAuth) {
                Debug.LogWarning($"Received input command from {this.name}, but the networking mode is not server authoritative. Command will be ignored.");
                return;
            }

            // We may get null commands when the client pauses command sending because of unconfirmed commands.
            // We basically ignore these as this is the client telling us "nothing". We could consider filtering
            // these on the client before sending.
            if (command == null) {
                // Debug.LogWarning($"Received null input command from {this.name}. Is the client ok?");
                return;
            }

            if (this.serverCommandBuffer.TryGetValue(command.commandNumber, out Input existingInput))
            {
                // Debug.Log("Received duplicate command number from client. Ignoring");
                return;
            }

            // Reject commands if our buffer is full.
            if (this.serverCommandBuffer.Count > this.serverCommandBufferMaxSize)
            {
                Debug.LogWarning("Dropping command " + command.commandNumber +
                                 " for "+ this.name + " due to exceeding command buffer size. First command in buffer is " + this.serverCommandBuffer.Values[0]);
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

        private void ServerReceiveInputCommand(Input command)
        {
            ProcessClientInputOnServer(command);
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

        private void ServerReceiveFullSnapshotRequest(int connectionId) {
            // This essentially removes the server's knowledge that the client has a valid snapshot to diff
            // off of and triggers the sending of a new full snapshot.
            this.serverAckedSnapshots.Remove(connectionId);
        }

        #endregion
    }
}