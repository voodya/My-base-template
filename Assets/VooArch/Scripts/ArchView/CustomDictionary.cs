using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if HAS_VOODYA  
namespace Voodya.Core
{
    [System.Serializable]
    public class CustomDictionaryViewElement<T, V> where V : class
    {
        public CustomDictionaryViewElement(T key, V value)
        {
            Key = key; Value = value;
        }

        public T Key;
        public V Value;
    }

    [System.Serializable]
    public class CustomDictionary<T, V> : IEnumerable<KeyValuePair<T, V>> where V : class
    {
        [SerializeField] private List<CustomDictionaryViewElement<T, V>> _data;

        private Dictionary<T, V> _rawData;
        private List<T> _keys;

        public CustomDictionary(Dictionary<T, V> dict)
        {
            _data = new List<CustomDictionaryViewElement<T, V>>();
            foreach (var item in dict)
            {
                CustomDictionaryViewElement<T, V> el = new CustomDictionaryViewElement<T, V>(item.Key, item.Value);
                _data.Add(el);
            }
            _rawData = dict;
            _keys = CreateKeysFromRaw(_rawData);
        }

        public List<T> Keys
        {
            get
            {
                if (_rawData == null || _rawData.Count == 0)
                    CreateNewRawData();
                return _keys;

            }
        }

        public V this[T key]
        {
            get
            {
                if (_rawData == null || _rawData.Count == 0)
                    CreateNewRawData();
                if (_rawData.ContainsKey(key))
                    return _rawData[key];
                else return null;
            }

            set
            {
                if (_rawData == null || _rawData.Count == 0)
                    CreateNewRawData();
                if (_rawData.ContainsKey(key))
                {
                    _rawData[key] = value;

                    RegenerateViewData();
                }
                else
                {
                    _rawData.Add(key, value);
                    _keys.Add(key);
                    _data.Add(new CustomDictionaryViewElement<T, V>(key, value));
                }


            }
        }

        private void RegenerateViewData()
        {
            _data = new();
            foreach (var el in _rawData)
            {
                _data.Add(new CustomDictionaryViewElement<T, V>(el.Key, el.Value));
            }
        }

        private void CreateNewRawData()
        {
            if (_data == null)
            {
                _data = new List<CustomDictionaryViewElement<T, V>>();
            }
            _rawData = new Dictionary<T, V>();
            _keys = new List<T>();
            foreach (var item in _data)
            {
                //_rawData.Add(item.Key, item.Value);
                _rawData[item.Key] = item.Value;
                _keys.Add(item.Key);
            }
        }
        private List<T> CreateKeysFromRaw(Dictionary<T, V> dict)
        {
            var leys = new List<T>();
            foreach (var item in dict)
            {
                leys.Add(item.Key);
            }
            return leys;
        }

        public bool ContainsKey(T key)
        {
            if (_rawData == null)
                CreateNewRawData();

            return _rawData.ContainsKey(key);
        }

        public void Add(T key, V value)
        {
            if (_rawData == null)
            {
                _rawData = new Dictionary<T, V>();
                _keys = new List<T>();
            }
            _rawData[key] = value;
            _keys.Add(key);
        }

        public bool Remove(T key)
        {
            if (_rawData == null)
                CreateNewRawData();
            if (!_rawData.Remove(key))
                return false;
            _keys.Remove(key);
            RegenerateViewData();
            return true;
        }

        public Dictionary<T, V> GetRawDictionary()
        {
            if (_rawData == null)
                CreateNewRawData();
            return _rawData;
        }

        public IEnumerator<KeyValuePair<T, V>> GetEnumerator()
        {
            if (_rawData == null)
                CreateNewRawData();
            return _rawData.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            if (_rawData == null)
                CreateNewRawData();
            return _rawData.GetEnumerator();
        }
    }


}



#endif