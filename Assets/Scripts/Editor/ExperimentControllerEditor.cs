#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom Inspector for ExperimentController.
/// All trial controls are disabled outside Play Mode to prevent accidental clicks.
/// </summary>
[CustomEditor(typeof(ExperimentController))]
public class ExperimentControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        ExperimentController ctrl = (ExperimentController)target;

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("🔬  Experiment Controller", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        // ── Reference ────────────────────────────────────────────────────
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Reference", EditorStyles.miniBoldLabel);
        ctrl.highlightManager = (KitchenHighlightManager)EditorGUILayout.ObjectField(
            "Highlight Manager", ctrl.highlightManager,
            typeof(KitchenHighlightManager), true);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);

        // ── Condition Setup ───────────────────────────────────────────────
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Condition", EditorStyles.miniBoldLabel);

        ctrl.highlightType = (KitchenHighlightManager.HighlightType)
            EditorGUILayout.EnumPopup("Highlight Type", ctrl.highlightType);

        ctrl.targetingMode = (KitchenHighlightManager.TargetingMode)
            EditorGUILayout.EnumPopup("Targeting Mode", ctrl.targetingMode);

        // Subset size slider — only visible in Subset mode
        if (ctrl.targetingMode == KitchenHighlightManager.TargetingMode.Subset)
        {
            EditorGUI.indentLevel++;
            ctrl.subsetSize = EditorGUILayout.IntSlider(
                "Subset Size", ctrl.subsetSize, 1, 10);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);

        // ── Trial Controls ────────────────────────────────────────────────
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Trial Controls", EditorStyles.miniBoldLabel);

        bool inPlayMode   = Application.isPlaying;
        bool trialActive  = inPlayMode && ctrl.highlightManager != null
                            && ctrl.highlightManager.IsTrialActive();
        bool trialDone    = inPlayMode && ctrl.highlightManager != null
                            && ctrl.highlightManager.IsTrialComplete();

        // Start Trial — green, always available in play mode
        EditorGUI.BeginDisabledGroup(!inPlayMode);
        GUI.backgroundColor = new Color(0.3f, 0.9f, 0.4f);
        if (GUILayout.Button("▶   Start Trial  (reset + apply condition)", GUILayout.Height(36)))
            ctrl.StartTrial();
        GUI.backgroundColor = Color.white;
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(2);

        // Apply Condition / Stop — only useful while trial is running
        EditorGUI.BeginDisabledGroup(!trialActive);

        GUI.backgroundColor = new Color(0.3f, 0.6f, 1f);
        if (GUILayout.Button("⚙   Apply Condition Only  (no reset)", GUILayout.Height(28)))
            ctrl.ApplyConditionOnly();

        GUI.backgroundColor = new Color(1f, 0.6f, 0.2f);
        if (GUILayout.Button("⏭   Simulate Collect Next  (testing)", GUILayout.Height(28)))
            ctrl.SimulateCollectNext();

        GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
        if (GUILayout.Button("⏹   Stop Trial", GUILayout.Height(28)))
            ctrl.StopTrial();

        GUI.backgroundColor = Color.white;
        EditorGUI.EndDisabledGroup();

        // Context hint
        if (!inPlayMode)
            EditorGUILayout.HelpBox("Enter Play Mode to use trial controls.", MessageType.Info);
        else if (!trialActive && !trialDone)
            EditorGUILayout.HelpBox("Press ▶ Start Trial to begin.", MessageType.Info);
        else if (trialDone)
            EditorGUILayout.HelpBox("✓ Trial complete. Press ▶ Start Trial to run again.",
                MessageType.None);

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);

        // ── Live Status (Play Mode only) ──────────────────────────────────
        if (inPlayMode && ctrl.highlightManager != null)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Live Status", EditorStyles.miniBoldLabel);

            int total     = ctrl.highlightManager.targets.Count;
            int remaining = ctrl.highlightManager.RemainingCount();
            int collected = total - remaining;

            // Status row
            string statusLabel = !trialActive && collected == 0 ? "Idle"
                               : trialDone                       ? "Complete ✓"
                               : "Running";
            Color statusColour = trialDone  ? Color.green
                               : trialActive ? Color.cyan
                               :               Color.gray;

            GUIStyle statusStyle = new GUIStyle(EditorStyles.boldLabel);
            statusStyle.normal.textColor = statusColour;
            EditorGUILayout.LabelField("Status", statusLabel, statusStyle);

            EditorGUILayout.LabelField("Highlight Type",
                ctrl.highlightManager.highlightType.ToString());
            EditorGUILayout.LabelField("Targeting Mode",
                ctrl.highlightManager.targetingMode.ToString());

            if (ctrl.highlightManager.targetingMode ==
                KitchenHighlightManager.TargetingMode.Subset)
                EditorGUILayout.LabelField("Subset Size",
                    ctrl.highlightManager.subsetSize.ToString());

            EditorGUILayout.LabelField("Collected",
                $"{collected} / {total}");

            // Next target name
            if (trialActive)
            {
                ItemHighlight next = ctrl.highlightManager.GetNextTarget();
                EditorGUILayout.LabelField("Next Target",
                    next != null ? next.gameObject.name : "—");
            }

            // Progress bar
            EditorGUILayout.Space(2);
            Rect barRect  = GUILayoutUtility.GetRect(18, 18, GUILayout.ExpandWidth(true));
            float progress = total > 0 ? (float)collected / total : 0f;
            EditorGUI.ProgressBar(barRect, progress,
                trialDone ? "Trial Complete" : $"{remaining} remaining");

            EditorGUILayout.EndVertical();

            Repaint(); // refresh every frame in Play Mode
        }

        if (GUI.changed)
            EditorUtility.SetDirty(ctrl);
    }
}
#endif