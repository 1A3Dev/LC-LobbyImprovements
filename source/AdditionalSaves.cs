using HarmonyLib;
// using System.Collections.Generic;
// using System.Linq;
// using System.Text.RegularExpressions;
// using UnityEngine;
// using UnityEngine.UI;
// using Object = UnityEngine.Object;

namespace LobbyImprovements
{
    [HarmonyPatch]
    public class AdditionalSaves
    {
        //[HarmonyPatch(typeof(SaveFileUISlot), "Awake")]
        //[HarmonyPostfix]
        //private static void SFUS_Awake(SaveFileUISlot __instance)
        //{
        //    if (__instance.fileNum >= 3)
        //    {
        //        __instance.fileString = $"LCSaveFile{__instance.fileNum + 1}";
        //    }
        //}

        //[HarmonyPatch(typeof(GameNetworkManager), "Start")]
        //[HarmonyPostfix]
        //private static void GNM_Start(GameNetworkManager __instance)
        //{
        //    if (__instance.saveFileNum >= 3)
        //    {
        //        __instance.currentSaveFileName = $"LCSaveFile{__instance.saveFileNum + 1}";
        //    }
        //}

        //[HarmonyPatch(typeof(DeleteFileButton), "DeleteFile")]
        //[HarmonyPrefix]
        //private static bool DFB_DeleteFile(DeleteFileButton __instance)
        //{
        //    if (__instance.fileToDelete >= 3)
        //    {
        //        string text = $"LCSaveFile{__instance.fileToDelete + 1}";

        //        MenuManager menuManager = Object.FindFirstObjectByType<MenuManager>();
        //        if (ES3.FileExists(text))
        //        {
        //            ES3.DeleteFile(text);
        //            menuManager.MenuAudio.PlayOneShot(__instance.deleteFileSFX);
        //        }
        //        SaveFileUISlot[] array = Object.FindObjectsByType<SaveFileUISlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        //        for (int i = 0; i < array.Length; i++)
        //        {
        //            if (array[i].fileNum == __instance.fileToDelete)
        //            {
        //                array[i].fileNotCompatibleAlert.enabled = false;
        //                menuManager.filesCompatible[__instance.fileToDelete] = true;
        //            }
        //        }

        //        return false;
        //    }

        //    return true;
        //}

        //[HarmonyPatch(typeof(MenuManager), "Start")]
        //[HarmonyPostfix]
        //private static void MM_Start(MenuManager __instance)
        //{
        //    if (__instance.isInitScene) return;

        //    List<string> saveFiles = new List<string>();
        //    foreach (string file in ES3.GetFiles())
        //    {
        //        if (Regex.IsMatch(file, @"^LCSaveFile\d+$") && ES3.FileExists(file))
        //            saveFiles.Add(file);
        //    }
        //    saveFiles.Sort((a, b) =>
        //    {
        //        int fileNumA = int.Parse(a.Substring(10));
        //        int fileNumB = int.Parse(b.Substring(10));
        //        return fileNumA.CompareTo(fileNumB);
        //    });

        //    GameObject firstFileObj = GameObject.Find("Canvas/MenuContainer/LobbyHostSettings/FilesPanel/File1");
        //    SaveFileUISlot[] array = Object.FindObjectsByType<SaveFileUISlot>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
        //    int[] fileNums = array.Select(a => a.fileNum).ToArray();
        //    foreach (string file in saveFiles)
        //    {
        //        PluginLoader.StaticLogger.LogInfo("Found save file: " + file);
        //        int fileNum = int.Parse(file.Substring(10));
        //        if (!fileNums.Contains(fileNum))
        //        {
        //            GameObject newFile = GameObject.Instantiate(firstFileObj, firstFileObj.transform.parent);
        //            newFile.name = $"File{fileNum}";
        //            SaveFileUISlot saveSlot = newFile.GetComponent<SaveFileUISlot>();
        //            saveSlot.fileButton = newFile.GetComponent<Button>();
        //            saveSlot.buttonAnimator = newFile.GetComponent<Animator>();
        //            saveSlot.fileNum = fileNum - 1;
        //        }
        //    }
        //}
    }
}
