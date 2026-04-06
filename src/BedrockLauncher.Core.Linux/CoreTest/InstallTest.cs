using System;
using System.Collections.Generic;
using System.Text;
using BedrockLauncher.Core;
using BedrockLauncher.Core.CoreOption;
using BedrockLauncher.Core.Linux;
using BedrockLauncher.Core.VersionJsons;

namespace CoreTest
{
	[TestClass]
	public sealed class InstallTest
	{
		[TestMethod]
		public void Test()
		{
			var bedrockCore = new BedrockCore();
			var localGamePackageOptions = new LocalGamePackageOptions()
			{

				Type = MinecraftBuildTypeVersion.GDK,
				GameTypeVersion = MinecraftGameTypeVersion.Release,
				InstallDstFolder = Path.GetFullPath("./Test7829"),
				GameName = "88991",
				FileFullPath = @"D:\Windows11\Download\MICROSOFT.MINECRAFTUWP_1.26.1004.0_x64__8wekyb3d8bbwe.msixvc"
			};
			bedrockCore.InstallPackageAsync(localGamePackageOptions).Wait();
			//var launchOptions = new LaunchOptions()
			//{
			//	GameFolder = Path.GetFullPath("./Test78"),
			//	GameType = MinecraftGameTypeVersion.Release,
			//	MinecraftBuildType = MinecraftBuildTypeVersion.UWP,
			//};
			//var process = bedrockCore.LaunchGameAsync(launchOptions).Result;
			//Assert.IsNotNull(process);
		}
	}
}
