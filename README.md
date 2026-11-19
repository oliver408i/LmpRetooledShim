# LmpRetooledShim

Lets **Luna Multiplayer 0.29.2** run on **KSP Retooled** (tested on 1.13.0-rc18) until LMP supports
Retooled itself. Workaround only. See KSP1Retooled/retooled#35.

1. LMP's version check rejects KSP 1.13.0. The shim makes `CompatibilityChecker.IsCompatible()` pass.
   Without it, LMP aborts its startup halfway and throws NullReferenceExceptions.
2. Retooled removed the private `ConfigNode.WriteNode` that LMP's serializer uses. The shim supplies
   it via `ConfigNode.WriteRootNode`. Without it, LMP disconnects on the first message from the server.

## Install

Copy `GameData/000_LmpRetooledShim` into your KSP `GameData`. It needs HarmonyKSP (`000_Harmony`), which
LMP already includes. Remove it once LMP supports Retooled.

## Build

    dotnet build -c Release -p:KSPRoot=/path/to/KSP
