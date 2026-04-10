using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class PermutationListGenerator : MonoBehaviour
{

    [SerializeField]
    private bool generateFiles = true; // Set to false to skip file generation and just log the permutations
    private void Start()
    {
        if (generateFiles)
         GenerateAndSavePermutations();
    }

    private void GenerateAndSavePermutations()
    {
        // Generate all permutations of 3 items with indices 0, 1, 2, 3
        List<(int, int, int)> permutations = new List<(int, int, int)>();

        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                for (int k = 0; k < 4; k++)
                {
                    permutations.Add((i, j, k));
                }
            }
        }

        // Create Experiment Data folder if it doesn't exist
        string experimentDataPath = Path.Combine(Application.persistentDataPath, "Experiment Data");
        if (!Directory.Exists(experimentDataPath))
        {
            Directory.CreateDirectory(experimentDataPath);
        }

        // Save to CSV
        string csvPath = Path.Combine(experimentDataPath, "permutations.csv");
        SavePermutationsToCSV(permutations, csvPath);

        Debug.Log($"Permutations saved to: {csvPath}");
        Debug.Log($"Total permutations: {permutations.Count}");
    }

    private void SavePermutationsToCSV(List<(int, int, int)> permutations, string filePath)
    {
        using (StreamWriter writer = new StreamWriter(filePath))
        {
            // Write header
            writer.WriteLine("Index1,Index2,Index3");

            // Write permutations
            foreach (var perm in permutations)
            {
                writer.WriteLine($"{perm.Item1},{perm.Item2},{perm.Item3}");
            }
        }
    }
}
