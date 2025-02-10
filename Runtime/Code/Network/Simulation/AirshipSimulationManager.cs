using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

namespace Code.Network.Simulation
{
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

        // This could also be considered OnSetTick (and maybe will in the future)
        /**
         * This action notifies all watching components that they need to set their
         * state to be based on the snapshot captured just before or on the provided
         * time. Components should expect a PerformTick() call sometime after this
         * function completes.
         */
        public static Action<double> OnSetSnapshot;

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
            
            OnPerformTick?.Invoke(NetworkTime.time, false);
            // TODO: it seems that our FixedUpdate is sometimes getting called with the same
            // value for NetworkTime.time. I'm not sure why or what causes that. I would have expected
            // it to be continuously advancing. Perhaps we do need to just create a tick system and use only that
            Debug.Log("Simulate call. Main Tick: " + NetworkTime.time);
            Physics.Simulate(Time.fixedDeltaTime);
            OnCaptureSnapshot?.Invoke(NetworkTime.time, false);

            this.tickTimes.Add(NetworkTime.time);
            while (this.tickTimes.Count > 0 && NetworkTime.time - this.tickTimes[0] > 1)
            {
                this.tickTimes.RemoveAt(0);
            }
        }

        /**
         * Requests a simulation based on the provided time. Requesting a simulation will roll back the physics
         * world to the snapshot just before or at the base time provided. Calling the returned tick function
         * will advance the simulation and re-simulate the calls to OnPerformTick, Physics.Simulate(), and OnCaptureSnapshot
         */
        public void PerformResimulation(double baseTime)
        {
            if (resimulationSimulationActive)
            {
                throw new ApplicationException(
                    "Re-simulation requested while a re-simulation is already active. Report this.");
            }

            if (this.tickTimes.Count == 0)
            {
                throw new ApplicationException("Re-simulation requested before any ticks have occured. Report this.");
            }
            
            int afterIndex = this.tickTimes.FindIndex((time) =>
            {
                if (time > baseTime) return true;
                return false;
            });
            if (afterIndex == -1)
            {
                throw new ApplicationException("Re-simulation request used a base time of " + baseTime +
                                               ", but the last tick time was " + this.tickTimes[^1] + ". Current time is: "+ NetworkTime.time + ". Report this.");
            }
            // If the base time further in the past that our history goes, we reset to the oldest history we have (0) instead.
            int tickIndex = afterIndex == 0 ? 0 : afterIndex - 1;
            
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
    }
}