﻿using System;
using BepInEx.Configuration;
using HG;
using MonoMod.RuntimeDetour;
using RoR2;
using RoR2.UI;
using RoR2BepInExPack.Reflection;
using UnityEngine;

namespace RoR2BepInExPack.VanillaFixes;

/// <summary>
/// Fix that attempts to revert the changes made by Gearbox, which made most of the game's logic dependent on frame rate.
/// </summary>
internal class FixFrameRateDependantLogic
{
    private static ConfigEntry<bool> _isFixEnabled;

    private static Hook _fixedUpdateHook;
    private static Hook _updateHook;

    private static Hook _fixedUpdateHookPCMC;
    private static Hook _updateHookPCMC;

    private static Hook _healthBarUpdateHook;

    internal static void Init(ConfigFile config)
    {
        try
        {
            _isFixEnabled = config.Bind(
            "General", "Fix Frame Rate Dependant Logic",
            false,
            "Determines whether or not to activate the fix that attempts to revert the changes made by Gearbox, which made most of the game's logic dependent on frame rate.");

            if (!_isFixEnabled.Value)
            {
                return;
            }

            _fixedUpdateHook = new Hook(
                typeof(HG.MonoBehaviourManager).GetMethod("FixedUpdate", ReflectionHelper.AllFlags),
                FixedUpdateHook);

            _healthBarUpdateHook = new Hook(
                typeof(HealthBar).GetMethod(nameof(HealthBar.Update), ReflectionHelper.AllFlags),
                HealthBarUpdateHook);

            _updateHook = new Hook(
                typeof(HG.MonoBehaviourManager).GetMethod(nameof(HG.MonoBehaviourManager.Update), ReflectionHelper.AllFlags),
                UpdateHook);

            _fixedUpdateHookPCMC = new Hook(
                typeof(PlayerCharacterMasterController).GetMethod("FixedUpdate", ReflectionHelper.AllFlags),
                FixedUpdatePCMCHook);

            _updateHookPCMC = new Hook(
                typeof(PlayerCharacterMasterController).GetMethod(nameof(PlayerCharacterMasterController.Update), ReflectionHelper.AllFlags),
                UpdatePCMCHook);
        }
        catch (Exception e)
        {
            Log.Warning("Failed applying Frame Rate Dependant Logic Fix\n" + e);
        }
    }

    private static void HealthBarUpdateHook(HealthBar self)
    {
        self.UpdateHealthbar(Time.deltaTime);
    }

