using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

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
     * Function that will be run when the simulation manager is ready to perform a resimulation.
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
    public class AirshipSimulationManager : MonoBehaviour
    {
        private static AirshipSimulationManager _instance = null;

        public static AirshipSimulationManager instance
        {
            get
            {
                if (!_instance)
                {
                    Debug.Log("Creating Prediction Singleton");
                    var go = new GameObject("AirshipSimulationManager");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<AirshipSimulationManager>();
                }

                return _instance;
            }
        }

        /**
         * This function notifies all watching components that a re-simulation
         * is about to occur. The boolean parameter will be true if a re-simulation
         * is about to occur, and will be false if the re-simulation has finished.
         *
         * Most components watching this will want to set their rigidbodies to
         * kinematic if they do not wish to take part in the re-simulation. Physics
         * will be ticked during re-simulation.
         */
        public static Action<bool> OnSetPaused;

        /**
         * This action notifies all watching components that they need to set their
         * state to be based on the snapshot captured just before or on the provided
         * time. Components should expect a PerformTick() call sometime after this
         * function completes.
         */
        public static Action<double> OnSetSnapshot;

        /**
         * This action notifies listeners that we are performing a lag compensation check.
         * This action is only ever invoked on the server. Components listening to this
         * action should set their state to be what the client would have seen at the provided
         * tick time. Keep in mind, this means that any components the client would have been
         * observing should be rolled back an additional amount to account for the client
         * interpolation. You can convert a time to an exact tick time using
         * GetLastSimulationTime() to find the correct tick time for any given time in the
         * last 1 second.
         *
         * After a lag compensation check is completed, OnSetSnapshot will be called to correct
         * the physics world to it's current state.
         *
         * clientId - The connectionId of the client we are simulating the view of
         * currentTime - The tick time that triggered this compensation check
         * latency - The estimated time it takes for a client message to reach the server. (client.rtt / 2)
         */
        public static Action<int, double, double> OnLagCompensationCheck;

        /**
         * This action tells all watching components that they need to perform a tick.
         * A Physics.Simulate() call will be made after PerformTick completes.
         */
        public static Action<double, bool> OnPerformTick;

        /**
         * Informs all watching components that the simulation tick has been performed
         * and that a new snapshot of the resulting Physics.Simulate() should be captured.
         * This snapshot should be the state for the provided tick number in history.
         */
        public static Action<double, bool> OnCaptureSnapshot;

        private bool resimulationSimulationActive = false;
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
            if (!isActive) return;
            if (Physics.simulationMode != SimulationMode.Script) return;
            // TODO: consider skipping fixed updates where the clock has not advanced.
            
            // Before running any commands, we perform any resimulation requests that were made during
            // the last tick. This ensures that resimulations don't affect command processing and
            // that all commands run on the most up to date predictions.
            while (this.resimulationRequests.TryDequeue(out ResimulationRequest request))
            {
                try
                {
                    request.callback(this.PerformResimulation);
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
            }

            // Perform the standard tick behavior
            OnPerformTick?.Invoke(NetworkTime.time, false);
            Debug.Log("Simulate call. Main Tick: " + NetworkTime.time);
            Physics.Simulate(Time.fixedDeltaTime);
            OnCaptureSnapshot?.Invoke(NetworkTime.time, false);

            // Process any lag compensation requests now that we have completed the ticking and snapshot creation
            // Note: This process is placed after snapshot processing so that changes made to physics (like an impulse)
            // are processed on the _next_ tick. This is safe because the server never resimulates.
            var processedLagCompensation = false;
            foreach (var request in this.lagCompensationRequests)
            {
                processedLagCompensation = true;
                try
                {
                    OnLagCompensationCheck?.Invoke(request.client.connectionId, this.tickTimes[^1],
                        request.client.rtt / 2);
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
                // Reset back to the server view of the world at the current time.
                OnSetSnapshot?.Invoke(NetworkTime.time);
                // Invoke all of the callbacks for modifying physics that should be applied in the next tick.
                while (this.lagCompensationRequests.Count > 0)
                {
                    this.lagCompensationRequests[0].complete();
                    this.lagCompensationRequests.RemoveAt(0);
                }
            }
            
            // Add our completed tick time into our history
            this.tickTimes.Add(NetworkTime.time);
            // Keep the tick history around only for 1 second. This limits our lag compensation amount.
            while (this.tickTimes.Count > 0 && NetworkTime.time - this.tickTimes[0] > 1)
            {
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
        public void ScheduleLagCompensation(NetworkConnectionToClient client, CheckWorld checkCallback, RollbackComplete completeCallback)
        {
            this.lagCompensationRequests.Add(new LagCompensationRequest()
            {
                check = checkCallback,
                complete = completeCallback,
                client = client
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
         * Requests a simulation based on the provided time. Requesting a simulation will roll back the physics
         * world to the snapshot just before or at the base time provided. Calling the returned tick function
         * will advance the simulation and re-simulate the calls to OnPerformTick, Physics.Simulate(), and OnCaptureSnapshot
         *
         * This function is used internally to implement the scheduled resimulations.
         */
        private void PerformResimulation(double baseTime)
        {
            if (resimulationSimulationActive)
            {
                throw new ApplicationException(
                    "Re-simulation requested while a re-simulation is already active. Report this.");
            }

            // If the base time further in the past that our history goes, we reset to the oldest history we have (0) instead.
            int tickIndex = this.CalculateIndexBeforeTime(baseTime);

            this.resimulationSimulationActive = true;
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
                    Debug.Log("Simulate call. Replay Tick: " + this.tickTimes[tickIndex]);
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
                this.resimulationSimulationActive = false;
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
                throw new ApplicationException("Time calculation request used a base time of " + baseTime +
                                               ", but the last tick time was " + this.tickTimes[^1] +
                                               ". Current time is: " + NetworkTime.time + ". Report this.");
            }

            // If the base time further in the past that our history goes, we reset to the oldest history we have (0) instead.
            return afterIndex == 0 ? 0 : afterIndex - 1;
        }
    }
}