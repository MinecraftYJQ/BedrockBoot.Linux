using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using BedrockLauncher.Core.CoreOption;
using BedrockLauncher.Core.GdkDecode;
using BedrockLauncher.Core.Utils;

namespace BedrockLauncher.Core.Linux
{
	public enum MinecraftBuildTypeVersion
	{
		GDK,
		UWP,
		UNKNOWN
	}

	public enum MinecraftGameTypeVersion
	{
		Preview,
		Release,
		All,
	}
	public class BedrockCore
	{
		public BedrockCore()
		{
			
		}
		public async Task<bool> InstallPackageAsync(LocalGamePackageOptions options)
		{
			Directory.CreateDirectory(options.InstallDstFolder);
			if (options.Type == MinecraftBuildTypeVersion.GDK)
			{
				try
				{
					await Task.Run((async () =>
					{
						CikKey cik = new CikKey(options.GameTypeVersion switch
						{
							MinecraftGameTypeVersion.Release => _DEFINE_REF2.rel,
							MinecraftGameTypeVersion.Preview => _DEFINE_REF2.pre,
							_ => null
						});
						var msiXvdDecoder = new MsiXVDDecoder(cik);
						var msiXvdStream = new MsiXVDStream(options.FileFullPath);
						msiXvdStream.Parse();
						options.InstallStates?.Report(InstallStates.Extracting);
						await msiXvdStream.ExtractTaskAsync(Path.GetFullPath(options.InstallDstFolder), msiXvdDecoder,
							options.ExtractionProgress, options.CancellationToken.GetValueOrDefault());
						options.InstallStates?.Report(InstallStates.Extracted);

					}));
					return true;
				}
				catch
				{
					return false;
				}
			}
			else
			{
				return false;
			}
		}
		/// <summary>
		/// Retrieves the download URI for a package based on its metadata
		/// </summary>
		/// <returns>The resolved download URI as a string</returns>
		/// <exception cref="BedrockCoreNoAvailbaleVersionUri">Thrown when no available URI is found for the metadata</exception>
		public async Task<string> GetPackageUri(BuildInfo buildInfo, Architecture devicesArch)
		{
			var find = buildInfo.Variations.Find((variation => variation.Arch == devicesArch));
			if (find == null)
				throw new BedrockCoreException($"Unable to find {devicesArch} Version");
			if (find.MetaData.Count == 0)
				throw new BedrockCoreNoAvailbaleVersionUri("There is no available Uri to download");
			return await GetPackageUriInside(find.MetaData.Last());
		}
		private async Task<string> GetPackageUriInside([NotNull] string metadata)
		{
			if (metadata.StartsWith("http"))
				return metadata;

			try
			{
				var uri = await UpdateIDHelper.GetUriAsync(metadata);
				if (string.IsNullOrEmpty(uri))
					throw new BedrockCoreNoAvailbaleVersionUri("There is no available uri for this");
				return uri;
			}
			catch
			{
				throw;
			}
		}
	}
}
