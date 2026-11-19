// LmpRetooledShim: lets Luna Multiplayer 0.29.2 run on KSP Retooled (1.13.0) until LMP supports it.
// 1. LMP's CompatibilityChecker rejects KSP 1.13, which aborts MainSystem.Awake halfway -> make it pass (for now, unconditionally)
// 2. Retooled removed the private ConfigNode.WriteNode that LMP's ConfigNodeSerializer binds by reflection,
//    leaving its three delegates null -> supply them (WriteRootNode + Flush, PreFormatConfig, RecurseFormat).
// Must load before LMP (GameData/000_LmpRetooledShim).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace LmpRetooledShim
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class LmpRetooledShim : MonoBehaviour
    {
        static MethodInfo writeRootNode;

        void Awake()
        {
            var types = AccessTools.AllTypes().Where(t => t.Assembly.GetName().Name == "LmpClient").ToList();
            var checker = types.FirstOrDefault(t => t.FullName == "LmpClient.Utilities.CompatibilityChecker");
            var serializer = types.FirstOrDefault(t => t.FullName == "LmpClient.Extensions.ConfigNodeSerializer");
            if (checker == null || serializer == null) { Debug.Log("[LmpRetooledShim] LMP not found, nothing to do"); return; }

            new Harmony("lmp.retooled.shim").Patch(checker.GetMethod("IsCompatible", AccessTools.all),
                prefix: new HarmonyMethod(typeof(LmpRetooledShim), nameof(IsCompatiblePrefix)));

            if (typeof(ConfigNode).GetMethod("WriteNode", AccessTools.all) != null) { Debug.Log("[LmpRetooledShim] version check patched, but ConfigNode.WriteNode was present, serializer not patched"); return; }
            RuntimeHelpers.RunClassConstructor(serializer.TypeHandle);
            writeRootNode = typeof(ConfigNode).GetMethod("WriteRootNode", AccessTools.all, null, new[] { typeof(TextWriter) }, null);
            var pre = typeof(ConfigNode).GetMethod("PreFormatConfig", AccessTools.all);
            var rec = typeof(ConfigNode).GetMethod("RecurseFormat", AccessTools.all, null, new[] { typeof(List<string[]>) }, null);
            Set(serializer, "WriteNodeThunk", "WriteNodeDelegate", dt => Delegate.CreateDelegate(dt, typeof(LmpRetooledShim).GetMethod(nameof(WriteNode))));
            Set(serializer, "PreFormatConfigThunk", "PreFormatConfigDelegate", dt => Delegate.CreateDelegate(dt, null, pre));
            Set(serializer, "RecurseFormatThunk", "RecurseFormatDelegate", dt => Delegate.CreateDelegate(dt, null, rec));
            Debug.Log("[LmpRetooledShim] version check and ConfigNode serializer patched");
        }

        static bool IsCompatiblePrefix(ref bool __result) { __result = true; return false; }

        // Stand-in for the removed ConfigNode.WriteNode(StreamWriter). LMP reads the stream before disposing the writer.
        public static void WriteNode(ConfigNode node, StreamWriter writer) { writeRootNode.Invoke(node, new object[] { writer }); writer.Flush(); }

        static void Set(Type serializer, string prop, string delegateName, Func<Type, Delegate> make)
        {
            var field = serializer.GetField($"<{prop}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static);
            var type = serializer.GetNestedType(delegateName, BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null && type != null) field.SetValue(null, make(type));
            else Debug.LogWarning($"[LmpRetooledShim] could not set {prop}");
        }
    }
}
