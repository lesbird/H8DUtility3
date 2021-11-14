using System.Collections;
using System.Collections.Generic;
using System.IO.Ports;
using UnityEngine;

/// <summary>
/// H8DImager
/// Class to communicate with H89LDR on a Heathkit computer over a serial port.
/// H89LDR commands:
/// R - read image from H8/89 to PC over serial port
///		(R then R again for each track - replies with 'R' after each track or 'r' if bad read
/// W - write image from PC to H8/H89 over serial port
///		(W then W again for each track - replies with 'W' after each track
///		H37: W followed by [SPT],[SECSIZ],[SIDES],[DENSITY] then W for each track
/// V - set volume number
/// C - check volume number
/// T - read disk volume
/// 0 - set SY0
/// 1 - set SY1
/// 2 - set SY2
/// 4 - set 1S40T
/// 5 - set 2S40T
/// 6 - set 1S80T
/// 7 - set 2S80T
/// 9 - set 9600 baud
/// ( - set 19200 baud
/// A - set disk side 0
/// B - set disk side 1
/// Q - query for disk type returns 4,5,6,7 or (H37) secs per track, nsides, secsize, density
/// Z - read track
/// E - examine track (attempts full track read, returns data)
/// F - format disk
/// ? - returns '?' if alive
/// </summary>
public class H8DImager : MonoBehaviour
{
	public UnityEngine.UI.Dropdown comDropdown;
	public UnityEngine.UI.Dropdown driveDropdown;
	public UnityEngine.UI.Toggle drive80TrkToggle;
	public UnityEngine.UI.Toggle driveDSToggle;
	public UnityEngine.UI.Button[] buttonsToEnable;
	public UnityEngine.UI.Text textLogPrefab;
	public UnityEngine.UI.ScrollRect scrollViewRect;
	public UnityEngine.UI.InputField trackNumberField;
	public UnityEngine.UI.InputField diskLabelField;
	public UnityEngine.UI.Toggle volumeOverrideToggle;
	public UnityEngine.UI.InputField volumeNumberField;
	public UnityEngine.UI.Slider progressBar;
	public UnityEngine.UI.Text progressValue;
	public UnityEngine.UI.Button clientStatusButton;
	public UnityEngine.UI.Button saveLoaderButton;
	public UnityEngine.UI.Toggle driveOverride;
	public UnityEngine.UI.Toggle h37Toggle;
	public UnityEngine.UI.Toggle densityToggle;
	public UnityEngine.UI.InputField sectorsPerTrackField;
	public UnityEngine.UI.Toggle baud9600Toggle;
	public UnityEngine.UI.Toggle baud19200Toggle;
	public UnityEngine.UI.Toggle baud38400Toggle;
	public UnityEngine.UI.Toggle baud56000Toggle;
	public GameObject formatDiskPanel;
	public UnityEngine.UI.Toggle formatDiskDSToggle;
	public UnityEngine.UI.Toggle formatDiskDensToggle;
	public UnityEngine.UI.Toggle formatDisk05Toggle;
	public UnityEngine.UI.Toggle formatDisk08Toggle;
	public UnityEngine.UI.Toggle formatDisk09Toggle;
	public UnityEngine.UI.Toggle formatDisk10Toggle;
	public UnityEngine.UI.Toggle formatDisk16Toggle;
	public GameObject saveLoaderPanel;
	public GameObject sendLoaderPanel;
	public GameObject readDiskVerifyPanel;
	public GameObject sendDiskVerifyPanel;

	private SerialPort serialPort;
	private int inBufIdx;
	private int expectedBytes;
	private bool readTrack;

	private int h37Sectors;
	private int h37SectorSize;
	private int h37Tracks;
	private int h37Sides;
	private string h37Density;

	private int volumeOverrideValue;

	private bool isReadingDisk;
	private bool isWritingDisk;
	private bool abortTransfer;

	public int baudUpdate;

	private bool saveImage;
	private byte[] readBuf = new byte[(8192 * 80 * 2) + 1]; // +1 to account for handshake byte
	private int readBufIdx;
	private byte[] sendBuf;
	private int sendBufIdx;

	public static H8DImager Instance;

    void Awake()
    {
		Instance = this;
    }

    void Start()
	{
		DisableButtons();
		ShowHelp();
	}

	public void COMInit()
	{
		string[] comPortNames = SerialPort.GetPortNames();
		List<string> comPortNamesList = new List<string>();
		//comPortNamesList.Add("H89EMU");
		if (comPortNames != null && comPortNames.Length > 0)
		{
			for (int i = 0; i < comPortNames.Length; i++)
			{
				comPortNamesList.Add(comPortNames[i]);
			}
		}
		comDropdown.ClearOptions();
		comDropdown.AddOptions(comPortNamesList);

		string lastPortUsed = PlayerPrefs.GetString("port");
		if (!string.IsNullOrEmpty(lastPortUsed))
		{
			for (int i = 0; i < comDropdown.options.Count; i++)
			{
				if (comDropdown.options[i].text.Equals(lastPortUsed))
				{
					comDropdown.value = i;
					baudUpdate = PlayerPrefs.GetInt("baud");
					//baudUpdate = 9600;
				}
			}
		}

		if (serialPort != null)
		{
			if (serialPort.IsOpen)
			{
				serialPort.Close();
				serialPort = null;
			}
		}

		//SendToLog(comPortNamesList.Count.ToString() + " ports detected");
		SendToLog("DISK IMAGER READY");
	}

	void COMOpen()
	{
		int baud = baud9600Toggle.isOn ? 9600 : baud19200Toggle.isOn ? 19200 : baud38400Toggle.isOn ? 38400 : 56000;
		string comPortString = comDropdown.captionText.text;

		//SendToLog("Attempt to open " + comPortString);

		if (serialPort != null && serialPort.IsOpen)
		{
			serialPort.Close();
			serialPort = null;
		}

		//SendToLog("Opening " + comPortString);

		serialPort = new SerialPort(comPortString, baud, Parity.None, 8, StopBits.One);
		serialPort.Handshake = Handshake.None;
		serialPort.ReadBufferSize = 4096;

		SendToLog(serialPort.PortName + " BAUD=" + baud.ToString());

		PlayerPrefs.SetString("port", serialPort.PortName);
		PlayerPrefs.SetInt("baud", baud);

		try
		{
			serialPort.Open();
			serialPort.RtsEnable = true;
			serialPort.DtrEnable = true;

			//SendToLog("SerialPort open");
		}
		catch
		{
			serialPort.Close();
			serialPort = null;

			//SendToLog("No serial port found");
		}
	}

	void ShowHelp()
	{
		//SendToLog("<color=lime>H8D DISK IMAGER</color>");
	}

	public void H37ToggleSelected()
	{
		if (h37Toggle.isOn)
        {
			driveOverride.isOn = true;
			driveDSToggle.isOn = true;
        }
	}

	public void Baud9600Toggle()
	{
		if (baudUpdate != 0)
		{
			return;
		}
		baudUpdate = 9600;
	}

	public void Baud19200Toggle()
	{
		if (baudUpdate != 0)
		{
			return;
		}
		baudUpdate = 19200;
	}

	public void Baud38400Toggle()
	{
		if (baudUpdate != 0)
		{
			return;
		}
		baudUpdate = 38400;
	}

	public void Baud56000Toggle()
	{
		if (baudUpdate != 0)
		{
			return;
		}
		baudUpdate = 56000;
	}

