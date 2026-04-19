using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace JuiceUp.Editor
{
    [CustomEditor(typeof(UiJuiceAnimator)), CanEditMultipleObjects]
    public class UiJuiceAnimatorEditor : UnityEditor.Editor
    {
        private SerializedProperty autoPlayProp;
        private SerializedProperty staggerProp;
        private SerializedProperty includeInactiveProp;
        private SerializedProperty presetProp;
        private SerializedProperty autoRandomizeProp;
        private SerializedProperty strengthProp;
        private SerializedProperty durationMinProp;
        private SerializedProperty durationMaxProp;
        private SerializedProperty elementsProp;
        private SerializedProperty enabledSoundProp;
        private SerializedProperty deactivateAfterPlayOutProp;
        private SerializedProperty destroyAfterPlayOutProp;
        private SerializedProperty onAnimationStartProp;
        private SerializedProperty onAnimationFinishProp;

        // Foldout states stored in EditorPrefs
        private const string PrefKeyPrefix = "UiJuiceAnimatorEditor_Foldout_";
        private bool playbackFoldout;
        private bool randomizationFoldout;
        private bool eventsFoldout;
        private bool elementsFoldout;
        private bool comingFeaturesFoldout;

        // Animation Combo System
        private enum ComboActionType
        {
            PlayIn,
            PlayOut,
            Wait
        }

        [System.Serializable]
        private class ComboAction
        {
            public ComboActionType actionType = ComboActionType.PlayIn;
            public float waitTime = 0.5f;
        }

        private List<ComboAction> comboActions = new List<ComboAction>();
        private bool isPlayingCombo = false;
        private int currentComboIndex = 0;
        private bool comboLoop = false;
        private bool comboPingPong = false;
        private bool comboPingPongReverse = false; // For pingpong direction

        private void OnEnable()
        {
            autoPlayProp = serializedObject.FindProperty("autoPlayOnEnable");
            staggerProp = serializedObject.FindProperty("stagger");
            includeInactiveProp = serializedObject.FindProperty("includeInactiveChildren");
            presetProp = serializedObject.FindProperty("preset");
            autoRandomizeProp = serializedObject.FindProperty("autoRandomizeOnPlay");
            strengthProp = serializedObject.FindProperty("strength");
            durationMinProp = serializedObject.FindProperty("durationMin");
            durationMaxProp = serializedObject.FindProperty("durationMax");
            elementsProp = serializedObject.FindProperty("elements");
            enabledSoundProp = serializedObject.FindProperty("enabledSound");
            deactivateAfterPlayOutProp = serializedObject.FindProperty("deactivateGameObjectAfterPlayOut");
            destroyAfterPlayOutProp = serializedObject.FindProperty("destroyGameObjectAfterPlayOut");
            onAnimationStartProp = serializedObject.FindProperty("onAnimationStart");
            onAnimationFinishProp = serializedObject.FindProperty("onAnimationFinish");
            
            // Load foldout states from EditorPrefs
            playbackFoldout = EditorPrefs.GetBool(PrefKeyPrefix + "Playback", true);
            randomizationFoldout = EditorPrefs.GetBool(PrefKeyPrefix + "Randomization", true);
            eventsFoldout = EditorPrefs.GetBool(PrefKeyPrefix + "Events", true);
            elementsFoldout = EditorPrefs.GetBool(PrefKeyPrefix + "Elements", true);
            comingFeaturesFoldout = EditorPrefs.GetBool(PrefKeyPrefix + "ComingFeatures", false);
            
            // Initialize combo with default actions if empty
            if (comboActions.Count == 0)
            {
                comboActions.Add(new ComboAction { actionType = ComboActionType.PlayIn, waitTime = 0f });
                comboActions.Add(new ComboAction { actionType = ComboActionType.Wait, waitTime = 1f });
                comboActions.Add(new ComboAction { actionType = ComboActionType.PlayOut, waitTime = 0f });
            }
            
            // Subscribe to editor update to repaint inspector when animations are running
            EditorApplication.update += RepaintIfAnimating;
            EditorApplication.update += UpdateComboSequence;
        }

        private void OnDisable()
        {
            // Unsubscribe from editor update
            EditorApplication.update -= RepaintIfAnimating;
            EditorApplication.update -= UpdateComboSequence;
        }

        private void RepaintIfAnimating()
        {
            // Only repaint in edit mode when not playing
            if (EditorApplication.isPlaying)
                return;

            // Check if any target is currently animating
            bool isAnimating = false;
            foreach (var obj in targets)
            {
                if (obj is UiJuiceAnimator animator && animator.IsAnimating)
                {
                    isAnimating = true;
                    break;
                }
            }

            // Repaint the inspector while animations are running
            if (isAnimating)
            {
                Repaint();
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawPlaybackButtons();
            EditorGUILayout.Space(8);
            DrawPlaybackSection();
            EditorGUILayout.Space();
            DrawRandomizationSection();
            EditorGUILayout.Space();
            DrawEventsSection();
            EditorGUILayout.Space();
            DrawElementsSection();
            EditorGUILayout.Space();
            DrawComingFeaturesSection();

            serializedObject.ApplyModifiedProperties();
        }

        private void SaveFoldoutState(string key, bool value)
        {
            EditorPrefs.SetBool(PrefKeyPrefix + key, value);
        }

        private void CommitInspectorChanges()
        {
            // Important: Buttons (Play/Randomize/etc.) should operate on the latest inspector values.
            // Without this, changing a value (e.g., Feeling Preset) and immediately clicking a button
            // can run using the old serialized value.
            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
        }

        private void DrawPlaybackSection()
        {
            bool newFoldoutState = EditorGUILayout.Foldout(playbackFoldout, "Playback", true, EditorStyles.foldoutHeader);
            if (newFoldoutState != playbackFoldout)
            {
                playbackFoldout = newFoldoutState;
                SaveFoldoutState("Playback", playbackFoldout);
            }

            if (playbackFoldout)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.Space(2);
                    EditorGUILayout.PropertyField(autoPlayProp);
                    EditorGUILayout.PropertyField(staggerProp);
                    EditorGUILayout.PropertyField(includeInactiveProp, new GUIContent("Include Inactive On Scan"));

                    EditorGUILayout.Space(6);
                    EditorGUILayout.PropertyField(deactivateAfterPlayOutProp);
                    EditorGUILayout.PropertyField(destroyAfterPlayOutProp);

                    EditorGUILayout.Space(2);
                }
            }
        }

        private float comboWaitStartTime = 0f;
        private float comboWaitDuration = 0f;

        private void DrawComingFeaturesSection()
        {
            bool newFoldoutState = EditorGUILayout.Foldout(comingFeaturesFoldout, "🚀 Coming Features", true, EditorStyles.foldoutHeader);
            if (newFoldoutState != comingFeaturesFoldout)
            {
                comingFeaturesFoldout = newFoldoutState;
                SaveFoldoutState("ComingFeatures", comingFeaturesFoldout);
            }

            if (comingFeaturesFoldout)
            {
                EditorGUI.BeginDisabledGroup(true);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.Space(4);
                    
                    // Enabled Sound
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Enabled Sound", GUILayout.Width(120));
                    EditorGUILayout.Toggle(enabledSoundProp.boolValue);
                    EditorGUILayout.LabelField("Coming Soon...", EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUILayout.Space(6);
                    
                    // Animation Combo
                    EditorGUILayout.LabelField("Animation Combo", EditorStyles.miniBoldLabel);
                    EditorGUILayout.Space(2);
                    EditorGUILayout.LabelField("Coming Soon...", EditorStyles.centeredGreyMiniLabel);
                    
                    EditorGUILayout.Space(4);
                }
                EditorGUI.EndDisabledGroup();
            }
        }

        private void StartComboSequence()
        {
            if (comboActions.Count == 0 || isPlayingCombo)
                return;

            isPlayingCombo = true;
            currentComboIndex = 0;
            comboWaitStartTime = 0f;
            comboWaitDuration = 0f;
            comboPingPongReverse = false;
            
            ExecuteComboAction(0);
        }

        private void StopComboSequence()
        {
            isPlayingCombo = false;
            currentComboIndex = 0;
            
            // Stop all animations
            foreach (var obj in targets)
            {
                if (obj is UiJuiceAnimator animator)
                {
                    animator.Stop();
                }
            }
        }

        private void ExecuteComboAction(int index)
        {
            if (index >= comboActions.Count)
            {
                StopComboSequence();
                return;
            }

            var action = comboActions[index];
            currentComboIndex = index;

            if (action.actionType == ComboActionType.Wait)
            {
                // Start waiting
                comboWaitStartTime = (float)EditorApplication.timeSinceStartup;
                comboWaitDuration = action.waitTime;
            }
            else
            {
                // Execute animation action
                CommitInspectorChanges();
                foreach (var obj in targets)
                {
                    if (obj is UiJuiceAnimator animator)
                    {
                        if (action.actionType == ComboActionType.PlayIn)
                        {
                            animator.PlayIn();
                        }
                        else if (action.actionType == ComboActionType.PlayOut)
                        {
                            animator.PlayOut();
                        }
                        EditorUtility.SetDirty(animator);
                    }
                }

                // Wait for animation to complete or use wait time
                float waitTime = action.waitTime > 0 ? action.waitTime : 1f; // Default 1 second if no wait specified
                comboWaitStartTime = (float)EditorApplication.timeSinceStartup;
                comboWaitDuration = waitTime;
            }
        }

        private void UpdateComboSequence()
        {
            if (!isPlayingCombo || comboActions.Count == 0)
                return;

            float currentTime = (float)EditorApplication.timeSinceStartup;
            
            // Check if we should wait for animation to complete
            if (currentComboIndex < comboActions.Count && currentComboIndex >= 0)
            {
                var currentAction = comboActions[currentComboIndex];
                
                if (currentAction.actionType != ComboActionType.Wait)
                {
                    // For PlayIn/PlayOut, check if animation is still running
                    bool anyAnimating = false;
                    foreach (var obj in targets)
                    {
                        if (obj is UiJuiceAnimator animator && animator.IsAnimating)
                        {
                            anyAnimating = true;
                            break;
                        }
                    }

                    // If animation finished, wait for the specified wait time, then move to next
                    if (!anyAnimating)
                    {
                        if (comboWaitStartTime == 0f)
                        {
                            // Animation just finished, start wait timer
                            comboWaitStartTime = currentTime;
                        }
                        else if ((currentTime - comboWaitStartTime) >= comboWaitDuration)
                        {
                            // Wait time passed, move to next action
                            MoveToNextComboAction();
                            Repaint();
                        }
                    }
                }
                else
                {
                    // For Wait action, just check time
                    if ((currentTime - comboWaitStartTime) >= comboWaitDuration)
                    {
                        MoveToNextComboAction();
                        Repaint();
                    }
                }
            }
        }

        private void MoveToNextComboAction()
        {
            if (comboPingPong)
            {
                // PingPong mode: reverse direction at ends
                if (comboPingPongReverse)
                {
                    // Going backwards
                    currentComboIndex--;
                    if (currentComboIndex < 0)
                    {
                        // Reached start, reverse direction and go forward
                        comboPingPongReverse = false;
                        currentComboIndex = 1; // Start from second action (skip first since we just did it)
                        if (currentComboIndex >= comboActions.Count)
                        {
                            // Only one action, just restart it
                            currentComboIndex = 0;
                        }
                    }
                }
                else
                {
                    // Going forwards
                    currentComboIndex++;
                    if (currentComboIndex >= comboActions.Count)
                    {
                        // Reached end, reverse direction and go backward
                        comboPingPongReverse = true;
                        currentComboIndex = comboActions.Count - 2; // Start from second-to-last (skip last since we just did it)
                        if (currentComboIndex < 0)
                        {
                            // Only one action, just restart it
                            currentComboIndex = 0;
                        }
                    }
                }
            }
            else
            {
                // Normal or Loop mode
                currentComboIndex++;
                if (currentComboIndex >= comboActions.Count)
                {
                    if (comboLoop)
                    {
                        // Loop: restart from beginning
                        currentComboIndex = 0;
                    }
                    else
                    {
                        // No loop: stop sequence
                        StopComboSequence();
                        return;
                    }
                }
            }

            if (currentComboIndex >= 0 && currentComboIndex < comboActions.Count)
            {
                ExecuteComboAction(currentComboIndex);
            }
        }

        private void DrawRandomizationSection()
        {
            bool newFoldoutState = EditorGUILayout.Foldout(randomizationFoldout, "Randomization & Feel", true, EditorStyles.foldoutHeader);
            if (newFoldoutState != randomizationFoldout)
            {
                randomizationFoldout = newFoldoutState;
                SaveFoldoutState("Randomization", randomizationFoldout);
            }

            if (randomizationFoldout)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.Space(2);
                    
                    // Check if any target is currently animating (only in edit mode)
                    bool isAnimating = false;
                    if (!EditorApplication.isPlaying)
                    {
                        foreach (var obj in targets)
                        {
                            if (obj is UiJuiceAnimator animator && animator.IsAnimating)
                            {
                                isAnimating = true;
                                break;
                            }
                        }
                    }
                    
                    // Surprise Me button
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.Space();
                        
                        GUIStyle surpriseStyle = new GUIStyle(GUI.skin.button);
                        surpriseStyle.fontSize = 12;
                        surpriseStyle.fontStyle = FontStyle.Bold;
                        surpriseStyle.padding = new RectOffset(12, 12, 6, 6);
                        
                        Color originalColor = GUI.color;
                        Color originalContentColor = GUI.contentColor;
                        
                        EditorGUI.BeginDisabledGroup(isAnimating);
                        
                        if (!isAnimating)
                        {
                            // Fun colorful gradient effect - purple/pink
                            GUI.color = new Color(0.8f, 0.4f, 1f, 1f);
                            GUI.contentColor = Color.white;
                        }
                        else
                        {
                            GUI.contentColor = new Color(0.5f, 0.5f, 0.5f, 1f);
                        }
                        
                        if (GUILayout.Button("✨ SURPRISE ME! ✨", surpriseStyle, GUILayout.Height(28), GUILayout.ExpandWidth(true)))
                        {
                            CommitInspectorChanges();
                            foreach (var obj in targets)
                            {
                                if (obj is UiJuiceAnimator animator)
                                {
                                    Undo.RecordObject(animator, "Surprise Me - Random Preset");
                                    
                                    // Get all preset values
                                    var presetValues = System.Enum.GetValues(typeof(UiJuiceAnimator.FeelingPreset));
                                    
                                    // Random preset
                                    var randomPreset = (UiJuiceAnimator.FeelingPreset)presetValues.GetValue(UnityEngine.Random.Range(0, presetValues.Length));
                                    animator.preset = randomPreset;
                                    
                                    // Random strength between 0.5 and 2.0 for more interesting results
                                    animator.strength = UnityEngine.Random.Range(0.5f, 2.0f);
                                    
                                    // Random duration range (min between 0.1 and 2.0, max between min+0.2 and 5.0)
                                    float randomMin = UnityEngine.Random.Range(0.1f, 2.0f);
                                    float randomMax = UnityEngine.Random.Range(randomMin + 0.2f, 5.0f);
                                    animator.durationMin = randomMin;
                                    animator.durationMax = randomMax;
                                    
                                    // Apply the preset
                                    animator.ApplyFeelingPreset();
                                    
                                    // Auto play in to see the surprise!
                                    animator.PlayIn();
                                    
                                    EditorUtility.SetDirty(animator);
                                }
                            }
                            
                            // Force repaint to show the new values
                            Repaint();
                        }
                        
                        GUI.color = originalColor;
                        GUI.contentColor = originalContentColor;
                        EditorGUI.EndDisabledGroup();
                        
                        EditorGUILayout.Space();
                    }
                    
                    EditorGUILayout.Space(4);
                    
                    EditorGUILayout.PropertyField(presetProp, new GUIContent("Feeling Preset"));
                    EditorGUILayout.PropertyField(strengthProp, new GUIContent("Strength"));
                    EditorGUILayout.PropertyField(autoRandomizeProp, new GUIContent("Randomize Animation"));

                    DrawDurationRange();
                    EditorGUILayout.Space(2);
                }
            }
        }

        private void DrawPlaybackButtons()
        {
            // Check if any target is currently animating (only in edit mode)
            bool isAnimating = false;
            if (!EditorApplication.isPlaying)
            {
                foreach (var obj in targets)
                {
                    if (obj is UiJuiceAnimator animator && animator.IsAnimating)
                    {
                        isAnimating = true;
                        break;
                    }
                }
            }

            // Draw background box for better visual separation
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.Space(6);
                
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.Space(8);
                    
                    // Play In button - more fun and vibrant
                    EditorGUI.BeginDisabledGroup(isAnimating);
                    GUIStyle playInStyle = new GUIStyle(GUI.skin.button);
                    playInStyle.fontSize = 13;
                    playInStyle.fontStyle = FontStyle.Bold;
                    playInStyle.padding = new RectOffset(15, 15, 8, 8);
                    playInStyle.alignment = TextAnchor.MiddleCenter;
                    
                    Color originalColor = GUI.color;
                    Color originalContentColor = GUI.contentColor;
                    
                    if (!isAnimating)
                    {
                        // Bright vibrant green for Play In
                        GUI.color = new Color(0.2f, 0.9f, 0.3f, 1f);
                        GUI.contentColor = Color.white;
                    }
                    else
                    {
                        GUI.contentColor = new Color(0.5f, 0.5f, 0.5f, 1f);
                    }
                    
                    if (GUILayout.Button("▶▶ PLAY IN", playInStyle, GUILayout.Height(36), GUILayout.ExpandWidth(true)))
                    {
                        CommitInspectorChanges();
                        foreach (var obj in targets)
                        {
                            if (obj is UiJuiceAnimator animator)
                            {
                                Undo.RegisterFullObjectHierarchyUndo(animator.gameObject, "Play In UI Juice");
                                animator.PlayIn();
                                EditorUtility.SetDirty(animator);
                            }
                        }
                    }
                    
                    GUI.color = originalColor;
                    GUI.contentColor = originalContentColor;
                    
                    EditorGUILayout.Space(8);
                    
                    // Play Out button - more fun and vibrant
                    if (!isAnimating)
                    {
                        // Bright vibrant red/orange for Play Out
                        GUI.color = new Color(1f, 0.3f, 0.2f, 1f);
                        GUI.contentColor = Color.white;
                    }
                    else
                    {
                        GUI.contentColor = new Color(0.5f, 0.5f, 0.5f, 1f);
                    }
                    
                    if (GUILayout.Button("◀◀ PLAY OUT", playInStyle, GUILayout.Height(36), GUILayout.ExpandWidth(true)))
                    {
                        CommitInspectorChanges();
                        foreach (var obj in targets)
                        {
                            if (obj is UiJuiceAnimator animator)
                            {
                                Undo.RegisterFullObjectHierarchyUndo(animator.gameObject, "Play Out UI Juice");
                                animator.PlayOut();
                                EditorUtility.SetDirty(animator);
                            }
                        }
                    }
                    
                    GUI.color = originalColor;
                    GUI.contentColor = originalContentColor;
                    EditorGUI.EndDisabledGroup();
                    
                    EditorGUILayout.Space(8);
                }
                
                EditorGUILayout.Space(6);

                if (isAnimating)
                {
                    EditorGUILayout.HelpBox("Animation in progress. Please wait for it to finish.", MessageType.Info);
                }
            }
        }

        private void DrawEventsSection()
        {
            bool newFoldoutState = EditorGUILayout.Foldout(eventsFoldout, "Events", true, EditorStyles.foldoutHeader);
            if (newFoldoutState != eventsFoldout)
            {
                eventsFoldout = newFoldoutState;
                SaveFoldoutState("Events", eventsFoldout);
            }

            if (eventsFoldout)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.Space(2);
                    EditorGUILayout.PropertyField(onAnimationStartProp);
                    EditorGUILayout.PropertyField(onAnimationFinishProp);
                    EditorGUILayout.Space(2);
                }
            }
        }

        private void DrawElementsSection()
        {
            bool newFoldoutState = EditorGUILayout.Foldout(elementsFoldout, "Elements", true, EditorStyles.foldoutHeader);
            if (newFoldoutState != elementsFoldout)
            {
                elementsFoldout = newFoldoutState;
                SaveFoldoutState("Elements", elementsFoldout);
            }

            if (elementsFoldout)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.Space(2);
                    
                    // Hierarchy buttons
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.Space();
                        
                        GUIStyle buttonStyle = new GUIStyle(EditorStyles.miniButtonLeft);
                        buttonStyle.padding = new RectOffset(8, 8, 4, 4);
                        
                        if (GUILayout.Button("Scan Children", buttonStyle, GUILayout.Height(22)))
                        {
                            CommitInspectorChanges();
                            foreach (var obj in targets)
                            {
                                if (obj is UiJuiceAnimator animator)
                                {
                                    Undo.RecordObject(animator, "Scan UI Juice Children");
                                    animator.RebuildFromHierarchy(includeInactiveProp.boolValue);
                                    EditorUtility.SetDirty(animator);
                                }
                            }
                        }

                        buttonStyle = new GUIStyle(EditorStyles.miniButtonRight);
                        buttonStyle.padding = new RectOffset(8, 8, 4, 4);
                        
                        if (GUILayout.Button("Clear Elements", buttonStyle, GUILayout.Height(22)))
                        {
                            CommitInspectorChanges();
                            foreach (var obj in targets)
                            {
                                if (obj is UiJuiceAnimator animator)
                                {
                                    Undo.RecordObject(animator, "Clear UI Juice Elements");
                                    animator.elements.Clear();
                                    EditorUtility.SetDirty(animator);
                                }
                            }
                        }
                        
                        EditorGUILayout.Space();
                    }

                    EditorGUILayout.Space(4);
                    
                    // Elements list
                    EditorGUILayout.PropertyField(elementsProp, true);
                    EditorGUILayout.Space(2);
                }
            }
        }

        private void DrawDurationRange()
        {
            EditorGUILayout.LabelField("Duration Range (s)", EditorStyles.miniBoldLabel);

            var min = durationMinProp.floatValue;
            var max = durationMaxProp.floatValue;

            EditorGUILayout.MinMaxSlider(new GUIContent("Min / Max"), ref min, ref max, 0.1f, 5f);
            min = Mathf.Clamp(min, 0.1f, 5f);
            max = Mathf.Clamp(max, 0.1f, 5f);
            if (max < min) max = min;

            durationMinProp.floatValue = min;
            durationMaxProp.floatValue = max;

            Rect rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            float spacing = 5f;
            float fieldWidth = (rect.width - spacing) / 2f;
            
            Rect minRect = new Rect(rect.x, rect.y, fieldWidth, rect.height);
            Rect maxRect = new Rect(rect.x + fieldWidth + spacing, rect.y, fieldWidth, rect.height);
            
            EditorGUI.BeginChangeCheck();
            float newMin = EditorGUI.FloatField(minRect, min);
            float newMax = EditorGUI.FloatField(maxRect, max);
            if (EditorGUI.EndChangeCheck())
            {
                durationMinProp.floatValue = newMin;
                durationMaxProp.floatValue = newMax;
            }
        }

    }
}
