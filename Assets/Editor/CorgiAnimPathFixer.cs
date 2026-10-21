using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Callbacks;
using UnityEngine;

/// <summary>
/// 一部のアニメーションFBX(195個)はスケルトンの外側に余分なラッパーノードを持ち、
/// インポート後のカーブパスが "Corgi@Skin/Root/..." になっているため、モデル階層 "Root/..."
/// にバインドされず再生されない。このスクリプトは該当クリップを Ani_Fixed/ に .anim として複製し、
/// パス先頭の "Corgi@Skin/" を除去した上で、Animator Controller 内の参照を差し替える。
/// </summary>
public static class CorgiAnimPathFixer
{
    const string AniDir = "Assets/Dog_WelshCorgi/Ani";
    const string FixedDir = "Assets/Dog_WelshCorgi/Ani_Fixed";
    const string ControllerDir = "Assets/Dog_WelshCorgi/Animator";
    const string MarkerFile = "corgi_anim_fix_pending";
    const string ReportFile = "CorgiAnimFix_Report.txt";
    const string WrapperNode = "Corgi@Skin";
    const string WrapperPrefix = WrapperNode + "/";

    static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

    [DidReloadScripts]
    static void AutoRunIfPending()
    {
        string marker = Path.Combine(ProjectRoot, MarkerFile);
        if (File.Exists(marker))
        {
            File.Delete(marker);
            EditorApplication.delayCall += Run;
        }
        string dumpMarker = Path.Combine(ProjectRoot, "corgi_anim_dump_pending");
        if (File.Exists(dumpMarker))
        {
            File.Delete(dumpMarker);
            EditorApplication.delayCall += Dump;
        }
        string renameMarker = Path.Combine(ProjectRoot, "corgi_anim_rename_pending");
        if (File.Exists(renameMarker))
        {
            File.Delete(renameMarker);
            EditorApplication.delayCall += RenameFixedClips;
        }
    }

    [MenuItem("Tools/Corgi/Rename Fixed Clips (append _Fixed)")]
    public static void RenameFixedClips()
    {
        var sb = new StringBuilder();
        int renamed = 0, skipped = 0;
        foreach (var animPath in Directory.GetFiles(FixedDir, "*.anim", SearchOption.TopDirectoryOnly)
                     .Select(p => p.Replace('\\', '/')).OrderBy(p => p))
        {
            string name = Path.GetFileNameWithoutExtension(animPath);
            if (name.EndsWith("_Fixed")) { skipped++; continue; }
            string error = AssetDatabase.RenameAsset(animPath, name + "_Fixed");
            if (string.IsNullOrEmpty(error)) { renamed++; sb.AppendLine($"{name} -> {name}_Fixed"); }
            else sb.AppendLine($"FAILED {name}: {error}");
        }
        AssetDatabase.SaveAssets();
        sb.AppendLine();
        sb.AppendLine($"renamed: {renamed}, already suffixed: {skipped}");
        File.WriteAllText(Path.Combine(ProjectRoot, "CorgiAnimRename_Report.txt"), sb.ToString());
        Debug.Log($"[CorgiAnimPathFixer] renamed {renamed} clips. report: CorgiAnimRename_Report.txt");
    }

    [MenuItem("Tools/Corgi/Dump Anim Bindings (diagnostic)")]
    public static void Dump()
    {
        var sb = new StringBuilder();
        string[] targets =
        {
            AniDir + "/001_Ball_Catch+Jump.fbx",
            AniDir + "/002_Ball_Jump.fbx",
            AniDir + "/003_Ball_Jump+Catch.fbx",
        };
        foreach (var fbxPath in targets)
        {
            sb.AppendLine("=== " + fbxPath + " ===");
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                var clip = asset as AnimationClip;
                if (clip == null || clip.name.StartsWith("__preview")) continue;
                var fb = AnimationUtility.GetCurveBindings(clip);
                sb.AppendLine($"clip '{clip.name}' floatBindings:{fb.Length}");
                foreach (var path in fb.Select(b => b.path).Distinct().OrderBy(p => p))
                    sb.AppendLine("  path: '" + path + "'");
            }
            sb.AppendLine();
        }

        sb.AppendLine("=== model prefab hierarchy (Assets/Dog_WelshCorgi/Base/WelshCorgi.fbx) ===");
        var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Dog_WelshCorgi/Base/WelshCorgi.fbx");
        if (model != null) DumpTransform(model.transform, "", sb, 3);

