using System;
using Code.Player.Character.Net;
using Force.Crc32;
using UnityEngine;

namespace Code.Network.StateSystem.Structures
{
    /**
     * Base class for state diffs when using a networked state system. State diffs are the representation of
     * the difference between two StateSnapshots. 
     */
    public class StateDiff {

        /// <summary>
        /// The time of the base snapshot
        /// </summary>
        public double baseTime;
        
        /// <summary>
        /// The CRC32 of the resulting snapshot when applied to the correct base.
        /// </summary>
        public uint crc32;
    }
}