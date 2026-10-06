using System;
using System.Collections.Generic;
using UnityEngine;

namespace Loongdum.SceneFlow
{
    [Serializable]
    public sealed class LevelEntry
    {
        [SerializeField] private string id;
        [SerializeField] private string title;
        [SerializeField, TextArea(2, 3)] private string description;
        [SerializeField] private string scenePath;

        public string Id => id;
        public string Title => title;
        public string Description => description;
        public string ScenePath => scenePath;

        public LevelEntry(string id, string title, string description, string scenePath)
        {
            this.id = id;
            this.title = title;
            this.description = description;
            this.scenePath = scenePath;
        }
    }

    [CreateAssetMenu(menuName = "Loongdum/Level Catalog")]
    public sealed class LevelCatalog : ScriptableObject
    {
        [SerializeField] private string selectionScenePath;
        [SerializeField] private List<LevelEntry> levels = new List<LevelEntry>();

        public string SelectionScenePath => selectionScenePath;
        public IReadOnlyList<LevelEntry> Levels => levels;

        public LevelEntry FindById(string id)
        {
            return levels.Find(level => level != null &&
                string.Equals(level.Id, id, StringComparison.Ordinal));
        }

        public LevelEntry FindByScenePath(string path)
        {
            return levels.Find(level => level != null &&
                string.Equals(level.ScenePath, path, StringComparison.Ordinal));
        }

        public LevelEntry GetNext(LevelEntry current)
        {
            int index = levels.IndexOf(current);
            return index >= 0 && index + 1 < levels.Count ? levels[index + 1] : null;
        }

        public bool Validate(out string error)
        {
            if (string.IsNullOrWhiteSpace(selectionScenePath))
            {
                error = "Level Catalog requires a selection scene.";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var paths = new HashSet<string>(StringComparer.Ordinal) { selectionScenePath };
            foreach (LevelEntry level in levels)
            {
                if (level == null || string.IsNullOrWhiteSpace(level.Id) ||
                    string.IsNullOrWhiteSpace(level.Title) || string.IsNullOrWhiteSpace(level.ScenePath))
                {
                    error = "Each level requires an ID, title and scene.";
                    return false;
                }

                if (!ids.Add(level.Id) || !paths.Add(level.ScenePath))
                {
                    error = "Level IDs and scene paths must be unique.";
                    return false;
                }
            }

            error = null;
            return true;
        }

#if UNITY_EDITOR
        public void SetInitialConfiguration(string menuPath, LevelEntry firstLevel)
        {
            selectionScenePath = menuPath;
            levels = new List<LevelEntry> { firstLevel };
        }
#endif
    }
}