        sb.AppendLine();
        sb.AppendLine("=== imported model roots of animation FBX ===");
        foreach (var fbxPath in targets)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            sb.AppendLine("--- " + fbxPath);
            if (go != null) DumpTransform(go.transform, "", sb, 3);
        }

        File.WriteAllText(Path.Combine(ProjectRoot, "CorgiAnimDump.txt"), sb.ToString());
        Debug.Log("[CorgiAnimPathFixer] dump written to CorgiAnimDump.txt");
    }

    static void DumpTransform(Transform t, string indent, StringBuilder sb, int maxDepth)
    {
        sb.AppendLine(indent + t.name);
        if (maxDepth <= 0) return;
        foreach (Transform c in t)
            DumpTransform(c, indent + "  ", sb, maxDepth - 1);
    }

    [MenuItem("Tools/Corgi/Fix Broken Anim Paths")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[CorgiAnimPathFixer] Play中は実行できません。停止してから Tools/Corgi/Fix Broken Anim Paths を実行してください。");
            return;
        }

        var report = new StringBuilder();
        report.AppendLine("CorgiAnimPathFixer report - " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        report.AppendLine();

        if (!AssetDatabase.IsValidFolder(FixedDir))
            AssetDatabase.CreateFolder("Assets/Dog_WelshCorgi", "Ani_Fixed");

        var clipMap = new Dictionary<AnimationClip, AnimationClip>();
        int fbxScanned = 0, fbxBroken = 0, droppedCurveTotal = 0;

        var fbxPaths = Directory.GetFiles(AniDir, "*.fbx", SearchOption.TopDirectoryOnly)
            .Select(p => p.Replace('\\', '/')).OrderBy(p => p).ToList();

        try
        {
            foreach (var fbxPath in fbxPaths)
            {
                fbxScanned++;
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                {
                    var clip = asset as AnimationClip;
                    if (clip == null || clip.name.StartsWith("__preview")) continue;

                    var floatBindings = AnimationUtility.GetCurveBindings(clip);
                    var objBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
                    bool broken = floatBindings.Concat(objBindings)
                        .Any(b => b.path == WrapperNode || b.path.StartsWith(WrapperPrefix));
                    if (!broken) continue;

                    fbxBroken++;
                    var fixedClip = new AnimationClip
                    {
                        frameRate = clip.frameRate,
                        wrapMode = clip.wrapMode,
                    };

                    int rewritten = 0, copied = 0, dropped = 0;
                    var droppedProps = new List<string>();

                    foreach (var b in floatBindings)
                    {
                        var nb = b;
                        if (b.path == WrapperNode) { dropped++; droppedProps.Add(b.propertyName); continue; }
                        if (b.path.StartsWith(WrapperPrefix)) { nb.path = b.path.Substring(WrapperPrefix.Length); rewritten++; }
                        else copied++;
                        AnimationUtility.SetEditorCurve(fixedClip, nb, AnimationUtility.GetEditorCurve(clip, b));
                    }
                    foreach (var b in objBindings)
                    {
                        var nb = b;
                        if (b.path == WrapperNode) { dropped++; droppedProps.Add(b.propertyName); continue; }
                        if (b.path.StartsWith(WrapperPrefix)) { nb.path = b.path.Substring(WrapperPrefix.Length); rewritten++; }
                        else copied++;
                        AnimationUtility.SetObjectReferenceCurve(fixedClip, nb, AnimationUtility.GetObjectReferenceCurve(clip, b));
                    }

                    AnimationUtility.SetAnimationClipSettings(fixedClip, AnimationUtility.GetAnimationClipSettings(clip));
                    AnimationUtility.SetAnimationEvents(fixedClip, AnimationUtility.GetAnimationEvents(clip));

                    string assetPath = FixedDir + "/" + clip.name + ".anim";
                    AssetDatabase.DeleteAsset(assetPath);
                    fixedClip.name = clip.name;
                    AssetDatabase.CreateAsset(fixedClip, assetPath);
                    clipMap[clip] = fixedClip;
                    droppedCurveTotal += dropped;

                    report.AppendLine($"{fbxPath} -> {assetPath}  (rewritten:{rewritten} kept:{copied} dropped-on-Corgi:{dropped}{(dropped > 0 ? " [" + string.Join(", ", droppedProps) + "]" : "")})");
                }
                if (fbxScanned % 20 == 0)
                    EditorUtility.DisplayProgressBar("CorgiAnimPathFixer", $"clips {fbxScanned}/{fbxPaths.Count}", (float)fbxScanned / fbxPaths.Count);
            }

            AssetDatabase.SaveAssets();

            report.AppendLine();
            report.AppendLine($"FBX scanned: {fbxScanned}, broken clips fixed: {fbxBroken}, dropped curves on '{WrapperNode}' node total: {droppedCurveTotal}");
            report.AppendLine();

            foreach (var ctrlPath in Directory.GetFiles(ControllerDir, "*.controller", SearchOption.TopDirectoryOnly)
                         .Select(p => p.Replace('\\', '/')).OrderBy(p => p))
            {
                var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
                if (ctrl == null) { report.AppendLine($"{ctrlPath}: FAILED TO LOAD"); continue; }
                int replaced = 0;
                foreach (var layer in ctrl.layers)
                    replaced += ReplaceInStateMachine(layer.stateMachine, clipMap);
                if (replaced > 0) EditorUtility.SetDirty(ctrl);
                report.AppendLine($"{ctrlPath}: replaced {replaced} motion references");
            }

            AssetDatabase.SaveAssets();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        File.WriteAllText(Path.Combine(ProjectRoot, ReportFile), report.ToString());
        Debug.Log($"[CorgiAnimPathFixer] done. fixed {fbxBroken} clips. report: {ReportFile}");
    }

    static int ReplaceInStateMachine(AnimatorStateMachine sm, Dictionary<AnimationClip, AnimationClip> map)
    {
        int n = 0;
        foreach (var cs in sm.states)
        {
            var st = cs.state;
            if (st.motion is AnimationClip c && map.TryGetValue(c, out var repl))
            {
                st.motion = repl;
                EditorUtility.SetDirty(st);
                n++;
            }
            else if (st.motion is BlendTree bt)
            {
                n += ReplaceInBlendTree(bt, map);
            }
        }
        foreach (var csm in sm.stateMachines)
            n += ReplaceInStateMachine(csm.stateMachine, map);
        return n;
    }

    static int ReplaceInBlendTree(BlendTree bt, Dictionary<AnimationClip, AnimationClip> map)
    {
        int n = 0;
        var children = bt.children;
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].motion is AnimationClip c && map.TryGetValue(c, out var repl))
            {
                children[i].motion = repl;
                n++;
            }
            else if (children[i].motion is BlendTree nested)
            {
                n += ReplaceInBlendTree(nested, map);
            }
        }
        if (n > 0)
        {
            bt.children = children;
            EditorUtility.SetDirty(bt);
        }
        return n;
    }
}