	void Update()
	{
		//if (Input.GetKeyDown(KeyCode.T))
		//{
		//	SendToLog("Test line " + Random.Range(0, 65536).ToString());
		//}

		if (saveImage)
        {
			saveImage = false;
			SaveDiskImage();
        }

		if (baudUpdate != 0)
		{
			baud9600Toggle.isOn = (baudUpdate == 9600) ? true : false;
			baud19200Toggle.isOn = (baudUpdate == 19200) ? true : false;
			baud38400Toggle.isOn = (baudUpdate == 38400) ? true : false;
			baud56000Toggle.isOn = (baudUpdate == 56000) ? true : false;

			SendToLog("BAUD RATE SET TO " + baudUpdate.ToString("N0"));

			if (serialPort != null && serialPort.IsOpen)
			{
				//byte[] cmdBuf = new byte[16];
				//cmdBuf[0] = (byte)'9';
				//if (baud19200Toggle.isOn)
				//{
				//	cmdBuf[0] = (byte)'(';
				//}
				//serialPort.Write(cmdBuf, 0, 1);

				serialPort.Close();
				serialPort = null;

				DisableButtons();
			}

			baudUpdate = 0;
		}

		if (isReadingDisk || isWritingDisk)
		{
			driveDropdown.interactable = false;
			baud9600Toggle.interactable = false;
			baud19200Toggle.interactable = false;
			baud38400Toggle.interactable = false;
			baud56000Toggle.interactable = false;
			volumeNumberField.interactable = false;
		}
		else
		{
			volumeNumberField.interactable = volumeOverrideToggle.isOn;
			if (volumeNumberField.interactable)
			{
				if (!string.IsNullOrEmpty(volumeNumberField.text))
				{
					int v;
					if (int.TryParse(volumeNumberField.text, out v))
					{
						volumeOverrideValue = (byte)v;
					}
				}
			}

			if (driveOverride.isOn)
			{
				driveDSToggle.interactable = true;
			}
			else
			{
				driveDSToggle.interactable = false;
			}

			driveDropdown.interactable = true;
			if (driveDropdown.captionText.text.Equals("SY1"))
			{
				drive80TrkToggle.isOn = true;
			}
			else
			{
				drive80TrkToggle.isOn = false;
			}
		}

		if (formatDiskPanel.activeInHierarchy)
		{
			// formatting disk
		}
	}

	void SaveDiskImage()
	{
		FilePicker.Instance.title.text = "Save Disk Image";
		FilePicker.Instance.onCompleteCallback += SaveDiskImageComplete;
		FilePicker.Instance.onOpenPicker += SaveDiskOnOpen;
		FilePicker.Instance.ShowPicker(true);
	}

	void SaveDiskOnOpen()
	{
		FilePicker.Instance.onOpenPicker -= SaveDiskOnOpen;

		int diskHash;
		string fileName = H8DCataloger.GetHDOSLabel(readBuf, out diskHash);
		if (string.IsNullOrEmpty(fileName) || H8DCataloger.GetCleanFileName(fileName).Length < 20)
        {
			fileName = H8DCataloger.GetCleanFileName(diskLabelField.text);
        }
		fileName = H8DCataloger.GetRenameFileName(fileName, diskHash);
		fileName += h37Toggle.isOn ? ".H37" : ".H8D";

		/*
		fileName = fileName.Replace(' ', '-');

		if (h37Toggle.isOn)
		{
			// 160256-1S40T-MFM-
			string filePre = h37Sectors.ToString("D2") + h37SectorSize.ToString("D4") + "-" + h37Sides.ToString() + "S" + h37Tracks.ToString("D2") + "T-" + h37Density + "_";
			string filePost = "_" + System.DateTime.Now.ToString("yyyyMMddHHmm");
			fileName = filePre + fileName + filePost + ".H37";
		}
		*/
		FilePicker.Instance.fileInputField.text = fileName;
	}

	void SaveDiskImageComplete(string path)
	{
		FilePicker.Instance.onCompleteCallback -= SaveDiskImageComplete;

		string finalPath = path;

		if (h37Toggle.isOn)
		{
			// "012345678901234567890123456789012" 32 bytes meta data
			// "SPT=xx SSZ=xxxx TRK=xx SID=xx MFM"
			byte[] data = new byte[readBufIdx + 32];
			System.Buffer.BlockCopy(readBuf, 0, data, 0, readBufIdx);
			string diskSpecs = "SPT=" + h37Sectors.ToString("D2") + " SSZ=" + h37SectorSize.ToString("D4") + " TRK=" + h37Tracks.ToString("D2") + " SID=" + h37Sides.ToString() + " " + h37Density;
			byte[] specs = System.Text.Encoding.ASCII.GetBytes(diskSpecs);
			System.Buffer.BlockCopy(specs, 0, data, readBufIdx, specs.Length);
			System.IO.File.WriteAllBytes(finalPath, data);
		}
		else
		{
			byte[] data = new byte[readBufIdx];
			System.Buffer.BlockCopy(readBuf, 0, data, 0, readBufIdx);
			System.IO.File.WriteAllBytes(finalPath, data);
		}

		SendToLog("File saved to " + finalPath);
		SendToLog("Total bytes " + readBufIdx.ToString("N0"));

		clientStatusButton.interactable = true;
		EnableButtons();
	}

	public void ClientStatusPressed()
	{
		// send command and wait for response
		StartCoroutine(CheckClientReady());
	}

	IEnumerator CheckClientReady()
	{
		if (serialPort == null || !serialPort.IsOpen)
		{
			COMOpen();
		}

		serialPort.DiscardInBuffer();

		yield return new WaitForEndOfFrame();

		if (serialPort != null && serialPort.IsOpen)
		{
			byte[] cmdBuf = new byte[16];
			cmdBuf[0] = (byte)'?';
			serialPort.Write(cmdBuf, 0, 1);

			//SendToLog("SYSTEM QUERY SENT");

			yield return new WaitForEndOfFrame();

			bool clientIsReady = false;

			while (true)
			{
				try
				{
					if (serialPort.BytesToRead > 0)
					{
						int c = serialPort.ReadByte();
						if (c == '?')
						{
							clientIsReady = true;
							SendToLog("Client is ready");
							EnableButtons();
						}
						else
						{
							SendToLog("Client is not ready");
							DisableButtons();
						}
						break;
					}
				}
				catch
				{
					DisableButtons();
				}

				yield return new WaitForEndOfFrame();
			}

			if (clientIsReady)
			{
				// determine if using H37Imager
				cmdBuf[0] = (byte)'3';
				serialPort.Write(cmdBuf, 0, 1);
				int res = serialPort.ReadByte();
				if (res == cmdBuf[0])
				{
					h37Toggle.isOn = true;
				}
			}
		}
	}

	bool SetDrive()
	{
		byte[] cmdBuf = new byte[16];

		if (driveDropdown.captionText.text.Equals("SY1"))
		{
			//drive80TrkToggle.isOn = true;
			cmdBuf[0] = (byte)'1';
		}
		else
		{
			cmdBuf[0] = (byte)'0';
		}

		// send drive designator (0,1,2)
		serialPort.Write(cmdBuf, 0, 1);
		int res = serialPort.ReadByte();
		if (res == cmdBuf[0])
		{
			return true;
		}
		return false;
	}

