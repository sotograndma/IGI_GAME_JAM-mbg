using System.Collections.Generic;
using MBG.Data;
using MBG.Kitchen;
using MBG.QTE;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace MBG.Catering
{
    /// <summary>
    /// Membuat data catering contoh dan memasang CateringController di __Systems.
    ///
    /// Additive dan idempoten: asset yang sudah ada tidak pernah ditimpa, jadi
    /// angka yang sudah kamu tuning aman. Yang dilengkapi hanyalah isian yang
    /// benar-benar kosong (misal daftar langkah resep yang belum diisi).
    /// </summary>
    public static class CateringDataBuilder
    {
        const string MenuPath = "Tools/MBG/Build Catering Data";

        const string SystemsName = "__Systems";
        const string DataFolder = "Assets/_Game/Data";
        const string CateringFolder = DataFolder + "/Catering";
        const string QteFolder = DataFolder + "/QTE";

        [MenuItem(MenuPath, false, 104)]
        public static void BuildCateringData()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Build Catering Data",
                    "Tool ini tidak bisa dijalankan saat Play mode. Keluar dari Play mode dulu.", "OK");
                return;
            }

            var easy = AssetDatabase.LoadAssetAtPath<QTEConfigSO>($"{QteFolder}/QTE_Easy.asset");
            var normal = AssetDatabase.LoadAssetAtPath<QTEConfigSO>($"{QteFolder}/QTE_Normal.asset");
            if (easy == null || normal == null)
            {
                Debug.LogError("[MBG] Preset QTE belum ada. Jalankan Tools > MBG > Build QTE Setup dulu.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[MBG] Tidak ada scene aktif yang valid.");
                return;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build Catering Data");

            var log = new List<string>();
            bool changed = false;

            EnsureFolder(CateringFolder);

            CateringBalanceSO balance = EnsureBalance(log, ref changed);

            RecipeStepSO stepPrep = EnsureStep("Step_Prep_PotongSayur", StationType.Prep, easy,
                                               "Potong sayur", log, ref changed);
            RecipeStepSO stepCook = EnsureStep("Step_Cooking_Masak", StationType.Cooking, normal,
                                               "Masak nasi & lauk", log, ref changed);
            RecipeStepSO stepPack = EnsureStep("Step_Packing_Kemas", StationType.Packing, easy,
                                               "Kemas ke kotak", log, ref changed);

            RecipeSO recipe = EnsureRecipe(stepPrep, stepCook, stepPack, log, ref changed);
            RecipientSO recipient = EnsureRecipient(log, ref changed);

            var orders = new List<OrderSO>
            {
                EnsureOrder("Order_Sekolah_Kecil", recipe, recipient, 40, 180f, 300, 600, log, ref changed),
                EnsureOrder("Order_Sekolah_Sedang", recipe, recipient, 100, 300f, 800, 1500, log, ref changed),
                EnsureOrder("Order_Sekolah_Besar", recipe, recipient, 200, 480f, 1600, 3000, log, ref changed),
            };

            AssetDatabase.SaveAssets();

            changed |= EnsureController(scene, balance, orders, log);

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log("[MBG] Build Catering Data selesai:\n- " + string.Join("\n- ", log) +
                          "\nJangan lupa simpan scene (Ctrl+S).");
            }
            else
            {
                Debug.Log("[MBG] Data catering sudah lengkap. Tidak ada perubahan.");
            }

            Undo.CollapseUndoOperations(undoGroup);
        }

        [MenuItem(MenuPath, true)]
        static bool ValidateBuildCateringData() => !EditorApplication.isPlayingOrWillChangePlaymode;

        // ---- Asset ---------------------------------------------------------

        static CateringBalanceSO EnsureBalance(List<string> log, ref bool changed)
        {
            var balance = LoadOrCreate<CateringBalanceSO>("CateringBalance", out bool created);
            if (created)
            {
                changed = true;
                log.Add($"{CateringFolder}/CateringBalance.asset dibuat (Perfect 1.0, Good 0.7, Miss 0.35, Gagal 0).");
            }
            return balance;
        }

        static RecipeStepSO EnsureStep(string assetName, StationType station, QTEConfigSO config,
                                       string actionLabel, List<string> log, ref bool changed)
        {
            var step = LoadOrCreate<RecipeStepSO>(assetName, out bool created);
            if (!created) return step;

            step.station = station;
            step.qteConfig = config;
            step.qteType = QTEType.TimingBar;
            step.actionLabel = actionLabel;
            EditorUtility.SetDirty(step);

            changed = true;
            log.Add($"{assetName} dibuat ({station}, \"{actionLabel}\", {config.name}).");
            return step;
        }

        static RecipeSO EnsureRecipe(RecipeStepSO prep, RecipeStepSO cook, RecipeStepSO pack,
                                     List<string> log, ref bool changed)
        {
            var recipe = LoadOrCreate<RecipeSO>("Recipe_NasiKotak", out bool created);

            if (created)
            {
                recipe.displayName = "Nasi Kotak";
                recipe.portionsPerBatch = 20;
            }

            // Isi langkah kalau memang masih kosong — tidak menimpa urutan yang
            // sudah pernah diatur.
            if (recipe.steps == null || recipe.steps.Count == 0)
            {
                recipe.steps = new List<RecipeStepSO> { prep, cook, pack };
                EditorUtility.SetDirty(recipe);
                changed = true;
                log.Add("Recipe_NasiKotak diisi 3 langkah: Potong sayur -> Masak -> Kemas (20 porsi per batch).");
            }
            else if (created)
            {
                EditorUtility.SetDirty(recipe);
                changed = true;
            }

            return recipe;
        }

        static RecipientSO EnsureRecipient(List<string> log, ref bool changed)
        {
            var recipient = LoadOrCreate<RecipientSO>("Recipient_Sekolah", out bool created);
            if (!created) return recipient;

            recipient.institutionName = "SD Negeri 03 Sukamaju";
            recipient.patienceMultiplier = 1f;
            EditorUtility.SetDirty(recipient);

            changed = true;
            log.Add("Recipient_Sekolah dibuat (SD Negeri 03 Sukamaju).");
            return recipient;
        }

        static OrderSO EnsureOrder(string assetName, RecipeSO recipe, RecipientSO recipient,
                                   int portions, float deadline, int gold, int score,
                                   List<string> log, ref bool changed)
        {
            var order = LoadOrCreate<OrderSO>(assetName, out bool created);
            if (!created) return order;

            order.recipe = recipe;
            order.recipient = recipient;
            order.totalPortions = portions;
            order.deadlineSeconds = deadline;
            order.baseGoldReward = gold;
            order.baseScoreReward = score;
            EditorUtility.SetDirty(order);

            changed = true;
            int batches = recipe != null ? recipe.GetBatchCount(portions) : 0;
            log.Add($"{assetName} dibuat ({portions} porsi = {batches} batch, {deadline:0}s, {gold} gold).");
            return order;
        }

        static T LoadOrCreate<T>(string assetName, out bool created) where T : ScriptableObject
        {
            string path = $"{CateringFolder}/{assetName}.asset";

            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                created = false;
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            created = true;
            return asset;
        }

        // ---- Scene ---------------------------------------------------------

        static bool EnsureController(Scene scene, CateringBalanceSO balance, List<OrderSO> orders,
                                     List<string> log)
        {
            GameObject systems = FindRoot(scene, SystemsName);
            if (systems == null)
            {
                Debug.LogError($"[MBG] '{SystemsName}' belum ada. Jalankan Tools > MBG > Setup Systems Object dulu.");
                return false;
            }

            bool changed = false;

            var controller = systems.GetComponent<CateringController>();
            if (controller == null)
            {
                controller = Undo.AddComponent<CateringController>(systems);
                changed = true;
                log.Add($"CateringController ditambahkan ke '{SystemsName}'.");
            }

            var so = new SerializedObject(controller);

            SerializedProperty balanceProp = so.FindProperty("balance");
            if (balanceProp != null && balanceProp.objectReferenceValue != balance)
            {
                balanceProp.objectReferenceValue = balance;
                changed = true;
                log.Add("CateringController.balance diikat ke CateringBalance.asset.");
            }

            SerializedProperty ordersProp = so.FindProperty("sampleOrders");
            if (ordersProp != null && ordersProp.arraySize == 0)
            {
                ordersProp.arraySize = orders.Count;
                for (int i = 0; i < orders.Count; i++)
                    ordersProp.GetArrayElementAtIndex(i).objectReferenceValue = orders[i];

                changed = true;
                log.Add($"CateringController.sampleOrders diisi {orders.Count} pesanan " +
                        "(F3 memakai yang pertama: Order_Sekolah_Kecil).");
            }

            so.ApplyModifiedProperties();
            return changed;
        }

        static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
            }
            return null;
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(folder);

            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
