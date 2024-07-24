using System.Text;
using MelonLoader;
using MelonLoader.Utils;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2Cpppb_H;
using PuzzleBoyManiax;

[assembly: MelonInfo(typeof(PBManiax), "Puzzle Boy Maniax Edition", "1.0.0", "pomariin")]
[assembly: MelonGame("アトラス", "smt3hd")]

namespace PuzzleBoyManiax;
public class PBManiax: MelonMod
{
    public static PBManiax? instance;
    public static readonly string configPath = Path.Combine(MelonEnvironment.UserDataDirectory, "ModsCfg", "PuzzleBoyManiax.cfg");
    private static MelonPreferences_Category cfgCategory = null!;
    private static MelonPreferences_Entry<short[]> cfg_stageOrder = null!;
    private static MelonPreferences_Entry<short[]> cfg_scriptOrder = null!;
    private static bool hasInit = false;

    private static void LoadCfg()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);

        if (cfgCategory == null)
        {
            cfgCategory = MelonPreferences.CreateCategory("PuzzleBoyManiax");
            cfg_stageOrder = cfgCategory.CreateEntry("stageOrder", new short[] {2,4,32,8,38,36,37,48,42,49}, "Stage order", "List of stages, from first to last");
            cfg_scriptOrder = cfgCategory.CreateEntry("scriptOrder", new short[] {0,-1,1,-1,2,-1,-1,3,-1,4}, "Script order", "ID of script associated with each stage");
        }

        cfgCategory.SetFilePath(configPath); // it also loads the category by default
        if (!File.Exists(configPath) ) 
        {
            instance!.LoggerInstance.Msg("Created new config file.");
            cfgCategory.SaveToFile();
        }
    }

    public override void OnInitializeMelon()
    {
        instance = this;
        LoadCfg();
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        if (!hasInit)
        {
            Patch.ReplaceTable();
            hasInit = true;
        }
    }

    public static void PrintCfg()
    {
        if (cfg_stageOrder.Value.Length == 0)
        {
            instance!.LoggerInstance.Warning("Error: stageOrder is set as empty. Can't read PuzzleBoyManiax.cfg");
            return;
        }
        if (cfg_scriptOrder.Value.Length == 0)
        {
            instance!.LoggerInstance.Warning("Warning: scriptOrder is set as empty in PuzzleBoyManiax.cfg.");
        }
        
        StringBuilder sb = new();
        sb.AppendLine($"Reading config file (length: {cfg_stageOrder.Value.Length})...");
        for (int i = 0; i < cfg_stageOrder.Value.Length; i++) 
        {
            sb.Append($"ID {i}: [stage = {cfg_stageOrder.Value[i]}");
            if (i <= cfg_scriptOrder.Value.Length - 1)
            {
                sb.Append($", label = {cfg_scriptOrder.Value[i]}");
            }
            sb.AppendLine("]");
        }
        instance!.LoggerInstance.Msg(sb.ToString());
    }

    public static void PrintDebug(int tableID)
    {
        StringBuilder sb = new();
        sb.AppendLine($"Reading in-game level table (length: {pbGame.pbLevelTable[tableID].Length})...");
        for (int i = 0; i < pbGame.pbLevelTable[tableID].Length; i++)
        {
            sb.AppendLine($"ID {i}: [stage = {pbGame.pbLevelTable[tableID][i].stage}, label = {pbGame.pbLevelTable[tableID][i].label}]");
        }
        instance!.LoggerInstance.Msg(sb.ToString());
    }

    private class Patch
    {
        public static bool IsStageValid(short val)
        {
            if (val < -1 | val > 49)
            {
                return false;
            }
            return true;
        }
        
        public static bool IsScriptValid(short val)
        {
            if (val < -1 | val > 5)
            {
                return false;
            }
            return true;
        }

        public static void ReplaceStages(Il2CppReferenceArray<pbLevelTable_t> tableArray)
        {
            int elements = tableArray.Length - 2; // no first and last id

            if (cfg_stageOrder.Value.Length == 0) 
            {
                instance!.LoggerInstance.Warning("Warning: stageOrder is empty. No changes made to level order");
                return;
            }

            if (cfg_scriptOrder.Value.Length > cfg_stageOrder.Value.Length)
            {
                instance!.LoggerInstance.Warning("Warning: scriptOrder has more elements than stageOrder");
            }

            if (cfg_stageOrder.Value.Length > elements)
            {
                instance!.LoggerInstance.Warning($"Warning: stageOrder contains more than {elements} stages. The last levels will be ignored");
            }

            for (int i = 0; i < elements ; i++ )
            {
                // ID 0 of tableArray has something to do with pbGame.GetAllStageClearBit()... we don't touch that
                // cfg_stageOrder needs to be offset by 1 to match tableArray
                int next = i + 1;

                // When we run out of stages set them to -1
                if (i >= cfg_stageOrder.Value.Length) 
                {
                    tableArray[next].stage = -1;
                    continue;
                }

                if (!IsStageValid(cfg_stageOrder.Value[i]))
                {
                    instance!.LoggerInstance.Warning($"Error: stage {cfg_stageOrder.Value[i]} is not allowed. Skipping replacement of ID {next}");
                    continue;
                }
                tableArray[next].stage = cfg_stageOrder.Value[i];
            }
        }
        
        public static void ReplaceScripts(Il2CppReferenceArray<pbLevelTable_t> tableArray)
        {
            int elements = tableArray.Length - 2;

            if (cfg_scriptOrder.Value.Length == 0) 
            {
                instance!.LoggerInstance.Warning("Warning: scriptOrder is empty. Missing values are set as -1");
            } 
            else if (cfg_scriptOrder.Value.Length < cfg_stageOrder.Value.Length)
            {
                instance!.LoggerInstance.Warning("Warning: stageOrder has more elements than scriptOrder. Missing values are set as -1");
            }

            for (int i = 0; i < elements ; i++ )
            {
                int next = i + 1;

                // When we run out of scripts set them to -1
                if (i >= cfg_scriptOrder.Value.Length)
                {
                    tableArray[next].label = -1;
                    continue;
                }

                if (!IsScriptValid(cfg_scriptOrder.Value[i]))
                {
                    instance!.LoggerInstance.Warning($"Error: script {cfg_scriptOrder.Value[i]} is not allowed. Defaulting to -1 for ID {next}");
                    tableArray[next].label = -1;
                    continue;
                }
                tableArray[next].label = cfg_scriptOrder.Value[i];
            }
        }

        // The main function
        // I wish I could actually replace the table, not just the values
        public static void ReplaceTable(int tableID = 0) // This technically supports changing the other 2 tables
        {
            if (cfg_stageOrder.Value.Length == 0 && cfg_scriptOrder.Value.Length == 0)
            {
                instance!.LoggerInstance.Error("Error: stageOrder and scriptOrder are both empty. No changes made");
                return;
            }

            Il2CppReferenceArray<pbLevelTable_t> replaced = pbGame.pbLevelTable[tableID];
            ReplaceStages(replaced);
            ReplaceScripts(replaced);
            instance!.LoggerInstance.Msg("Puzzle Boy has been patched successfully.");
        }

        // Calling this inside of Puzzle Boy potentially gets you bugged walls
        public static void DebugChangeLevel(int target, short stage, short script) // Table 0 only for now
        {
            int table = 0; 
            if (target <= 0 | target > 20) 
            {
                instance!.LoggerInstance.Warning($"Error: can't write in position {target} of pbLevelTable.");
                return;
            }

            if (!IsStageValid(stage)) 
            {
                instance!.LoggerInstance.Warning($"Error: stage {stage} is not allowed. Skipping replacement");
                return;
            }
            pbGame.pbLevelTable[table][target].stage = stage;

            if (!IsScriptValid(script))
            {
                instance!.LoggerInstance.Warning($"Error: script {script} is not allowed. Defaulting to -1");
                pbGame.pbLevelTable[table][target].label = -1;
                return;
            }
            pbGame.pbLevelTable[table][target].label = script;
        }
    }
}

