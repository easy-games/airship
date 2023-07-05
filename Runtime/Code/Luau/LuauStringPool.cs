using FishNet.Object.Prediction.Delegating;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;


//Because string allocations create memory on the heap, we don't want to leave those dangling around for GC to run over
//Better to pool them and recycle them
namespace Luau
{
    public unsafe class StringPool
    {
        private class StringEntry
        {
            public int Size;
            public int UsageCount;
            public string CachedString;
            public ulong Hash;
        }

        private Dictionary<ulong, StringEntry> entries = new();
        private int maxMemory;
        private int currentMemory;
        
        public StringPool(int maxMemory)
        {
            this.maxMemory = maxMemory;
            currentMemory = 0;
        }
        
        public string GetString(char* str, int size, out ulong hash)
        {
            hash = MakeHash(str, size);
            if (entries.TryGetValue(hash, out var entry))
            {
                entry.UsageCount++;
                return entry.CachedString;
            }
           
            if (entry != null)
            {
                entry.UsageCount++;
                return entry.CachedString;
            }

            if (currentMemory + size > maxMemory)
            {
                FreeMemory(size);
            }
            
            string cachedString = new string((sbyte*)str, 0, size);
            //It's fun to print these out and see whats going on!
            //Debug.Log($"Allocating string {cachedString} of size {size}");

            entry = new StringEntry { Size = size, UsageCount = 1, CachedString = cachedString, Hash = hash };
            entries.Add(hash, entry);
            currentMemory += size;

            return entry.CachedString;
        }

        public ulong MakeHash(char* str, int size)
        {
            ulong hash = 5381;
            for (int i = 0; i < size; i++)
            {
                hash = ((hash << 5) + hash) + str[i];
            }
            return hash;
        }
        
        private bool CompareStrings(String str1, char* str2, int size)
        {
            if (size != str1.Length)
            {
                return false;
            }
                                        
            for (int i = 0; i < size; i++)
            {
                if (str1[i] != str2[i])
                {
                    return false;
                }
            }
            return true;
        }
 
        private void FreeMemory(int size)
        {
            
            Debug.Log("String pool full, freeing up 25% of it!");
            
            while (currentMemory > maxMemory * 0.75)
            {
                //Find the least used string.
                StringEntry leastUsed = null;
                foreach (var entry in entries.Values)
                {
                    if (leastUsed == null || entry.UsageCount < leastUsed.UsageCount)
                    {
                        leastUsed = entry;
                    }
                }
                if (leastUsed == null)
                {
                    return;
                }
                currentMemory -= leastUsed.Size;
                entries.Remove(leastUsed.Hash);
            }
        }
    }
}
