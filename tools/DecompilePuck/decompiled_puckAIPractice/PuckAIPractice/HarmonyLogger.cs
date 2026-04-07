using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace PuckAIPractice;

public static class HarmonyLogger
{
	public static void PatchSpecificMethods(Harmony harmony, Type targetType, List<string> methodNames)
	{
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Expected O, but got Unknown
		IEnumerable<MethodInfo> enumerable = from m in targetType.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
			where !m.IsSpecialName && !m.IsConstructor && methodNames.Contains(m.Name)
			select m;
		foreach (MethodInfo item in enumerable)
		{
			try
			{
				MethodInfo method = typeof(HarmonyLogger).GetMethod("LogMethodPostfix", BindingFlags.Static | BindingFlags.NonPublic);
				HarmonyMethod val = new HarmonyMethod(method);
				harmony.Patch((MethodBase)item, (HarmonyMethod)null, val, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			}
			catch (Exception)
			{
			}
		}
	}

	private static void LogMethodPostfix(MethodBase __originalMethod)
	{
	}
}
