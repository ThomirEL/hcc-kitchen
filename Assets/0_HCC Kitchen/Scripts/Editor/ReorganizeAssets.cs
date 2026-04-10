using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;

public class ReorganizeAssets : EditorWindow
{
    [MenuItem("Assets/Reorganize Kitchen Assets")]
    public static void ShowWindow()
    {
        if (!EditorUtility.DisplayDialog("Reorganize Assets",
            "This will reorganize your assets folder.\n\n" +
            "1. Delete duplicate can prefabs\n" +
            "2. Create new folder structure\n" +
            "3. Move assets to new locations\n" +
            "4. Delete empty folders\n\n" +
            "Make sure to BACKUP your project first (commit to git)!",
            "Continue", "Cancel"))
        {
            return;
        }

        Reorganize();
    }

    private static void Reorganize()
    {
        int movedCount = 0;
        int deletedCount = 0;
        List<string> errors = new List<string>();

        // Phase 1: Delete duplicate can prefabs
        Debug.Log("=== Phase 1: Deleting duplicate can prefabs ===");
        string[] duplicatePrefabs = new string[]
        {
            "Assets/Kitchen Assets/Meshes/cans/Prefabs/Beans.prefab",
            "Assets/Kitchen Assets/Meshes/cans/Prefabs/Chickpeas.prefab",
            "Assets/Kitchen Assets/Meshes/cans/Prefabs/Peas.prefab",
            "Assets/Kitchen Assets/Meshes/cans/Prefabs/Tomato.prefab",
            "Assets/Kitchen Assets/Meshes/cans/Prefabs/Corn.prefab",
            "Assets/Kitchen Assets/Meshes/cans/Prefabs/Mushroom.prefab",
            "Assets/Kitchen Assets/Meshes/cans/Prefabs/Peaches.prefab",
            "Assets/Kitchen Assets/Meshes/cans/Prefabs/Coconut.prefab",
            "Assets/Kitchen Assets/Meshes/cans/Prefabs/Soup.prefab",
            "Assets/Kitchen Assets/Meshes/cans/Prefabs/Tuna.prefab"
        };

        foreach (string prefab in duplicatePrefabs)
        {
            if (AssetExists(prefab))
            {
                if (DeleteAsset(prefab))
                {
                    deletedCount++;
                    Debug.Log($"Deleted duplicate: {prefab}");
                }
            }
        }

        // Delete the duplicate Prefabs folder
        if (AssetExists("Assets/Kitchen Assets/Meshes/cans/Prefabs"))
        {
            if (DeleteAsset("Assets/Kitchen Assets/Meshes/cans/Prefabs"))
            {
                deletedCount++;
                Debug.Log("Deleted empty folder: Assets/Kitchen Assets/Meshes/cans/Prefabs");
            }
        }

        // Phase 2: Create new folder structure
        Debug.Log("\n=== Phase 2: Creating new folder structure ===");
        CreateFolder("Assets/Art");
        CreateFolder("Assets/Art/3D");
        CreateFolder("Assets/Art/3D/Furniture");
        CreateFolder("Assets/Art/3D/Items");
        CreateFolder("Assets/Art/3D/Items/Cans");
        CreateFolder("Assets/Art/3D/Items/Bottles");
        CreateFolder("Assets/Art/3D/Items/Jars");
        CreateFolder("Assets/Art/3D/Items/Produce");
        CreateFolder("Assets/Art/3D/Items/Packaged");
        CreateFolder("Assets/Art/3D/Items/DryGoods");
        CreateFolder("Assets/Art/3D/Items/Kitchenware");
        CreateFolder("Assets/Art/3D/Items/Refrigerated");
        CreateFolder("Assets/Art/2D");
        CreateFolder("Assets/Art/2D/Labels");
        CreateFolder("Assets/Art/2D/DryGoods");
        CreateFolder("Assets/Materials");
        CreateFolder("Assets/Materials/Base");
        CreateFolder("Assets/Materials/Items");
        CreateFolder("Assets/Prefabs");
        CreateFolder("Assets/Prefabs/Furniture");
        CreateFolder("Assets/Prefabs/Items");
        CreateFolder("Assets/RuntimeTextures");
        CreateFolder("Assets/RuntimeTextures/SpiceLabels");

        // Phase 3: Move assets
        Debug.Log("\n=== Phase 3: Moving assets ===");

        // --- Move 3D Cans (Meshes/cans/) ---
        MoveAsset("Assets/Kitchen Assets/Meshes/cans/can_beans_uv.obj", "Assets/Art/3D/Items/Cans/can_beans_uv.obj", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/cans/can_soup.obj", "Assets/Art/3D/Items/Cans/can_soup.obj", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/cans/can_tuna.obj", "Assets/Art/3D/Items/Cans/can_tuna.obj", ref movedCount);

        // Can textures
        MoveAsset("Assets/Kitchen Assets/Meshes/cans/can_beans.png", "Assets/Art/3D/Items/Cans/can_beans.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/cans/can_chickpeas.png", "Assets/Art/3D/Items/Cans/can_chickpeas.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/cans/can_coconut.png", "Assets/Art/3D/Items/Cans/can_coconut.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/cans/can_corn.png", "Assets/Art/3D/Items/Cans/can_corn.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/cans/can_mushrooms.png", "Assets/Art/3D/Items/Cans/can_mushrooms.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/cans/can_peaches.png", "Assets/Art/3D/Items/Cans/can_peaches.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/cans/can_peas.png", "Assets/Art/3D/Items/Cans/can_peas.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/cans/can_soup.png", "Assets/Art/3D/Items/Cans/can_soup.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/cans/can_tomato.png", "Assets/Art/3D/Items/Cans/can_tomato.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/cans/can_tuna.png", "Assets/Art/3D/Items/Cans/can_tuna.png", ref movedCount);

        // --- Move Can Materials ---
        MoveAsset("Assets/Kitchen Assets/Meshes/cans/Materials/CanMaterial.mat", "Assets/Materials/Items/CanMaterial.mat", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/cans/Materials/CAN BEANS.mat", "Assets/Materials/Items/CAN_BEANS.mat", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/cans/Materials/UV CAN.mat", "Assets/Materials/Items/UV_CAN.mat", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/cans/Materials/can_beans.mat", "Assets/Materials/Items/can_beans.mat", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/cans/Materials/can_chickpeas 1.mat", "Assets/Materials/Items/can_chickpeas_1.mat", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/cans/Materials/can_chickpeas.mat", "Assets/Materials/Items/can_chickpeas.mat", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/cans/Materials/can_corn.mat", "Assets/Materials/Items/can_corn.mat", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/cans/Materials/can_mushrooms.mat", "Assets/Materials/Items/can_mushrooms.mat", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/cans/Materials/can_peaches 1.mat", "Assets/Materials/Items/can_peaches_1.mat", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/cans/Materials/can_peaches.mat", "Assets/Materials/Items/can_peaches.mat", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/cans/Materials/can_peas.mat", "Assets/Materials/Items/can_peas.mat", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/cans/Materials/can_soup.mat", "Assets/Materials/Items/can_soup.mat", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/cans/Materials/can_tomato.mat", "Assets/Materials/Items/can_tomato.mat", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/cans/Materials/can_tuna.mat", "Assets/Materials/Items/can_tuna.mat", ref movedCount);

        // --- Move Textures/Bottles ---
        MoveAsset("Assets/Kitchen Assets/Textures/bottles/bottle_wine_tex.png", "Assets/Art/3D/Items/Bottles/bottle_wine_tex.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Textures/bottles/can_soda_tex.png", "Assets/Art/3D/Items/Bottles/can_soda_tex.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Textures/bottles/carton_milk_tex.png", "Assets/Art/3D/Items/Bottles/carton_milk_tex.png", ref movedCount);

        // Bottle Materials
        MoveAsset("Assets/Kitchen Assets/Textures/bottles/Materials/bottle_wine_tex.mat", "Assets/Materials/Items/bottle_wine_tex.mat", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Textures/bottles/Materials/carton_milk_tex.mat", "Assets/Materials/Items/carton_milk_tex.mat", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Textures/bottles/Materials/can_soda_tex.mat", "Assets/Materials/Items/can_soda_tex.mat", ref movedCount);

        // --- Move Textures/Cans ---
        MoveAsset("Assets/Kitchen Assets/Textures/cans/can_beans_tex.png", "Assets/Art/3D/Items/Cans/can_beans_tex.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Textures/cans/can_soup_tex.png", "Assets/Art/3D/Items/Cans/can_soup_tex.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Textures/cans/can_tuna_tex.png", "Assets/Art/3D/Items/Cans/can_tuna_tex.png", ref movedCount);

        // --- Move Textures/Packaged ---
        MoveAsset("Assets/Kitchen Assets/Textures/packaged/bag_chips_tex.png", "Assets/Art/3D/Items/Packaged/bag_chips_tex.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Textures/packaged/box_cereal_tex.png", "Assets/Art/3D/Items/Packaged/box_cereal_tex.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Textures/packaged/box_pasta_tex.png", "Assets/Art/3D/Items/Packaged/box_pasta_tex.png", ref movedCount);

        // Packaged Materials
        MoveAsset("Assets/Kitchen Assets/Textures/packaged/Materials/bag_chips_tex.mat", "Assets/Materials/Items/bag_chips_tex.mat", ref movedCount);

        // --- Move Textures/Produce ---
        MoveAsset("Assets/Kitchen Assets/Textures/produce/fruit_apple_tex.png", "Assets/Art/3D/Items/Produce/fruit_apple_tex.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Textures/produce/fruit_lemon_tex.png", "Assets/Art/3D/Items/Produce/fruit_lemon_tex.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Textures/produce/veg_carrot_tex.png", "Assets/Art/3D/Items/Produce/veg_carrot_tex.png", ref movedCount);

        // Produce Materials
        MoveAsset("Assets/Kitchen Assets/Textures/produce/Materials/veg_carrot_tex.mat", "Assets/Materials/Items/veg_carrot_tex.mat", ref movedCount);

        // --- Move Textures/Spices ---
        MoveAsset("Assets/Kitchen Assets/Textures/spices/condiment_ketchup_tex.png", "Assets/Art/3D/Items/Jars/condiment_ketchup_tex.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Textures/spices/spice_jar_paprika_tex.png", "Assets/Art/3D/Items/Jars/spice_jar_paprika_tex.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Textures/spices/spice_jar_pepper_tex.png", "Assets/Art/3D/Items/Jars/spice_jar_pepper_tex.png", ref movedCount);

        // Spice Materials
        MoveAsset("Assets/Kitchen Assets/Textures/spices/Materials/spice_jar_paprika_tex.mat", "Assets/Materials/Items/spice_jar_paprika_tex.mat", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Textures/spices/Materials/spice_jar_pepper_tex.mat", "Assets/Materials/Items/spice_jar_pepper_tex.mat", ref movedCount);

        // --- Move Meshes/Assets (Kitchenware, Spices, DryGoods, Refrigerated) ---
        // Kitchenware
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/bowl.obj", "Assets/Art/3D/Items/Kitchenware/bowl.obj", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/fork.obj", "Assets/Art/3D/Items/Kitchenware/fork.obj", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/glass.obj", "Assets/Art/3D/Items/Kitchenware/glass.obj", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/grinder_pepper.obj", "Assets/Art/3D/Items/Kitchenware/grinder_pepper.obj", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/grinder_salt.obj", "Assets/Art/3D/Items/Kitchenware/grinder_salt.obj", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/knife.obj", "Assets/Art/3D/Items/Kitchenware/knife.obj", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/mug.obj", "Assets/Art/3D/Items/Kitchenware/mug.obj", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/pan_small.obj", "Assets/Art/3D/Items/Kitchenware/pan_small.obj", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/pan_medium.obj", "Assets/Art/3D/Items/Kitchenware/pan_medium.obj", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/pan_large.obj", "Assets/Art/3D/Items/Kitchenware/pan_large.obj", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/plate.obj", "Assets/Art/3D/Items/Kitchenware/plate.obj", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/pot_small.obj", "Assets/Art/3D/Items/Kitchenware/pot_small.obj", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/pot_medium.obj", "Assets/Art/3D/Items/Kitchenware/pot_medium.obj", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/pot_large.obj", "Assets/Art/3D/Items/Kitchenware/pot_large.obj", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/spoon.obj", "Assets/Art/3D/Items/Kitchenware/spoon.obj", ref movedCount);

        // Kitchenware textures
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/bowl_tex.png", "Assets/Art/3D/Items/Kitchenware/bowl_tex.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/fork_tex.png", "Assets/Art/3D/Items/Kitchenware/fork_tex.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/glass_tex.png", "Assets/Art/3D/Items/Kitchenware/glass_tex.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/knife_tex.png", "Assets/Art/3D/Items/Kitchenware/knife_tex.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/mug_tex.png", "Assets/Art/3D/Items/Kitchenware/mug_tex.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/pan_small_tex.png", "Assets/Art/3D/Items/Kitchenware/pan_small_tex.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/pan_medium_tex.png", "Assets/Art/3D/Items/Kitchenware/pan_medium_tex.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/pan_large_tex.png", "Assets/Art/3D/Items/Kitchenware/pan_large_tex.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/spoon_tex.png", "Assets/Art/3D/Items/Kitchenware/spoon_tex.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/pot_small_tex.png", "Assets/Art/3D/Items/Kitchenware/pot_small_tex.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/pot_medium_tex.png", "Assets/Art/3D/Items/Kitchenware/pot_medium_tex.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/pot_large_tex.png", "Assets/Art/3D/Items/Kitchenware/pot_large_tex.png", ref movedCount);

        // Spices (move to Jars)
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/spice_chili.obj", "Assets/Art/3D/Items/Jars/spice_chili.obj", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/spice_cinnamon.obj", "Assets/Art/3D/Items/Jars/spice_cinnamon.obj", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/spice_cumin.obj", "Assets/Art/3D/Items/Jars/spice_cumin.obj", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/spice_garlic.obj", "Assets/Art/3D/Items/Jars/spice_garlic.obj", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/spice_ginger.obj", "Assets/Art/3D/Items/Jars/spice_ginger.obj", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/spice_oregano.obj", "Assets/Art/3D/Items/Jars/spice_oregano.obj", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/spice_paprika.obj", "Assets/Art/3D/Items/Jars/spice_paprika.obj", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/spice_turmeric.obj", "Assets/Art/3D/Items/Jars/spice_turmeric.obj", ref movedCount);

        // Dry Goods
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/dry_cereal.obj", "Assets/Art/3D/Items/DryGoods/dry_cereal.obj", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/dry_flour.obj", "Assets/Art/3D/Items/DryGoods/dry_flour.obj", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/dry_oats.obj", "Assets/Art/3D/Items/DryGoods/dry_oats.obj", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/dry_pasta.obj", "Assets/Art/3D/Items/DryGoods/dry_pasta.obj", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/dry_rice.obj", "Assets/Art/3D/Items/DryGoods/dry_rice.obj", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/dry_sugar.obj", "Assets/Art/3D/Items/DryGoods/dry_sugar.obj", ref movedCount);

        // Dry Goods textures
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/dry_cereal_tex.png", "Assets/Art/3D/Items/DryGoods/dry_cereal_tex.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/dry_flour_tex.png", "Assets/Art/3D/Items/DryGoods/dry_flour_tex.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/dry_oats_tex.png", "Assets/Art/3D/Items/DryGoods/dry_oats_tex.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/dry_pasta_tex.png", "Assets/Art/3D/Items/DryGoods/dry_pasta_tex.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/dry_rice_tex.png", "Assets/Art/3D/Items/DryGoods/dry_rice_tex.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/dry_sugar_tex.png", "Assets/Art/3D/Items/DryGoods/dry_sugar_tex.png", ref movedCount);

        // Refrigerated items
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/fridge_butter.obj", "Assets/Art/3D/Items/Refrigerated/fridge_butter.obj", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/fridge_cheese.obj", "Assets/Art/3D/Items/Refrigerated/fridge_cheese.obj", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/fridge_cream.obj", "Assets/Art/3D/Items/Refrigerated/fridge_cream.obj", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/fridge_eggs.obj", "Assets/Art/3D/Items/Refrigerated/fridge_eggs.obj", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/fridge_juice.obj", "Assets/Art/3D/Items/Refrigerated/fridge_juice.obj", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/fridge_ketchup.obj", "Assets/Art/3D/Items/Refrigerated/fridge_ketchup.obj", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/fridge_leftovers.obj", "Assets/Art/3D/Items/Refrigerated/fridge_leftovers.obj", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/fridge_mayo.obj", "Assets/Art/3D/Items/Refrigerated/fridge_mayo.obj", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/fridge_milk.obj", "Assets/Art/3D/Items/Refrigerated/fridge_milk.obj", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/fridge_oj.obj", "Assets/Art/3D/Items/Refrigerated/fridge_oj.obj", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/fridge_yoghurt.obj", "Assets/Art/3D/Items/Refrigerated/fridge_yoghurt.obj", ref movedCount);

        // Refrigerated textures
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/fridge_butter_tex.png", "Assets/Art/3D/Items/Refrigerated/fridge_butter_tex.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/fridge_cheese_tex.png", "Assets/Art/3D/Items/Refrigerated/fridge_cheese_tex.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/fridge_cream_tex.png", "Assets/Art/3D/Items/Refrigerated/fridge_cream_tex.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/fridge_eggs_tex.png", "Assets/Art/3D/Items/Refrigerated/fridge_eggs_tex.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/fridge_juice_tex.png", "Assets/Art/3D/Items/Refrigerated/fridge_juice_tex.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/fridge_ketchup_tex.png", "Assets/Art/3D/Items/Refrigerated/fridge_ketchup_tex.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/fridge_leftovers_tex.png", "Assets/Art/3D/Items/Refrigerated/fridge_leftovers_tex.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/fridge_mayo_tex.png", "Assets/Art/3D/Items/Refrigerated/fridge_mayo_tex.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/fridge_milk_tex.png", "Assets/Art/3D/Items/Refrigerated/fridge_milk_tex.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/fridge_oj_tex.png", "Assets/Art/3D/Items/Refrigerated/fridge_oj_tex.png", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Meshes/Assets/fridge_yoghurt_tex.png", "Assets/Art/3D/Items/Refrigerated/fridge_yoghurt_tex.png", ref movedCount);

