using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class H8DCataloger : MonoBehaviour
{
	public UnityEngine.UI.InputField textFieldPrefab;
	public UnityEngine.UI.InputField fileFieldPrefab;
	public UnityEngine.UI.InputField fileTitlePrefab;
	public UnityEngine.UI.ScrollRect diskImageListView;
	public UnityEngine.UI.ScrollRect diskFileListView;
	public UnityEngine.UI.Text workingFolderText;
	public UnityEngine.UI.Text diskImageCount;
	public UnityEngine.UI.Text diskFileCount;
	public UnityEngine.UI.InputField diskLabelText;
	public UnityEngine.UI.InputField diskVolume;

	public struct DiskFileItem
	{
		public string lineItem;
		public int type;
		public int size;
		public int imageIndex;
		public string fileName;
		public string fileExt;
	}

	private List<string> diskImageList = new List<string>();
	private List<DiskFileItem> diskFileList = new List<DiskFileItem>();

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

	public struct CPMDirEntry
	{
		public byte flag;
		public byte[] filename;
		public byte[] fileext;
		public byte extent;
		public byte[] unused;
		public byte sector_count;
		public byte[] alloc_map;
	}

	private CPMDirEntry cpmDirEntry = new CPMDirEntry();

	public struct DiskContentItem
	{
		public string fileName;
		public string fileExt;
		public long fileSize;
		public string fileCreateDate;
		public string fileAlterationDate;
	}

	private List<DiskContentItem> diskFileContentList = new List<DiskContentItem>();

	private List<int> selectedDiskImageList = new List<int>();
	private List<int> selectedFileList = new List<int>();

	private int diskTotalSize;
	private int diskFreeSize;

	private int currentDiskIdx;
	private byte[] diskImageBuffer;
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
	private byte[] fileBuffer;

	void Start()
	{
		dirEntry.filename = new byte[8];
		dirEntry.fileext = new byte[3];
		string path = PlayerPrefs.GetString("workingfolder");
		SetWorkingFolder(path);
	}

	void Update()
	{
		if (H8DManager.Instance.GetButtonDown())
		{
			bool shifted = H8DManager.Instance.IsShifted();
			Vector2 mousePos = Input.mousePosition;

			for (int i = 0; i < diskImageListView.content.childCount; i++)
			{
				Transform t = diskImageListView.content.GetChild(i);
				RectTransform rect = t.GetComponent<RectTransform>();
				if (rect != null)
				{
					UnityEngine.UI.InputField text = t.GetComponent<UnityEngine.UI.InputField>();
					if (text != null)
					{
						float x1 = rect.position.x;
						float y1 = rect.position.y + (rect.rect.height / 2);
						float x2 = x1 + rect.rect.width / 2;
						float y2 = y1 - rect.rect.height;
						if (mousePos.x > x1 && mousePos.x < x2 && mousePos.y < y1 && mousePos.y > y2)
						{
							if (selectedDiskImageList.Contains(i))
							{
								selectedDiskImageList.Remove(i);
							}
							else
							{
								if (shifted && selectedDiskImageList.Count > 0)
								{
									int lastIdx = selectedDiskImageList[selectedDiskImageList.Count - 1];
									if (lastIdx < i)
									{
										for (int n = lastIdx; n < i; n++)
										{
											selectedDiskImageList.Add(n);
										}
									}
									else if (lastIdx > i)
									{
										for (int n = i; n < lastIdx; n++)
										{
											selectedDiskImageList.Add(n);
										}
									}
								}
								selectedDiskImageList.Add(i);
								selectedDiskImageList.Sort();
							}
						}
					}
				}
			}

			for (int i = 0; i < diskImageListView.content.childCount; i++)
			{
				Transform t = diskImageListView.content.GetChild(i);
				UnityEngine.UI.InputField text = t.GetComponent<UnityEngine.UI.InputField>();
				if (selectedDiskImageList.Contains(i))
				{
					text.image.color = Color.green;
				}
				else
				{
					text.image.color = Color.white;
				}
			}

			string lastDiskLabel = string.Empty;
			string lastDiskVolume = string.Empty;
			for (int i = 0; i < diskFileListView.content.childCount; i++)
			{
				Transform t = diskFileListView.content.GetChild(i);
				RectTransform rect = t.GetComponent<RectTransform>();
				if (rect != null)
				{
					UnityEngine.UI.InputField text = t.GetComponent<UnityEngine.UI.InputField>();
					if (text != null)
					{
						float x1 = rect.position.x;
						float y1 = rect.position.y + (rect.rect.height / 2);
						float x2 = x1 + rect.rect.width / 2;
						float y2 = y1 - rect.rect.height;
						if (mousePos.x > x1 && mousePos.x < x2 && mousePos.y < y1 && mousePos.y > y2)
						{
							if (diskFileList[i].type == 0)
							{
								if (selectedFileList.Contains(i))
								{
									selectedFileList.Remove(i);
								}
								else
								{
									selectedFileList.Add(i);
								}
								//diskVolume.text = lastDiskVolume;
								//diskLabelText.text = lastDiskLabel;
							}
						}
					}
					if (diskFileList[i].type == 0)
					{
						if (selectedFileList.Contains(i))
						{
							text.image.color = Color.green;
						}
						else
						{
							text.image.color = Color.white;
						}
					}
					if (diskFileList[i].type == 1)
					{
						if (!string.IsNullOrEmpty(diskFileList[i].lineItem) && diskFileList[i].lineItem.Length > 3)
						{
							string v = diskFileList[i].lineItem.Substring(0, 3);
							lastDiskVolume = v;
							string l = diskFileList[i].lineItem.Substring(4);
							lastDiskLabel = l;
						}
					}
				}
			}
		}
	}

	void ProcessFile(int diskImageIdx)
	{
		currentDiskIdx = diskImageIdx;
		string filePath = diskImageList[diskImageIdx];

		if (System.IO.File.Exists(filePath))
		{
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

	void ProcessHDOSDiskImage()
	{
		//Debug.Log("ProcessHDOSDiskImage()");
		HDOSDiskInfo disk_info = new HDOSDiskInfo();
		disk_info.label = new byte[60];
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
		System.Buffer.BlockCopy(diskImageBuffer, offset, disk_info.label, 0, 60);
		offset += 60;
		disk_info.reserved = System.BitConverter.ToUInt16(diskImageBuffer, offset);
		offset += 2;
		disk_info.sectors_per_track = diskImageBuffer[offset++];

		Debug.Log("disk_info.dir_sector=" + disk_info.dir_sector.ToString("X") + " grt_sector=" + disk_info.grt_sector.ToString("X"));

		string labelstr = System.Text.Encoding.ASCII.GetString(disk_info.label, 0, 60);
		Debug.Log("Disk Label:" + labelstr);
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
		while (true)
		{
			if (offset == 0)
			{
				break;
			}

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
					entry_count = 0;
					Debug.Log("offset=" + offset.ToString("X"));
				}
				if (offset == 0)
				{
					break;
				}
				if (entry.filename[0] == 0xFE || entry.filename[0] == 0xFF)
				{
					// empty entry - try next file
					continue;
				}
				break;
			}

			if (entry.filename[0] != 0xFE && entry.filename[0] != 0xFF)
			{
				int fsize = ComputeHDOSFileSize(entry, disk_info.sectors_per_group);

				if (fsize == -1)
				{
					Debug.Log("!! DIRECTORY IS CORRUPT !!");
					Debug.Log("!!   FILESIZE FAILED    !!");
					return;
				}

				diskTotalSize += (ushort)fsize;
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
					Debug.Log("Searching for " + searchFileName + "." + searchFileExt + " found file " + f + "." + e);
					if (f.Equals(searchFileName) && e.Equals(searchFileExt))
					{
						fileBuffer = new byte[fsize];
						FillFileBufferHDOS(entry);
						return;
					}
				}
			}
		}

		if (string.IsNullOrEmpty(searchFileName))
		{
			diskFreeSize = ComputeHDOSFreeSize(disk_info.sectors_per_group);
		}
	}

	int ComputeHDOSFileSize(HDOSDirEntry entry, int sectorsPerGroup)
	{
		int grp_count = 1;
		byte grp = entry.first_group_num;
		if (grp < 4 || grp >= 200)
		{
			return 0;
		}
		while (HDOSGrtTable[grp] != 0 && grp_count < 256)
		{
			if (grp < 4 || grp >= 200)
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

		int total_size = ((grp_count - 1) * sectorsPerGroup) + entry.last_sector_index;
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

	void FillFileBufferHDOS(HDOSDirEntry entry)
	{
		Debug.Log("FillFileBuffer()");
		// get diskInfo
		// fill file buffer
		// dump to text
	}

	void ProcessCPMDiskImage()
	{
		int fsize = 0;
		int disk_file_count = 0;
		int total_size = 0;
		int disk_size = diskImageBuffer.Length / 1024 - 10; // dcp disk size on disk

		CPMDirEntry entry = new CPMDirEntry();
		entry.filename = new byte[8];
		entry.fileext = new byte[3];
		entry.unused = new byte[2];
		entry.alloc_map = new byte[16];
		int offset = 0x1E00;
		int startOffset = offset;

		while (true)
		{
			while (true)
			{
				if (offset < diskImageBuffer.Length)
				{
					entry.flag = diskImageBuffer[offset++];
					System.Buffer.BlockCopy(diskImageBuffer, offset, entry.filename, 0, 8);
					offset += 8;
					System.Buffer.BlockCopy(diskImageBuffer, offset, entry.fileext, 0, 3);
					offset += 3;
					for (int i = 0; i < 3; i++)
					{
						entry.fileext[i] = (byte)(entry.fileext[i] & 0x7f);
					}
					entry.extent = diskImageBuffer[offset++];
					System.Buffer.BlockCopy(diskImageBuffer, offset, entry.unused, 0, 2);
					offset += 2;
					entry.sector_count = diskImageBuffer[offset++];
					System.Buffer.BlockCopy(diskImageBuffer, offset, entry.alloc_map, 0, 16);
					offset += 16;
					if (diskImageBuffer.Length < (2560 * 2 * 80))
					{
						// 1S40T or 2S40T
						if (offset == 0x2100)
						{
							offset = 0x2200;
						}
						else if (offset == 0x2500)
						{
							offset = 0x2600;
						}
						else if (offset == 0x2800)
						{
							offset = 0;
							break;
						}
					}
					else
					{
						// 2S80T
						if (offset == 0x2B00)
						{
							offset = 0x2C00;
						}
						else if (offset == 0x2D00)
						{
							offset = 0x2E00;
						}
						else if (offset == 0x2F00)
						{
							offset = 0x3000;
						}
						else if (offset == 0x3100)
						{
							offset = 0;
							break;
						}
					}
					/*
					// check for erased sector - all 0xE5 and adjust directory start point if needed
					if (entry.flag == 0xE5 && entry.filename[0] == 0xE5)
					{
						if (startOffset == 0x1E00)
						{
							offset = 0x2200;
							startOffset = offset;
						}
						else if (startOffset == 0x2200)
						{
							offset = 0x2600;
							startOffset = offset;
						}
						else
						{
							// all directory sectors searched, exit out of loop
							offset = 0;
							startOffset = offset;
							break;
						}

						continue;
					}
					*/
					if (entry.flag != 0xE5) // dcp not an erased directory entry
					{
						// valid directory entry
						break;
					}

					/* dcp
                    if (entry.flag != 0)
                    {
                        continue;
                    }
                    */
				}
				else
				{
					// bail out of loop
					diskFileContentList.Clear();
					total_size = 0;
					offset = 0;
					break;
				}
			}

			if (offset == 0)
			{
				break;
			}

			fsize += (ushort)(entry.sector_count * 128);
			// dcp assumes directory entries are sequential

			if (entry.sector_count < 0x80)
			{
				if (fsize % 1024 != 0)
					fsize = (ushort)(fsize / 1024 + 1);
				else
					fsize = (ushort)(fsize / 1024);
				string fname = System.Text.Encoding.UTF8.GetString(entry.filename, 0, 8);
				string f = fname.Replace('\0', ' ');
				string fext = System.Text.Encoding.UTF8.GetString(entry.fileext, 0, 3);
				string e = fext.Replace('\0', ' ');

				if (string.IsNullOrEmpty(searchFileName))
				{
					DiskContentItem disk_file_entry = new DiskContentItem();
					disk_file_entry.fileName = f;
					disk_file_entry.fileExt = e;
					disk_file_entry.fileSize = fsize;
					disk_file_entry.fileCreateDate = "         ";
					disk_file_entry.fileAlterationDate = string.Empty;
					diskFileContentList.Add(disk_file_entry);
				}
				else
				{
					if (f.Equals(searchFileName) && e.Equals(searchFileExt))
					{
						fileBuffer = new byte[fsize];
						FillFileBufferCPM(entry);
						return;
					}
				}

				total_size += fsize;

				disk_file_count++;
				fsize = 0;
			}
		}

		diskTotalSize = total_size;
		diskFreeSize = disk_size - total_size; // dcp
	}

	void FillFileBufferCPM(CPMDirEntry entry)
	{
	}

	DiskFileBuffer FillFileBuffer(string fileName, string fileExt, int imageIdx)
	{
		searchFileName = fileName;
		searchFileExt = fileExt;

		Debug.Log("Search file=" + fileName + "." + fileExt + " imageIdx=" + imageIdx.ToString());

		ProcessFile(imageIdx);

		DiskFileBuffer b = new DiskFileBuffer();
		if (fileBuffer != null && fileBuffer.Length > 0)
		{
			b.fileBuffer = fileBuffer;
			b.fileName = fileName;
			b.fileExt = fileExt;
			b.fileSize = fileBuffer.Length;
		}
		return b;
	}

	// view a file with filename.ext on disk image imageIdx
	void ViewFile(string fileName, string fileExt, int imageIdx)
	{
		DiskFileBuffer buffer = FillFileBuffer(fileName, fileExt, imageIdx);
	}


	void FillDiskImageListView()
	{
		while (diskImageListView.content.childCount > 0)
		{
			Transform t = diskImageListView.content.GetChild(0);
			DestroyImmediate(t.gameObject);
		}

		for (int i = 0; i < diskImageList.Count; i++)
		{
			UnityEngine.UI.InputField textField = Instantiate(textFieldPrefab, diskImageListView.content);
			textField.text = diskImageList[i].Substring(workingFolderText.text.Length);
		}

		diskImageCount.text = diskImageList.Count.ToString() + " images";
	}

	void FillDiskFileListView()
	{
		while (diskFileListView.content.childCount > 0)
		{
			Transform t = diskFileListView.content.GetChild(0);
			DestroyImmediate(t.gameObject);
		}

		for (int i = 0; i < diskFileList.Count; i++)
		{
			UnityEngine.UI.InputField textField = null;
			if (diskFileList[i].type == 1)
			{
				textField = Instantiate<UnityEngine.UI.InputField>(fileTitlePrefab, diskFileListView.content);
			}
			else
			{
				textField = Instantiate<UnityEngine.UI.InputField>(fileFieldPrefab, diskFileListView.content);
				if (diskFileList[i].type == 2)
				{
					textField.image.color = Color.green;
				}
				else
				{
					textField.image.color = Color.white;
				}
			}
			textField.text = diskFileList[i].lineItem;
		}

		diskFileCount.text = diskFileList.Count.ToString() + " files";
	}

	public void FolderButton()
	{
		string path = Crosstales.FB.FileBrowser.OpenSingleFolder();
		SetWorkingFolder(path);
	}

	public void SetWorkingFolder(string path)
	{
		workingFolderText.text = path;

		Debug.Log("workingFolder=" + workingFolderText.text);

		if (!string.IsNullOrEmpty(workingFolderText.text))
		{
			string[] files = System.IO.Directory.GetFiles(workingFolderText.text, "*.?8?", System.IO.SearchOption.AllDirectories); // match .h8d or .H8D or .H8d
			if (files.Length > 0)
			{
				Debug.Log("H8D files=" + files.Length.ToString());
				diskImageList = new List<string>(files);
				diskImageList.Sort();

				FillDiskImageListView();

				PlayerPrefs.SetString("workingfolder", workingFolderText.text);
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
			ProcessFile(selectedDiskImageList[i]);

			int idx = selectedDiskImageList[i];
			string title = diskImageList[idx].Substring(workingFolderText.text.Length);
			int n = title.IndexOf(System.IO.Path.DirectorySeparatorChar);
			title = title.Substring(n + 1).ToUpper();
			title = title.Replace(".H8D", "");
			AddFileItem(title, 1, 12, idx);
			for (int j = 0; j < diskFileContentList.Count; j++)
			{
				// add to the file list UI scroll view
				string s = diskFileContentList[j].fileName + "." + diskFileContentList[j].fileExt + " " + diskFileContentList[j].fileSize.ToString("D4") + " " + diskFileContentList[j].fileCreateDate;
				AddFileItem(s, 0, 20, idx, j);
			}
			string freeBlocks = "USED=" + diskTotalSize.ToString("D4") + " FREE=" + diskFreeSize.ToString("D4");
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
		selectedFileList.Clear();
		selectedDiskImageList.Clear();
		for (int i = 0; i < diskImageListView.content.childCount; i++)
		{
			UnityEngine.UI.InputField textField = diskImageListView.content.GetChild(i).GetComponent<UnityEngine.UI.InputField>();
			textField.image.color = Color.white;
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

	public void ExtractButton()
	{
	}

	public void SaveHTMLButton()
	{
	}

	public void SaveTextButton()
	{
	}

	public void AddHDOSButton()
	{
	}

	public void AddCPMButton()
	{
	}

	public static bool IsHDOSDisk(byte[] diskImageBuffer)
	{
		if ((diskImageBuffer[0] == 0xAF && diskImageBuffer[1] == 0xD3 && diskImageBuffer[2] == 0x7D && diskImageBuffer[3] == 0xCD) ||   //  V1.x
			(diskImageBuffer[0] == 0xC3 && diskImageBuffer[1] == 0xA0 && diskImageBuffer[2] == 0x22 && diskImageBuffer[3] == 0x20) ||   //  V2.x
			(diskImageBuffer[0] == 0xC3 && diskImageBuffer[1] == 0xA0 && diskImageBuffer[2] == 0x22 && diskImageBuffer[3] == 0x30) ||   //  V3.x
			(diskImageBuffer[0] == 0xC3 && diskImageBuffer[1] == 0x1D && diskImageBuffer[2] == 0x24 && diskImageBuffer[3] == 0x20) ||     //  V2.x Super-89
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
