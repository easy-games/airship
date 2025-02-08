using System.Collections.Generic;
using UnityEngine;

namespace Code.Network.Simulation
{
    public class History<T>
    {
        private int maxSize;
        private SortedList<double, T> history = new SortedList<double, T>();

        public History(int maxSize)
        {
            this.maxSize = maxSize;
        }

        /**
         * Adds an entry at the provided time. Additionally makes room for the entry
         * by removing the oldest entry if the list has reached it's maximum capacity.
         */
        public T Add(double time, T entry)
        {
            var added = this.history.TryAdd(time, entry);
            if (!added)
            {
                Debug.LogWarning("Attempted to add an entry to history that already exists. The new entry will be ignored.");
                return this.GetExact(time);
            }
            while (this.history.Count > this.maxSize)
            {
                this.history.RemoveAt(0);
            }

            return entry;
        }

        /**
         * Gets the entry nearest entry before or at the provided time.
         */
        public T Get(double time)
        {
            // older than oldest
            if (time < history.Keys[0]) {
                return history.Values[0];
            }
            
            // newer than the newest
            if (time > history.Keys[^1])
            {
                return history.Values[^1];
            }
            
            // Find it in the list. This could be converted to a modified binary
            // search where we look for the nearest value that is still lower than or
            // equal to our search value.
            T prev = history.Values[0];
            for (int i = 0; i < history.Count; ++i)
            {
                double key = history.Keys[i];

                // exact match?
                if (time == key)
                {
                    return history.Values[i];
                }

                // did we check beyond timestamp? then return the previous two.
                if (key > time)
                {
                    return prev;
                }

                // remember the last
                prev = history.Values[i];
            }

            return prev;
        }

        public T GetExact(double time)
        {
            this.history.TryGetValue(time, out T value);
            return value;
        }

        /**
         * Removes the entry directly at the given time.
         */
        public bool Remove(double time)
        {
            return this.history.Remove(time);
        }

        /**
         * Clears all history entries.
         */
        public void Clear()
        {
            this.history.Clear();
        }

        /**
         * Clears all history entries before the provided time. Does _not_ clear
         * the entry at that time.
         */
        public void ClearBefore(double time)
        {
            while (this.history.Count > 0 && this.history.Keys[0] < time)
            {
                this.history.RemoveAt(0);
            }
        }
    }
}