        // --- Move Base Materials to Materials/Base ---
        MoveAsset("Assets/Base Materials/Black.mat", "Assets/Materials/Base/Black.mat", ref movedCount);
        MoveAsset("Assets/Base Materials/Blue Group Spices.mat", "Assets/Materials/Base/Blue Group Spices.mat", ref movedCount);
        MoveAsset("Assets/Base Materials/Dark.mat", "Assets/Materials/Base/Dark.mat", ref movedCount);
        MoveAsset("Assets/Base Materials/defaultMat.mat", "Assets/Materials/Base/defaultMat.mat", ref movedCount);
        MoveAsset("Assets/Base Materials/Glass.mat", "Assets/Materials/Base/Glass.mat", ref movedCount);
        MoveAsset("Assets/Base Materials/Panel.mat", "Assets/Materials/Base/Panel.mat", ref movedCount);
        MoveAsset("Assets/Base Materials/Plastic White.mat", "Assets/Materials/Base/Plastic White.mat", ref movedCount);
        MoveAsset("Assets/Base Materials/Red Group Spices.mat", "Assets/Materials/Base/Red Group Spices.mat", ref movedCount);
        MoveAsset("Assets/Base Materials/Red.mat", "Assets/Materials/Base/Red.mat", ref movedCount);
        MoveAsset("Assets/Base Materials/Sponge.mat", "Assets/Materials/Base/Sponge.mat", ref movedCount);
        MoveAsset("Assets/Base Materials/White.mat", "Assets/Materials/Base/White.mat", ref movedCount);
        MoveAsset("Assets/Base Materials/Wood.mat", "Assets/Materials/Base/Wood.mat", ref movedCount);

