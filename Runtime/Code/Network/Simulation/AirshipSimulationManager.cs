using System;
using Code.Player.Character.Net;
using Code.Player.Character.NetworkedMovement;
using Unity.VisualScripting;
using UnityEngine;

namespace Code.Network.Simulation
{
    public delegate void Simulate();

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
        public static Action<int, bool> PerformTick;

        /**
         * Informs all watching components that the simulation tick has been performed
         * and that a new snapshot of the resulting Physics.Simulate() should be captured.
         * This snapshot should be the state for the provided tick number in history.
         */
        public static Action<int, bool> CaptureSnapshot;
        

        private bool simulationActive = false;
        private bool isActive = false;
        private int tick = 0;

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
            
            PerformTick?.Invoke(this.tick, false);
            Physics.Simulate(Time.fixedDeltaTime);
            CaptureSnapshot?.Invoke(this.tick, false);
            tick++;
        }

        /**
         *
         */
        public (Simulate, FinishSimulation) RequestSimulation(int startTick)
        {
            if (simulationActive)
            {
                throw new ApplicationException("Re-simulation requested while a re-simulation is already active. Report this.");
            }
            
            OnSetPaused?.Invoke(true);
            OnSetSnapshot?.Invoke(startTick);
            var resimTick = startTick + 1;
            return (
                // Function to continue ticking the simulation.
                () =>
                {
                    if (this.tick < resimTick)
                    {
                        Debug.LogWarning("A re-simulation has surpassed current tick. This shouldn't happen and may cause unusual behavior. Report this.");
                    }
                    PerformTick?.Invoke(resimTick, true);
                    Physics.Simulate(Time.fixedDeltaTime);
                    CaptureSnapshot?.Invoke(resimTick, true);
                },
                // Function to finish the simulation.
                () =>
                {
                    OnSetSnapshot?.Invoke(this.tick);
                    OnSetPaused?.Invoke(false);
                });
        }
    }
}