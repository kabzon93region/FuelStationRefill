using System;
using System.Collections.Generic;
using UnityEngine;

namespace FuelStationRefill
{
    [Serializable]
    public class FuelZonesData
    {
        public List<FuelZone> Zones = new List<FuelZone>();
    }

    [Serializable]
    public class FuelZone
    {
        public string Name = "Fuel Station";
        public Vector3Serializable Position;
        public float Radius = 5f;
        public string MapName = "";

        // Runtime-only fields (not serialized to JSON)
        [NonSerialized] public float CurrentFuel;
        [NonSerialized] public float MaxFuelGenerated;
        [NonSerialized] public bool IsActive = true;

        /// <summary>
        /// Returns Unity Vector3 from serialized position.
        /// </summary>
        public Vector3 GetPosition()
        {
            return new Vector3(Position.X, Position.Y, Position.Z);
        }

        /// <summary>
        /// Checks if this zone is active in the current map.
        /// Empty MapName means zone is active on all maps.
        /// </summary>
        public bool IsActiveInCurrentMap()
        {
            if (string.IsNullOrEmpty(MapName))
                return true;

            try
            {
                var world = Comfort.Common.Singleton<EFT.GameWorld>.Instance;
                if (world?.MainPlayer != null)
                {
                    string currentMap = world.MainPlayer.Location ?? "";
                    bool match = string.Equals(currentMap, MapName, StringComparison.OrdinalIgnoreCase);
                    return match;
                }
            }
            catch { }

            return true; // If we can't determine map, assume active
        }
    }

    [Serializable]
    public class Vector3Serializable
    {
        public float X;
        public float Y;
        public float Z;

        public Vector3Serializable() { }

        public Vector3Serializable(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public Vector3Serializable(Vector3 v)
        {
            X = v.x;
            Y = v.y;
            Z = v.z;
        }
    }
}
