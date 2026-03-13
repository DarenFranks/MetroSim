// =============================================================================
// TrafficManager.cs  –  Vehicle spawning, routing, and congestion simulation.
//
// Each tick a pool of virtual "trips" is generated between residential and
// commercial/industrial zones.  Trips are routed via A* and the tiles along
// each route accumulate TrafficDensity, which is then used by:
//   • ZoneManager (desirability)
//   • Economy (commercial success bonus)
//   • Pathfinding (congestion cost)
// =============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace MetroSim
{
    public class TrafficManager : MonoBehaviour
    {
        // ── Stats ─────────────────────────────────────────────────────────────
        public float AverageCongestion { get; private set; }
        public int   ActiveVehicles    { get; private set; }

        // Pool of active trips (represented as paths on the road network)
        private List<Trip> _activeTrips = new List<Trip>(Config.MAX_VEHICLES);

        private struct Trip
        {
            public List<Vector2Int> Path;
            public int              Step;    // current position in path
        }

        // ── Simulate ──────────────────────────────────────────────────────────

        public void Simulate(GridMap map)
        {
            // Decay existing congestion
            map.ForEach(t =>
            {
                if (t.TrafficDensity > 0f)
                    t.TrafficDensity = Mathf.Max(0f, t.TrafficDensity - Config.CONGESTION_DECAY);
            });

            // Advance existing trips
            AdvanceTrips(map);

            // Spawn new trips based on population and jobs
            SpawnTrips(map);

            // Compute average congestion for display
            float total = 0f; int count = 0;
            map.ForEach(t =>
            {
                if (!t.IsRoad) return;
                total += t.TrafficDensity;
                count++;
            });
            AverageCongestion = count > 0 ? total / count : 0f;
            ActiveVehicles    = _activeTrips.Count;
        }

        // ── Trip spawning ─────────────────────────────────────────────────────

        private void SpawnTrips(GridMap map)
        {
            // Collect origin tiles (residential) and destination tiles (commercial/industrial)
            var origins      = new List<TileData>();
            var destinations = new List<TileData>();

            map.ForEach(t =>
            {
                if (t.Zone == ZoneType.Residential && t.Density >= DensityLevel.Low && t.HasRoadAccess)
                    origins.Add(t);
                if ((t.Zone == ZoneType.Commercial || t.Zone == ZoneType.Industrial)
                    && t.Density >= DensityLevel.Low && t.HasRoadAccess)
                    destinations.Add(t);
            });

            if (origins.Count == 0 || destinations.Count == 0) return;

            // How many new trips to spawn this tick
            int budget = Mathf.Min(
                Config.MAX_VEHICLES - _activeTrips.Count,
                Mathf.Max(1, origins.Count / 20));

            for (int i = 0; i < budget; i++)
            {
                TileData origin = origins[Random.Range(0, origins.Count)];
                TileData dest   = destinations[Random.Range(0, destinations.Count)];

                var path = PathFinder.FindPath(map, origin.X, origin.Y, dest.X, dest.Y);
                if (path.Count < 2) continue;

                _activeTrips.Add(new Trip { Path = path, Step = 0 });
            }
        }

        // ── Trip advancement ──────────────────────────────────────────────────

        private void AdvanceTrips(GridMap map)
        {
            int steps = 3; // tiles advanced per tick (simulated vehicle speed)

            for (int i = _activeTrips.Count - 1; i >= 0; i--)
            {
                Trip trip = _activeTrips[i];

                // Advance the trip along its path
                for (int s = 0; s < steps && trip.Step < trip.Path.Count; s++)
                {
                    Vector2Int pos  = trip.Path[trip.Step];
                    TileData   tile = map.Get(pos.x, pos.y);
                    if (tile != null && tile.IsRoad)
                    {
                        float capacity = RoadNetwork.Capacity(tile.Road);
                        // Add traffic contribution (capped at 1.0)
                        tile.TrafficDensity = Mathf.Min(1f, tile.TrafficDensity + 1f / capacity);
                    }
                    trip.Step++;
                }
                _activeTrips[i] = trip;

                // Remove finished trips
                if (trip.Step >= trip.Path.Count)
                    _activeTrips.RemoveAt(i);
            }
        }
    }
}
