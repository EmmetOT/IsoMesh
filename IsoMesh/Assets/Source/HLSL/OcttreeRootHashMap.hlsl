#ifndef OCTTREE_ROOT_HASHMAP
#define OCTTREE_ROOT_HASHMAP

uint _Capacity; // 128 * 1024 * 1024;

#define EMPTY (0xFFFFFFFF)
#define NUM_KEYVALUES (_Capacity / 2)
#define MAX_CHECKS (256)

// a simple gpu hash map, see: https://nosferalatu.com/SimpleGPUHashTable.html
// note: don't really need the values here? could probably just store keys

struct KeyValue
{
    uint Key;
    int Value;
};

RWStructuredBuffer<KeyValue> _OcttreeRootHashMap;

uint Hash(uint k)
{
    k ^= k >> 16;
    k *= 0x85ebca6b;
    k ^= k >> 13;
    k *= 0xc2b2ae35;
    k ^= k >> 16;
    return k & (_Capacity - 1);
}

uint HashInt3(int3 v)
{
    v = clamp(v + 512, 0, 1023);
    uint hash = 17;
    hash = hash * 31 + Hash(v.x);
    hash = hash * 31 + Hash(v.y);
    hash = hash * 31 + Hash(v.z);
    return hash;
}


uint HashFloat3(float3 v)
{
    v = clamp(v + 512.0, 0, 1023.0) * 100.0;
    uint hash = 17;
    hash = hash * 31 + Hash((uint)v.x);
    hash = hash * 31 + Hash((uint)v.y);
    hash = hash * 31 + Hash((uint)v.z);
    return hash;
}


void Insert(uint key, uint value)
{
    uint slot = Hash(key);
    uint checks = 0;
    
    while (checks < MAX_CHECKS)
    {
        uint prev = EMPTY;
        InterlockedCompareExchange(_OcttreeRootHashMap[slot].Key, EMPTY, key, prev);

        if (prev == EMPTY)
        {
            _OcttreeRootHashMap[slot].Value = value;
        }
        
        slot = (slot + 1) & (_Capacity - 1);
        
        ++checks;
    }
}

uint Lookup(uint key)
{
    int slot = Hash(key);
    uint checks = 0;

    while (checks < MAX_CHECKS)
    {
        if (_OcttreeRootHashMap[slot].Key == key)
            return _OcttreeRootHashMap[slot].Value;

        if (_OcttreeRootHashMap[slot].Key == EMPTY)
            return EMPTY;

        slot = (slot + 1) & (_Capacity - 1);
        
        ++checks;
    }
    
    return EMPTY;
}

void Delete(uint key)
{
    uint slot = Hash(key);
    uint checks = 0;

    while (checks < MAX_CHECKS)
    {
        if (_OcttreeRootHashMap[slot].Key == key)
        {
            _OcttreeRootHashMap[slot].Value = EMPTY;
            return;
        }

        if (_OcttreeRootHashMap[slot].Key == EMPTY)
            return;

        slot = (slot + 1) & (_Capacity - 1);
        
        ++checks;
    }
}

#endif // OCTTREE_ROOT_HASHMAP