using System;
using UnityEngine;

namespace schrader.Server
{
    internal static class ReflectionUtils
    {
        internal static object GetManagerInstance(Type managerType)
        {
            return SpawnManager.GetManagerInstance(managerType);
        }

        internal static Type FindTypeByName(params string[] names)
        {
            return SpawnManager.FindTypeByName(names);
        }

        internal static bool TryGetBladeSpawn(Component playerComp, out Vector3 spawnPos, out Quaternion rot, out Vector3 vel)
        {
            return SpawnManager.TryGetBladeSpawn(playerComp, out spawnPos, out rot, out vel);
        }
    }
}