        // --- Move Render Textures to RuntimeTextures ---
        MoveAsset("Assets/Base Materials/Can.renderTexture", "Assets/RuntimeTextures/Can.renderTexture", ref movedCount);
        MoveAsset("Assets/Base Materials/DryGoods.renderTexture", "Assets/RuntimeTextures/DryGoods.renderTexture", ref movedCount);
        MoveAsset("Assets/Base Materials/SpiceLabel.renderTexture", "Assets/RuntimeTextures/SpiceLabel.renderTexture", ref movedCount);

        // --- Move SpiceLabels PNGs ---
        string[] spiceLabels = new string[]
        {
            "Basil", "Cayenne", "Chili_Flakes", "Chili_Powder", "Coriander",
            "Cumin", "Garam_Masala", "Garlic", "Ginger", "MSG",
            "Oregano", "Pepper", "Rosemary", "Thyme", "Turmeric", "White_Pepper"
        };
        foreach (string label in spiceLabels)
        {
            MoveAsset($"Assets/SpiceLabels/{label}.png", $"Assets/Art/2D/Labels/{label}.png", ref movedCount);
        }

        // --- Move Runtime Render Textures Spice Sprites ---
        MoveAsset("Assets/Kitchen Assets/Runtime Render Textures/Spice Sprites", "Assets/RuntimeTextures/SpiceLabels/Spice Sprites", ref movedCount);

