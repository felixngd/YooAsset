﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YooAsset;

/// <summary>
/// 创建文件下载器
/// </summary>
public class FsmCreateDownloader : IFsmNode
{
	public string Name { private set; get; } = nameof(FsmCreateDownloader);

	void IFsmNode.OnEnter()
	{
		Debug.Log("创建补丁下载器！");
		PatchEventDispatcher.SendPatchStepsChangeMsg(EPatchStates.CreateDownloader);
		GameBoot.Instance.StartCoroutine(CreateDownloader());
	}
	void IFsmNode.OnUpdate()
	{
	}
	void IFsmNode.OnExit()
	{
	}

	IEnumerator CreateDownloader()
	{
		yield return new WaitForSecondsRealtime(0.5f);

		int downloadingMaxNum = 10;
		int failedTryAgain = 3;
		PatchManager.Downloader = YooAssets.CreatePatchDownloader(downloadingMaxNum, failedTryAgain);
		if (PatchManager.Downloader.TotalDownloadCount == 0)
		{
			Debug.Log("没有发现需要下载的资源");
			FsmManager.Transition(nameof(FsmPatchDone));
		}
		else
		{
			Debug.Log($"一共发现了{PatchManager.Downloader.TotalDownloadCount}个资源需要更新下载。");

			// 发现新更新文件后，挂起流程系统
			// 注意：开发者需要在下载前检测磁盘空间不足
			int totalDownloadCount = PatchManager.Downloader.TotalDownloadCount;
			long totalDownloadBytes = PatchManager.Downloader.TotalDownloadBytes;
			PatchEventDispatcher.SendFoundUpdateFilesMsg(totalDownloadCount, totalDownloadBytes);
		}
	}
}