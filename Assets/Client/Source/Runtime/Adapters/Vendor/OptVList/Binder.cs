using System.Collections;
using System.Collections.Generic;

namespace Client.Adapters.Vendor
{
    public class Binder<T1, T2> : IEnumerable<KeyValuePair<T1, T2>>
    {
        private readonly Dictionary<T1, T2> _forward;
        private readonly Dictionary<T2, T1> _reversed;
        
        public int Count => _forward.Count;
        
        public Binder(int capacity = 5)
        {
            _forward = new Dictionary<T1, T2>(capacity);
            _reversed = new Dictionary<T2, T1>(capacity);
        }

        public void Add(T1 key, T2 value)
        {
            _forward.Add(key, value);
            _reversed.Add(value, key);
        }

        public void Add(T2 key, T1 value)
        {
            _reversed.Add(key, value);
            _forward.Add(value, key);
        }

        public bool TryGetValue(T1 key, out T2 result)
        {
            return _forward.TryGetValue(key, out result);
        }

        public bool TryGetValue(T2 key, out T1 result)
        {
            return _reversed.TryGetValue(key, out result);
        }

        public bool ContainsKey(T1 key)
        {
            return _forward.ContainsKey(key);
        }

        public bool ContainsValue(T2 value)
        {
            return _forward.ContainsValue(value);
        }

        public bool ContainsKey(T2 key)
        {
            return _reversed.ContainsKey(key);
        }

        public bool ContainsValue(T1 value)
        {
            return _reversed.ContainsValue(value);
        }
                
        public T2 this[T1 index]
        {
            get => _forward[index];
            set
            {
                if (_forward.ContainsKey(index))
                    _reversed.Remove(_forward[index]);

                _forward[index] = value; 
                _reversed[value] = index;
            } 
        }
        
        public T1 this[T2 index]
        {
            get => _reversed[index];
            set
            {
                if (_reversed.ContainsKey(index))
                    _forward.Remove(_reversed[index]);
                
                _reversed[index] = value;
                _forward[value] = index;
            } 
        }

        public void Remove(T1 key)
        {
            _reversed.Remove(_forward[key]);
            _forward.Remove(key);
        }

        public void Remove(T2 key)
        {
            _forward.Remove(_reversed[key]);
            _reversed.Remove(key);
        }

        public void Clear()
        {
            _forward.Clear();
            _reversed.Clear();
        }

        public IEnumerator<KeyValuePair<T1, T2>> GetEnumerator()
        {
            return _forward.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}