        // --- Move DryGoods PNGs to Art/2D/DryGoods ---
        MoveAsset("Assets/DryGoods/almond_flour.png", "Assets/Art/2D/DryGoods/almond_flour.png", ref movedCount);
        MoveAsset("Assets/DryGoods/bulgur.png", "Assets/Art/2D/DryGoods/bulgur.png", ref movedCount);
        MoveAsset("Assets/DryGoods/flour.png", "Assets/Art/2D/DryGoods/flour.png", ref movedCount);
        MoveAsset("Assets/DryGoods/lentils.png", "Assets/Art/2D/DryGoods/lentils.png", ref movedCount);
        MoveAsset("Assets/DryGoods/penne.png", "Assets/Art/2D/DryGoods/penne.png", ref movedCount);
        MoveAsset("Assets/DryGoods/quinoa.png", "Assets/Art/2D/DryGoods/quinoa.png", ref movedCount);
        MoveAsset("Assets/DryGoods/rice.png", "Assets/Art/2D/DryGoods/rice.png", ref movedCount);
        MoveAsset("Assets/DryGoods/spaghetti.png", "Assets/Art/2D/DryGoods/spaghetti.png", ref movedCount);

        // --- Move Furniture Prefabs ---
        MoveAsset("Assets/Kitchen Assets/Prefabs/Closed Cabinet.prefab", "Assets/Prefabs/Furniture/Closed Cabinet.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs/Drawers.prefab", "Assets/Prefabs/Furniture/Drawers.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs/Fridge.prefab", "Assets/Prefabs/Furniture/Fridge.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs/Oven.prefab", "Assets/Prefabs/Furniture/Oven.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs/Open Cabinet.prefab", "Assets/Prefabs/Furniture/Open Cabinet.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs/Top Shelf.prefab", "Assets/Prefabs/Furniture/Top Shelf.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs/Top Shelf with Doors .prefab", "Assets/Prefabs/Furniture/Top Shelf with Doors .prefab", ref movedCount);