	// 0 or 1
	bool SetSide(int side)
	{
		Debug.Log("SetSide() side=" + side.ToString());

		byte[] cmdBuf = new byte[16];

		cmdBuf[0] = (byte)'A';
		if (side != 0)
		{
			cmdBuf[0] = (byte)'B';
		}
		serialPort.Write(cmdBuf, 0, 1);
		int res = serialPort.ReadByte();
		if (res == cmdBuf[0])
		{
			return true;
		}
		return false;
	}

	public void DiskQueryPressed()
	{
		StartCoroutine(DiskQueryCoroutine());
	}

	IEnumerator DiskQueryCoroutine()
	{
		byte[] cmdBuf = new byte[16];

		if (SetDrive())
		{
			cmdBuf[0] = (byte)'Q';
			serialPort.Write(cmdBuf, 0, 1);

			yield return new WaitForEndOfFrame();

			while (serialPort.BytesToRead == 0 && !abortTransfer)
			{
				yield return new WaitForEndOfFrame();
			}
			if (!abortTransfer)
			{
				int cmd = serialPort.ReadByte();
				if (cmd == cmdBuf[0])
				{
					ShowQueryResults();
				}
			}
		}
	}

	public void ReadDiskPressed()
	{
		UnityEngine.UI.Text[] textArray = readDiskVerifyPanel.GetComponentsInChildren<UnityEngine.UI.Text>();
		for (int i = 0; i < textArray.Length; i++)
        {
			if (textArray[i].name.Equals("Desc"))
            {
				textArray[i].text = "Insert disk in <b>DRIVE " + driveDropdown.captionText.text + "</b> and click BEGIN to start or click CANCEL to abort";
				break;
            }
        }
		readDiskVerifyPanel.SetActive(true);
	}

	public void CancelReadDisk()
	{
		readDiskVerifyPanel.SetActive(false);
	}

	public void ReadDisk()
	{
		readDiskVerifyPanel.SetActive(false);
		StartCoroutine(ReadDiskCoroutine());
	}

	IEnumerator ReadDiskCoroutine()
	{
		isReadingDisk = true;
		abortTransfer = false;

		progressBar.value = 0;
		progressValue.text = string.Empty;

		yield return new WaitForEndOfFrame();
		yield return new WaitForEndOfFrame();

		if (serialPort != null && serialPort.IsOpen)
		{
			DisableButtons();
			clientStatusButton.interactable = false;
			volumeNumberField.interactable = false;

			int diskType = 0; // 0=1S40T, 1=2S40T, 2=1S80T, 3=2S80T

			if (!driveOverride.isOn)
			{
				driveDSToggle.isOn = false;
				//drive80TrkToggle.isOn = false;
			}

			// determine if using H8DIMGR2, H37IMGR or H89LDR
			bool h8dImgr = SetDrive();

			int res = 0;
			byte[] cmdBuf = new byte[16];
			byte[] inBuf = new byte[serialPort.ReadBufferSize];

			string driveDesignator = h37Toggle.isOn ? "DK" : "SY";
			SendToLog("DRIVE " + driveDesignator + ": SELECTED");
			
			if (h8dImgr)
            {
				if (driveOverride.isOn && !h37Toggle.isOn)
				{
					diskType = 0;
					/// 4 - set 1S40T
					/// 5 - set 2S40T
					/// 6 - set 1S80T
					/// 7 - set 2S80T
					cmdBuf[0] = (byte)'4';
					if (driveDSToggle.isOn)
					{
						if (drive80TrkToggle.isOn)
						{
							diskType = 3; // 2S80T
							cmdBuf[0] = (byte)'7';
						}
						else
						{
							diskType = 1; // 2S40T
							cmdBuf[0] = (byte)'5';
						}
					}
					else
                    {
						if (drive80TrkToggle.isOn)
						{
							diskType = 2; // 1S80T
							cmdBuf[0] = (byte)'6';
						}
                    }
					
					serialPort.Write(cmdBuf, 0, 1);

					while (serialPort.BytesToRead <= 0)
					{
						yield return new WaitForEndOfFrame();
					}

					res = serialPort.ReadByte();
					if (res == cmdBuf[0])
					{
						// disk type confirmed
					}
					else
                    {
						SendToLog("RETURN DISK TYPE " + (char)res + " DOES NOT MATCH " + (char)cmdBuf[0]);
                    }
				}
				else
				{
					// query system to auto-detect disk type
					cmdBuf[0] = (byte)'Q';
					serialPort.Write(cmdBuf, 0, 1);

					while (serialPort.BytesToRead <= 0)
					{
						yield return new WaitForEndOfFrame();
					}

					res = serialPort.ReadByte();

					//Debug.Log(cmdBuf[0].ToString() + " sent reply=" + cmd.ToString());

					if (res == 'Q')
					{
						if (h37Toggle.isOn)
						{
							int trkRes = ShowQueryResults();

							if (trkRes == 4)
							{
								// correct drive selected
							}
							else
							{
								SendToLog("<color=yellow><b>WRONG DRIVE SELECTED OR DISK IS UNREADABLE</b></color>");

								isReadingDisk = false;
								EnableButtons();

								yield break;
							}

							cmdBuf[0] = (byte)'4';
							if (drive80TrkToggle.isOn)
                            {
								diskType = driveDSToggle.isOn ? 3 : 2;
                            }
							else
                            {
								diskType = driveDSToggle.isOn ? 1 : 0;
                            }
							if (diskType == 1)
							{
								cmdBuf[0] = (byte)'5';
							}
							else if (diskType == 2)
							{
								cmdBuf[0] = (byte)'6';
							}
							else if (diskType == 3)
							{
								cmdBuf[0] = (byte)'7';
							}
							serialPort.Write(cmdBuf, 0, 1);

							while (serialPort.BytesToRead <= 0)
							{
								yield return new WaitForEndOfFrame();
							}

							res = serialPort.ReadByte();
							if (res != cmdBuf[0])
							{
								SendToLog("DISKTYPE RETURNED " + (char)res + " INSTEAD OF " + (char)cmdBuf[0]);
							}
						}
						else
						{
							while (serialPort.BytesToRead <= 0)
							{
								yield return new WaitForEndOfFrame();
							}
							diskType = serialPort.ReadByte();

							SendToLog("DISK QUERY RETURNED " + diskType.ToString());

							if (diskType == 1)
							{
								driveDSToggle.isOn = true;
							}
							else if (diskType == 2)
							{
								driveDSToggle.isOn = false;
							}
							else if (diskType == 3)
							{
								driveDSToggle.isOn = true;
							}
						}
					}
				}
			}

			if (diskType == 0)
			{
				SendToLog("DISKTYPE 1S40T");
			}
			else if (diskType == 1)
			{
				SendToLog("DISKTYPE 2S40T");
			}
			else if (diskType == 2)
			{
				SendToLog("DISKTYPE 1S80T");
			}
			else if (diskType == 3)
			{
				SendToLog("DISKTYPE 2S80T");
			}

			// final drive synchronization
			if (h8dImgr)
			{
				cmdBuf[0] = (byte)'0';
				if (diskType == 2 || diskType == 3)
                {
					// 80 track disks use SY1
					cmdBuf[0] = (byte)'1';
                }
				serialPort.Write(cmdBuf, 0, 1);
				res = serialPort.ReadByte();

				yield return new WaitForSeconds(1);
			}

			cmdBuf[0] = (byte)'R'; // switch to read disk state
			serialPort.Write(cmdBuf, 0, 1);

			yield return new WaitForSeconds(1);

			int track = 0;
			int expectedTracks = (diskType == 0) ? 40 : (diskType == 1) ? 80 : (diskType == 2) ? 80 : 160;
			int trackSize = h37Sectors * h37SectorSize; // actual bytes to write to buffer (received bytes might be less if bad sectors)
			if (!h37Toggle.isOn)
			{
				trackSize = 2560;
			}
			float trackTime = 0;
			int mins;
			int secs;
			string t = string.Empty;

			readBufIdx = 0;

			trackNumberField.text = track.ToString("D2");

			do
			{
				if (abortTransfer)
				{
					AbortTransferImage();
					yield break;
				}
				cmdBuf[0] = (byte)'R'; // ask for next track
				serialPort.Write(cmdBuf, 0, 1);

				int expectedBytes = 1;

				int trackBytes = 0;
				while (true)
				{
					int bytesToRead = serialPort.BytesToRead;
					if (bytesToRead > 0)
                    {
						if (expectedBytes == 1)
						{
							expectedBytes = 2560;
							sectorsPerTrackField.text = "10 / 256";

							if (h37Toggle.isOn)
							{
								int lowbyte = serialPort.ReadByte();
								int hibyte = serialPort.ReadByte();
								int sectorsPerTrack = serialPort.ReadByte();
								sectorsPerTrackField.text = sectorsPerTrack.ToString() + " / " + h37SectorSize.ToString();
								expectedBytes = (hibyte * 256) + lowbyte;
								bytesToRead -= 3;
							}
						}
						if (bytesToRead > 0)
						{
							serialPort.Read(inBuf, 0, bytesToRead);
							System.Buffer.BlockCopy(inBuf, 0, readBuf, readBufIdx, bytesToRead);
							readBufIdx += bytesToRead;
							trackBytes += bytesToRead;
						}
					}

					yield return new WaitForEndOfFrame();

					int sides = driveDSToggle.isOn ? 2 : 1;
					int actualSide = (sides == 2) ? (track % sides) + 1 : 1;
					int actualTrack = track / sides;

					trackTime += Time.deltaTime;

					if (trackBytes > 0)
					{
						mins = (int)trackTime / 60;
						secs = (int)trackTime % 60;
						t = mins.ToString() + ":" + secs.ToString("D2");
						int n = Mathf.Min(trackBytes, expectedBytes);
						trackNumberField.text = actualTrack.ToString("D2") + "/" + actualSide.ToString() + " [" + n.ToString("D4") + "/" + expectedBytes.ToString() + " " + t + "]";
					}

					if (expectedBytes > 1)
					{
						float progress = (float)readBufIdx / (expectedTracks * expectedBytes);
						progressBar.value = progress;
						progress = Mathf.RoundToInt(progress * 100);
						progressValue.text = progress.ToString("N3") + "%";
					}

					if (trackBytes > expectedBytes) // a full track is expectedBytes + 1
					{
						if (track == 0)
						{
							if (IsHDOSDisk(readBuf))
							{
								volumeNumberField.text = GetHDOSVolume(readBuf);
								diskLabelField.text = GetHDOSLabel(readBuf);
							}
							else
							{
								if (h37Toggle.isOn)
								{
									if (h37Sectors == 8 || h37Sectors == 9)
									{
										diskLabelField.text = "DOS-DISK";
									}
									else
									{
										diskLabelField.text = "CPM-DISK";
									}
								}
								else
								{
									diskLabelField.text = "CPM-DISK";
								}
							}
						}

						// check for handshake byte 'R' or 'r' at expectedBytes + 1
						if (trackBytes == expectedBytes + 1)
						{
							if (readBuf[readBufIdx - 1] == (byte)'R' || readBuf[readBufIdx - 1] == (byte)'r')
							{
								readBufIdx--; // rewind for handshake byte
								trackBytes--;
								if (trackBytes < trackSize)
								{
									while (trackBytes < trackSize)
									{
										readBuf[readBufIdx++] = 0;
										trackBytes++;
									}
									SendToLog("RECEIVED TRACK " + actualTrack.ToString() + " SIDE " + actualSide.ToString() + " <color=red>BAD TRACK</color>");
								}
								else
								{
									SendToLog("RECEIVED TRACK " + actualTrack.ToString() + " SIDE " + actualSide.ToString());
								}
								trackBytes = 0;
								track++;
								break;
							}
						}
					}
				}
			} while (track < expectedTracks);

			mins = (int)trackTime / 60;
			secs = (int)trackTime % 60;
			t = mins.ToString() + ":" + secs.ToString("D2");
			SendToLog("DISK IMAGE RECEIVED IN " + t);

			saveImage = true;
		}

		isReadingDisk = false;
	}

