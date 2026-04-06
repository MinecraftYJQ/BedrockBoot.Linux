#pragma warning disable
using System.Diagnostics;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualBasic;
using BedrockLauncher.Core.Utils;

namespace BedrockLauncher.Core.GdkDecode;

public class MsiXVDStream : IDisposable
{
	private const ulong XVD_HEADER_INCL_SIGNATURE_SIZE = 0x3000;
	public string[] EncryptionKeys { get; private set; }
	public MsiXVDHeader Header { get; private set; }
	public bool IsEncrypted { get; private set; }
	public readonly BinaryReader Reader;
	public FileStream XvdFileStream;
	private ulong HashTreePageCount;
	private ulong HashTreePageOffset;
	private ulong MutableDataOffset;
	private bool DataIntegrity;
	private bool Resiliency;
	private ulong HashTreeLevels;
	private ulong XvdUserDataOffset;
	private bool HasSegmentMetadata;
	private SegmentMetadataHeader SegmentMetadataHeaders;
	private SegmentsAbout[] Segments;
	private string[] _segmentPaths;
	public XvcInfo XvcInfo;
	private XvcRegionHeader[] XvcRegions;
	private XvcUpdateSegment[] XvcUpdateSegments;
	private XvcRegionSpecifier[] XvcRegionSpecifiers;
	private UserDataHeader UserDataHeader;
	private bool HasUserDataFile;
	private UserDataPackageFilesHeader UserDataPackageFiles;
	private Dictionary<string, UserDataPackageFileEntry> UserDataPackages = new Dictionary<string, UserDataPackageFileEntry>();
	private Dictionary<string, byte[]> UserDataPackageContents = new Dictionary<string, byte[]>();
	private int HashEntryLength;
	
	// 添加路径规范化标志
	private bool _isUnixLikeSystem;
	
	public MsiXVDStream(string fileUri)
	{
		if (!File.Exists(fileUri))
			throw new FileNotFoundException("Can't found the file");
		XvdFileStream = File.Open(fileUri, FileMode.Open, FileAccess.ReadWrite);
		Reader = new BinaryReader(XvdFileStream);
		
		// 检测操作系统类型
		_isUnixLikeSystem = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || 
						   RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
	}
	
	public void Parse()
	{
		XvdFileStream.Position = 0;
		ParseFileHeader();
		Resiliency = Header.Volumes.HasFlag(MsiXVDVolumeAttributes.ResiliencyEnabled);
		DataIntegrity = !Header.Volumes.HasFlag(MsiXVDVolumeAttributes.DataIntegrityDisabled);
		HashTreePageCount = CalculateNumberHashPages(out HashTreeLevels,Header.NumberOfHashedPages,Resiliency);
		MutableDataOffset = Extensions.PageToOffset(Header.EmbeddedXvdPageCount) + XVD_HEADER_INCL_SIGNATURE_SIZE;
		HashTreePageOffset = Header.MutableDataLength + MutableDataOffset;
		XvdUserDataOffset = (DataIntegrity ? Extensions.PageToOffset(HashTreePageCount) : 0) + HashTreePageOffset;
		ParaseUserData();
		if (UserDataPackageContents.ContainsKey("SegmentMetadata.bin"))
		{
			ParseSegment();
		}
		ParseArea();
		List<string> strings = new List<string>();
		for (int i = 0; i < XvcInfo.EncryptionKeyIds.Length; i++)
		{
			var key = new Guid(XvcInfo.EncryptionKeyIds[i].KeyId).ToString();
			if (key == "00000000-0000-0000-0000-000000000000")
			{
				continue;
			}
			strings.Add(key);
		}

		EncryptionKeys = strings.ToArray();
	}
	
