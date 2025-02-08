using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

namespace Code.Network.Simulation
{
    /**
     * Advances the re-simulation by one tick. This call will return true if there
     * are more ticks to simulate, and false if not.
     *
     * This call will trigger OnPerformTick(), Physics.Simulate(), and OnCaptureSnapshot()
     * in that order.
     */
    public delegate bool Simulate();

    public delegate void FinishSimulation();

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

        private bool simulationActive = false;
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

            if (simulationActive)
            {
                Debug.LogWarning(
                    "Re-simulation was active during a new FixedUpdate. Canceling re-simulation. Report this.");
                this.EndSimulation(true);
            }

            this.tickTimes.Add(NetworkTime.time);
            while (this.tickTimes.Count > 0 && NetworkTime.time - this.tickTimes[0] > 1)
            {
                this.tickTimes.RemoveAt(0);
            }

            // this.tickTimes.Add(this.tick, NetworkTime.time);
            // while (this.tickTimes.Values.Count > 0 && NetworkTime.time - this.tickTimes.Values[0] > 1)
            // {
            //     // Only keep 1 second of tick times. This limits how far into the future we can predict.
            //     this.tickTimes.RemoveAt(0);
            // }

            // Todo: see when we need to do that SimulateTransforms thing that flushes changes
            OnPerformTick?.Invoke(NetworkTime.time, false);
            Physics.Simulate(Time.fixedDeltaTime);
            OnCaptureSnapshot?.Invoke(NetworkTime.time, false);
        }

        /**
         * Requests a simulation based on the provided time. Requesting a simulation will roll back the physics
         * world to the snapshot just before or at the base time provided. Calling the returned tick function
         * will advance the simulation and re-simulate the calls to OnPerformTick, Physics.Simulate(), and OnCaptureSnapshot
         */
        public (Simulate, FinishSimulation) RequestSimulation(double baseTime)
        {
            if (simulationActive)
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
            int tickIndex = afterIndex == 0 ? 0 : afterIndex - 1;
            var invalidateFunctions = false;
            
            OnSetPaused?.Invoke(true);
            OnSetSnapshot?.Invoke(this.tickTimes[tickIndex]);
            return (
                // Function to continue ticking the simulation.
                () =>
                {
                    if (invalidateFunctions)
                    {
                        Debug.LogWarning("Attempted to tick simulation after the request was invalidated. Report this.");
                        return false;
                    }
                    
                    if (!simulationActive)
                    {
                        Debug.LogWarning("Attempted to tick simulation after the simulation has ended. Report this.");
                        invalidateFunctions = true;
                        return false;
                    }
                    
                    if (this.tickTimes.Count <= tickIndex)
                    {
                        Debug.LogWarning(
                            "A re-simulation has surpassed current tick. Report this.");
                        this.EndSimulation(false);
                        invalidateFunctions = true;
                        return false;
                    }

                    OnPerformTick?.Invoke(this.tickTimes[tickIndex], true);
                    Physics.Simulate(Time.fixedDeltaTime);
                    OnCaptureSnapshot?.Invoke(this.tickTimes[tickIndex], true);
                    tickIndex++;
                    return this.tickTimes.Count == tickIndex;
                },
                // Function to finish the simulation.
                () =>
                {
                    if (invalidateFunctions)
                    {
                        Debug.LogWarning("Attempted to end simulation after the request was invalidated. Report this.");
                        return;
                    }
                    this.EndSimulation(tickIndex != this.tickTimes.Count - 1);
                    invalidateFunctions = true;
                });
        }

        /**
         * Ends a simulation if one is active.
         */
        private void EndSimulation(bool resetToPresent)
        {
            if (!simulationActive)
            {
                Debug.LogWarning("Attempted to end simulation when one was not active. Report this.");
                return;
            }
            if (resetToPresent) OnSetSnapshot?.Invoke(this.tickTimes[^1]);
            OnSetPaused?.Invoke(false);
            this.simulationActive = false;
        }
    }
}