using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MBG.Core
{
    /// <summary>
    /// Satu jendela untuk seluruh angka balancing.
    ///
    /// Alih-alih menulis ulang tiap field (yang pasti ketinggalan begitu ada field
    /// baru), jendela ini menggambar Inspector asli tiap asset di dalam foldout.
    /// Jadi field apa pun yang ditambahkan ke ScriptableObject mana pun langsung
    /// muncul di sini tanpa mengubah kode dashboard.
    /// </summary>
    public class BalancingDashboard : EditorWindow
    {
        class Section
        {
            public string Title;
            public string TypeName;
            public List<ScriptableObject> Assets = new();
            public bool Expanded = true;
        }

        // Urutan tampil dibuat mengikuti alur permainan, bukan abjad.
        static readonly (string title, string typeName)[] Groups =
        {
            ("Reputasi", "ReputationConfigSO"),
            ("Bayaran & Skor", "ScoringConfigSO"),
            ("Kualitas Masakan", "CateringBalanceSO"),
            ("Hari", "DayConfigSO"),
            ("Pesanan", "OrderSO"),
            ("Resep", "RecipeSO"),
            ("Langkah Resep", "RecipeStepSO"),
            ("Pemesan", "RecipientSO"),
            ("QTE", "QTEConfigSO"),
            ("Pola Ritme", "RhythmPatternSO"),
            ("Gangguan (umum)", "ObstacleConfigSO"),
            ("Ormas", "OrmasConfigSO"),
            ("Santet", "SantetConfigSO"),
            ("Pajak Ilegal", "IllegalTaxConfigSO"),
            ("Naskah Pungli", "TaxDialogueSO"),
            ("Teks Mengetik", "TypingChallengeSO"),
            ("Tata Letak Dapur", "KitchenLayoutSO"),
            ("Tema UI", "UIStyle"),
        };

        readonly List<Section> _sections = new();
        readonly Dictionary<ScriptableObject, UnityEditor.Editor> _editors = new();

        Vector2 _scroll;
        string _filter = "";

        [MenuItem("Tools/MBG/Balancing Dashboard", false, 200)]
        public static void Open()
        {
            var window = GetWindow<BalancingDashboard>("MBG Balancing");
            window.minSize = new Vector2(420f, 400f);
            window.Refresh();
        }

        void OnEnable() => Refresh();

        void OnDisable() => ClearEditors();

        void ClearEditors()
        {
            foreach (UnityEditor.Editor editor in _editors.Values)
            {
                if (editor != null) DestroyImmediate(editor);
            }
            _editors.Clear();
        }

        void Refresh()
        {
            ClearEditors();
            _sections.Clear();

            foreach ((string title, string typeName) in Groups)
            {
                var section = new Section { Title = title, TypeName = typeName };

                foreach (string guid in AssetDatabase.FindAssets($"t:{typeName}"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                    if (asset != null) section.Assets.Add(asset);
                }

                section.Assets.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
                _sections.Add(section);
            }
        }

        void OnGUI()
        {
            DrawToolbar();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            bool anyShown = false;

            foreach (Section section in _sections)
            {
                if (section.Assets.Count == 0) continue;
                if (!MatchesFilter(section)) continue;

                anyShown = true;
                DrawSection(section);
            }

            if (!anyShown)
            {
                EditorGUILayout.HelpBox(
                    string.IsNullOrWhiteSpace(_filter)
                        ? "Belum ada asset balancing. Jalankan tool Tools > MBG > Build ... dulu."
                        : $"Tidak ada yang cocok dengan \"{_filter}\".",
                    MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            _filter = GUILayout.TextField(_filter, EditorStyles.toolbarSearchField, GUILayout.MinWidth(160f));

            if (GUILayout.Button("Bersihkan", EditorStyles.toolbarButton, GUILayout.Width(80f)))
            {
                _filter = "";
                GUI.FocusControl(null);
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Muat ulang", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                Refresh();

            if (GUILayout.Button("Simpan asset", EditorStyles.toolbarButton, GUILayout.Width(100f)))
                AssetDatabase.SaveAssets();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "Semua angka balancing di satu tempat. Perubahan langsung tersimpan ke asset — " +
                "tekan Simpan asset kalau ingin menulisnya ke disk sekarang juga.",
                MessageType.None);
        }

        bool MatchesFilter(Section section)
        {
            if (string.IsNullOrWhiteSpace(_filter)) return true;

            if (section.Title.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0) return true;

            foreach (ScriptableObject asset in section.Assets)
            {
                if (asset.name.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }

            return false;
        }

        void DrawSection(Section section)
        {
            EditorGUILayout.Space(4f);

            section.Expanded = EditorGUILayout.Foldout(
                section.Expanded, $"{section.Title}  ({section.Assets.Count})", true, EditorStyles.foldoutHeader);

            if (!section.Expanded) return;

            EditorGUI.indentLevel++;

            foreach (ScriptableObject asset in section.Assets)
            {
                if (asset == null) continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(asset.name, EditorStyles.boldLabel);

                if (GUILayout.Button("Pilih", GUILayout.Width(60f)))
                {
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                }
                EditorGUILayout.EndHorizontal();

                DrawInlineInspector(asset);

                EditorGUILayout.EndVertical();
            }

            EditorGUI.indentLevel--;
        }

        void DrawInlineInspector(ScriptableObject asset)
        {
            if (!_editors.TryGetValue(asset, out UnityEditor.Editor editor) || editor == null)
            {
                editor = UnityEditor.Editor.CreateEditor(asset);
                _editors[asset] = editor;
            }

            if (editor == null) return;

            EditorGUI.BeginChangeCheck();
            editor.OnInspectorGUI();

            if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(asset);
        }
    }
}
