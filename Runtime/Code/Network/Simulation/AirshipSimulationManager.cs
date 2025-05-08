using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using Tayx.Graphy.Resim;
using UnityEngine;
using UnityEngine.Serialization;

namespace Code.Network.Simulation
{
    /**
     * Callback used to check the world state at the time requested. You should consider the physics world
     * to be read only while this function executes. Changes to the physics state will be overwritten after
     * your function returns. Use the RollbackComplete callback to modify the physics world based on the
     * results of your check.
     */
    public delegate void CheckWorld();

    /**
     * Callback used to modify physics in the next server tick. The physics world is set to the most recent
     * server tick, and can be modified freely. These modifications will be reconciled to the clients as part
     * of the next server tick.
     *
     * Use this callback to do things like add impulses to hit characters, move them, or anything else that
     * changes physics results.
     */
    public delegate void RollbackComplete();

    /**
     * Requests a simulation based on the provided time. Requesting a simulation will roll back the physics
     * world to the snapshot just before or at the base time provided. Calling the returned tick function
     * will advance the simulation and re-simulate the calls to OnPerformTick, Physics.Simulate(), and OnCaptureSnapshot
     *
     * When this call completes, the world will be at the last completed tick.
     */
    public delegate void PerformResimulate(double baseTime);

    /**
     * Function that will be run when the simulation manager is ready to perform a resimulation. Remember that
     * the simulation base time provided to the resimulate function is the **local time** you wish to resimulate from.
     * The local time should be one provided by the SimulationManager during a tick.
     * Do not pass NetworkTime.time or a similar server derived time to this resimulate function.
     *
     * This function should not be used on the server.
     */
    public delegate void PerformResimulationCallback(PerformResimulate simulateFunction);

    struct LagCompensationRequest
    {
        public CheckWorld check;
        public RollbackComplete complete;
        public NetworkConnectionToClient client;
    }

    struct ResimulationRequest
    {
        public PerformResimulationCallback callback;
    }

    /**
     * The simulation manager is responsible for calling Physics.Simulate and providing generic hooks for other systems to use.
     * Server authoritative networking uses the simulation manager to perform resimulations of its client predictions.
     */
    [LuauAPI]
    public class AirshipSimulationManager : Singleton<AirshipSimulationManager>
    {
        /**
         * This function notifies all watching components that a re-simulation
         * is about to occur. The boolean parameter will be true if a re-simulation
         * is about to occur, and will be false if the re-simulation has finished.
         *
         * Most components watching this will want to set their rigidbodies to
         * kinematic if they do not wish to take part in the re-simulation. Physics
         * will be ticked during re-simulation.
         */
        public event Action<bool> OnSetPaused;

        /**
         * This action notifies all watching components that they need to set their
         * state to be based on the snapshot captured just before or on the provided
         * time. Components should expect a PerformTick() call sometime after this
         * function completes.
         */
        public event Action<object> OnSetSnapshot;

        /**
         * This action notifies listeners that we are performing a lag compensation check.
         * This action is only ever invoked on the server. Components listening to this
         * action should set their state to be what the client would have seen at the provided
         * tick time. Keep in mind, this means that any components the client would have been
         * observing (ie. other player characters) should be rolled back an additional amount to account for the client
         * interpolation. You can convert a time to an exact tick time using
         * GetLastSimulationTime() to find the correct tick time for any given time in the
         * last 1 second.
         *
         * After a lag compensation check is completed, OnSetSnapshot will be called to correct
         * the physics world to it's current state.
         *
         * clientId - The connectionId of the client we are simulating the view of
         * currentTime - The tick time that triggered this compensation check
         * rtt - The estimated time it takes for a message to reach the client and then be returned to the server (aka. ping) (rtt / 2 = latency)
         */
        public event Action<int, double, double> OnLagCompensationCheck;

        /**
         * This action tells all watching components that they need to perform a tick.
         * A Physics.Simulate() call will be made after PerformTick completes.
         */
        public event Action<double, bool> OnPerformTick;

        public event Action<object, object> OnTick;