	private void ParseFileHeader()
	{
		var sizeOf = Marshal.SizeOf(typeof(MsiXVDHeader));
		var readBytes = Reader.ReadBytes(sizeOf);
		var header = Extensions.GetstructFromBytes<MsiXVDHeader>(readBytes);
		Header = header;
		IsEncrypted = !header.Volumes.HasFlag(MsiXVDVolumeAttributes.EncryptionDisabled);
		HashEntryLength = IsEncrypted ? 0x14 : 0x18;
	}
	
	private void ParaseUserData()
	{
		XvdFileStream.Position = (long)XvdUserDataOffset;

		var userDataBuffer = new byte[Header.UserDataLength];
		var bytesRead = XvdFileStream.Read(userDataBuffer.AsSpan());
		
		using var binaryReader = new BinaryReader(new MemoryStream(userDataBuffer));
		byte[] bytes = binaryReader.ReadBytes(Marshal.SizeOf(typeof(UserDataHeader)));
		this.UserDataHeader = Extensions.GetstructFromBytes<UserDataHeader>(bytes);
		if (UserDataHeader.Type == UserDataType.PackageFiles)
		{
			HasUserDataFile = true;

			binaryReader.BaseStream.Position = UserDataHeader.Length;
			byte[] bytes_ = binaryReader.ReadBytes(Marshal.SizeOf(typeof(UserDataPackageFilesHeader)));
			UserDataPackageFiles = Extensions.GetstructFromBytes<UserDataPackageFilesHeader>(bytes_);
			
			var fileEntriesCount = (int)UserDataPackageFiles.FileCount;

			UserDataPackages.EnsureCapacity(fileEntriesCount);

			foreach (var fileEntry in Extensions.GetstructArraysFromBytes<UserDataPackageFileEntry>(binaryReader.ReadBytes(Marshal.SizeOf(typeof(UserDataPackageFileEntry))*fileEntriesCount),fileEntriesCount))
			{
				binaryReader.BaseStream.Position = UserDataHeader.Length + fileEntry.Offset;
				var fileData = new byte[fileEntry.Size];
				_ = binaryReader.BaseStream.Read(fileData.AsSpan());
			
				UserDataPackages[fileEntry.FilePath] = fileEntry;
				UserDataPackageContents[fileEntry.FilePath] = fileData;
			}
		}
	}
	
	private void ParseArea()
	{
		var xvcInfoOffset = Extensions.PageToOffset(Header.UserDataPageCount) + XvdUserDataOffset;
		XvdFileStream.Position = (int)xvcInfoOffset;
		var xvcInfo = new byte[Header.XvcDataLength];
		var xvcInfoSpan = xvcInfo.AsSpan();
		var _ = XvdFileStream.Read(xvcInfo.AsSpan());
		using var xvcInfoReader = new BinaryReader(new MemoryStream(xvcInfo));
		XvcInfo = Extensions.GetstructFromBytes<XvcInfo>(xvcInfoReader.ReadBytes(Marshal.SizeOf(typeof(XvcInfo))));
		if (XvcInfo.Version>=1)
		{
			XvcRegions = Extensions.GetstructArraysFromBytes<XvcRegionHeader>(xvcInfoReader.ReadBytes((int)(Marshal.SizeOf(typeof(XvcRegionHeader))*(XvcInfo.RegionCount))),XvcInfo.RegionCount);
			XvcUpdateSegments = Extensions.GetstructArraysFromBytes<XvcUpdateSegment>(xvcInfoReader.ReadBytes((int)(Marshal.SizeOf(typeof(XvcUpdateSegment)) * (XvcInfo.UpdateSegmentCount))), XvcInfo.UpdateSegmentCount);

			if (XvcInfo.Version >= 2)
			{
				XvcRegionSpecifiers = Extensions.GetstructArraysFromBytes<XvcRegionSpecifier>(xvcInfoReader.ReadBytes((int)(Marshal.SizeOf(typeof(XvcRegionSpecifier)) * (XvcInfo.RegionSpecifierCount))), XvcInfo.RegionSpecifierCount);
			}
		}
	}
	
