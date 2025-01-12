using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using AutoActMod.Actions;
using HarmonyLib;
using UnityEngine;

namespace AutoActMod.Patches;

[HarmonyPatch]
static class Misc
{
    [HarmonyPostfix, HarmonyPatch(typeof(CharaRenderer), nameof(CharaRenderer.OnEnterScreen))]
    public static void CharaRenderer_OnEnterScreen_Patch()
    {
        if (AutoActMod.Active && Settings.IgnoreEnemySpotted)
        {
            EClass.player.enemySpotted = false;
        }
    }

    [HarmonyPostfix, HarmonyPatch(typeof(AIAct), nameof(AIAct.SetChild))]
    static void AIAct_SetChild_Patch(AIAct __instance, AIAct seq)
    {
        if (seq is AI_Goto go && __instance is AI_Shear or TaskPoint && __instance is not TaskPlow)
        {
            go.ignoreConnection = true;
        }
    }

    // To fix AutoAct being unable to be interrupted by attacks
    [HarmonyPrefix, HarmonyPatch(typeof(AIAct), nameof(AIAct.Success))]
    static bool AIAct_Success_Patch(AIAct __instance, ref AIAct.Status __result)
    {
        if (__instance.child.HasValue()
            && __instance.child.status == AIAct.Status.Fail
            && (__instance.onChildFail.IsNull() || __instance.onChildFail() == AIAct.Status.Fail))
        {
            if (__instance is AutoAct aa)
            {
                aa.CancelRetry();
            }
            if (__instance.parent is AutoAct aa2)
            {
                aa2.CancelRetry();
            }
            __result = __instance.Cancel();
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(TaskBuild), nameof(TaskBuild.OnProgressComplete))]
    static class TaskBuild_OnProgressComplete_Patch
    {
        internal static bool Success = false;
        static void Prefix() => Success = false;
        static void Postfix(TaskBuild __instance)
        {
            if (Success || __instance.parent is not AutoActBuild a)
            {
                return;
            }

            a.field.Add(__instance.pos);
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return new CodeMatcher(instructions)
                .MatchStartForward(
                    new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(Recipe), nameof(Recipe.Build), [typeof(TaskBuild)])))
                .Advance(1)
                .Insert(
                    new CodeInstruction(Transpilers.EmitDelegate(() => { Success = true; })))
                .InstructionEnumeration();
        }
    }

#if DEBUG
    // [HarmonyPrefix]
    // [HarmonyPatch(typeof(Task), nameof(Task.Destroy))]
    // static bool Task_Destroy_Prefix(Task __instance)
    // {
    //     Utils.Trace();
    //     return true;
    // }

    // [HarmonyPrefix]
    // [HarmonyPatch(typeof(AIAct), "Tick")]
    // static bool AIAct_Tick_Prefix(AIAct __instance, ref AIAct.Status __result)
    // {
    //     // return true;
    //     if (__instance is AutoAct a && a is not AutoActWait)
    //     {
    //         if (__instance.owner.IsPC)
    //         {
    //             return true;
    //         }
    //         AutoActMod.Log(a + " Start Tick" + "Owner: " + a.owner.Name + "Held: " + a.owner.held + "Tool: " + a.owner.Tool);
    //         if (a.child.HasValue())
    //         {
    //             AutoActMod.Log("==Child: " + a.child + " | " + a.child.status);
    //             if (a.child.child.HasValue())
    //             {
    //                 AutoActMod.Log("====Child child: " + a.child.child + " | " + a.child.child.status);
    //             }
    //         }
    //         if (__instance.owner == null || (__instance.isFail != null && __instance.isFail()))
    //         {
    //             __result = __instance.Cancel();
    //             return false;
    //         }
    //         AutoActMod.Log("Tick 1");
    //         if (__instance.IsChildRunning)
    //         {
    //             switch (__instance.child.Tick())
    //             {
    //                 case AIAct.Status.Running:
    //                     __result = AIAct.Status.Running;
    //                     AutoActMod.Log("Child running");
    //                     return false;
    //                 case AIAct.Status.Fail:
    //                     AutoActMod.Log("Child fail");
    //                     if (__instance.onChildFail != null)
    //                     {
    //                         __result = __instance.onChildFail();
    //                         return false;
    //                     }
    //                     __result = AIAct.Status.Fail;
    //                     return false;
    //                 case AIAct.Status.Success:
    //                     AutoActMod.Log("Child success");
    //                     if (__instance.owner == null || (__instance.isFail != null && __instance.isFail()))
    //                     {
    //                         __result = __instance.Cancel();
    //                         return false;
    //                     }
    //                     break;
    //             }
    //         }
    //         AutoActMod.Log("Tick 2");
    //         if (__instance.Enumerator == null)
    //         {
    //             AutoActMod.Log("Start Action");
    //             __instance.Start();
    //             if (__instance.status != AIAct.Status.Running)
    //             {
    //                 __result = __instance.status;
    //                 return false;
    //             }
    //         }
    //         AutoActMod.Log("Tick 3");
    //         if (!__instance.Enumerator.MoveNext())
    //         {
    //             __result = __instance.Success(null);
    //             AutoActMod.Log("Success result" + __result);
    //             return false;
    //         }
    //         AutoActMod.Log("Tick 4");
    //         AutoActMod.Log(__instance + " End Tick | " + __instance.status);
    //         __result = __instance.status;
    //         return false;
    //     }
    //     return true;
    // }
#endif
}