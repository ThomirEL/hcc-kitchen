#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom Inspector for ExperimentController.
/// Manages multi-trial experiment setup, generation, and execution.
/// Trial controls are disabled outside Play Mode to prevent accidental clicks.
/// </summary>
[CustomEditor(typeof(ExperimentController))]
public class ExperimentControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        ExperimentController ctrl = (ExperimentController)target;

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("🔬  Multi-Trial Experiment Controller", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        // ── Reference ────────────────────────────────────────────────────
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("References", EditorStyles.miniBoldLabel);

        SerializedProperty loggingEnabledProp = serializedObject.FindProperty("loggingEnabled");
        SerializedProperty participantIdProp = serializedObject.FindProperty("ParticipantID");
        SerializedProperty highlightManagerProp = serializedObject.FindProperty("highlightManager");
        SerializedProperty playerStartPosProp = serializedObject.FindProperty("playerStartPosition");

        EditorGUILayout.PropertyField(loggingEnabledProp);
        EditorGUILayout.PropertyField(participantIdProp);
        EditorGUILayout.PropertyField(highlightManagerProp);
        EditorGUILayout.PropertyField(playerStartPosProp);

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);

        // ── Trial Configuration ───────────────────────────────────────────
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Trial Configuration", EditorStyles.miniBoldLabel);
        
        SerializedProperty numTrialsProp = serializedObject.FindProperty("numTrials");
        SerializedProperty trialDelayProp = serializedObject.FindProperty("trialCompletionDelay");
        
        EditorGUILayout.PropertyField(numTrialsProp);
        EditorGUILayout.PropertyField(trialDelayProp);
        
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);

        // ── Experiment Controls ──────────────────────────────────────────
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Experiment Controls", EditorStyles.miniBoldLabel);

        bool inPlayMode = Application.isPlaying;
        bool experimentActive = inPlayMode && ctrl.highlightManager != null;

        // Generate Trials — always available
        EditorGUI.BeginDisabledGroup(!Application.isPlaying);
        GUI.backgroundColor = new Color(1f, 0.76f, 0.03f);
        if (GUILayout.Button("⚙   Generate Trials", GUILayout.Height(28)))
            ctrl.GenerateTrials();
        GUI.backgroundColor = Color.white;
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(2);

        // Start Experiment — green, main action
        EditorGUI.BeginDisabledGroup(!Application.isPlaying);
        GUI.backgroundColor = new Color(0.3f, 0.9f, 0.4f);
        if (GUILayout.Button("▶   Start Experiment", GUILayout.Height(36)))
            ctrl.StartExperiment();
        GUI.backgroundColor = Color.white;
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(2);

        // Skip Trial — while experiment running
        EditorGUI.BeginDisabledGroup(!experimentActive);
        GUI.backgroundColor = new Color(1f, 0.6f, 0.2f);
        if (GUILayout.Button("⏭   Skip to Next Trial", GUILayout.Height(28)))
            ctrl.SkipToNextTrial();
        GUI.backgroundColor = Color.white;
        EditorGUI.EndDisabledGroup();

        // Stop Experiment — red, destructive
        EditorGUI.BeginDisabledGroup(!experimentActive);
        GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
        if (GUILayout.Button("⏹   Stop Experiment", GUILayout.Height(28)))
            ctrl.StopExperiment();
        GUI.backgroundColor = Color.white;
        EditorGUI.EndDisabledGroup();

        // Context hint
        if (!inPlayMode)
            EditorGUILayout.HelpBox("Enter Play Mode to use experiment controls.", MessageType.Info);
        else
            EditorGUILayout.HelpBox("1) Click 'Generate Trials' to create trial configurations\n" +
                                    "2) Click 'Start Experiment' to begin\n" +
                                    "3) System auto-progresses after 5 seconds between trials",
                MessageType.Info);

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);

        // ── Trials List ──────────────────────────────────────────────────
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Trials", EditorStyles.miniBoldLabel);
        
        SerializedProperty trialsProp = serializedObject.FindProperty("trials");
        EditorGUILayout.PropertyField(trialsProp, true);
        
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
            bool trialActive  = ctrl.highlightManager.IsTrialActive();
            bool trialDone    = ctrl.highlightManager.IsTrialComplete();
            
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

        serializedObject.ApplyModifiedProperties();
    }
}
#endif