using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tacticals.Code.Maps
{
    internal class MapConstants
    {
        public const int BLOCK_SIZE = 15;
        public const int BIOMEHEATMAPSCALE = 2; // 2:1

        /// <summary>
        /// Pathfinding cells per map block edge. The flow field grid is
        /// (mapWidth * PATHFIND_SCALE) x (mapHeight * PATHFIND_SCALE) cells,
        /// each BLOCK_SIZE / PATHFIND_SCALE world units across.
        /// Independent of BIOMEHEATMAPSCALE - that one drives terrain/biome sampling.
        /// </summary>
        public const int PATHFIND_SCALE = 4; // 15 / 4 = 3.75 world units per cell

        /// <summary>
        /// Ground movement-speed multipliers for difficult terrain. 1.0 = unimpeded.
        /// These affect speed ONLY - they never enter flow-field path cost, so units slow down
        /// crossing forest but are not rerouted around it. The player picks the route.
        /// </summary>
        public const float MOVE_FACTOR_MIN = 0.25f;    // hard floor, however much terrain stacks up
        public const float MOVE_FACTOR_FOREST = 0.5f;  // cell containing a tree
        public const float MOVE_FACTOR_RIVER = 0.4f;   // river block
    }
}