        // --- Move Item Prefabs ---
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/Beans.prefab", "Assets/Prefabs/Items/Beans.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/Chickpeas.prefab", "Assets/Prefabs/Items/Chickpeas.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/Coconut.prefab", "Assets/Prefabs/Items/Coconut.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/Corn.prefab", "Assets/Prefabs/Items/Corn.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/Mushroom.prefab", "Assets/Prefabs/Items/Mushroom.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/Peaches.prefab", "Assets/Prefabs/Items/Peaches.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/Peas.prefab", "Assets/Prefabs/Items/Peas.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/Soup.prefab", "Assets/Prefabs/Items/Soup.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/Tomato.prefab", "Assets/Prefabs/Items/Tomato.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/Tuna.prefab", "Assets/Prefabs/Items/Tuna.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/bowl.prefab", "Assets/Prefabs/Items/bowl.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/Cloth.prefab", "Assets/Prefabs/Items/Cloth.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/dry_cereal.prefab", "Assets/Prefabs/Items/dry_cereal.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/dry_flour.prefab", "Assets/Prefabs/Items/dry_flour.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/dry_oats.prefab", "Assets/Prefabs/Items/dry_oats.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/dry_pasta.prefab", "Assets/Prefabs/Items/dry_pasta.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/dry_rice.prefab", "Assets/Prefabs/Items/dry_rice.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/dry_sugar.prefab", "Assets/Prefabs/Items/dry_sugar.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/Fork.prefab", "Assets/Prefabs/Items/Fork.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/fridge_butter.prefab", "Assets/Prefabs/Items/fridge_butter.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/fridge_cheese.prefab", "Assets/Prefabs/Items/fridge_cheese.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/fridge_cream.prefab", "Assets/Prefabs/Items/fridge_cream.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/fridge_eggs.prefab", "Assets/Prefabs/Items/fridge_eggs.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/fridge_juice.prefab", "Assets/Prefabs/Items/fridge_juice.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/fridge_ketchup.prefab", "Assets/Prefabs/Items/fridge_ketchup.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/fridge_leftovers.prefab", "Assets/Prefabs/Items/fridge_leftovers.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/fridge_mayo.prefab", "Assets/Prefabs/Items/fridge_mayo.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/fridge_milk.prefab", "Assets/Prefabs/Items/fridge_milk.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/fridge_oj.prefab", "Assets/Prefabs/Items/fridge_oj.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/fridge_yoghurt.prefab", "Assets/Prefabs/Items/fridge_yoghurt.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/glass.prefab", "Assets/Prefabs/Items/glass.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/grinder_pepper.prefab", "Assets/Prefabs/Items/grinder_pepper.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/grinder_salt.prefab", "Assets/Prefabs/Items/grinder_salt.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/Kiwi.prefab", "Assets/Prefabs/Items/Kiwi.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/Knife.prefab", "Assets/Prefabs/Items/Knife.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/Lemon.prefab", "Assets/Prefabs/Items/Lemon.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/Mug.prefab", "Assets/Prefabs/Items/Mug.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/Orange.prefab", "Assets/Prefabs/Items/Orange.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/Pan.prefab", "Assets/Prefabs/Items/Pan.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/plate.prefab", "Assets/Prefabs/Items/plate.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/pot_large.prefab", "Assets/Prefabs/Items/pot_large.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/Sponge.prefab", "Assets/Prefabs/Items/Sponge.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/spice_chili.prefab", "Assets/Prefabs/Items/spice_chili.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/spice_cinnamon.prefab", "Assets/Prefabs/Items/spice_cinnamon.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/spice_cumin.prefab", "Assets/Prefabs/Items/spice_cumin.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/spice_garlic.prefab", "Assets/Prefabs/Items/spice_garlic.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/spice_ginger.prefab", "Assets/Prefabs/Items/spice_ginger.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/spice_oregano.prefab", "Assets/Prefabs/Items/spice_oregano.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/spice_paprika.prefab", "Assets/Prefabs/Items/spice_paprika.prefab", ref movedCount);
        MoveAsset("Assets/Kitchen Assets/Prefabs Items/spice_turmeric.prefab", "Assets/Prefabs/Items/spice_turmeric.prefab", ref movedCount);

