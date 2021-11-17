using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class H8DCataloger : MonoBehaviour
{
	public UnityEngine.UI.Toggle dirContentPrefab;
	public UnityEngine.UI.Toggle fileContentPrefab;
	public UnityEngine.UI.InputField fileTitlePrefab;
	public UnityEngine.UI.ScrollRect diskImageListView;
	public UnityEngine.UI.ScrollRect diskFileListView;
	public UnityEngine.UI.InputField workingFolderText;
	public UnityEngine.UI.Text diskImageCount;
	public UnityEngine.UI.Text diskFileCount;
	public UnityEngine.UI.InputField diskLabelText;
	public UnityEngine.UI.InputField diskVolume;
	public Transform fileViewRoot;
	public UnityEngine.UI.ScrollRect fileScrollRect;
	public UnityEngine.UI.Text fileViewPrefab;
	public UnityEngine.UI.Text fileViewTitle;
	public UnityEngine.UI.Text fileViewFooter;
	public UnityEngine.UI.Button allFilesButton;
	public UnityEngine.UI.Text versionText;
	public GameObject renamePanel;
	public UnityEngine.UI.InputField searchInputField;

	public struct DiskFileItem
	{
		public string lineItem;
		public int type;	// 0=file data,1=header,2=footer
		public int size;
		public int imageIndex;
		public string fileName;
		public string fileExt;
	}

	private List<string> diskImageList = new List<string>();
	private List<DiskFileItem> diskFileList = new List<DiskFileItem>();
	private List<int> diskImageListIdx = new List<int>();

	public struct HDOSDiskInfo
	{
		public byte serial_num;
		public ushort init_date;
		public long dir_sector;
		public long grt_sector;
		public byte sectors_per_group;
		public byte volume_type;
		public byte init_version;
		public long rgt_sector;
		public ushort volume_size;
		public ushort phys_sector_size;
		public byte flags;
		public byte[] label;
		public ushort reserved;
		public byte sectors_per_track;
	}

	public struct HDOSDirEntry
	{
		public byte[] filename;
		public byte[] fileext;
		public byte project;
		public byte version;
		public byte cluster_factor;
		public byte flags;
		public byte flags2;
		public byte first_group_num;
		public byte last_group_num;
		public byte last_sector_index;
		public ushort creation_date;
		public ushort alteration_date;
	}

	private HDOSDirEntry dirEntry = new HDOSDirEntry();

	public class CPMDirEntry
	{
		public byte flag;		// st
		public byte[] filename;	// f
		public byte[] fileext;	// e
		public byte extentl;    // ex
		public byte extenth;	// s2 (extent = s2ex)
		public byte reserved;	// always 0
		public byte sectors;	// sector count
		public byte[] alloc_map; // block map
	}

	public struct DiskContentItem
	{
		public string fileName;
		public string fileExt;
		public long fileSize;
		public string fileCreateDate;
		public string fileAlterationDate;
	}
	/*
	[System.Serializable]
	public class HUGLibraryRename
	{
		public string hugPartNum;
		public string hugFileName;
	}

	public HUGLibraryRename[] hugLibraryRename;
	*/
	private List<DiskContentItem> diskFileContentList = new List<DiskContentItem>();

	private List<int> selectedDiskImageList = new List<int>();
	private List<int> selectedFileList = new List<int>();

	private int diskTotalSize;
	private int diskFreeSize;

	private int currentDiskIdx;
	[HideInInspector]
	public byte[] diskImageBuffer;
	private byte[] HDOSGrtTable;

	public struct DiskFileBuffer
	{
		public int diskImageIdx;
		public string fileName;
		public string fileExt;
		public int fileSize;
		public byte[] fileBuffer;
	}

	private List<DiskFileBuffer> diskFileBufferList = new List<DiskFileBuffer>();

	private string searchFileName;
	private string searchFileExt;
	private int searchFileLines;
	private byte[] fileBuffer;
	private string searchString;

	public delegate void OnFillFileBufferComplete();
	public OnFillFileBufferComplete onFillFileBufferComplete;

	public static H8DCataloger Instance;

	public class DiskFileContentSorter : IComparer<DiskContentItem>
	{
		public int Compare(DiskContentItem a, DiskContentItem b)
		{
			string fileNameA = a.fileName + a.fileExt;
			string fileNameB = b.fileName + b.fileExt;
			return fileNameA.CompareTo(fileNameB);
		}
	}

    void Awake()
    {
		Instance = this;

		versionText.text = Application.version;
    }

    void Start()
	{
		dirEntry.filename = new byte[8];
		dirEntry.fileext = new byte[3];
		string path = PlayerPrefs.GetString("workingfolder");
		SetWorkingFolder(path);
	}

	void Update()
	{
		int imageCount = 0;
		for (int i = 0; i < diskImageListView.content.childCount; i++)
		{
			int idx = diskImageListIdx[i];
			UnityEngine.UI.Toggle toggle = diskImageListView.content.GetChild(i).GetComponent<UnityEngine.UI.Toggle>();
			if (toggle != null)
			{
				imageCount++;
				if (toggle.isOn)
				{
					if (selectedDiskImageList.Contains(idx))
					{
						continue;
					}
					selectedDiskImageList.Add(idx);
				}
				else
				{
					if (selectedDiskImageList.Contains(idx))
					{
						selectedDiskImageList.Remove(idx);
					}
				}
			}
		}

		int fileCount = 0;
		for (int i = 0; i < diskFileListView.content.childCount; i++)
		{
			UnityEngine.UI.Toggle toggle = diskFileListView.content.GetChild(i).GetComponent<UnityEngine.UI.Toggle>();
			if (toggle != null)
			{
				fileCount++;
				if (toggle.isOn)
				{
					if (selectedFileList.Contains(i))
					{
						continue;
					}
					selectedFileList.Add(i);
				}
				else
				{
					if (selectedFileList.Contains(i))
					{
						selectedFileList.Remove(i);
					}
				}
			}
		}

		diskImageCount.text = selectedDiskImageList.Count.ToString() + " / " + imageCount.ToString();
		diskFileCount.text = selectedFileList.Count.ToString() + " / " + fileCount.ToString();
	}

	void ProcessDiskImage(int diskImageIdx)
	{
		//Debug.Log("ProcessFile() diskImageIdx=" + diskImageIdx.ToString());

		currentDiskIdx = diskImageIdx;
		string filePath = diskImageList[diskImageIdx];

		if (System.IO.File.Exists(filePath))
		{
			//Debug.Log(filePath);

			diskImageBuffer = System.IO.File.ReadAllBytes(filePath);
			diskFileContentList.Clear();
			ProcessDiskImage();
		}
	}

	void ProcessDiskImage()
	{
		if (IsHDOSDisk(diskImageBuffer))
		{
			ProcessHDOSDiskImage();
		}
		else if (IsCPMDisk(diskImageBuffer))
		{
			ProcessCPMDiskImage();
		}
	}

	//
	// HDOS functions
	//

	// iterates through HDOS directory structure and fills in diskFileContentList
	// with disk image files
	void ProcessHDOSDiskImage()
	{
		//Debug.Log("ProcessHDOSDiskImage()");

		HDOSDiskInfo disk_info = new HDOSDiskInfo();
		disk_info.label = new byte[68];
		int offset = 0x900;
		disk_info.serial_num = diskImageBuffer[offset++];
		disk_info.init_date = System.BitConverter.ToUInt16(diskImageBuffer, offset);
		offset += 2;
		disk_info.dir_sector = System.BitConverter.ToUInt16(diskImageBuffer, offset) * 256;
		offset += 2;
		disk_info.grt_sector = System.BitConverter.ToUInt16(diskImageBuffer, offset) * 256;
		offset += 2;
		disk_info.sectors_per_group = diskImageBuffer[offset++];
		disk_info.volume_type = diskImageBuffer[offset++];
		disk_info.init_version = diskImageBuffer[offset++];
		disk_info.rgt_sector = System.BitConverter.ToUInt16(diskImageBuffer, offset) * 256;
		offset += 2;
		disk_info.volume_size = System.BitConverter.ToUInt16(diskImageBuffer, offset);
		offset += 2;
		disk_info.phys_sector_size = System.BitConverter.ToUInt16(diskImageBuffer, offset);
		offset += 2;
		disk_info.flags = diskImageBuffer[offset++];
		System.Buffer.BlockCopy(diskImageBuffer, offset, disk_info.label, 0, 68);
		offset += 60;
		disk_info.reserved = System.BitConverter.ToUInt16(diskImageBuffer, offset);
		offset += 2;
		disk_info.sectors_per_track = diskImageBuffer[offset++];

		//Debug.Log("disk_info.dir_sector=" + disk_info.dir_sector.ToString("X") + " grt_sector=" + disk_info.grt_sector.ToString("X"));

		// HDOS labels can be longer than 60 in certain cases
		string labelstr2 = System.Text.Encoding.ASCII.GetString(disk_info.label, 0, 68);
		string labelstr = string.Empty;
		for (int labelIdx = 0; labelIdx < labelstr2.Length; labelIdx++)
		{
			if (labelstr2[labelIdx] < 0x20 || labelstr2[labelIdx] >= 0x7F)
			{
				labelstr += ' ';
			}
			else
			{
				labelstr += labelstr2[labelIdx];
			}
		}
		//Debug.Log("Disk Label:" + labelstr);

		diskLabelText.text = labelstr.Trim();
		diskVolume.text = disk_info.serial_num.ToString("D3");

		HDOSGrtTable = new byte[256];
		System.Buffer.BlockCopy(diskImageBuffer, (int)disk_info.grt_sector, HDOSGrtTable, 0, 256);

		diskTotalSize = 0;
		ReadHDOSDiskContents(disk_info);
	}

	void ReadHDOSDiskContents(HDOSDiskInfo disk_info)
	{
		int offset = (int)disk_info.dir_sector;
		int entry_count = 0;
		List<int> nextDirBlockList = new List<int>();
		while (true)
		{
			HDOSDirEntry entry = dirEntry;

			while (true)
			{
				// try to read a valid directory entry skipping all empty entries
				System.Buffer.BlockCopy(diskImageBuffer, offset, entry.filename, 0, 8);
				offset += 8;
				System.Buffer.BlockCopy(diskImageBuffer, offset, entry.fileext, 0, 3);
				offset += 3;

				entry.project = diskImageBuffer[offset++];
				entry.version = diskImageBuffer[offset++];
				entry.cluster_factor = diskImageBuffer[offset++];
				entry.flags = diskImageBuffer[offset++];
				entry.flags2 = diskImageBuffer[offset++];
				entry.first_group_num = diskImageBuffer[offset++];
				entry.last_group_num = diskImageBuffer[offset++];
				entry.last_sector_index = diskImageBuffer[offset++];
				entry.creation_date = System.BitConverter.ToUInt16(diskImageBuffer, offset);
				offset += 2;
				entry.alteration_date = System.BitConverter.ToUInt16(diskImageBuffer, offset);
				offset += 2;

				entry_count++;
				if (entry_count == 22)
				{
					int max_entries = System.BitConverter.ToUInt16(diskImageBuffer, offset);
					offset += 2;
					int cur_dir_blk = System.BitConverter.ToUInt16(diskImageBuffer, offset) * 256;
					offset += 2;
					int nxt_dir_blk = System.BitConverter.ToUInt16(diskImageBuffer, offset) * 256;
					offset += 2;
					offset = nxt_dir_blk;
					// prevent endless loop in case of corrupt directory structure
					if (nextDirBlockList.Contains(offset))
					{
						offset = 0;
					}
					else
					{
						nextDirBlockList.Add(offset);
					}
					entry_count = 0;
					//Debug.Log("offset=" + offset.ToString("X"));
				}
				if (entry.project != 0)
				{
					offset = 0;
				}
				if (offset == 0)
				{
					break;
				}
				if (entry.filename[0] == 0 || entry.filename[0] == 0xFE || entry.filename[0] == 0xFF)
				{
					// empty entry - try next file
					continue;
				}
				break;
			}

			if (offset == 0)
			{
				// done with directory scan
				break;
			}

			if (entry.filename[0] != 0 && entry.filename[0] != 0xFE && entry.filename[0] != 0xFF)
			{
				int fsize = ComputeHDOSFileSize(entry, disk_info.sectors_per_group);

				if (fsize == -1)
				{
					Debug.Log("!! DIRECTORY IS CORRUPT !!");
					Debug.Log("!!   FILESIZE FAILED    !!");
					return;
				}

				diskTotalSize += fsize;
				ushort day = (ushort)(entry.creation_date & 0x001F);
				if (day == 0) day = 1;
				ushort mon = (ushort)((entry.creation_date & 0x01E0) >> 5);
				string month = "Jan";
				switch (mon)
				{
					case 1:
						month = "Jan";
						break;
					case 2:
						month = "Feb";
						break;
					case 3:
						month = "Mar";
						break;
					case 4:
						month = "Apr";
						break;
					case 5:
						month = "May";
						break;
					case 6:
						month = "Jun";
						break;
					case 7:
						month = "Jul";
						break;
					case 8:
						month = "Aug";
						break;
					case 9:
						month = "Sep";
						break;
					case 10:
						month = "Oct";
						break;
					case 11:
						month = "Nov";
						break;
					case 12:
						month = "Dec";
						break;
				}

				ushort year = (ushort)((entry.creation_date & 0x7E00) >> 9);
				if (year == 0)
				{
					year = 9;
				}
				else if (year + 70 > 99)
				{
					year = 99;
				}
				string cre_date = string.Format("{0:D2}-{1}-{2}", day, month, year + 70);
				string fname = System.Text.Encoding.UTF8.GetString(entry.filename, 0, 8);
				string f = fname.Replace('\0', ' ');
				string fext = System.Text.Encoding.UTF8.GetString(entry.fileext, 0, 3);
				string e = fext.Replace('\0', ' ');

				if (string.IsNullOrEmpty(searchFileName))
				{
					DiskContentItem item = new DiskContentItem();
					item.fileName = f;
					item.fileExt = e;
					item.fileSize = fsize;
					item.fileCreateDate = cre_date;
					item.fileAlterationDate = string.Empty;
					diskFileContentList.Add(item);
				}
				else
				{
					//Debug.Log("Searching for " + searchFileName + "." + searchFileExt + " found file " + f + "." + e);
					if (f.Equals(searchFileName) && e.Equals(searchFileExt))
					{
						FillFileBufferHDOS(disk_info, entry);
						return;
					}
				}
			}
		}

		if (string.IsNullOrEmpty(searchFileName))
		{
			diskFileContentList.Sort(new DiskFileContentSorter());

			diskFreeSize = ComputeHDOSFreeSize(disk_info.sectors_per_group);
		}
	}

	int ComputeHDOSFileSize(HDOSDirEntry entry, int sectorsPerGroup)
	{
		int grp_count = 0;
		byte grp = entry.first_group_num;
		if (grp < 3 || grp >= 255)
		{
			return 0;
		}

		while (HDOSGrtTable[grp] != 0 && grp_count < 256)
		{
			if (grp < 3)
			{
				return (-1);
			}
			grp = HDOSGrtTable[grp];
			grp_count++;
		}

		if (grp_count == 256)
		{
			return (-1);
		}

		int total_size = (grp_count * sectorsPerGroup) + entry.last_sector_index;
		return total_size;
	}

	int ComputeHDOSFreeSize(int sectorsPerGroup)
	{
		int grp_count = 0;
		int grp = 0;
		while (HDOSGrtTable[grp] != 0 && grp_count < 256)
		{
			grp = HDOSGrtTable[grp];
			grp_count++;
		}
		if (grp_count == 256)
		{
			return (0);
		}
		return grp_count * sectorsPerGroup;
	}

	// fills the fileBuffer array with the contents of the file pointed to by 'entry'
	void FillFileBufferHDOS(HDOSDiskInfo disk_info, HDOSDirEntry entry)
	{
		//Debug.Log("FillFileBufferHDOS()");

		int grp = entry.first_group_num;
		int fsize = ComputeHDOSFileSize(entry, disk_info.sectors_per_group); // size in sectors
		int totalBytes = (fsize + disk_info.sectors_per_group) * 256; // (fsize * disk_info.sectors_per_group * 256) + (entry.last_sector_index * 256);

		Debug.Log("file size in bytes=" + totalBytes.ToString());

		fileBuffer = new byte[totalBytes];

		int bytes_to_read = 0;
		int fileBufferOffset = 0;
		int diskImageOffset = 0;

		do
		{
			int sector_addr = grp * (disk_info.sectors_per_group * 256);
			bytes_to_read = disk_info.sectors_per_group * 256;
			System.Buffer.BlockCopy(diskImageBuffer, sector_addr, fileBuffer, fileBufferOffset, bytes_to_read);
			fileBufferOffset += bytes_to_read;
			diskImageOffset = sector_addr + bytes_to_read;
			grp = HDOSGrtTable[grp];
		} while (grp != 0);

		bytes_to_read = entry.last_sector_index * 256;

		//string fileName = System.Text.Encoding.ASCII.GetString(entry.filename);
		//string fileExt = System.Text.Encoding.ASCII.GetString(entry.fileext);
		//Debug.Log("bytes_to_read=" + bytes_to_read.ToString() + " fileBufferOffset=" + fileBufferOffset.ToString() + " fileBuffer.Length=" + fileBuffer.Length.ToString() + " diskImageOffset=" + diskImageOffset.ToString() + " diskImageBuffer.Length=" + diskImageBuffer.Length.ToString());

		if (bytes_to_read > 0)
		{
			if (diskImageOffset < diskImageBuffer.Length)
			{
				System.Buffer.BlockCopy(diskImageBuffer, diskImageOffset, fileBuffer, fileBufferOffset, bytes_to_read);
				fileBufferOffset += bytes_to_read;
			}
		}

		//Debug.Log("fileSizeInBytes=" + fileBufferOffset.ToString() + " totalBytesAllocated=" + totalBytes.ToString());
	}

	//
	// CP/M functions
	//

	// iterates through the directory structure of a CP/M disk
	// Heathkit CPM: 256 byte sectors, 1024/2048 byte allocation block size
	void ProcessCPMDiskImage()
	{
		int usedSize = 0;

		List<CPMDirEntry> fileEntryList = new List<CPMDirEntry>();
		fileEntryList.Clear();

		int offset = 0x1E00;
		int dirArrayIdx = 0;

		int trackSize = 2560;
		int diskBufferSize = diskImageBuffer.Length;
		int disk2s80t = trackSize * 80 * 2;
		bool is2s80t = (diskBufferSize < disk2s80t) ? false : true;
		int albSize = is2s80t ? 0x800 : 0x400; // for computing file size

		int systemTracks = 3;
		int unusableSpace = is2s80t ? 4096 : 2048; // for computing remaining space
		int diskSize = diskBufferSize - (systemTracks * trackSize) - unusableSpace;

		// intlv=4
		// track=   0, 1, 2, 3, 4, 5, 6, 7, 8, 9 (10 physical per track)
		// skew=    0, 4, 8, 2, 6, 1, 5, 9, 3, 7 (10 logical sectors)
		// offsets in skew order
		// 0x1E00
		// 0x2200
		// 0x2600
		// 0x2000
		// 0x2400
		// 0x1F00
		// 0x2300
		// 0x2700
		// 0x2100
		// 0x2500
		int[] skewedDirOffsets = { 0x1E00, 0x2200, 0x2600, 0x2000, 0x2400, 0x1F00, 0x2300, 0x2700, 0x2100, 0x2500 };

		while (true)
		{
			CPMDirEntry entry = null;

			while (true)
			{
				if (offset < diskBufferSize)
				{
					entry = new CPMDirEntry();
					entry.filename = new byte[8];
					entry.fileext = new byte[3];
					entry.alloc_map = new byte[16];

					entry.flag = diskImageBuffer[offset++]; // [0] 0xE5 or user number
					System.Buffer.BlockCopy(diskImageBuffer, offset, entry.filename, 0, 8); // [1-8] file name
					offset += 8;
					System.Buffer.BlockCopy(diskImageBuffer, offset, entry.fileext, 0, 3); // [9-11] file extension
					offset += 3;
					for (int i = 0; i < 3; i++)
					{
						entry.fileext[i] = (byte)(entry.fileext[i] & 0x7F);
					}
					entry.extentl = diskImageBuffer[offset++]; // [12] extent number
					entry.extenth = diskImageBuffer[offset++]; // [13] reserved
					entry.reserved = diskImageBuffer[offset++]; // [14] reserved
					entry.sectors = diskImageBuffer[offset++]; // [15] number of sectors used in last alb
					System.Buffer.BlockCopy(diskImageBuffer, offset, entry.alloc_map, 0, 16); // [16-31] allocation block map
					offset += 16;

					if ((offset % 256) == 0)
					{
						dirArrayIdx++;
						offset = skewedDirOffsets[dirArrayIdx];
					}
					if (offset == 0x2700)
					{
						offset = 0;
						break;
					}

					/*
					if (!is2s80t)
					{
						// HARD SECTOR 1S40T or 2S40T
						// 0x1E00
						// 0x2200
						// 0x2600
						// 0x2000
						// 0x2400
						// 0x1F00
						// 0x2300
						// 0x2700
						// 0x2100
						// 0x2500
						if (offset == 0x1F00)
                        {
							offset = 0x2200;
						}
						else if (offset == 0x2300)
						{
							offset = 0x2600;
						}
						else if (offset == 0x2700)
						{
							offset = 0;
							break;
						}
					}
					else
					{
						// HARD SECTOR 2S80T
						// 0x1E00
						// 0x2200
						// 0x2600
						// 0x2000
						// 0x2400
						// 0x1F00
						// 0x2300
						// 0x2700 x
						// 0x2100 x
						// 0x2500 x
						if ((offset % 256) == 0)
						{
							dirArrayIdx++;
							// skewed directory offsets
							int[] offsetArray = { 0x1E00, 0x2200, 0x2600, 0x2000, 0x2400, 0x1F00, 0x2300, 0x2700, 0x2100, 0x2500 };
							offset = offsetArray[dirArrayIdx];
						}
						if (offset == 0x2700)
						{
							offset = 0;
							break;
						}
					}
					*/
					if (entry.flag == 0xE5 || entry.filename[0] == 0xE5)
					{
						// skip invalid entries
						continue;
					}
				}
				break;
			}

			if (entry != null && entry.flag != 0xE5 && entry.filename[0] != 0xE5)
			{
				fileEntryList.Add(entry);
				string fname = System.Text.Encoding.UTF8.GetString(entry.filename, 0, 8);
				string f = fname.Replace('\0', ' ');
				string fext = System.Text.Encoding.UTF8.GetString(entry.fileext, 0, 3);
				string e = fext.Replace('\0', ' ');

				// debug info
				string deb = entry.flag.ToString("X2") + " " + fname + "." + fext + " ";
				deb += entry.extentl.ToString("X2") + " ";
				deb += entry.extenth.ToString("X2") + " ";
				deb += entry.sectors.ToString("X2") + " ";
				for (int i = 0; i < entry.alloc_map.Length; i++)
				{
					deb += "." + entry.alloc_map[i].ToString("X2");
				}
				//Debug.Log("fileEntryList.Add() entry=" + deb);

				if (entry.sectors < 0x80)
				{
					// compute size on disk
					int fsize = 0;
					for (int i = 0; i < fileEntryList.Count; i++)
                    {
						// iterate through fileEntryList looking for entry.filename and add up used ALBs
						if (ByteCompare(fileEntryList[i].filename, entry.filename))
                        {
							if (ByteCompare(fileEntryList[i].fileext, entry.fileext))
                            {
								for (int j = 0; j < fileEntryList[i].alloc_map.Length; j++)
								{
									if (fileEntryList[i].alloc_map[j] != 0)
									{
										fsize += albSize;
									}
								}
								//Debug.Log("entry.filename=" + fname + "." + fext + " fsize=" + fsize.ToString());
							}
						}
                    }

					DiskContentItem disk_file_entry = new DiskContentItem();
					disk_file_entry.fileName = f;
					disk_file_entry.fileExt = e;
					disk_file_entry.fileSize = fsize;
					disk_file_entry.fileCreateDate = "         ";
					disk_file_entry.fileAlterationDate = string.Empty;

					if (string.IsNullOrEmpty(searchFileName))
					{
						diskFileContentList.Add(disk_file_entry);
					}
					else
					{
						if (f.Equals(searchFileName) && e.Equals(searchFileExt))
						{
							string fileName = f.Trim() + "." + e.Trim();
							FillFileBufferCPM(entry, fileEntryList);
							return;
						}
					}

					usedSize += fsize;
				}
			}

			if (offset == 0)
			{
				break;
			}
		}

		diskFileContentList.Sort(new DiskFileContentSorter());

		diskTotalSize = usedSize / 1024; // total size on disk used up
		diskFreeSize = (diskSize - usedSize) / 1024; // available size on disk
	}

	// extracts file contents from a CP/M disk into fileBuffer[]
	void FillFileBufferCPM(CPMDirEntry entry, List<CPMDirEntry> entryList)
	{
		// start with H17 hard sector then expand to soft-sector once working
		// disk type { 0xE5, 0x800, 0x1e00, 1, 0x800, 4, 10, 0x100, 80, 2}, // H17 96tpi SD DS
		// disk type { 0xE5, 0x400, 0x1e00, 1, 0x800, 4, 10, 0x100, 40, 1}, // H17 48tpi SD SS

		int disk2s80t = 2560 * 80 * 2; // if diskImageBuffer.Length is less than this then we are 1S40T, 2S40T or 1S80T (albSize=0x400)
		int diskBufferSize = diskImageBuffer.Length;
		int albSize = (diskBufferSize < disk2s80t) ? 0x400 : 0x800; // 1024 or 2048 bytes
		int interleave = 4;
		int sectorsPerTrack = 10;
		int sectorSize = 0x100;
		int logicalSectorSize = 0x80;
		int sectorsPerBlock = albSize / sectorSize;
		int trackSize = sectorSize * sectorsPerTrack;

		List<CPMDirEntry> fileDirEntries = new List<CPMDirEntry>();
		int fileSize = entry.sectors * 128;
		for (int i = 0; i < entryList.Count; i++)
        {
			// determine if this CPMDirEntry is for the file we are looking for
			bool pass = true;
			for (int j = 0; j < entry.filename.Length; j++)
			{
				if (entry.filename[j] != entryList[i].filename[j])
				{
					pass = false;
				}
			}
			for (int j = 0; j < entry.fileext.Length; j++)
			{
				if (entry.fileext[j] != entryList[i].fileext[j])
				{
					pass = false;
				}
			}
			if (pass)
			{
				// CPMDirEntry is part of our file so compute fileSize and add to our fileDirEntries list
				fileSize += entryList[i].sectors * 256;
				fileDirEntries.Add(entryList[i]);
			}
        }

		//Debug.Log("fileDirEntries.Count=" + fileDirEntries.Count.ToString());
		fileBuffer = new byte[fileSize];
		int fileBufferOffset = 0;

		// TODO: add support for other disk types besides H8D (IMD, H37)
		// intlv=4
        // track=   0, 1, 2, 3, 4, 5, 6, 7, 8, 9 (10 logical per track)
		// skew=    0, 4, 8, 2, 6, 1, 5, 9, 3, 7 (10 skewed physical sectors)
		int[] skew = H8DCPMFile.Instance.BuildSkew(interleave, sectorsPerTrack);
		int offset = 0x1E00;

		// fileDirEntries contains all the CPMDirEntry's for the file we are looking for
		for (int i = 0; i < fileDirEntries.Count; i++)
		{
			for (int j = 0; j < fileDirEntries[i].alloc_map.Length; j++)
			{
				// loop through all allocation blocks
				int alb = fileDirEntries[i].alloc_map[j];
				if (alb > 0)
				{
					// copy sectors from alb to fileBuffer[]
					for (int k = 0; k < sectorsPerBlock; k++)
					{
						// loop through all sectors in an allocation block
						int albOffset = (alb * albSize) + (k * sectorSize);
						int track = (offset + albOffset) / trackSize;
						int trackOffset = track * trackSize;
						int logicalSec = (albOffset / sectorSize) % sectorsPerTrack;
						int physicalSec = skew[logicalSec];
						int imageOffset = trackOffset + (physicalSec * sectorSize);

						//Debug.Log("alb=" + alb.ToString() + " albOffset=" + albOffset.ToString() + " imageOffset=" + imageOffset.ToString() + " track=" + track.ToString() + " logicalSector=" + logicalSec.ToString() + " physicalSector=" + physicalSec.ToString() + " fileBufferOffset=" + fileBufferOffset.ToString() + " entry.sectors=" + entry.sectors.ToString() + " fileSize=" + fileSize.ToString());

						int cpmSectors = sectorSize / logicalSectorSize;
						for (int n = 0; n < cpmSectors; n++)
						{
							if (fileBufferOffset < fileSize)
							{
								System.Buffer.BlockCopy(diskImageBuffer, imageOffset, fileBuffer, fileBufferOffset, logicalSectorSize);
								fileBufferOffset += logicalSectorSize;
								imageOffset += logicalSectorSize;
							}
						}
					}
				}
			}
		}
	}

	// fills the fileBuffer array with the contents of a file in a disk image
	// calls ProcessFile() which determines disk image type and calls in to
	// the appropriate CP/M or HDOS functions to extract the file contents
	DiskFileBuffer FillFileBuffer(string fileName, string fileExt, int imageIdx)
	{
		searchFileName = fileName;
		searchFileExt = fileExt;
		searchFileLines = 0;

		ProcessDiskImage(imageIdx);

		DiskFileBuffer b = new DiskFileBuffer();
		if (fileBuffer != null && fileBuffer.Length > 0)
		{
			b.fileBuffer = fileBuffer;
			b.fileName = fileName;
			b.fileExt = fileExt;
			b.fileSize = fileBuffer.Length;
		}

		if (onFillFileBufferComplete != null)
		{
			onFillFileBufferComplete();
		}

		return b;
	}

	// callback for completion of file content extraction into fileBuffer[] array
	void OnFillFileBufferView()
	{
		onFillFileBufferComplete -= OnFillFileBufferView;

		// dump to text
		if (fileBuffer != null && fileBuffer.Length > 0)
		{
			fileViewRoot.gameObject.SetActive(true);

			if (IsBinaryFile())
			{
				// show in hex dump format
				ShowFileViewHex();
			}
			else
			{
				// show in text format
				string text = System.Text.Encoding.ASCII.GetString(fileBuffer);
				ShowFileViewText(text);
			}
			fileViewTitle.text = searchFileName.Trim() + "." + searchFileExt.Trim();
			fileViewFooter.text = searchFileLines.ToString() + " LINES";
		}
	}

	// view a file with filename.ext on disk image imageIdx
	void ViewFile(string fileName, string fileExt, int imageIdx)
	{
		onFillFileBufferComplete += OnFillFileBufferView;
		FillFileBuffer(fileName, fileExt, imageIdx);
	}

	public void HexViewerButton()
	{
		if (selectedDiskImageList.Count > 0)
		{
			int idx = selectedDiskImageList[0];
			string filePath = diskImageList[idx];

			//Debug.Log("HexViewerButton() filePath=" + filePath);

			if (System.IO.File.Exists(filePath))
			{
				fileViewRoot.gameObject.SetActive(true);
				fileBuffer = System.IO.File.ReadAllBytes(filePath);
				ShowFileViewHex();
			}
		}
	}

	// hex dump file
	void ShowFileViewHex()
	{
		StartCoroutine(ShowFileViewHexCoroutine());
	}

	IEnumerator ShowFileViewHexCoroutine()
	{
		int startHex = 0;
		byte[] buf = new byte[16];
		string hexLine = string.Empty;
		List<string> lineList = new List<string>();

		searchFileLines = 0;
		while (startHex < fileBuffer.Length)
		{
			hexLine += startHex.ToString("X8") + " ";
			int remain = fileBuffer.Length - startHex - 1;
			int len = remain > buf.Length ? buf.Length : remain;
			System.Buffer.BlockCopy(fileBuffer, startHex, buf, 0, len);
			for (int i = 0; i < buf.Length; i++)
            {
				hexLine += buf[i].ToString("X2") + " ";
			}
			for (int i = 0; i < buf.Length; i++)
			{
				char c = (char)buf[i];
				if (char.IsLetterOrDigit(c) || char.IsPunctuation(c) || char.IsSymbol(c))
				{
					hexLine += c;
				}
				else
				{
					hexLine += ".";
				}
				buf[i] = 0;
			}

			searchFileLines++;
			if ((searchFileLines % 256) == 0)
			{
				lineList.Add(hexLine);
				hexLine = string.Empty;

				fileViewFooter.text = searchFileLines.ToString() + " LINES";
				yield return new WaitForEndOfFrame();
			}
			else
			{
				hexLine += System.Environment.NewLine;
			}

			startHex += buf.Length;
		}

		if (hexLine.Length > 0)
		{
			lineList.Add(hexLine);
		}

		yield return new WaitForEndOfFrame();

		ShowFileViewTextList(lineList);
	}

	// text format file
	void ShowFileViewText(string text, bool format = true)
	{
		List<string> formattedText = null;
		if (format)
		{
			formattedText = FormatForView(text);
		}

		ShowFileViewTextList(formattedText);
	}

	void ShowFileViewTextList(List<string> lineList)
	{
		StartCoroutine(ShowFileViewTextListCoroutine(lineList));
	}

	IEnumerator ShowFileViewTextListCoroutine(List<string> lineList)
	{
		yield return new WaitForEndOfFrame();

		for (int i = 0; i < lineList.Count; i++)
		{
			UnityEngine.UI.Text textPrefab = Instantiate(fileViewPrefab, fileScrollRect.content);
			textPrefab.text = lineList[i];
		}

		fileViewFooter.text = searchFileLines.ToString() + " LINES";
	}

	// expand tabs to 8 chars
	List<string> FormatForView(string s)
	{
		char[] splitChars = { (char)0x0A };
		string[] lines = s.Split(splitChars, System.StringSplitOptions.None);
		List<string> result = new List<string>();
		bool term = false;

		for (int i = 0; i < lines.Length; i++)
		{
			int n = 0;
			string str = string.Empty;
			for (int j = 0; j < lines[i].Length; j++)
			{
				char c = lines[i][j];
				if (c == 0x09)
				{
					// pad with spaces to columns of 8
					do
					{
						str += ' ';
						n++;
					} while ((n % 8) != 0);
				}
				else
				{
					str += c;
					n++;
				}
				if (c == 0x1A || c == 0x00)
				{
					// CTL-Z terminates text files in CP/M
					term = true;
					break;
				}
			}
			result.Add(str);

			if (term)
			{
				break;
			}
		}

		searchFileLines = result.Count;

		// returns formatted text
		return result;
	}

	bool IsBinaryFile()
	{
		for (int i = 0; i < 256; i++)
		{
			if (i < fileBuffer.Length)
			{
				if (fileBuffer[i] == 0x00)
				{
					continue;
				}
				if (fileBuffer[i] == 0x07)
				{
					continue;
				}
				if (fileBuffer[i] == 0x09)
				{
					continue;
				}
				if (fileBuffer[i] == 0x0A)
				{
					continue;
				}
				if (fileBuffer[i] == 0x0D)
				{
					continue;
				}
				//if (fileBuffer[i] == 0x1B)
				//{
				//	continue;
				//}
				if (fileBuffer[i] >= 32 && fileBuffer[i] < 127)
				{
					continue;
				}
				return true;
			}
		}
		return false;
	}

	// save viewed file contents to disk
	public void SaveFile()
	{
		FilePicker.Instance.title.text = "Save File";
		FilePicker.Instance.onCompleteCallback += SaveFileComplete;
		FilePicker.Instance.onOpenPicker += SaveFileOpenPicker;
		FilePicker.Instance.ShowPicker(true);
	}

	void SaveFileOpenPicker()
	{
		// file picker opened so fill in the default file name
		FilePicker.Instance.onOpenPicker -= SaveFileOpenPicker;
		string fileName = searchFileName.Trim() + "." + searchFileExt.Trim();
		FilePicker.Instance.fileInputField.text = fileName;
	}

	void SaveFileComplete(string filePath)
	{
		// file picker is closed so save the file contents
		FilePicker.Instance.onCompleteCallback -= SaveFileComplete;

		if (!string.IsNullOrEmpty(filePath))
		{
			if (IsBinaryFile())
			{
				System.IO.File.WriteAllBytes(filePath, fileBuffer);
			}
			else
			{
				string text = System.Text.Encoding.ASCII.GetString(fileBuffer);
				List<string> formattedText = FormatForView(text);
				
				System.IO.File.WriteAllLines(filePath, formattedText);
			}
		}
	}

	// shows all disk images in a folder
	void FillDiskImageListView()
	{
		while (diskImageListView.content.childCount > 0)
		{
			Transform t = diskImageListView.content.GetChild(0);
			DestroyImmediate(t.gameObject);
		}

		diskImageListIdx.Clear();
		for (int i = 0; i < diskImageList.Count; i++)
		{
			if (string.IsNullOrEmpty(searchString) || diskImageList[i].ToUpper().Contains(searchString))
			{
				diskImageListIdx.Add(i);

				UnityEngine.UI.Toggle content = Instantiate(dirContentPrefab, diskImageListView.content);
				UnityEngine.UI.Text[] text = content.GetComponentsInChildren<UnityEngine.UI.Text>();
				if (text[0].name.ToLower().Equals("label"))
				{
					text[0].text = System.IO.Path.GetFileName(diskImageList[i]);
					text[1].text = System.IO.Path.GetDirectoryName(diskImageList[i]).ToUpper();
					text[1].text = text[1].text.Substring(text[1].text.LastIndexOf(System.IO.Path.DirectorySeparatorChar));
				}
				else
				{
					text[1].text = System.IO.Path.GetFileName(diskImageList[i]);
					text[0].text = System.IO.Path.GetDirectoryName(diskImageList[i]).ToUpper();
					text[0].text = text[1].text.Substring(text[1].text.LastIndexOf(System.IO.Path.DirectorySeparatorChar));
				}
				//content.GetComponentInChildren<UnityEngine.UI.Text>().text = diskImageList[i].Substring(workingFolderText.text.Length + 1);
				content.gameObject.SetActive(true);
			}
		}
	}

	// shows directory listing of a disk image
	void FillDiskFileListView()
	{
		while (diskFileListView.content.childCount > 0)
		{
			Transform t = diskFileListView.content.GetChild(0);
			DestroyImmediate(t.gameObject);
		}

		for (int i = 0; i < diskFileList.Count; i++)
		{
			if (diskFileList[i].type == 1 || diskFileList[i].type == 2)
			{
				string s = diskFileList[i].lineItem;
				if (diskFileList[i].type == 1)
                {
					s = System.IO.Path.GetFileName(s);
                }
				UnityEngine.UI.InputField textField = Instantiate(fileTitlePrefab, diskFileListView.content);
				textField.text = s;
			}
			else
			{
				UnityEngine.UI.Toggle content = Instantiate(fileContentPrefab, diskFileListView.content);
				UnityEngine.UI.Text textField = content.GetComponentInChildren<UnityEngine.UI.Text>();
				textField.text = diskFileList[i].lineItem;
			}
		}

		diskFileCount.text = diskFileList.Count.ToString() + " files";
	}

	public void FolderButton()
	{
		FilePicker.Instance.title.text = "Choose Folder";
		FilePicker.Instance.onCompleteCallback += SetWorkingFolder;
		FilePicker.Instance.ShowPicker();
	}

	public void SetWorkingFolder(string path)
	{
		FilePicker.Instance.onCompleteCallback -= SetWorkingFolder;

		workingFolderText.text = path;

		Debug.Log("workingFolder=" + workingFolderText.text);

		diskImageList = null;

		if (!string.IsNullOrEmpty(workingFolderText.text))
		{
			string[] files = System.IO.Directory.GetFiles(workingFolderText.text, "*.?8?", System.IO.SearchOption.AllDirectories); // match .h8d or .H8D or .H8d
			string[] files37 = System.IO.Directory.GetFiles(workingFolderText.text, "*.?37", System.IO.SearchOption.AllDirectories);
			if (files.Length > 0 || files37.Length > 0)
			{
				//Debug.Log("H8D files=" + files.Length.ToString());
				if (files.Length > 0)
				{
					diskImageList = new List<string>(files);
				}
				if (files37.Length > 0)
				{
					if (diskImageList != null && diskImageList.Count > 0)
					{
						for (int i = 0; i < files37.Length; i++)
						{
							diskImageList.Add(files37[i]);
						}
					}
					else
					{
						diskImageList = new List<string>(files37);
					}
				}
				if (diskImageList != null && diskImageList.Count > 0)
				{
					diskImageList.Sort();

					FillDiskImageListView();

					PlayerPrefs.SetString("workingfolder", workingFolderText.text);
				}
			}
		}
	}

	public void CatalogButton()
	{
		searchFileName = string.Empty;
		searchFileExt = string.Empty;

		diskFileList.Clear();

		for (int i = 0; i < selectedDiskImageList.Count; i++)
		{
			ProcessDiskImage(selectedDiskImageList[i]);

			int idx = selectedDiskImageList[i];
			string title = diskImageList[idx].Substring(workingFolderText.text.Length);
			int n = title.IndexOf(System.IO.Path.DirectorySeparatorChar);
			title = title.Substring(n + 1).ToUpper();
			title = title.Replace(".H8D", "");
			AddFileItem(title, 1, 12, idx);
			bool isHDOSDisk = IsHDOSDisk(diskImageBuffer);
			for (int j = 0; j < diskFileContentList.Count; j++)
			{
				// add to the file list UI scroll view
				string size = diskFileContentList[j].fileSize.ToString("D3");
				if (!isHDOSDisk)
				{
					int fs = (int)diskFileContentList[j].fileSize / 1024;
					size = fs.ToString("D3") + "K";
				}
				string s = diskFileContentList[j].fileName + "." + diskFileContentList[j].fileExt + " " + size + " " + diskFileContentList[j].fileCreateDate;
				AddFileItem(s, 0, 20, idx, j);
			}
			string freeBlocks = string.Empty;
			if (IsHDOSDisk(diskImageBuffer))
			{
				freeBlocks = "USED=" + diskTotalSize.ToString() + " FREE=" + diskFreeSize.ToString();
			}
			else
			{
				freeBlocks = "USED=" + diskTotalSize.ToString() + "K FREE=" + diskFreeSize.ToString() + "K";
			}
			AddFileItem(freeBlocks, 2, 18, idx);
		}

		FillDiskFileListView();
	}

	public void AddFileItem(string s, int type, int size, int idx, int fileContentIdx = -1)
	{
		DiskFileItem item = new DiskFileItem();
		item.lineItem = s;
		item.type = type;
		item.size = size;
		item.imageIndex = idx;
		if (fileContentIdx != -1)
		{
			item.fileName = diskFileContentList[fileContentIdx].fileName;
			item.fileExt = diskFileContentList[fileContentIdx].fileExt;
		}
		else
		{
			item.fileName = string.Empty;
			item.fileExt = string.Empty;
		}
		diskFileList.Add(item);
	}

	public void ClearSelectionsButton()
	{
		for (int i = 0; i < diskImageListView.content.childCount; i++)
		{
			UnityEngine.UI.Toggle toggle = diskImageListView.content.GetChild(i).GetComponent<UnityEngine.UI.Toggle>();
			if (toggle != null)
			{
				toggle.isOn = false;
			}
		}
		ClearSelectedFiles();
	}

	void ClearSelectedFiles()
	{
		for (int i = 0; i < diskFileListView.content.childCount; i++)
		{
			UnityEngine.UI.Toggle toggle = diskFileListView.content.GetChild(i).GetComponent<UnityEngine.UI.Toggle>();
			if (toggle != null)
			{
				toggle.isOn = false;
			}
		}
	}

	public void SelectAllImagesButton()
	{
		for (int i = 0; i < diskImageListView.content.childCount; i++)
		{
			UnityEngine.UI.Toggle toggle = diskImageListView.content.GetChild(i).GetComponent<UnityEngine.UI.Toggle>();
			if (toggle != null)
			{
				toggle.isOn = true;
			}
		}
	}

	public void SelectAllFilesButton()
	{
		SetSelectedFilesAll(true);
	}

	public void ClearAllFilesButton()
	{
		SetSelectedFilesAll(false);
	}

	void SetSelectedFilesAll(bool select)
	{
		if (diskFileListView.content.childCount > 0)
		{
			for (int i = 0; i < diskFileListView.content.childCount; i++)
			{
				UnityEngine.UI.Toggle toggle = diskFileListView.content.GetChild(i).GetComponent<UnityEngine.UI.Toggle>();
				if (toggle != null)
				{
					toggle.isOn = select;
				}
			}
		}
	}

	public void IMDConvertButton()
	{
	}

	public void ViewButton()
	{
		if (selectedFileList.Count > 0)
		{
			int n = selectedFileList[0];
			ViewFile(diskFileList[n].fileName, diskFileList[n].fileExt, diskFileList[n].imageIndex);
		}
	}

	public void ViewFileClose()
	{
		fileViewRoot.gameObject.SetActive(false);
		StartCoroutine(ScrollRectCleanupCoroutine());
	}

	IEnumerator ScrollRectCleanupCoroutine()
	{
		Debug.Log("ScrollRectCleanup begin");

		int n = 0;
		while (fileScrollRect.content.childCount > 0)
		{
			DestroyImmediate(fileScrollRect.content.GetChild(0).gameObject);
			n++;

			if (n == 1000)
			{
				yield return new WaitForEndOfFrame();
			}
		}
		yield return new WaitForEndOfFrame();

		Debug.Log("ScrollRectCleanup done");
	}

	public void ExtractButton()
	{
		//Debug.Log("ExtractButton()");
		FilePicker.Instance.title.text = "Choose Folder";
		FilePicker.Instance.onCompleteCallback += ExtractFileBinary;
		FilePicker.Instance.ShowPicker();
	}

	void ExtractFileBinary(string path)
	{
		FilePicker.Instance.onCompleteCallback -= ExtractFileBinary;
		ExtractFileBinaryComplete(path);
	}

	void ExtractFileBinaryComplete(string path)
	{
		StartCoroutine(ExtractFileBinaryCoroutine(path));
	}

	IEnumerator ExtractFileBinaryCoroutine(string path)
	{
		if (!string.IsNullOrEmpty(path))
		{
			if (selectedFileList.Count == 0)
			{
				// select all files
				SetSelectedFilesAll(true);
			}

			yield return new WaitForEndOfFrame();
			yield return new WaitForEndOfFrame();

			for (int i = 0; i < selectedFileList.Count; i++)
			{
				int n = selectedFileList[i];
				string fileName = diskFileList[n].fileName;
				string fileExt = diskFileList[n].fileExt;
				int diskImageIdx = diskFileList[n].imageIndex;
				FillFileBuffer(fileName, fileExt, diskImageIdx);

				string targetFolderPath = diskImageList[diskImageIdx].Substring(workingFolderText.text.Length + 1);
				targetFolderPath = System.IO.Path.Combine(path, targetFolderPath);

				//Debug.Log("targetFolderPath=" + targetFolderPath);

				if (!System.IO.Directory.Exists(targetFolderPath))
				{
					System.IO.Directory.CreateDirectory(targetFolderPath);
				}

				string s = fileName.Trim() + "." + fileExt.Trim();
				string filePath = System.IO.Path.Combine(targetFolderPath, s);

				//Debug.Log("filePath=" + filePath);
				if (IsBinaryFile())
				{
					System.IO.File.WriteAllBytes(filePath, fileBuffer);
				}
				else
				{
					string text = System.Text.Encoding.ASCII.GetString(fileBuffer);
					List<string> formattedText = FormatForView(text);
					
					System.IO.File.WriteAllLines(filePath, formattedText);
				}
			}
		}
	}

	// writes out diskFileContentList as an HTML file
	public void SaveHTMLButton()
	{
		List<string> htmlData = new List<string>();
		htmlData.Add("<!DOCTYPE html>");
		htmlData.Add("<html>");
		htmlData.Add("<body>");
		htmlData.Add("<center>");
		htmlData.Add("<pre>");

		htmlData.Add("<h2>DISK IMAGE CATALOG</h2>");

		int diskCount = 0;
		int fileCount = 0;
		for (int i = 0; i < diskFileList.Count; i++)
		{
			DiskFileItem item = diskFileList[i];
			if (item.type == 1)
			{
				// header
				htmlData.Add("<hr>");
				htmlData.Add("<h3>");
				htmlData.Add(item.lineItem);
				htmlData.Add("</h3>");
				diskCount++;
			}
			else if (item.type == 2)
			{
				// footer
				htmlData.Add("<h3>");
				htmlData.Add(item.lineItem);
				htmlData.Add("</h3>");
			}
			else
			{
				// file data
				htmlData.Add(item.lineItem.Trim());
				fileCount++;
			}
		}

		htmlData.Add("<hr>");

		string s = "<h3>TOTAL DISKS=" + diskCount.ToString() + " TOTAL FILES=" + fileCount.ToString() + "</h3>";
		htmlData.Add(s);

		htmlData.Add("<h4>Created by H8DUTILITY 3 on " + System.DateTime.Now.ToShortDateString() + "</h4>");

		htmlData.Add("<hr>");
		htmlData.Add("</pre>");
		htmlData.Add("</center>");
		htmlData.Add("</body>");
		htmlData.Add("</html>");

		string path = workingFolderText.text;
		string filePath = System.IO.Path.Combine(path, "H8DCATALOG.HTML");

		Debug.Log("SaveHTML() filePath=" + filePath);

		if (System.IO.File.Exists(filePath))
		{
			System.IO.File.Delete(filePath);
		}
		System.IO.File.WriteAllLines(filePath, htmlData.ToArray());
	}

	// writes out diskFileContentList as a text file
	public void SaveTextButton()
	{
		List<string> textData = new List<string>();

		string line = "==========================";

		textData.Add("DISK IMAGE CATALOG");
		textData.Add("");

		int diskCount = 0;
		int fileCount = 0;
		for (int i = 0; i < diskFileList.Count; i++)
		{
			DiskFileItem item = diskFileList[i];
			if (item.type == 1)
			{
				// header
				textData.Add(line);
				textData.Add(item.lineItem);
				textData.Add("");
				diskCount++;
			}
			else if (item.type == 2)
			{
				// footer
				textData.Add("");
				textData.Add(item.lineItem);
			}
			else
			{
				// file data
				textData.Add(item.lineItem.Trim());
				fileCount++;
			}
		}
		textData.Add(line);

		string s = "TOTAL DISKS=" + diskCount.ToString() + " TOTAL FILES=" + fileCount.ToString();
		textData.Add(s);
		textData.Add("");
		textData.Add("Created by H8DUTILITY 3 on " + System.DateTime.Now.ToShortDateString());

		string path = workingFolderText.text;
		string filePath = System.IO.Path.Combine(path, "H8DCATALOG.TXT");

		Debug.Log("SaveText() filePath=" + filePath);

		if (System.IO.File.Exists(filePath))
		{
			System.IO.File.Delete(filePath);
		}
		System.IO.File.WriteAllLines(filePath, textData.ToArray());
	}

	// insert a file into a HDOS disk image
	public void AddHDOSButton()
	{
	}


	// insert a file into a CP/M disk image
	public void AddCPMButton()
	{
	}

	// renames all disk images to a standard format (ex: 885-1234_FILENAME-ALL-CAPS-SEP-BY-DASHES.H8D)
	public void RenameAllButton()
	{
		// HUG naming: 885-1234_DISKLABEL-SEP-BY-DASHES.H8D
		// Generic naming: FILENAME-ALL-CAPS-SEP-BY-DASHES.H8D
		renamePanel.SetActive(true);
	}

	public void RenameAllContinue()
	{
		renamePanel.SetActive(false);
		StartCoroutine(RenameAllCoroutine());
	}

	public void RenameAllCancel()
	{
		renamePanel.SetActive(false);
	}

	IEnumerator RenameAllCoroutine()
	{
		yield return new WaitForEndOfFrame();

		for (int i = 0; i < diskImageList.Count; i++)
		{
			int idx = GetDiskImageListIdx(i);
			if (idx == -1)
			{
				continue;
			}

			int diskHash;
			string fileExt = System.IO.Path.GetExtension(diskImageList[idx]).ToUpper();

			string fileName = GetHDOSLabelOrFileName(diskImageList[idx], out diskHash);
			if (string.IsNullOrEmpty(fileName) || GetCleanFileName(fileName).Length < 20)
			{
				fileName = System.IO.Path.GetFileNameWithoutExtension(diskImageList[idx]);
			}

			string renameFile = GetRenameFileName(fileName, diskHash);
			renameFile += fileExt;

			string filePath = System.IO.Path.GetDirectoryName(diskImageList[idx]);
			string directoryName = filePath;
			filePath = System.IO.Path.Combine(directoryName, renameFile);

			if (!System.IO.File.Exists(filePath))
			{
				System.IO.File.Move(diskImageList[idx], filePath);
			}

			//Debug.Log("oldname=" + diskImageList[idx] + " newname=" + filePath);

			if ((i % 50) == 0)
			{
				yield return new WaitForEndOfFrame();
			}
		}

		yield return new WaitForEndOfFrame();

		SetWorkingFolder(workingFolderText.text);
		FillDiskImageListView();

		RenameAllCancel();
	}

	// fileName = fileName without extension
	public static string GetRenameFileName(string fileName, int diskHash)
	{
		string renameFile = GetCleanFileName(fileName);

		if (renameFile.Length >= 8)
		{
			string prefix = renameFile.Substring(0, 8);
			if (prefix.Contains("885-") && renameFile.Length >= 9)
			{
				// make sure 885-xxxx_
				if (renameFile[9] == '-')
				{
					string postfix = renameFile.Substring(10);
					renameFile = prefix + "_" + postfix;
				}
				else if (renameFile[10] == '-')
				{
					string postfix = renameFile.Substring(11);
					renameFile = prefix + "_" + postfix;
				}
				else
				{
					string postfix = renameFile.Substring(9);
					renameFile = prefix + "_" + postfix;
				}
			}
		}

		string hashStr = diskHash.ToString("X8");
		if (!renameFile.Contains(hashStr))
		{
			renameFile += "_" + hashStr;
		}

		renameFile = renameFile.ToUpper();

		return renameFile;
	}

	public static string GetHDOSLabelOrFileName(string diskImageListStr, out int diskHash)
	{
		string fileName = System.IO.Path.GetFileNameWithoutExtension(diskImageListStr);

		byte[] diskBytes = System.IO.File.ReadAllBytes(diskImageListStr);

		string labelFileName = GetHDOSLabel(diskBytes, out diskHash);
		if (!string.IsNullOrEmpty(labelFileName))
		{
			fileName = labelFileName;
		}
		return fileName;
	}

	public static string GetHDOSLabel(byte[] diskBytes, out int diskHash)
	{
		string labelName = string.Empty;
		if (IsHDOSDisk(diskBytes))
		{
			labelName = System.Text.ASCIIEncoding.ASCII.GetString(diskBytes, 0x911, 68);
		}
		diskHash = 0;
		for (int i = 0; i < diskBytes.Length; i++)
		{
			diskHash += diskBytes[i];
		}
		return labelName;
	}

	// s = fileName with no extension
	public static string GetCleanFileName(string s)
	{
		string fileName = string.Empty;

		bool escapeMode = false;
		bool graphicsMode = false;
		int spaceCount = 0;
		for (int i = 0; i < s.Length; i++)
		{
			if (escapeMode)
			{
				if (s[i] == 'F')
				{
					graphicsMode = true;
				}
				if (s[i] == 'G')
				{
					graphicsMode = false;
				}
				escapeMode = false;
				continue;
			}
			if (s[i] == 0x1B)
			{
				escapeMode = true;
				continue;
			}
			if (graphicsMode)
			{
				continue;
			}
			if (s[i] >= '0' && s[i] <= '9')
			{
				fileName += s[i];
				spaceCount = 0;
			}
			else if (s[i] >= 'A' && s[i] <= 'Z')
			{
				fileName += s[i];
				spaceCount = 0;
			}
			else if (s[i] >= 'a' && s[i] <= 'z')
			{
				fileName += s[i];
				spaceCount = 0;
			}
			else
			{
				if (fileName.Length > 0)
				{
					if (spaceCount == 0)
					{
						if (fileName[fileName.Length - 1] != '-')
						{
							fileName += '-';
						}
					}
				}
				spaceCount = 1;
			}
		}

		if (s.Contains("885-"))
		{
			int startIndex = s.IndexOf("885-");
			if (startIndex >= 8)
			{
				// move P/N to front if not already there
				int n = Mathf.Min(s.Length - startIndex, 8);
				string partNum = s.Substring(startIndex, n).Trim();
				fileName = partNum + "_" + fileName;
			}
		}

		char[] trimChars = { ' ', '-' };
		fileName = fileName.Trim(trimChars);

		return fileName;
	}

	public void SearchText()
	{
		searchString = searchInputField.text.ToUpper();
		FillDiskImageListView();
	}

	public void SearchClear()
	{
		searchInputField.text = string.Empty;
		//searchString = string.Empty;
		//FillDiskImageListView();
	}

	int GetDiskImageListIdx(int i)
	{
		if (diskImageListIdx.Count > 0)
        {
			if (!diskImageListIdx.Contains(i))
            {
				return -1;
            }
        }
		return i;
	}

	public string ByteBufferToString(byte[] b)
	{
		return System.Text.Encoding.ASCII.GetString(b);
	}

	public bool ByteCompare(byte[] a, byte[] b)
	{
		if (a.Length != b.Length)
		{
			return false;
		}
		for (int i = 0; i < a.Length; i++)
		{
			if (a[i] != b[i])
			{
				return false;
			}
		}
		return true;
	}

	// checks disk image to determine if it is an HDOS disk
	public static bool IsHDOSDisk(byte[] diskImageBuffer)
	{
		if ((diskImageBuffer[0] == 0xAF && diskImageBuffer[1] == 0xD3 && diskImageBuffer[2] == 0x7D && diskImageBuffer[3] == 0xCD) ||   //  V1.x
			(diskImageBuffer[0] == 0xC3 && diskImageBuffer[1] == 0xA0 && diskImageBuffer[2] == 0x22 && diskImageBuffer[3] == 0x20) ||   //  V2.x
			(diskImageBuffer[0] == 0xC3 && diskImageBuffer[1] == 0xA0 && diskImageBuffer[2] == 0x22 && diskImageBuffer[3] == 0x30) ||   //  V3.x
			(diskImageBuffer[0] == 0xC3 && diskImageBuffer[1] == 0x1D && diskImageBuffer[2] == 0x24 && diskImageBuffer[3] == 0x20) ||   //  V2.x Super-89
			(diskImageBuffer[0] == 0xC3 && diskImageBuffer[1] == 0xF8 && diskImageBuffer[2] == 0x23 && diskImageBuffer[3] == 0x20) ||   //  ?
			(diskImageBuffer[0] == 0x18 && diskImageBuffer[1] == 0x1E && diskImageBuffer[2] == 0x13 && diskImageBuffer[3] == 0x20) ||   //  OMDOS
			(diskImageBuffer[0] == 0xC3 && diskImageBuffer[1] == 0xD1 && diskImageBuffer[2] == 0x23 && diskImageBuffer[3] == 0x20))     //  OMDOS
		{
			return (true);
		}
		return false;
	}

	public static bool IsCPMDisk(byte[] diskImageBuffer)
	{
		if (IsHDOSDisk(diskImageBuffer))
		{
			return false;
		}
		return true;
	}
}