	int ShowQueryResults(bool brief = true)
	{
		int res = 0;
		if (h37Toggle.isOn)
		{
			int sectors = serialPort.ReadByte();
			int h37track = serialPort.ReadByte();
			int h37side = serialPort.ReadByte();
			int h37sector = serialPort.ReadByte();
			int h37sectorLen = serialPort.ReadByte();
			int h37crc1 = serialPort.ReadByte();
			int h37crc2 = serialPort.ReadByte();
			int numSides = serialPort.ReadByte();
			int lobyte = serialPort.ReadByte();
			int hibyte = serialPort.ReadByte();
			int density = serialPort.ReadByte();

			h37sectorLen = (h37sectorLen == 0) ? 128 : (h37sectorLen == 1) ? 256 : (h37sectorLen == 2) ? 512 : 1024;

			h37Sectors = sectors;
			h37SectorSize = h37sectorLen;
			h37Tracks = drive80TrkToggle.isOn ? 80 : 40;
			h37Sides = numSides;
			h37Density = density == 0x04 ? "MFM" : "FM";

			string densityStr = density == 0x04 ? "DD" : "SD";

			densityToggle.isOn = (density == 0x04) ? true : false;
			driveDSToggle.isOn = (h37Sides == 2) ? true : false;

			int bytes = (hibyte * 256) + lobyte;
			sectorsPerTrackField.text = sectors.ToString() + " / " + h37sectorLen.ToString();

			brief = false;
			if (brief)
			{
				// disk query (track 0 header + full track side 2 or side 1)
				SendToLog("QUERY RESULTS: SPT=" + sectors.ToString() + " NSIDES=" + numSides.ToString() + " SECSIZE=" + h37sectorLen.ToString() + " TRKSIZE=" + bytes.ToString() + " DENS=" + densityStr);
			}
			else
			{
				// track header contents
				SendToLog("QUERY RESULTS: SPT=" + sectors.ToString() + " TRK=" + h37track.ToString() + " SEC=" + h37sector.ToString() + " SIDE=" + h37side.ToString() + " NSIDES=" + numSides.ToString() + " SECSIZE=" + h37sectorLen.ToString() + " TRKSIZE=" + bytes.ToString() + " DENS=" + densityStr);
			}

			res = h37track;
		}
		else
		{
			int r = serialPort.ReadByte();
			SendToLog("QUERY RESULTS: " + r.ToString());
		}

		Debug.Log("ShowQueryResults() res=" + res.ToString());

		return res;
	}

	int ReadByte()
	{
		int b = -1;
		if (serialPort != null && serialPort.IsOpen && serialPort.BytesToRead > 0)
		{
			b = serialPort.ReadByte();
		}
		return b;
	}

