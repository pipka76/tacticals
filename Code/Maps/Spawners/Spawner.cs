using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Godot.OpenXRInterface;

namespace tacticals.Code.Maps.Spawners
{
    internal class Spawner
    {
        private Random _rand = new Random();
        private Dictionary<MapBlockStructureType, int> _limits = new Dictionary<MapBlockStructureType, int>();

        public virtual void RegisterLimit(MapBlockStructureType structureType, int maxInstances)
        {
            _limits.TryAdd(structureType, maxInstances);
        }

        public virtual void SpawnAt(MapBlock[][] map, int i, int j, MapBlockStructureType structureType, float probability = 1.0f)
        {
            if (map[i][j].StructureType != MapBlockStructureType.NONE)
                return;

            if (!CheckProbability(probability))
                return;

            if (_limits.TryGetValue(structureType, out int sLimit))
            {
                if (sLimit == 0)
                    return;
                _limits[structureType] = sLimit - 1;
            }

            map[i][j].StructureType = structureType;
        }

        protected bool CheckProbability(float probability)
        {
            int r = _rand.Next(0, 1000);
            if (r > 0 && r < (probability*1000))
                return true;
            return false;
        }
    }
}
