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
    }
}
