﻿using System;
using MonoMod.RuntimeDetour;
using RoR2;
using RoR2BepInExPack.Reflection;

namespace RoR2BepInExPack.VanillaFixes;

internal class FixConsoleLog
{
    private static Hook _hook;

    internal static void Init()
    {
        try
        {
            var hookConfig = new HookConfig() { ManualApply = true };
            _hook = new Hook(
                            typeof(UnitySystemConsoleRedirector).GetMethod(nameof(UnitySystemConsoleRedirector.Redirect), ReflectionHelper.AllFlags),
                            typeof(FixConsoleLog).GetMethod(nameof(FixConsoleLog.DoNothing), ReflectionHelper.AllFlags),
                            hookConfig
                        );
        }
        catch(Exception ex)
        {
            Log.Error($"{nameof(FixConsoleLog)} failed to initialize: {ex}");
        }
    }

    internal static void Enable()
    {
        _hook.Apply();
    }

    internal static void Disable()
    {
        _hook.Undo();
    }

    internal static void Destroy()
    {
        _hook.Free();
    }

    private static void DoNothing() { }
}