        /**
         * Informs all watching components that the simulation tick has been performed
         * and that a new snapshot of the resulting Physics.Simulate() should be captured.
         * This snapshot should be the state for the provided tick number in history.
         */
        public event Action<double, bool> OnCaptureSnapshot;

        /**
         * Fired when a tick leaves local history and will never be referenced again. You can use this
         * event to clean up any data that is no longer required.
         */
        public event Action<object> OnHistoryLifetimeReached;

        [NonSerialized] public bool replaying = false;
        
        private bool isActive = false;
        private List<double> tickTimes = new List<double>();
        private List<LagCompensationRequest> lagCompensationRequests = new();
        private Queue<ResimulationRequest> resimulationRequests = new();

        public void ActivateSimulationManager()
        {
            if (isActive) return;
            Physics.simulationMode = SimulationMode.Script;
            this.isActive = true;
        }
        
        public void FixedUpdate()
        {
            // Clients use their own timelines for physics. Do not compare times generated on a client with a time generated
            // on the server. The Server should estimate when a client created a command using it's own timeline and ping calculations
            // and a client should convert server authoritative state received to its own timeline by interpolating with NetworkTime.time
            // and capturing snapshots of the interpolated state on its own timeline.
            var time = NetworkServer.active ? NetworkTime.time : Time.unscaledTimeAsDouble;
            
            if (!isActive) return;
            if (Physics.simulationMode != SimulationMode.Script) return;
            
            // Before running any commands, we perform any resimulation requests that were made during
            // the last tick. This ensures that resimulations don't affect command processing and
            // that all commands run on the most up to date predictions.
            var resimBackTo = time;
            while (this.resimulationRequests.TryDequeue(out ResimulationRequest request))
            {
                try
                {
                    request.callback((requestedTime) =>
                    {
                        if (resimBackTo > requestedTime) resimBackTo = requestedTime;
                    });
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
            }

            // Only resimulate once. Go back to the farthest back time that was requested.
            if (resimBackTo != time) this.PerformResimulation(resimBackTo);

            // Perform the standard tick behavior
            OnPerformTick?.Invoke(time, false);
            OnTick?.Invoke(time, false);
            // Debug.Log("Simulate call. Main Tick: " + NetworkTime.time);
            Physics.Simulate(Time.fixedDeltaTime);
            OnCaptureSnapshot?.Invoke(time, false);

            // Process any lag compensation requests now that we have completed the ticking and snapshot creation
            // Note: This process is placed after snapshot processing so that changes made to physics (like an impulse)
            // are processed on the _next_ tick. This is safe because the server never resimulates.
            var processedLagCompensation = false;
            foreach (var request in this.lagCompensationRequests)
            {
                processedLagCompensation = true;
                try
                {
                   // Debug.LogWarning("Server lag compensation rolling back for client " + request.client.connectionId);
                    OnLagCompensationCheck?.Invoke(request.client.connectionId, time,
                        request.client.rtt);
                    request.check();
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
            }

            // If we processed lag compensation, we have some additional work to do
            if (processedLagCompensation)
            {
                // Debug.LogWarning("Server completed " + this.lagCompensationRequests.Count + " lag compensation requests. Resetting to current tick (" + time + ") and finalizing.");
                // Reset back to the server view of the world at the current time.
                OnSetSnapshot?.Invoke(time);
                // Invoke all of the callbacks for modifying physics that should be applied in the next tick.
                while (this.lagCompensationRequests.Count > 0)
                {
                    this.lagCompensationRequests[0].complete();
                    this.lagCompensationRequests.RemoveAt(0);
                }
            }

            // Add our completed tick time into our history
            this.tickTimes.Add(time);
            // Keep the tick history around only for 1 second. This limits our lag compensation amount.
            while (this.tickTimes.Count > 0 && time - this.tickTimes[0] > 1)
            {
                OnHistoryLifetimeReached?.Invoke(this.tickTimes[0]);
                this.tickTimes.RemoveAt(0);
            }
        }
        
        /**
         * Submits callbacks to be run later that will be able to view the physics world as the client
         * would have seen it at the current tick. This allows you to confirm if a clients input would
         * have hit a target from their point of view.
         *
         * This uses the clients estimated round trip time to determine what tick the client was likely
         * seeing and rolls back Physics to that tick. Once physics is rolled back, the callback function
         * is executed.
         */
        public void ScheduleLagCompensation(NetworkConnectionToClient client, CheckWorld checkCallback,
            RollbackComplete completeCallback)
        {
            this.lagCompensationRequests.Add(new LagCompensationRequest()
            {
                check = checkCallback,
                complete = completeCallback,
                client = client,
            });
        }

        /**
         * Schedules a resimulation to occur on the next tick. This allows correcting predicted history on a non authoritative client.
         * The callback provided will be called when the resimulation should occur. The callback will be passed a resimulate
         * function to trigger a resimulation of all ticks from the provided base time back to the present time.
         */
        public void ScheduleResimulation(PerformResimulationCallback callback)
        {
            this.resimulationRequests.Enqueue(new ResimulationRequest() { callback = callback });
        }

        /**
         * Allows typescript to request a resimulation from the provided time.
         */
        public void RequestResimulation(double time)
        {
            this.ScheduleResimulation((resim => resim(time)));
        }

        /**
         * Requests a simulation based on the provided time. Requesting a simulation will roll back the physics
         * world to the snapshot just before or at the base time provided. Calling the returned tick function
         * will advance the simulation and re-simulate the calls to OnPerformTick, Physics.Simulate(), and OnCaptureSnapshot
         *
         * This function is used internally to implement the scheduled resimulations.
         */
        private void PerformResimulation(double baseTime)
        {
            Debug.Log($"T:{Time.unscaledTimeAsDouble} Resimulating from {baseTime}");
            G_ResimMonitor.FrameResimValue = 100;
            
            if (replaying)
            {
                Debug.LogWarning("Resim already active");
                throw new ApplicationException(
                    "Re-simulation requested while a re-simulation is already active. Report this.");
            }

            // If the base time further in the past that our history goes, we reset to the oldest history we have (0) instead.
            int tickIndex = this.CalculateIndexBeforeTime(baseTime);

            this.replaying = true;
            try
            {
                OnSetPaused?.Invoke(true);
                OnSetSnapshot?.Invoke(this.tickTimes[tickIndex]);
                Physics.SyncTransforms();
                // Advance the tick so that we are re-processing the next tick after the base time provided.
                tickIndex++;

                while (tickIndex < this.tickTimes.Count)
                {
                    OnPerformTick?.Invoke(this.tickTimes[tickIndex], true);
                    // Debug.Log("Simulate call. Replay Tick: " + this.tickTimes[tickIndex]);
                    Physics.Simulate(Time.fixedDeltaTime);
                    OnCaptureSnapshot?.Invoke(this.tickTimes[tickIndex], true);
                    tickIndex++;
                }

                OnSetPaused?.Invoke(false);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
            finally
            {
                this.replaying = false;
            }
        }

        /**
         * Gets the exact time of the last simulation provided a given time less than 1 second ago.
         * Does not get times into the future.
         */
        public double GetLastSimulationTime(double time)
        {
            var index = this.CalculateIndexBeforeTime(time);
            return this.tickTimes[index];
        }

        /**
         * Calculates the index of the tick time just before or exactly at the time provided.
         */
        private int CalculateIndexBeforeTime(double baseTime)
        {
            if (this.tickTimes.Count == 0)
            {
                throw new ApplicationException("Resimulation requested before any ticks have occured. Report this.");
            }

            int afterIndex = this.tickTimes.FindIndex((time) =>
            {
                if (time > baseTime) return true;
                return false;
            });
            if (afterIndex == -1)
            {
                Debug.LogWarning("Time calculation request used a base time of " + baseTime +
                                 ", but the last tick time was " + this.tickTimes[^1] +
                                 ". Current time is: " + NetworkTime.time + ". Is your network ok?");
                return this.tickTimes.Count - 1;
            }

            // If the base time further in the past that our history goes, we reset to the oldest history we have (0) instead.
            return afterIndex == 0 ? 0 : afterIndex - 1;
        }
    }
}