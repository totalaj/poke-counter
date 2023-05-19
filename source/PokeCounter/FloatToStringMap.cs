using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokeCounter
{
    using StringMap = List<KeyValuePair<float, string>>;
    class FloatToStringMap
    {
        StringMap map;
        public FloatToStringMap(StringMap map)
        {
            map.Sort((KeyValuePair<float, string> a, KeyValuePair<float, string> b) => { return a.Key > b.Key ? -1 : 1; });
            this.map = map;
        }

        public string GetString(float input)
        {
            foreach (var keyvalue in map)
            {
                if (input >= keyvalue.Key)
                {
                    return keyvalue.Value;
                }
            }
            return "";
        }
    }
}