	void GetTrackSide(out int track, out int side)
	{
		side = 0;
		track = 0;
		string trackStr = trackNumberField.text;
		if (trackNumberField.text.Contains("/"))
		{
			// parse side if available
			int idx = trackNumberField.text.IndexOf('/');
			string sideStr = trackNumberField.text.Substring(idx + 1);
			if (int.TryParse(sideStr, out side))
			{
				// good parse
				side = Mathf.Clamp(side - 1, 0, 1);
			}
			trackStr = trackNumberField.text.Substring(0, idx);
		}
		if (int.TryParse(trackStr, out track))
		{
			// good parse
			if (driveDropdown.captionText.text.Equals("SY1"))
			{
				track = Mathf.Clamp(track, 0, 79);
			}
			else
			{
				track = Mathf.Clamp(track, 0, 39);
			}
		}
	}

	// read track header
	public void ReadTrack()
	{
		StartCoroutine(ReadTrackCoroutine());
	}

	IEnumerator ReadTrackCoroutine()
	{
		yield return new WaitForEndOfFrame();

		if (serialPort != null && serialPort.IsOpen)
		{
			int side;
			int track;
			GetTrackSide(out track, out side);
			if (SetDrive())
			{
				SetSide(side);

				byte[] cmdBuf = new byte[16];
				cmdBuf[0] = (byte)'T';
				serialPort.Write(cmdBuf, 0, 1);
				int c = serialPort.ReadByte();
				if (c == cmdBuf[0])
				{
					int t = track;

					SendToLog("READ HEADER TRACK=" + t.ToString() + " SIDE=" + side.ToString());
					yield return new WaitForEndOfFrame();

					cmdBuf[0] = (byte)t;
					serialPort.Write(cmdBuf, 0, 1);

					yield return new WaitForEndOfFrame();

					while (serialPort.BytesToRead == 0)
					{
						yield return new WaitForEndOfFrame();
					}
					ShowQueryResults(false);
				}
			}
		}
	}

	public void ExamineTrack()
	{
		StartCoroutine(ExamineTrackCoroutine());
	}

	IEnumerator ExamineTrackCoroutine()
	{
		int side;
		int track;
		GetTrackSide(out track, out side);
		if (SetDrive())
		{
			SetSide(side);

			byte[] cmdBuf = new byte[16];
			cmdBuf[0] = (byte)'E';
			serialPort.Write(cmdBuf, 0, 1);
			int c = serialPort.ReadByte();
			if (c == cmdBuf[0])
			{
				int t = track;

				cmdBuf[0] = (byte)t;
				serialPort.Write(cmdBuf, 0, 1);

				yield return new WaitForEndOfFrame();

				SendToLog("EXAMINE TRACK");

				yield return StartCoroutine(ReadIntoBuffer(0));

				ShowTrackHexDump(expectedBytes);
				ShowExamineResults(expectedBytes + 1);
			}
		}
	}

	IEnumerator ReadIntoBuffer(int destIdx)
	{
		readBufIdx = destIdx;
		expectedBytes = 1;

		byte[] inBuf = new byte[serialPort.ReadBufferSize];

		while (true)
		{
			int bytesToRead = serialPort.BytesToRead;
			if (bytesToRead > 0)
			{
				if (expectedBytes == 1)
				{
					expectedBytes = 2560;
					sectorsPerTrackField.text = "10";

					if (h37Toggle.isOn)
					{
						int lowbyte = serialPort.ReadByte();
						int hibyte = serialPort.ReadByte();
						int sectorsPerTrack = serialPort.ReadByte();
						sectorsPerTrackField.text = sectorsPerTrack.ToString() + " / " + h37SectorSize.ToString();
						expectedBytes = (hibyte * 256) + lowbyte;
						bytesToRead -= 3;
					}
				}
				if (bytesToRead > 0)
				{
					serialPort.Read(inBuf, 0, bytesToRead);
					System.Buffer.BlockCopy(inBuf, 0, readBuf, readBufIdx, bytesToRead);
					readBufIdx += bytesToRead;
				}

				int num = Mathf.Min(readBufIdx, expectedBytes);
				trackNumberField.text = "[" + num.ToString("D4") + "/" + expectedBytes.ToString() + "]";

				// track bytes + query results
				if (readBufIdx >= expectedBytes)
				{
					break;
				}
			}
			yield return new WaitForEndOfFrame();
		}
	}

	void ShowExamineResults(int startIdx)
	{
		/*
		int n = startIdx;
		int sectors = readBuf[n++];
		int h37track = readBuf[n++];
		int h37side = readBuf[n++];
		int h37sector = readBuf[n++];
		int h37sectorLen = readBuf[n++];
		int h37crc1 = readBuf[n++];
		int h37crc2 = readBuf[n++];
		int numSides = readBuf[n++];
		int lobyte = readBuf[n++];
		int hibyte = readBuf[n++];
		int density = readBuf[n++];

		h37sectorLen = (h37sectorLen == 0) ? 128 : (h37sectorLen == 1) ? 256 : (h37sectorLen == 2) ? 512 : 1024;

		h37Sectors = sectors;
		h37SectorSize = h37sectorLen;
		h37Tracks = drive80TrkToggle.isOn ? 80 : 40;
		h37Sides = numSides;
		h37Density = density == 0x04 ? "MFM" : "FM";

		string densityStr = density == 0x04 ? "DD" : "SD";

		densityToggle.isOn = (density == 0x04) ? true : false;
		driveDSToggle.isOn = (h37Sides == 2) ? true : false;
		
		int bytes = (hibyte * 256) + lobyte;
		sectorsPerTrackField.text = sectors.ToString() + " / " + h37sectorLen.ToString();
		// track header contents
		SendToLog("QUERY RESULTS: SPT=" + sectors.ToString() + " TRK=" + h37track.ToString() + " SEC=" + h37sector.ToString() + " SIDE=" + h37side.ToString() + " NSIDES=" + numSides.ToString() + " SECSIZE=" + h37sectorLen.ToString() + " TRKSIZE=" + bytes.ToString() + " DENS=" + densityStr);
		*/
	}

	List<string> hexDumpList = new List<string>();

	// readBuf contains track data
	void ShowTrackHexDump(int trackBytes, bool saveToFile = true)
	{
		hexDumpList.Clear();
		string s1 = string.Empty;
		string s2 = string.Empty;
		for (int i = 0; i < trackBytes; i++)
		{
			byte c = readBuf[i];
			if (s1.Length == 0)
			{
				s1 += i.ToString("X4") + ": ";
			}
			s1 += c.ToString("X2");
			if ((i % 32) > 0 && (i % 8) == 0)
			{
				s1 += ".";
			}
			else
			{
				s1 += " ";
			}
			s2 += GetHexChar(c);
			if (s2.Length == 32)
            {
				//Debug.Log(s2);
				string s = s1 + s2;
				hexDumpList.Add(s);
				SendToLog(s);

				s1 = string.Empty;
				s2 = string.Empty;
            }
		}
		if (s2.Length > 0)
		{
			string s = s1 + s2;
			hexDumpList.Add(s);
			SendToLog(s);
		}
		if (saveToFile)
		{
			FilePicker.Instance.title.text = "Save Hex Dump";
			FilePicker.Instance.onCompleteCallback += HexDumpSaveComplete;
			FilePicker.Instance.ShowPicker(true);
		}
	}

	void HexDumpSaveComplete(string filePath)
	{
		FilePicker.Instance.onCompleteCallback -= HexDumpSaveComplete;
		if (filePath != null && filePath.Length > 0)
		{
			System.IO.File.WriteAllLines(filePath, hexDumpList.ToArray());
		}
	}

