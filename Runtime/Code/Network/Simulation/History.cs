using System.Collections.Generic;
using UnityEngine;

namespace Code.Network.Simulation
{
    /**
     * The history class can be used to store a value on a sorted timeline. This class works much like a SortedList,
     * but includes additional convenience functions for working with SortedLists in the context of storing a history
     * over time.
     */
    public class History<T>
    {
        private int maxSize;
        private SortedList<double, T> history = new SortedList<double, T>();

        public IList<T> Values => history.Values;
        
        /**
         * Creates a history with the provided maximum entry size. You can set the max size to 0 for no maximum size.
         */
        public History(int maxSize)
        {
            this.maxSize = maxSize;
        }

        /**
         * Adds an entry at the provided time. Additionally makes room for the entry
         * by removing the oldest entry if the list has reached its maximum capacity.
         */
        public T Add(double time, T entry)
        {
            var added = this.history.TryAdd(time, entry);
            if (!added)
            {
                Debug.LogWarning("Attempted to add an entry to history that already exists. The new entry will be ignored. Time: " + time);
                return this.GetExact(time);
            }

            if (maxSize != 0)
            {
                while (this.history.Count > this.maxSize)
                {
                    this.history.RemoveAt(0);
                }
            }

            return entry;
        }

        /**
         * Overwrites an exact entry. If an existing entry can't be found, it will
         * not create a new entry.
         */
        public void Overwrite(double time, T entry)
        {
            var removed = this.history.Remove(time);
            if (removed)
            {
                this.history.Add(time, entry);
            }
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

        // TODO: implement GetAround and use stateHistory on observers for interpolation on LateUpdate()
        public bool GetAround(double time, out T before, out T after)
        {
            before = default;
            after  = default;
            
            if (history.Count < 2) {
                return false;
            }

            // older than oldest
            if (time < history.Keys[0]) {
                return false;
            }
            
            int index = 0; // manually count when iterating. easier than for-int loop.
            KeyValuePair<double, T> prev = new KeyValuePair<double, T>();
            
            for (int i = 0; i < history.Count; ++i)
            {
                double key = history.Keys[i];
                T value = history.Values[i];

                // exact match?
                if (time == key)
                {
                    before = value;
                    after = value;
                    return true;
                }

                // did we check beyond timestamp? then return the previous two.
                if (key > time)
                {
                    before = prev.Value;
                    after = value;
                    return true;
                }

                // remember the last
                prev = new KeyValuePair<double, T>(key, value);
                index += 1;
            }

            return false;
        }

        public T GetExact(double time)
        {
            this.history.TryGetValue(time, out T value);
            return value;
        }

        /**
         * Gets all entries after the provided time. Does _not_ get the
         * entry at that time.
         */
        public T[] GetAllAfter(double time)
        {
            if (time > this.history.Keys[^1])
            {
                return new T[] {};
            }

            var after = new List<T>();
            for (var i = this.history.Count - 1; i >= 0; i--)
            {
                if (this.history.Keys[i] < time)
                {
                    after.Reverse();
                    return after.ToArray();
                }
                after.Add(this.history.Values[i]);
            }

            after.Reverse();
            return after.ToArray();
        }

        public void RemoveAt(int index)
        {
            this.history.RemoveAt(index);
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
        public void ClearAllBefore(double time)
        {
            while (this.history.Count > 0 && this.history.Keys[0] < time)
            {
                this.history.RemoveAt(0);
            }
        }
    }
}