	private void ParseSegment()
	{
		var segmentMetadataData = UserDataPackageContents["SegmentMetadata.bin"];

		using var segmentMetadataStreamReader = new BinaryReader(new MemoryStream(segmentMetadataData));

		SegmentMetadataHeaders = Extensions.GetstructFromBytes<SegmentMetadataHeader>(segmentMetadataStreamReader.ReadBytes(Marshal.SizeOf(typeof(SegmentMetadataHeader))));
		HasSegmentMetadata = true;

		Segments = Extensions.GetstructArraysFromBytes<SegmentsAbout>(segmentMetadataStreamReader.ReadBytes(Marshal.SizeOf(typeof(SegmentsAbout))*(int)SegmentMetadataHeaders.SegmentCount), (int)SegmentMetadataHeaders.SegmentCount);
	
		_segmentPaths = new string[SegmentMetadataHeaders.SegmentCount];

		var segmentPathsStartOffset =
			SegmentMetadataHeaders.HeaderLength
			+ SegmentMetadataHeaders.SegmentCount * 0x10;

		for (int segmentIndex = 0; segmentIndex < Segments.Length; segmentIndex++)
		{
			var currentSegment = Segments[segmentIndex];

			segmentMetadataStreamReader.BaseStream.Position = segmentPathsStartOffset + currentSegment.PathOffset;

			var stringDataSpan = segmentMetadataStreamReader.ReadBytes(currentSegment.PathLength * 2).AsSpan();

			var rawPath = new string(MemoryMarshal.Cast<byte, char>(stringDataSpan));
			
			// 规范化路径 - 关键修复
			_segmentPaths[segmentIndex] = NormalizePath(rawPath);
		}
	}
	