	char GetHexChar(byte c)
	{
		if (c >= 0x20 && c < 0x7F)
		{
			return (char)c;
		}
		return '.';
	}

	public void GetDiskType()
	{
		StartCoroutine(GetDiskTypeCoroutine());
	}

	IEnumerator GetDiskTypeCoroutine()
	{
		yield return new WaitForEndOfFrame();

		if (serialPort != null && serialPort.IsOpen)
		{
			//byte[] cmdBuf = new byte[16];
			//cmdBuf[0] = (byte)'Q';
			//serialPort.Write(cmdBuf, 0, 1);
		}
	}

	public void SendDiskPressed()
	{
		FilePicker.Instance.title.text = "Load Disk Image";
		FilePicker.Instance.onCompleteCallback += SendDiskFolderComplete;
		FilePicker.Instance.ShowPicker(true);
	}

	public void SendDiskFolderComplete(string path)
	{
		Debug.Log("SendDiskFolderComplete() path=" + path);

		FilePicker.Instance.onCompleteCallback -= SendDiskFolderComplete;
		if (!string.IsNullOrEmpty(path))
		{
			sendBuf = System.IO.File.ReadAllBytes(path);
			sendDiskVerifyPanel.SetActive(true);
		}
	}

	public void SendDiskCancel()
	{
		sendDiskVerifyPanel.SetActive(false);
	}

	public void SendDiskVerify()
	{
		sendDiskVerifyPanel.SetActive(false);
		StartCoroutine(SendDiskCoroutine());
	}

	IEnumerator SendDiskCoroutine()
	{
		isWritingDisk = true;

		progressBar.value = 0;
		progressValue.text = string.Empty;

		yield return new WaitForEndOfFrame();
		/*
		// DEBUG
		if (serialPort != null && serialPort.IsOpen)
		{
			byte[] cmdBuf = new byte[16];
			cmdBuf[0] = (byte)'W';
			serialPort.Write(cmdBuf, 0, 1);

			int c = serialPort.ReadByte();
			if (c == cmdBuf[0])
			{
				SendToLog("FORMAT DISK 1S40T MFM");
				yield return new WaitForEndOfFrame();

				//for (int i = 0; i < 2; i++)
				//{
				//	yield return StartCoroutine(ReadIntoBuffer(0));
				//	ShowTrackHexDump(expectedBytes, false);
				//}
			}
		}
		*/
		int disk1s80t = 2560 * 80;
		int disk2s80t = 2560 * 80 * 2;

		int spt = 0;
		int ssz = 0;
		int ntr = 0;
		int nsd = 0;
		int den = 0;
		if (serialPort != null && serialPort.IsOpen)
		{
			DisableButtons();
			clientStatusButton.interactable = false;

			int diskType = 0;
			int imageBytes = sendBuf.Length;

			driveDSToggle.isOn = false;
			drive80TrkToggle.isOn = false;

			if (h37Toggle.isOn)
			{
				string s = string.Empty;
				if (FilePicker.Instance.GetFileName().Contains(".H8D"))
				{
					// if writing H8D to H37 then we use single density settings
					s = "SPT=10 SSZ=0256 "; // TRK=40 SID=1 FM hard sector settings
					if (imageBytes == disk1s80t)
					{
						// favor 2S40T over 1S80T
						s += "TRK=40 SID=2 ";
					}
					else if (imageBytes == disk2s80t)
					{
						s += "TRK=80 SID=2 ";
					}
					else
					{
						s += "TRK-40 SID=1 ";
					}
					s += "FM";
				}
				else
				{
					s = System.Text.Encoding.ASCII.GetString(sendBuf, imageBytes - 32, 32);
				}
				Debug.Log(s);
				// SPT=16 SSZ=0256 TRK=40 SID=2 MFM
				string sptStr = s.Substring(4, 2);
				string secSiz = s.Substring(11, 4);
				string numTrk = s.Substring(20, 2);
				string numSid = s.Substring(27, 1);
				string denStr = s.Substring(29);
				int num;
				if (int.TryParse(sptStr, out num))
				{
					spt = num;
				}
				if (int.TryParse(secSiz, out num))
				{
					ssz = num;
				}
				if (int.TryParse(numTrk, out num))
				{
					ntr = num;
				}
				if (int.TryParse(numSid, out num))
				{
					nsd = num;
				}
				if (denStr.Contains("MFM"))
				{
					den = 4;
				}
				if (ntr == 80)
                {
					diskType = 2;		// 1S80T
					if (nsd == 2)
					{
						diskType = 3;	// 2S80T
					}
                }
				else
                {
					diskType = 0;		// 1S40T
					if (nsd == 2)
					{
						diskType = 1;	// 2S40T
					}
                }
				Debug.Log("diskType=" + diskType.ToString() + " spt=" + spt.ToString() + " ssz=" + ssz.ToString() + " ntr=" + ntr.ToString() + " nsd=" + nsd.ToString() + " den=" + den.ToString());
			}
			else
			{
				int volumeOverrideValue = 0;
				if (volumeOverrideToggle.isOn)
				{
					if (!string.IsNullOrEmpty(volumeNumberField.text))
					{
						int.TryParse(volumeNumberField.text, out volumeOverrideValue);
					}
				}

				if (IsHDOSDisk(sendBuf))
				{
					diskLabelField.text = GetHDOSLabel(sendBuf);
					if (!volumeOverrideToggle.isOn)
					{
						// get volume number from HDOS header
						volumeOverrideValue = sendBuf[0x900];
					}
					// get disk type from HDOS header
					diskType = sendBuf[0x910];
				}
				else
				{
					if (imageBytes == disk1s80t)
					{
						// favor ds 40 track over 1s 80 track
						diskType = 1;
					}
					else if (imageBytes == disk2s80t)
					{
						diskType = 3;
					}
					diskLabelField.text = FilePicker.Instance.GetFileName();
				}

				volumeNumberField.text = volumeOverrideValue.ToString();
			}

			byte[] cmdBuf = new byte[16];
			// drive configuration should be: SY0=2S40T, SY1=2S80T
			if (diskType == 0 || diskType == 1)
			{
				cmdBuf[0] = (byte)'0'; // drive SY0:
			}
			else
			{
				cmdBuf[0] = (byte)'1'; // drive SY1:
			}
			serialPort.Write(cmdBuf, 0, 1);
			int c = serialPort.ReadByte();
			if (c == '?')
			{
				if (diskType != 0)
				{
					AbortTransferImage();
					SendToLog("CANNOT WRITE HIGH CAPACITY IMAGE TO DRIVE SY0");
					yield break;
				}
			}

			SendToLog("DRIVE SY" + (char)cmdBuf[0] + " SELECTED ON CLIENT");

			yield return new WaitForSeconds(1);

			// set expected disk type on client
			if (diskType == 0)
			{
				cmdBuf[0] = (byte)'4';
			}
			else if (diskType == 1)
			{
				cmdBuf[0] = (byte)'5';
			}
			else if (diskType == 2)
			{
				cmdBuf[0] = (byte)'6';
			}
			else if (diskType == 3)
			{
				cmdBuf[0] = (byte)'7';
			}
			serialPort.Write(cmdBuf, 0, 1);
			c = serialPort.ReadByte();
			if (c >= '4' && c <= '7')
			{
				SendToLog("DISK TYPE " + diskType.ToString() + " SET ON CLIENT");
			}

			yield return new WaitForSeconds(1);

			if (!h37Toggle.isOn)
			{
				// set disk volume number on client
				int.TryParse(volumeNumberField.text, out volumeOverrideValue);
				cmdBuf[0] = (byte)'V';
				cmdBuf[1] = (byte)volumeOverrideValue;
				serialPort.Write(cmdBuf, 0, 2);
				c = serialPort.ReadByte();
				if (c != 'V')
				{
					AbortTransferImage();
					SendToLog("VOLUME ASSIGNMENT FAILED");
					yield break;
				}

				SendToLog("DISK VOLUME SET TO " + volumeOverrideValue.ToString());

				yield return new WaitForSeconds(1);
			}

			// set toggles to reflect disk type
			int track = 0;
			int expectedTracks = (diskType == 0) ? 40 : (diskType == 1) ? 80 : (diskType == 2) ? 80 : 160;
			if (diskType == 1 || diskType == 3)
			{
				driveDSToggle.isOn = true;
			}
			if (diskType == 2 || diskType == 3)
			{
				drive80TrkToggle.isOn = true;
			}

			// switch client to write disk state
			cmdBuf[0] = (byte)'W';
			serialPort.Write(cmdBuf, 0, 1);

			int trackSize = 2560; // 256 * 10
			if (h37Toggle.isOn)
			{
				while (serialPort.BytesToRead <= 0 || serialPort.ReadByte() != 'W')
				{
					yield return new WaitForEndOfFrame();
				}

				trackSize = spt * ssz;
				// send sectorsPerTrack, sectorSize and density so know how to format the disk tracks
				cmdBuf[0] = (byte)spt;
				cmdBuf[1] = (byte)((ssz == 128) ? 0 : (ssz == 256) ? 1 : (ssz == 512) ? 2 : 3);
				cmdBuf[2] = (byte)den;
				int highByte = trackSize / 256;
				int lowByte = trackSize % 256;
				cmdBuf[3] = (byte)highByte;
				cmdBuf[4] = (byte)lowByte;
				serialPort.Write(cmdBuf, 0, 5);

				Debug.Log("trackSize=" + trackSize.ToString() + " spt=" + cmdBuf[0].ToString() + " ssz=" + cmdBuf[1].ToString() + " den=" + cmdBuf[2].ToString() + " H=" + cmdBuf[3].ToString() + " L=" + cmdBuf[4].ToString());

				yield return new WaitForEndOfFrame();
			}

			int mins = 0;
			int secs = 0;
			string t = string.Empty;

			float trackTime = 0;

			sendBufIdx = 0;
			do
			{
				if (h37Toggle.isOn)
				{
					// wait for 'W' handshake indicating format is done, ready for data
					while (serialPort.BytesToRead <= 0)
					{
						if (abortTransfer)
                        {
							break;
                        }
						yield return new WaitForEndOfFrame();
					}
					if (serialPort.BytesToRead > 0)
					{
						int res = serialPort.ReadByte();
						if (res != 'W')
						{
							abortTransfer = true;
						}
					}
				}
				if (abortTransfer)
				{
					AbortTransferImage();
					yield break;
				}

				cmdBuf[0] = (byte)'W';
				serialPort.Write(cmdBuf, 0, 1);

				yield return new WaitForEndOfFrame();
				yield return new WaitForEndOfFrame();
				yield return new WaitForEndOfFrame();
				yield return new WaitForEndOfFrame();
				yield return new WaitForEndOfFrame();
				yield return new WaitForEndOfFrame();
				yield return new WaitForEndOfFrame();
				yield return new WaitForEndOfFrame();

				sendBufIdx = track * trackSize;
				serialPort.Write(sendBuf, sendBufIdx, trackSize);

				int sides = driveDSToggle.isOn ? 2 : 1;
				int actualSide = (sides == 2) ? (track % sides) + 1 : 1;
				int actualTrack = track / sides;

				// wait for 'W' acknowledgement
				while (serialPort.BytesToRead <= 0 || serialPort.ReadByte() != 'W')
				{
					trackTime += Time.deltaTime;

					mins = (int)trackTime / 60;
					secs = (int)trackTime % 60;
					t = mins.ToString() + ":" + secs.ToString("D2");
					trackNumberField.text = actualTrack.ToString("D2") + "/" + actualSide.ToString() + " [" + t + "]";

					float progress = (float)sendBufIdx / (expectedTracks * trackSize);
					progressBar.value = progress;
					progress = Mathf.RoundToInt(progress * 100);
					progressValue.text = progress.ToString("N3") + "%";

					yield return new WaitForEndOfFrame();

					if (abortTransfer)
					{
						break;
					}
				}

				if (!abortTransfer)
				{
					SendToLog("TRACK " + actualTrack.ToString() + " SIDE " + actualSide.ToString() + " SENT");
					track++;
				}
			} while (track < expectedTracks);

			if (!abortTransfer)
			{
				SendToLog("DISK IMAGE \"" + FilePicker.Instance.GetFileName() + "\" SENT IN " + t);
			}
			else
			{
				SendToLog("DISK IMAGE ABORTED");
			}

			EnableButtons();
		}
		
		isWritingDisk = false;
	}

