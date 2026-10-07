using System;
using System.Collections.Generic;
using System.Linq;
using Loongdum.SceneFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>接上场景流要求的唯一 <see cref="LevelGoal"/>。判据与 Level03Flow 的汇合判定一致，
/// 所以接上不改变玩法，只是让通关也能被场景流收到。</summary>
public static class Level03GoalSetup
{
    [MenuItem("Tools/Loongdum/Level 03/Wire Scene Goal")]
    public static void Wire()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != Level03Builder.ScenePath)
        {
            if (scene.isDirty) throw new InvalidOperationException("Save the open scene before opening Level03.");
            scene = EditorSceneManager.OpenScene(Level03Builder.ScenePath, OpenSceneMode.Single);
        }

        Level03Flow[] flows = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Level03Flow>(true)).ToArray();
        if (flows.Length != 1)
            throw new InvalidOperationException("Level03 must hold exactly one Level03Flow; found " + flows.Length + ".");
        Level03Flow flow = flows[0];
        if (flow.player == null || flow.source == null)
            throw new InvalidOperationException("Level03Flow has no player or source reference.");

        LevelGoal[] goals = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<LevelGoal>(true)).ToArray();
        if (goals.Length > 1)
            throw new InvalidOperationException("Level03 already holds " + goals.Length + " LevelGoals; leaving the scene alone.");
        LevelGoal goal = goals.Length == 1 ? goals[0] : NewGoal(scene);

        // 汇合时场景流要能挂起移动和反转，和第二关那份 LevelGoal 挂的是同一对组件。
        var controls = new List<Behaviour>();
        WhiteboxPlayerMovement movement = flow.player.GetComponent<WhiteboxPlayerMovement>();
        if (movement != null) controls.Add(movement);
        if (flow.reversal != null) controls.Add(flow.reversal);
        goal.Configure(flow.player.transform, flow.source, controls.ToArray());

        EditorUtility.SetDirty(goal);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Saving the Level03 scene failed.");
        Debug.Log("[Scene Flow] Level 03 goal wired: player=" + flow.player.transform.name
            + ", source=" + flow.source.name + ", controls=" + controls.Count);
    }

    private static LevelGoal NewGoal(Scene scene)
    {
        var host = new GameObject("LevelGoal");
        SceneManager.MoveGameObjectToScene(host, scene);
        return host.AddComponent<LevelGoal>();
    }
}
