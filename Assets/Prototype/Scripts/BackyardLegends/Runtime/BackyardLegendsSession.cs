using System.Collections.Generic;
using System.Linq;
using BackyardLegends.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BackyardLegends.Runtime
{
    public sealed class BackyardLegendsSession : MonoBehaviour
    {
        private const string ThemeResourcePath = "BackyardLegends/Theme_Default";
        private const string ClassicResourcePath = "BackyardLegends/Rules_Classic";
        private const string StreetResourcePath = "BackyardLegends/Rules_Street";

        [SerializeField] private ThemeConfig themeOverride;
        [SerializeField] private string lobbySceneName = "LobbyScene";
        [SerializeField] private string gameplaySceneName = "GameplayScene";

        private RuleSetConfig[] ruleConfigs;

        public static BackyardLegendsSession Instance { get; private set; }

        public ThemeConfig Theme { get; private set; }
        public int SelectedModeIndex { get; private set; }
        public int SelectedTargetScore { get; private set; } = 100;
        public IReadOnlyList<RuleSetConfig> RuleConfigs => ruleConfigs;

        public RuleSetDefinition SelectedRule => GetSelectedRuleDefinition();

        private void Awake()
        {
            Initialize();
        }

        public static BackyardLegendsSession GetOrCreateRuntimeInstance()
        {
            if (Instance != null)
            {
                return Instance;
            }

            var existing = FindFirstObjectByType<BackyardLegendsSession>();
            if (existing != null)
            {
                existing.Initialize();
                return existing;
            }

            var sessionObject = new GameObject("Backyard Legends Session");
            var session = sessionObject.AddComponent<BackyardLegendsSession>();
            session.Initialize();
            return session;
        }

        public string[] GetModeLabels()
        {
            if (ruleConfigs == null || ruleConfigs.Length == 0)
            {
                return new[] { "Classic", "Street" };
            }

            return ruleConfigs.Select(config => config != null ? config.DisplayName : "Mode").ToArray();
        }

        public int[] GetTargetOptions()
        {
            var config = GetSelectedConfig();
            if (config != null && config.TargetScoreOptions != null && config.TargetScoreOptions.Length > 0)
            {
                return config.TargetScoreOptions.ToArray();
            }

            return new[] { 100, 200, 300 };
        }

        public void SelectMode(int modeIndex)
        {
            SelectedModeIndex = Mathf.Max(0, modeIndex);
            ClampSelections();
        }

        public void SelectTarget(int targetScore)
        {
            SelectedTargetScore = targetScore;
            ClampSelections();
        }

        public void LoadGameplayScene()
        {
            SceneManager.LoadScene(gameplaySceneName);
        }

        public void LoadLobbyScene()
        {
            SceneManager.LoadScene(lobbySceneName);
        }

        private void Initialize()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (Theme == null)
            {
                Theme = themeOverride != null
                    ? themeOverride
                    : Resources.Load<ThemeConfig>(ThemeResourcePath) ?? ThemeConfig.CreateFallback();
            }

            if (ruleConfigs == null || ruleConfigs.Length == 0)
            {
                ruleConfigs = LoadRuleConfigs();
            }

            ClampSelections();
        }

        private void ClampSelections()
        {
            if (ruleConfigs == null || ruleConfigs.Length == 0)
            {
                SelectedModeIndex = Mathf.Clamp(SelectedModeIndex, 0, 1);
                return;
            }

            SelectedModeIndex = Mathf.Clamp(SelectedModeIndex, 0, ruleConfigs.Length - 1);
            var targets = GetTargetOptions();
            if (targets.Length == 0)
            {
                SelectedTargetScore = 100;
                return;
            }

            if (!targets.Contains(SelectedTargetScore))
            {
                SelectedTargetScore = targets[0];
            }
        }

        private RuleSetDefinition GetSelectedRuleDefinition()
        {
            var config = GetSelectedConfig();
            if (config == null)
            {
                return SelectedModeIndex == 0
                    ? RuleSetConfig.CreateClassic(SelectedTargetScore)
                    : RuleSetConfig.CreateStreet(SelectedTargetScore);
            }

            var definition = config.ToDefinition(SelectedTargetScore);
            if (config.DisplayName.Contains("Street"))
            {
                definition.DisplayName = "Street";
                definition.AllowSpadesAnytime = true;
                definition.SpadesMustBeBroken = false;
                definition.FollowSuitRequired = true;
                definition.RenegePenaltyEnabled = true;
            }
            else
            {
                definition.DisplayName = "Classic";
                definition.AllowSpadesAnytime = false;
                definition.SpadesMustBeBroken = true;
                definition.FollowSuitRequired = true;
                definition.RenegePenaltyEnabled = false;
            }

            return definition;
        }

        private RuleSetConfig GetSelectedConfig()
        {
            if (ruleConfigs == null || ruleConfigs.Length == 0)
            {
                return null;
            }

            return ruleConfigs[Mathf.Clamp(SelectedModeIndex, 0, ruleConfigs.Length - 1)];
        }

        private static RuleSetConfig[] LoadRuleConfigs()
        {
            var configs = new List<RuleSetConfig>();
            var classic = Resources.Load<RuleSetConfig>(ClassicResourcePath);
            var street = Resources.Load<RuleSetConfig>(StreetResourcePath);
            if (classic != null)
            {
                configs.Add(classic);
            }

            if (street != null)
            {
                configs.Add(street);
            }

            return configs.ToArray();
        }
    }
}
