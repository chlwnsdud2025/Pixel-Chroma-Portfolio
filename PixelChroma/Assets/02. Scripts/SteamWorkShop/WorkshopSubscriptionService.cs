using Steamworks;
using System.Collections.Generic;
using UnityEngine;
using PixelChroma;  // GameManager 접근용

public static class WorkshopSubscriptionService
{
    public static List<WorkshopMapEntry> GetSubscribedMaps()
    {
        var result = new List<WorkshopMapEntry>();

        if (!GameManager.SteamInitialized)  // 수정
            return result;

        uint count = SteamUGC.GetNumSubscribedItems();
        if (count == 0)
            return result;

        var ids = new PublishedFileId_t[count];
        SteamUGC.GetSubscribedItems(ids, count);

        for (int i = 0; i < ids.Length; i++)
        {
            var state = (EItemState)SteamUGC.GetItemState(ids[i]);
            bool installed = (state & EItemState.k_EItemStateInstalled) != 0;

            string path = "";
            if (installed)
            {
                SteamUGC.GetItemInstallInfo(ids[i], out _, out path, 1024, out _);
            }

            result.Add(new WorkshopMapEntry(ids[i].m_PublishedFileId, path, installed));
        }

        return result;
    }

    public static void RequestDownload(ulong fileId)
    {
        if (!GameManager.SteamInitialized)  // 수정
            return;

        SteamUGC.DownloadItem(new PublishedFileId_t(fileId), true);
    }

    public static string GetInstallPath(ulong fileId)
    {
        if (!GameManager.SteamInitialized)  // 수정
            return string.Empty;

        var id = new PublishedFileId_t(fileId);
        var state = (EItemState)SteamUGC.GetItemState(id);

        if ((state & EItemState.k_EItemStateInstalled) == 0)
            return string.Empty;

        if (SteamUGC.GetItemInstallInfo(id, out _, out string path, 1024, out _))
            return path;

        return string.Empty;
    }

    public static bool IsSubscribed(ulong fileId)
    {
        if (!GameManager.SteamInitialized) return false;  // 수정
        var state = (EItemState)SteamUGC.GetItemState(new PublishedFileId_t(fileId));
        return (state & EItemState.k_EItemStateSubscribed) != 0;
    }

    public static bool IsInstalled(ulong fileId)
    {
        if (!GameManager.SteamInitialized) return false;  // 수정
        var state = (EItemState)SteamUGC.GetItemState(new PublishedFileId_t(fileId));
        return (state & EItemState.k_EItemStateInstalled) != 0;
    }

    public static void SubscribeItem(ulong fileId)
    {
        if (!GameManager.SteamInitialized) return;  // 수정
        SteamUGC.SubscribeItem(new PublishedFileId_t(fileId));
    }
}