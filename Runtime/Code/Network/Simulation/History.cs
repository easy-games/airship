using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Code.Network.Simulation
{
    /**
     * The history class can be used to store a value on a sorted tick timeline. This class works much like a SortedList,
     * but includes additional convenience functions for working with SortedLists in the context of storing a tick history
     * over time and marking entries as authoritative.
     */
    public class History<T>
    {
        private int maxSize;
        private SortedList<uint, T> history = new SortedList<uint, T>();
        private List<uint> authoritativeEntries = new List<uint>();

        public IList<T> Values => history.Values;
        public IList<uint> Keys => history.Keys;
        
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
        public T Add(uint tick, T entry)
        {
            var added = this.history.TryAdd(tick, entry);
            if (!added)
            {
                Debug.LogWarning("Attempted to add an entry to history that already exists. The new entry will be ignored. Time: " + tick);
                return this.GetExact(tick);
            }

            if (maxSize != 0)
            {
                while (this.history.Count > this.maxSize)
                {
                    this.authoritativeEntries.Remove(this.history.Keys[0]);
                    this.history.RemoveAt(0);
                }
            }

            return entry;
        }

        /**
         * Sets the entry at the provided time. Will always set the value of the entry.
         */
        public T Set(uint tick, T entry) {
            this.history.Remove(tick);
            this.Add(tick, entry);
            return entry;
        }

        public void SetAuthoritativeEntry(uint tick, bool authority)
        {
            var hasEntry = this.authoritativeEntries.Contains(tick);
            if (authority && hasEntry) return;
            if (!authority && !hasEntry) return;

            if (authority && !hasEntry)
            {
                this.authoritativeEntries.Add(tick);
                return;
            }

            if (!authority && hasEntry)
            {
                this.authoritativeEntries.Remove(tick);
                return;
            }
        }

        public bool IsAuthoritativeEntry(uint tick)
        {
            return this.authoritativeEntries.Contains(tick);
        }

        /**
         * Overwrites an exact entry. If an existing entry can't be found, it will
         * not create a new entry.
         */
        public void Overwrite(uint tick, T entry)
        {
            var removed = this.history.Remove(tick);
            if (removed) {
                this.history.Add(tick, entry);
            }
        }

        /**
         * Gets the entry nearest entry before or at the provided tick.
         */
        public T Get(uint tick)
        {
            if (history.Count == 0) return default;
            
            // older than oldest
            if (tick <= history.Keys[0]) {
                return history.Values[0];
            }
            
            // newer than the newest
            if (tick >= history.Keys[^1])
            {
                return history.Values[^1];
            }
            
            // Find it in the list. This could be converted to a modified binary
            // search where we look for the nearest value that is still lower than or
            // equal to our search value.
            T prev = history.Values[0];
            for (int i = 0; i < history.Count; ++i)
            {
                var key = history.Keys[i];

                // exact match?
                if (tick == key)
                {
                    return history.Values[i];
                }

                // did we check beyond tick? then return the previous.
                if (key > tick)
                {
                    return prev;
                }

                // remember the last
                prev = history.Values[i];
            }

            return prev;
        }

        public bool Has(uint tick) {
           return this.history.ContainsKey(tick);
        }
        
        /**
         * Given a fractional tick (ie. 295.4), gets the ticks that are next to that
         * tick (ie. 295 and 296).
         */
        public bool GetAround(double fractionalTick, out T before, out T after)
        {
            before = default;
            after  = default;
            
            if (history.Count < 2) {
                return false;
            }

            // older than oldest
            if (fractionalTick < history.Keys[0]) {
                return false;
            }
            
            int index = 0; // manually count when iterating. easier than for-int loop.
            KeyValuePair<uint, T> prev = new KeyValuePair<uint, T>();
            
            for (int i = 0; i < history.Count; ++i)
            {
                var key = history.Keys[i];
                T value = history.Values[i];

                // exact match?
                if (fractionalTick == key)
                {
                    before = value;
                    after = value;
                    return true;
                }

                // did we check beyond tick? then return the previous two.
                if (key > fractionalTick)
                {
                    before = prev.Value;
                    after = value;
                    return true;
                }

                // remember the last
                prev = new KeyValuePair<uint, T>(key, value);
                index += 1;
            }

            return false;
        }

        public T GetExact(uint tick)
        {
            this.history.TryGetValue(tick, out T value);
            return value;
        }

        /**
         * Gets all entries after the provided tick. Does _not_ get the
         * entry at that tick.
         */
        public T[] GetAllAfter(uint tick)
        {
            if (this.history.Keys.Count == 0 || tick > this.history.Keys[^1])
            {
                return new T[] {};
            }

            var after = new List<T>();
            // Iterate in reverse since our return values will likely include the end
            for (var i = this.history.Count - 1; i >= 0; i--)
            {
                if (this.history.Keys[i] < tick)
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
            if (index < 0 || index >= this.history.Count) return;
            var tick = this.history.Keys[index];
            this.authoritativeEntries.Remove(tick);
            this.history.RemoveAt(index);
        }

        /**
         * Removes the entry directly at the given tick.
         */
        public bool Remove(uint tick)
        {
            this.authoritativeEntries.Remove(tick);
            return this.history.Remove(tick);
        }

        /**
         * Clears all history entries.
         */
        public void Clear()
        {
            this.authoritativeEntries.Clear();
            this.history.Clear();
        }

        /**
         * Clears all history entries before the provided tick. Does _not_ clear
         * the entry at that tick.
         */
        public void ClearAllBefore(uint tick)
        {
            while (this.history.Count > 0 && this.history.Keys[0] < tick)
            {
                this.authoritativeEntries.Remove(this.history.Keys[0]);
                this.history.RemoveAt(0);
            }
        }
    }
}