        // Phase 4: Delete empty folders
        Debug.Log("\n=== Phase 4: Deleting empty folders ===");
        DeleteEmptyFolder("Assets/Kitchen Assets", ref deletedCount, ref errors);
        DeleteEmptyFolder("Assets/DryGoods", ref deletedCount, ref errors);
        DeleteEmptyFolder("Assets/Base Materials", ref deletedCount, ref errors);
        DeleteEmptyFolder("Assets/SpiceLabels", ref deletedCount, ref errors);

        // Summary
        Debug.Log($"\n=== Reorganization Complete ===");
        Debug.Log($"Assets moved: {movedCount}");
        Debug.Log($"Items deleted: {deletedCount}");
        if (errors.Count > 0)
        {
            Debug.LogError("Errors encountered:");
            foreach (string err in errors)
            {
                Debug.LogError($"  - {err}");
            }
        }
        else
        {
            Debug.Log("No errors!");
        }

        // Refresh AssetDatabase
        AssetDatabase.Refresh();
    }

    private static bool AssetExists(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Object>(path) != null;
    }

    private static void CreateFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string result = AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(path), System.IO.Path.GetFileName(path));
            if (!string.IsNullOrEmpty(result))
            {
                Debug.Log($"Created folder: {path}");
            }
        }
    }

    private static void MoveAsset(string source, string dest, ref int count)
    {
        if (AssetExists(source) && !AssetExists(dest))
        {
            string result = AssetDatabase.MoveAsset(source, dest);
            if (string.IsNullOrEmpty(result))
            {
                count++;
                Debug.Log($"Moved: {source} -> {dest}");
            }
            else
            {
                Debug.LogError($"Failed to move {source}: {result}");
            }
        }
        else if (AssetExists(source) && AssetExists(dest))
        {
            Debug.LogWarning($"Skipped (already exists): {source} -> {dest}");
        }
        else if (!AssetExists(source))
        {
            Debug.LogWarning($"Skipped (source not found): {source}");
        }
    }

    private static bool DeleteAsset(string path)
    {
        return AssetDatabase.DeleteAsset(path);
    }

    private static void DeleteEmptyFolder(string path, ref int count, ref List<string> errors)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        // Check if folder is empty
        string[] contents = AssetDatabase.FindAssets("*", new string[] { path });
        if (contents.Length == 0)
        {
            if (DeleteAsset(path))
            {
                count++;
                Debug.Log($"Deleted empty folder: {path}");
            }
        }
        else
        {
            Debug.LogWarning($"Folder not empty, skipping: {path} ({contents.Length} items)");
        }
    }
}
