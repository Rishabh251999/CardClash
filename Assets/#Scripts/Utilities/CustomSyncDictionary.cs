using System.Collections.Generic;

namespace CardClash
{
    public class CustomSyncDictionary<TKey, TValue> : Mirror.SyncIDictionary<TKey, TValue>
    {
        public CustomSyncDictionary(IDictionary<TKey, TValue> objects) : base(objects) 
        { 

        }

        public bool SetLocalValue(TKey key, TValue value)
        {
            if (ContainsKey(key))
            {
                objects[key] = value;
                return true;
            }
            return false;
        }
    }
}