	/// <summary>
	/// 规范化文件路径，使其符合当前操作系统的格式
	/// </summary>
	private string NormalizePath(string rawPath)
	{
		if (string.IsNullOrEmpty(rawPath))
			return rawPath;
		
		string normalizedPath = rawPath;
		
		// 1. 统一使用正斜杠作为内部表示
		normalizedPath = normalizedPath.Replace('\\', '/');
		
		// 2. 移除驱动器号前缀 (如 "C:", "D:" 等)
		if (normalizedPath.Contains(':'))
		{
			var colonIndex = normalizedPath.IndexOf(':');
			if (colonIndex >= 0 && colonIndex + 1 < normalizedPath.Length)
			{
				normalizedPath = normalizedPath.Substring(colonIndex + 1);
			}
		}
		
		// 3. 移除开头的路径分隔符
		normalizedPath = normalizedPath.TrimStart('/');
		
		// 4. 移除可能存在的 "Program Files/WindowsApps/" 等前缀
		var prefixesToRemove = new[] 
		{ 
			"Program Files/WindowsApps/",
			"Program Files (x86)/",
			"Windows/",
			"System32/"
		};
		
		foreach (var prefix in prefixesToRemove)
		{
			if (normalizedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			{
				normalizedPath = normalizedPath.Substring(prefix.Length);
				break;
			}
		}
		
		// 5. 清理多余的路径分隔符
		normalizedPath = CleanupPathSeparators(normalizedPath);
		
		// 6. 如果是 Unix 系统，保持正斜杠；如果是 Windows，转换为反斜杠
		if (!_isUnixLikeSystem)
		{
			normalizedPath = normalizedPath.Replace('/', '\\');
		}
		
		// 7. 移除路径中的无效字符
		normalizedPath = RemoveInvalidPathChars(normalizedPath);
		
		return normalizedPath;
	}
	
	/// <summary>
	/// 清理多余的路径分隔符
	/// </summary>
	private string CleanupPathSeparators(string path)
	{
		if (string.IsNullOrEmpty(path))
			return path;
		
		var result = new StringBuilder();
		bool lastWasSeparator = false;
		
		foreach (char c in path)
		{
			if (c == '/' || c == '\\')
			{
				if (!lastWasSeparator)
				{
					result.Append('/');
					lastWasSeparator = true;
				}
			}
			else
			{
				result.Append(c);
				lastWasSeparator = false;
			}
		}
		
		return result.ToString();
	}
	
	/// <summary>
	/// 移除路径中的无效字符
	/// </summary>
	private string RemoveInvalidPathChars(string path)
	{
		var invalidChars = Path.GetInvalidPathChars();
		var result = new StringBuilder();
		
		foreach (char c in path)
		{
			if (!invalidChars.Contains(c) || c == '/' || c == '\\')
			{
				result.Append(c);
			}
			else
			{
				result.Append('_');
			}
		}
		
		return result.ToString();
	}
	
	/// <summary>
	/// 确保路径安全性，防止路径遍历攻击
	/// </summary>
	private string GetSecureOutputPath(string outputDirectory, string relativePath)
	{
		// 组合完整路径
		string fullPath = Path.Combine(outputDirectory, relativePath);
		
		// 获取规范化后的完整路径
		fullPath = Path.GetFullPath(fullPath);
		
		// 获取输出目录的规范化路径
		string fullOutputDir = Path.GetFullPath(outputDirectory);
		
		// 确保输出路径在输出目录内
		if (!fullPath.StartsWith(fullOutputDir, StringComparison.OrdinalIgnoreCase))
		{
			throw new UnauthorizedAccessException($"Path traversal detected: {relativePath}");
		}
		
		return fullPath;
	}
	
	private static ulong CalculateNumberHashPages(out ulong hashTreeLevels, ulong hashedPagesCount, bool resilient)
	{
		const ulong PAGE_SIZE = 0x1000;
		const uint HASH_ENTRY_LENGTH = 0x18;
		const uint HASH_ENTRIES_IN_PAGE = (uint)(PAGE_SIZE / HASH_ENTRY_LENGTH); // 0xAA

		const uint DATA_BLOCKS_IN_LEVEL0_HASHTREE = HASH_ENTRIES_IN_PAGE; // 0xAA
		const uint DATA_BLOCKS_IN_LEVEL1_HASHTREE = HASH_ENTRIES_IN_PAGE * DATA_BLOCKS_IN_LEVEL0_HASHTREE; // 0x70E4
		const uint DATA_BLOCKS_IN_LEVEL2_HASHTREE = HASH_ENTRIES_IN_PAGE * DATA_BLOCKS_IN_LEVEL1_HASHTREE; // 0x4AF768
		const uint DATA_BLOCKS_IN_LEVEL3_HASHTREE = HASH_ENTRIES_IN_PAGE * DATA_BLOCKS_IN_LEVEL2_HASHTREE; // 0x31C84B10

		ulong hashTreePageCount = (hashedPagesCount + HASH_ENTRIES_IN_PAGE - 1) / HASH_ENTRIES_IN_PAGE;
		hashTreeLevels = 1;

		if (hashTreePageCount > 1)
		{
			ulong result = 2;
			while (result > 1)
			{
			
				ulong hashBlocks = 0;
				switch (hashTreeLevels)
				{
					case 0:
						hashBlocks = (hashedPagesCount + DATA_BLOCKS_IN_LEVEL0_HASHTREE - 1) / DATA_BLOCKS_IN_LEVEL0_HASHTREE;
						break;
					case 1:
						hashBlocks = (hashedPagesCount + DATA_BLOCKS_IN_LEVEL1_HASHTREE - 1) / DATA_BLOCKS_IN_LEVEL1_HASHTREE;
						break;
					case 2:
						hashBlocks = (hashedPagesCount + DATA_BLOCKS_IN_LEVEL2_HASHTREE - 1) / DATA_BLOCKS_IN_LEVEL2_HASHTREE;
						break;
					case 3:
						hashBlocks = (hashedPagesCount + DATA_BLOCKS_IN_LEVEL3_HASHTREE - 1) / DATA_BLOCKS_IN_LEVEL3_HASHTREE;
						break;
				}

				result = hashBlocks;
				hashTreeLevels += 1;
				hashTreePageCount += result;
			}
		}

		if (resilient)
			hashTreePageCount *= 2;

		return hashTreePageCount;
	}
	
	public async Task ExtractTaskAsync(string output, MsiXVDDecoder decoder, IProgress<DecompressProgress>? progress, CancellationToken cts = default)
	{
		// 规范化输出目录路径
		string normalizedOutput = _isUnixLikeSystem ? 
			output.Replace('\\', '/') : 
			output.Replace('/', '\\');
		
		// 确保输出目录存在
		Directory.CreateDirectory(normalizedOutput);
		
		await Task.Run(() =>
		{
			var firstSegmentOffset = Extensions.PageToOffset(XvcUpdateSegments[0].PageNum);
			XvcRegionHeader[] extractableRegionList =
				XvcRegions
					.Where(region =>
						(region.FirstSegmentIndex != 0 || firstSegmentOffset == region.Offset))
					.ToArray();
					
			int totalRegions = extractableRegionList.Length;
			int processedRegions = 0;
			
			for (int i = 0; i < extractableRegionList.Length; i++)
			{
				var region = extractableRegionList[i];
				if (cts.IsCancellationRequested)
				{
					return;
				}
				
				ExtractPart(
					progress,
					normalizedOutput,
					decoder,
					(uint)region.Id,
					region.Offset,
					region.Length,
					region.FirstSegmentIndex,
					IsEncrypted && region.KeyId != (0xffff),
					cts);
				
				processedRegions++;
				
				// 报告总体进度
				progress?.Report(new DecompressProgress()
				{
					CurrentCount = processedRegions,
					TotalCount = totalRegions,
					FileName = $"处理区域 {processedRegions}/{totalRegions}"
				});
			}
		});
	}
	
	private ulong CalculateHashEntryBlockOffset(ulong blockNo, out ulong hashEntryId)
	{
		var hashBlockPage = Extensions.ComputeHashBlockIndexForDataBlock(Header.Kind, HashTreeLevels,
			Header.NumberOfHashedPages, blockNo, 0, out hashEntryId, Resiliency);

		return
			HashTreePageOffset
			+ Extensions.PageToOffset(hashBlockPage);
	}
	
	private void ExtractPart(
		IProgress<DecompressProgress>? progressTask,
		string outputDirectory,
		MsiXVDDecoder decryptor,
		uint headerId,
		ulong regionStartOffset,
		ulong regionLength,
		uint startSegmentIndex,
		bool shouldDecrypt,
		CancellationToken cts
	)	
	{
		var tweakInitializationVector = (stackalloc byte[16]);

		if (shouldDecrypt)
		{
			MemoryMarshal.Cast<byte, uint>(tweakInitializationVector)[1] = headerId;
			Header.VdUid.AsSpan(0, 8).CopyTo(tweakInitializationVector[8..]);
		}

		var shouldRefreshPageCache = true;
		var totalPageCacheOffset = (long)regionStartOffset;
		var pageCacheOffset = 0;
		var pageCache = new byte[0x100000].AsSpan();
		var shouldRefreshHashCache = DataIntegrity;
		var totalHashCacheOffset =
			(long)CalculateHashEntryBlockOffset(Extensions.GetPageOffset(regionStartOffset - XvdUserDataOffset),
				out var hashCacheEntryIndex);

		var hashCacheOffset = (int)(hashCacheEntryIndex * 0x18);
		var hashCache = new byte[0x100000].AsSpan();
		var currentSegmentIndex = startSegmentIndex;
		var processedPageCount = 0;
		var totalPageCount = (long)Extensions.GetPageOffset(regionLength);
		int totalSegments = Segments.Length;
		int processedSegments = 0;

		while (Segments.Length > currentSegmentIndex && totalPageCount > processedPageCount)
		{
			if (cts.IsCancellationRequested)
			{
				return;
			}
			
			var segmentFileSize = Segments[currentSegmentIndex].FileSize;
			var segmentFilePath = _segmentPaths[currentSegmentIndex];
			
			// 使用安全的路径组合
			string outputFilePath;
			try
			{
				outputFilePath = GetSecureOutputPath(outputDirectory, segmentFilePath);
			}
			catch (UnauthorizedAccessException ex)
			{
				Debug.WriteLine($"安全路径检查失败: {ex.Message}");
				// 如果安全检查失败，使用备用方案
				string safeFileName = Path.GetFileName(segmentFilePath);
				outputFilePath = Path.Combine(outputDirectory, safeFileName);
			}
			
			var outputFileDirectory = Path.GetDirectoryName(outputFilePath);
			
			// 创建目录
			if (!string.IsNullOrEmpty(outputFileDirectory))
			{
				Directory.CreateDirectory(outputFileDirectory);
			}

			// 使用 FileMode.Create 确保覆盖已存在的文件
			using var outputFileStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write);
			
			// 报告当前正在提取的文件
			progressTask?.Report(new DecompressProgress()
			{
				CurrentCount = processedSegments,
				TotalCount = totalSegments,
				FileName = segmentFilePath
			});

			var remainingSegmentSize = segmentFileSize;

			do 
			{
				var currentChunkSize = (int)Math.Min(remainingSegmentSize, 0x1000);

				int bytesRead;
				if (shouldRefreshHashCache)
				{
					XvdFileStream.Position = totalHashCacheOffset;
					bytesRead = XvdFileStream.Read(hashCache);
					if (bytesRead != hashCache.Length)
					{
						// 处理部分读取的情况
						Debug.WriteLine($"Hash cache read {bytesRead} bytes, expected {hashCache.Length}");
					}
					shouldRefreshHashCache = false;
				}

				if (shouldRefreshPageCache)
				{
					XvdFileStream.Position = totalPageCacheOffset;
					bytesRead = XvdFileStream.Read(pageCache);
					if (bytesRead != pageCache.Length && bytesRead != 0)
					{
						// 处理部分读取的情况
						Debug.WriteLine($"Page cache read {bytesRead} bytes, expected {pageCache.Length}");
					}
					shouldRefreshPageCache = false;
				}

				var currentPageData = pageCache.Slice(pageCacheOffset, 0x1000);

				if (DataIntegrity)
				{
					var currentHashEntry = hashCache.Slice(hashCacheOffset, 0x18);
					
					if (shouldDecrypt)
					{
						MemoryMarshal.Cast<byte, uint>(tweakInitializationVector)[0] =
							MemoryMarshal.Cast<byte, uint>(currentHashEntry.Slice(HashEntryLength, sizeof(uint)))[0];
					}

					hashCacheOffset += 0x18;
					hashCacheEntryIndex++;
					
					if (hashCacheEntryIndex == 0xaa)
					{
						hashCacheEntryIndex = 0;
						hashCacheOffset += 0x10; 
					}

					if (hashCacheOffset >= hashCache.Length)
					{
						totalHashCacheOffset += hashCacheOffset;
						hashCacheOffset = 0;
						hashCacheEntryIndex = 0;
						shouldRefreshHashCache = true;
					}
				}

				if (shouldDecrypt)
				{
					decryptor.Decrypt(currentPageData, currentPageData, tweakInitializationVector);
				}

				outputFileStream.Write(currentPageData[..currentChunkSize]);

				remainingSegmentSize -= (uint)currentChunkSize;

				pageCacheOffset += 0x1000;
				if (pageCacheOffset >= pageCache.Length)
				{
					totalPageCacheOffset += pageCacheOffset;
					pageCacheOffset = 0;
					shouldRefreshPageCache = true;
				}

				processedPageCount++;
				
			} while (remainingSegmentSize > 0);
			
			currentSegmentIndex++;
			processedSegments++;
		}
	}

	public void Dispose()
	{
		Reader?.Dispose();
		XvdFileStream?.Dispose();
		GC.Collect();
	}
}