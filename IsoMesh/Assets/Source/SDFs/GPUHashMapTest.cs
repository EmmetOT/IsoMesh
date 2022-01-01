using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GPUHashMapTest
{
    public const uint Capacity = 32768;/*128 * 1024 * 1024;*/
    public const uint NumKeyValues = Capacity / 2;
    public const uint Empty = 0xFFFFFFFF;
    public const uint MaxChecks = 256;

    public KeyValue[] m_hashTable;

    public uint Hash(uint k)
    {
        k ^= k >> 16;
        k *= 0x85ebca6b;
        k ^= k >> 13;
        k *= 0xc2b2ae35;
        k ^= k >> 16;
        return k & (Capacity - 1);
    }

    public GPUHashMapTest()
    {
        m_hashTable = new KeyValue[Capacity];

        for (int i = 0; i < m_hashTable.Length; i++)
            m_hashTable[i].Key = Empty;
    }

    public void Insert(uint key, uint value)
    {
        uint slot = Hash(key);
        uint checks = 0;

        while (checks < MaxChecks)
        {
            CompareAndExchange(ref m_hashTable[slot].Key, Empty, key, out uint prev);

            if (prev == Empty || prev == key)
            {
                m_hashTable[slot].Value = value;
                break;
            }

            slot = (slot + 1) & (Capacity - 1);

            ++checks;
        }
    }

    public uint Lookup(uint key)
    {
        uint slot = Hash(key);
        uint checks = 0;

        while (checks < MaxChecks)
        {
            if (m_hashTable[slot].Key == key)
                return m_hashTable[slot].Value;

            if (m_hashTable[slot].Key == Empty)
                return Empty;

            slot = (slot + 1) & (Capacity - 1);

            ++checks;
        }

        return Empty;
    }

    public void Delete(uint key)
    {
        uint slot = Hash(key);
        uint checks = 0;

        while (checks < MaxChecks)
        {
            if (m_hashTable[slot].Key == key)
            {
                m_hashTable[slot].Value = Empty;
                return;
            }

            if (m_hashTable[slot].Key == Empty)
                return;

            slot = (slot + 1) & (Capacity - 1);

            ++checks;
        }
    }

    public static void CompareAndExchange(ref uint dest, in uint compare_value, in uint value, out uint original_value)
    {
        original_value = value;

        if (dest == compare_value)
            dest = value;
    }
}

public struct KeyValue
{
    public uint Key;
    public uint Value;
}