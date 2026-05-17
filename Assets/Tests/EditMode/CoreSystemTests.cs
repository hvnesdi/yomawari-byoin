using NUnit.Framework;
using UnityEngine;

public class CoreSystemTests
{
    [Test]
    public void FlagManager_SetAndGet()
    {
        var go = new GameObject();
        var fm = go.AddComponent<FlagManager>();

        fm.SetFlag(FlagType.MetNurse, true);
        Assert.IsTrue(fm.GetFlag(FlagType.MetNurse));

        fm.SetFlag(FlagType.MetNurse, false);
        Assert.IsFalse(fm.GetFlag(FlagType.MetNurse));

        Object.DestroyImmediate(go);
    }

    [Test]
    public void FlagManager_ResetAll()
    {
        var go = new GameObject();
        var fm = go.AddComponent<FlagManager>();

        fm.SetFlag(FlagType.FoundExit, true);
        fm.ResetAllFlags();
        Assert.IsFalse(fm.GetFlag(FlagType.FoundExit));

        Object.DestroyImmediate(go);
    }

    [Test]
    public void HallucinationManager_RaiseAndReduce()
    {
        var go = new GameObject();
        var hm = go.AddComponent<HallucinationManager>();
        hm.level = 0f;

        hm.RaiseLevel(3f);
        Assert.AreEqual(3f, hm.Level, 0.001f);

        hm.ReduceLevel(1f);
        Assert.AreEqual(2f, hm.Level, 0.001f);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void HallucinationManager_ClampedAtMax()
    {
        var go = new GameObject();
        var hm = go.AddComponent<HallucinationManager>();
        hm.maxLevel = 10f;

        hm.RaiseLevel(999f);
        Assert.AreEqual(10f, hm.Level, 0.001f);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void PlayerController_SpeedValues()
    {
        var go = new GameObject();
        go.AddComponent<CharacterController>();
        var pc = go.AddComponent<PlayerController>();

        Assert.Greater(pc.runSpeed, pc.walkSpeed);
        Assert.Less(pc.crouchSpeed, pc.walkSpeed);

        Object.DestroyImmediate(go);
    }
}