    private static void UpdateDefault(MonoBehaviourManager self)
    {
        for (int i = 0; i < self.registeredBehaviours.Count; i++)
        {
            IManagedMonoBehaviour managedMonoBehaviour = self.registeredBehaviours[i];
            if (managedMonoBehaviour.IsEnabled())
            {
                try
                {
                    managedMonoBehaviour.ManagedUpdate();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }
    }

    private static void FixedUpdateDefault(MonoBehaviourManager self)
    {
        for (int i = 0; i < self.registeredBehaviours.Count; i++)
        {
            IManagedMonoBehaviour managedMonoBehaviour = self.registeredBehaviours[i];
            if (managedMonoBehaviour.IsEnabled() && Time.deltaTime > 0f)
            {
                try
                {
                    managedMonoBehaviour.ManagedFixedUpdate(Time.deltaTime);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }
    }

    private static void UpdateGrouped(MonoBehaviourManager self)
    {
        for (int i = 0; i < self.groupCount; i++)
        {
            self.timeSinceLastUpdate[i] += Time.deltaTime;
        }

        for (int j = 0; j < self.registeredBehaviours.Count; j++)
        {
            IManagedMonoBehaviour managedMonoBehaviour = self.registeredBehaviours[j];
            bool alwaysUpdate = managedMonoBehaviour.AlwaysUpdate;
            bool flag = j % self.groupCount != self.groupIndex && !alwaysUpdate;

            if (!managedMonoBehaviour.IsEnabled() || flag)
            {
                continue;
            }

            try
            {
                if (Time.deltaTime > 0f)
                {
                    managedMonoBehaviour.ManagedUpdate();
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        self.timeSinceLastUpdate[self.groupIndex] = 0f;
        self.groupIndex++;
        self.groupIndex = self.groupIndex == self.groupCount ? 0 : self.groupIndex;
    }

    private static void FixedUpdateGrouped(MonoBehaviourManager self)
    {
        for (int j = 0; j < self.registeredBehaviours.Count; j++)
        {
            IManagedMonoBehaviour managedMonoBehaviour = self.registeredBehaviours[j];
            bool alwaysUpdate = managedMonoBehaviour.AlwaysUpdate;
            bool flag = j % self.groupCount != self.groupIndex && !alwaysUpdate;

            if (!managedMonoBehaviour.IsEnabled() || flag)
            {
                continue;
            }

            try
            {
                if (Time.deltaTime > 0f)
                {
                    managedMonoBehaviour.ManagedFixedUpdate(Time.deltaTime);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }


    private static void UpdateHook(Action<MonoBehaviourManager> orig, MonoBehaviourManager self)
    {
        if (self.groupCount == 0)
        {
            UpdateDefault(self);
            return;
        }
        UpdateGrouped(self);
    }

    private static void FixedUpdateHook(MonoBehaviourManager self)
    {
        if (self.groupCount == 0)
        {
            FixedUpdateDefault(self);
            return;
        }
        FixedUpdateGrouped(self);
    }

    private static void FixedUpdatePCMCHook(Action<PlayerCharacterMasterController> orig, PlayerCharacterMasterController self)
    {
        if (!self.hasEffectiveAuthority || !self.bodyInputs)
        {
            return;
        }
        bool newState = false;
        bool newState2 = false;
        bool newState3 = false;
        bool newState4 = false;
        bool newState5 = false;
        bool newState6 = false;
        bool newState7 = false;
        bool newState8 = false;
        bool newState9 = false;
        bool flag = false;
        if (PlayerCharacterMasterController.CanSendBodyInput(self.networkUser, out var localUser, out var inputPlayer, out var cameraRigController, out var onlyAllowMovement))
        {
            bool flag2 = false;
            flag2 = self.body.isSprinting;
            if (self.sprintInputPressReceived)
            {
                self.sprintInputPressReceived = false;
                flag2 = !flag2;
            }
            if (flag2)
            {
                Vector3 aimDirection = self.bodyInputs.aimDirection;
                aimDirection.y = 0f;
                aimDirection.Normalize();
                Vector3 moveVector = self.bodyInputs.moveVector;
                moveVector.y = 0f;
                moveVector.Normalize();
                if ((self.body.bodyFlags & CharacterBody.BodyFlags.SprintAnyDirection) == 0 && Vector3.Dot(aimDirection, moveVector) < PlayerCharacterMasterController.sprintMinAimMoveDot)
                {
                    flag2 = false;
                }
            }
            if (!onlyAllowMovement)
            {
                newState = inputPlayer.GetButton(7);
                newState2 = inputPlayer.GetButton(8);
                newState3 = inputPlayer.GetButton(9);
                newState4 = inputPlayer.GetButton(10);
                newState5 = inputPlayer.GetButton(5);
                newState6 = inputPlayer.GetButton(4);
                newState7 = flag2;
                newState8 = inputPlayer.GetButton(6);
                newState9 = inputPlayer.GetButton(28);
            }
        }
        else
        {
            flag = true;
        }
        self.bodyInputs.skill1.PushState(newState);
        self.bodyInputs.skill2.PushState(newState2);
        self.bodyInputs.skill3.PushState(newState3);
        self.bodyInputs.skill4.PushState(newState4);
        self.bodyInputs.interact.PushState(newState5);
        self.bodyInputs.jump.PushState(newState6);
        self.bodyInputs.jump.hasPressBeenClaimed = self.wasClaimed;
        self.wasClaimed = flag;
        self.bodyInputs.sprint.PushState(newState7);
        self.bodyInputs.activateEquipment.PushState(newState8);
        self.bodyInputs.ping.PushState(newState9);
        self.CheckPinging();

    }

    private static void UpdatePCMCHook(Action<PlayerCharacterMasterController> orig, PlayerCharacterMasterController self)
    {
        if (!self.hasEffectiveAuthority)
        {
            return;
        }
        GameObject bodyObject = self.master.GetBodyObject();
        if (self.previousBodyObject == null)
        {
            self.previousBodyObject = bodyObject;
            self.SetBody(bodyObject);
        }
        else if (self.previousBodyObject != bodyObject)
        {
            self.SetBody(bodyObject);
            self.previousBodyObject = bodyObject;
        }
        if (!self.bodyInputs)
        {
            return;
        }
        Vector3 moveVector = Vector3.zero;
        Vector3 aimDirection = self.bodyInputs.aimDirection;
        if (PlayerCharacterMasterController.CanSendBodyInput(self.networkUser, out var localUser, out var inputPlayer, out var cameraRigController, out var onlyAllowMovement))
        {
            Transform transform = cameraRigController.transform;
            self.sprintInputPressReceived |= inputPlayer.GetButtonDown(18);
            Vector2 vector = new Vector2(inputPlayer.GetAxis(0), inputPlayer.GetAxis(1));
            Vector2 vector2 = new Vector2(inputPlayer.GetAxis(12), inputPlayer.GetAxis(13));
            self.bodyInputs.SetRawMoveStates(vector + vector2);
            float sqrMagnitude = vector.sqrMagnitude;
            if (sqrMagnitude > 1f)
            {
                vector /= Mathf.Sqrt(sqrMagnitude);
            }
            if (self.bodyIsFlier)
            {
                moveVector = transform.right * vector.x + transform.forward * vector.y;
            }
            else
            {
                float y = transform.eulerAngles.y;
                moveVector = Quaternion.Euler(0f, y, 0f) * new Vector3(vector.x, 0f, vector.y);
            }
            aimDirection = (cameraRigController.crosshairWorldPosition - self.bodyInputs.aimOrigin).normalized;
        }
        self.bodyInputs.moveVector = moveVector;
        self.bodyInputs.aimDirection = aimDirection;
    }
}