	public void SendLoaderPressed()
	{
		StartCoroutine(SendLoaderCoroutine());
	}

	IEnumerator SendLoaderCoroutine()
	{
		string path = Application.streamingAssetsPath;
		string file = "H89LDR3.BIN";
		string filePath = System.IO.Path.Combine(path, file);

		byte[] buf = new byte[2];

		UnityEngine.UI.Text text = saveLoaderButton.GetComponentInChildren<UnityEngine.UI.Text>();

		ShowSendLoader();
		while (sendLoaderPanel.activeInHierarchy)
		{
			yield return new WaitForEndOfFrame();
		}

		if (serialPort == null || !serialPort.IsOpen)
		{
			COMOpen();
		}

		if (serialPort != null && serialPort.IsOpen)
		{
			SendToLog("SENDING H89LDR3.BIN");

			yield return new WaitForEndOfFrame();

			if (System.IO.File.Exists(filePath))
			{
				// sends loader in reverse byte order
				byte[] loader = System.IO.File.ReadAllBytes(filePath);
				for (int i = loader.Length - 1; i >= 0; i--)
				{
					buf[0] = loader[i];
					serialPort.Write(buf, 0, 1);
				}

				yield return new WaitForEndOfFrame();

				SendToLog("H89LDR.BIN SENT SUCCESSFULLY");

				ShowSaveLoader();
			}
			else
			{
				SendToLog("ERROR - H89LDR3.BIN NOT FOUND");
			}
		}

		yield return new WaitForEndOfFrame();
	}

	void ShowSendLoader()
	{
		sendLoaderPanel.SetActive(true);
	}

	public void HideSendLoader()
	{
		sendLoaderPanel.SetActive(false);
	}

	void ShowSaveLoader()
	{
		saveLoaderPanel.SetActive(true);
	}

	public void HideSaveLoader()
	{
		saveLoaderPanel.SetActive(false);
	}

	public void SaveLoaderButton()
	{
		byte[] buf = new byte[2];
		buf[0] = (byte)'S';
		serialPort.Write(buf, 0, 1);

		SendToLog("LOADER SAVED TO DISK");

		HideSaveLoader();
	}

	public void AbortPressed()
	{
		abortTransfer = true;
	}

	void AbortTransferImage()
	{
		byte[] cmdBuf = new byte[16];
		cmdBuf[0] = (byte)'?';
		serialPort.Write(cmdBuf, 0, 1);

		abortTransfer = false;
		isReadingDisk = false;
		isWritingDisk = false;

		EnableButtons();

		SendToLog("TRANSFER ABORT");
	}

