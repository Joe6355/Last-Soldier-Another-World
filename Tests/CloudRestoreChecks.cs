using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using YG;

public static class CloudRestoreChecks
{
    public static void Run()
    {
        var check = typeof(GameProgress).GetMethod("CanRestorePendingBackup", BindingFlags.Static | BindingFlags.NonPublic);
        Func<SavesYG, SavesYG, string, bool> restore = (local, remote, basis) => (bool)check.Invoke(null, new object[] { local, remote, basis });
        var remote = Save(20, 316, "test-owner");
        var local = Save(900, 158, "test-owner");
        Require(!restore(local, remote, ""), "Legacy high revision cannot replace a valid remote save without base evidence");
        Require(!restore(local, remote, JsonUtility.ToJson(Save(19, 158, "test-owner"))), "Changed remote state wins even when its counter is lower");
        Require(!restore(local, remote, JsonUtility.ToJson(Save(20, 100, "test-owner"))), "Equal revision with different data is a conflict, not the same cloud base");
        Require(restore(local, remote, JsonUtility.ToJson(remote)), "Pending local progress survives when the server still matches its confirmed base");
        Require(!restore(Save(20, 200, "test-owner"), remote, JsonUtility.ToJson(remote)), "Equal revisions do not replace the remote state");
        Require(!restore(Save(900, 200, "other-owner"), remote, JsonUtility.ToJson(remote)), "Another account cannot replace remote progress");
        local.cloudBaseKnown = false;
        Require(!restore(local, remote, JsonUtility.ToJson(remote)), "Unbased offline data cannot replace established cloud progress");
        Require(!restore(null, remote, JsonUtility.ToJson(remote)), "No backup uses remote progress");
        Debug.Log("CLOUD_RESTORE_PASS: eight cross-device and offline-base cases");
    }
    private static SavesYG Save(int revision, int coins, string owner)
    {
        var data = new SavesYG { progressVersion=1, ownerId=owner, cloudBaseKnown=true, idSave=revision };
        data.intKeys.Add("Coins"); data.intValues.Add(coins);
        return data;
    }
    private static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
        Debug.Log("CLOUD_RESTORE_CHECK: " + message);
    }
}