	private bool diskToggles;

	public void FormatDiskPressed()
	{
		formatDiskPanel.SetActive(true);
		formatDiskDSToggle.isOn = driveDSToggle.isOn;
		formatDiskDensToggle.isOn = densityToggle.isOn;
	}

	public void FormatDiskCancel()
	{
		formatDiskPanel.SetActive(false);
	}

	public void FormatDiskSPT(int n)
	{
		if (diskToggles)
		{
			return;
		}
		diskToggles = true;
		if (n == 5)
        {
			formatDisk08Toggle.isOn = false;
			formatDisk09Toggle.isOn = false;
			formatDisk10Toggle.isOn = false;
			formatDisk16Toggle.isOn = false;
		}
		if (n == 8)
		{
			formatDisk05Toggle.isOn = false;
			formatDisk09Toggle.isOn = false;
			formatDisk10Toggle.isOn = false;
			formatDisk16Toggle.isOn = false;
		}
		if (n == 9)
		{
			formatDisk05Toggle.isOn = false;
			formatDisk08Toggle.isOn = false;
			formatDisk10Toggle.isOn = false;
			formatDisk16Toggle.isOn = false;
		}
		if (n == 10)
		{
			formatDisk05Toggle.isOn = false;
			formatDisk08Toggle.isOn = false;
			formatDisk09Toggle.isOn = false;
			formatDisk16Toggle.isOn = false;
		}
		if (n == 16)
		{
			formatDisk05Toggle.isOn = false;
			formatDisk08Toggle.isOn = false;
			formatDisk09Toggle.isOn = false;
			formatDisk10Toggle.isOn = false;
		}
		diskToggles = false;
	}

	public void FormatDiskStart()
	{
		StartCoroutine(FormatDiskCoroutine());
	}

	IEnumerator FormatDiskCoroutine()
	{
		yield return new WaitForEndOfFrame();

		byte[] cmdBuf = new byte[16];

		UnityEngine.UI.Toggle[] toggles = formatDiskPanel.GetComponentsInChildren<UnityEngine.UI.Toggle>();
		for (int i = 0; i < toggles.Length; i++)
		{
			toggles[i].interactable = false;
		}
		UnityEngine.UI.Button[] buttons = formatDiskPanel.GetComponentsInChildren<UnityEngine.UI.Button>();
		for (int i = 0; i < buttons.Length; i++)
		{
			buttons[i].interactable = false;
		}

		driveDSToggle.isOn = formatDiskDSToggle.isOn;
		cmdBuf[0] = (byte)'4';
		if (driveDSToggle.isOn)
		{
			if (drive80TrkToggle.isOn)
			{
				cmdBuf[0] = (byte)'7';
			}
			else
			{
				cmdBuf[0] = (byte)'5';
			}
		}
		else
		{
			if (drive80TrkToggle.isOn)
			{
				cmdBuf[0] = (byte)'6';
			}
		}

		serialPort.Write(cmdBuf, 0, 1);

		while (serialPort.BytesToRead <= 0)
		{
			yield return new WaitForEndOfFrame();
		}

		int res = serialPort.ReadByte();
		if (res == cmdBuf[0])
		{
			// disk type confirmed
			cmdBuf[0] = (byte)'F';
			serialPort.Write(cmdBuf, 0, 1); // enter format disk mode

			while (serialPort.BytesToRead <= 0)
			{
				yield return new WaitForEndOfFrame();
			}

			res = serialPort.ReadByte();
			if (res == cmdBuf[0])
			{
				// send format params
				cmdBuf[0] = (byte)(formatDisk05Toggle.isOn ? 5 : formatDisk08Toggle.isOn ? 8 : formatDisk09Toggle.isOn ? 9 : formatDisk10Toggle.isOn ? 10 : 16);
				cmdBuf[1] = (byte)(formatDisk05Toggle.isOn ? 3 : formatDisk08Toggle.isOn ? 2 : formatDisk09Toggle.isOn ? 2 : formatDisk16Toggle.isOn ? 1 : 1);
				cmdBuf[2] = (byte)(formatDiskDensToggle.isOn ? 4 : 0);
				serialPort.Write(cmdBuf, 0, 3);

				while (serialPort.BytesToRead <= 0)
				{
					yield return new WaitForEndOfFrame();
				}

				res = serialPort.ReadByte();
				if (res == 'F')
				{
					// format start
				}

				while (serialPort.BytesToRead <= 0)
				{
					// wait until done
					yield return new WaitForEndOfFrame();
				}

				res = serialPort.ReadByte();
				if (res == 'F')
				{
					// format complete
				}
			}
		}

		for (int i = 0; i < toggles.Length; i++)
		{
			toggles[i].interactable = true;
		}
		for (int i = 0; i < buttons.Length; i++)
		{
			buttons[i].interactable = true;
		}

		formatDiskPanel.SetActive(false);
	}

	void EnableButtons()
	{
		for (int i = 0; i < buttonsToEnable.Length; i++)
		{
			buttonsToEnable[i].interactable = true;
		}
		volumeNumberField.interactable = volumeOverrideToggle.isOn;
		clientStatusButton.interactable = true;
	}

	void DisableButtons()
	{
		for (int i = 0; i < buttonsToEnable.Length; i++)
		{
			buttonsToEnable[i].interactable = false;
		}
		volumeNumberField.interactable = false;
	}

	void SendToLog(string s)
	{
		UnityEngine.UI.Text text = Instantiate(textLogPrefab, scrollViewRect.content);
		text.text = s;

		ScrollToBottom();
	}

	void ScrollToBottom()
    {
		StartCoroutine(ScrollToBottomCoroutine());
    }

	IEnumerator ScrollToBottomCoroutine()
	{
		yield return new WaitForEndOfFrame();
		yield return new WaitForEndOfFrame();
		scrollViewRect.verticalNormalizedPosition = 0;
	}

	public static string GetHDOSVolume(byte[] track_buffer)
	{
		string v = track_buffer[0x900].ToString("D3");
		return v;
	}

	public static string GetHDOSLabel(byte[] track_buffer)
	{
		System.Text.ASCIIEncoding encoding = new System.Text.ASCIIEncoding();
		byte[] l = new byte[60];
		for (int i = 0; i < 60; i++)
		{
			l[i] = track_buffer[0x911 + i];
		}
		string disk_label = string.Format("{0}", encoding.GetString(l, 0, 60));
		disk_label = disk_label.Trim();

		return disk_label;
	}

	public static bool IsHDOSDisk(byte[] track_buffer)
	{
		if ((track_buffer[0] == 0xAF && track_buffer[1] == 0xD3 && track_buffer[2] == 0x7D && track_buffer[3] == 0xCD) ||   //  V1.x
			(track_buffer[0] == 0xC3 && track_buffer[1] == 0xA0 && track_buffer[2] == 0x22 && track_buffer[3] == 0x20) ||   //  V2.x
			(track_buffer[0] == 0xC3 && track_buffer[1] == 0xA0 && track_buffer[2] == 0x22 && track_buffer[3] == 0x30) ||   //  V3.x
			(track_buffer[0] == 0xC3 && track_buffer[1] == 0x1D && track_buffer[2] == 0x24 && track_buffer[3] == 0x20))     //  V? Super-89
		{
			return (true);
		}
		return (false);
	}
}
