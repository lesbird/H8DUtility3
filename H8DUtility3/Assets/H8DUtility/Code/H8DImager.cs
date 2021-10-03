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
/// Q - query for disk type returns 4,5,6,7
/// Z - read track
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
	public GameObject saveLoaderPanel;
	public GameObject sendLoaderPanel;

	private SerialPort serialPort;
	private int inBufIdx;
	private int expectedBytes;
	private bool readTrack;

	private int h37Sectors;
	private int h37SectorSize;
	private int h37Tracks;
	private int h37Sides;
	private string h37Density;

	private byte volumeOverrideValue;

	private bool isReadingDisk;
	private bool isWritingDisk;
	private bool abortTransfer;

	public int baudUpdate;

	private bool saveImage;
	private byte[] readBuf = new byte[(8192 * 80 * 2) + 1]; // +1 to account for handshake byte
	private int readBufIdx;
	private byte[] sendBuf;
	private int sendBufIdx;

	void Start()
	{
		DisableButtons();
		COMInit();
		ShowHelp();
	}

	void COMInit()
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
				}
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

		if (!isReadingDisk && !isWritingDisk)
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
				drive80TrkToggle.interactable = true;
			}
			else
			{
				driveDSToggle.interactable = false;
				drive80TrkToggle.interactable = false;
			}
		}
	}

	void SaveDiskImage()
	{
		FilePicker.Instance.onCompleteCallback += SaveDiskImageComplete;
		FilePicker.Instance.onOpenPicker += SaveDiskOnOpen;
		FilePicker.Instance.ShowPicker(true);
	}

	void SaveDiskOnOpen()
	{
		FilePicker.Instance.onOpenPicker -= SaveDiskOnOpen;

		string fileName = diskLabelField.text;
		if (fileName.Contains("CP/M"))
        {
			fileName = "CPM_DISK_IMAGE";
        }
		FilePicker.Instance.fileInputField.text = fileName;
	}

	void SaveDiskImageComplete(string path)
	{
		SendToLog("Saving disk image to " + path);

		FilePicker.Instance.onCompleteCallback -= SaveDiskImageComplete;

		if (h37Toggle.isOn)
		{
			// "012345678901234567890123456789012" 32 bytes meta data
			// "SPT=xx SSZ=xxxx TRK=xx SID=xx MFM"
			byte[] data = new byte[readBufIdx + 32];
			System.Buffer.BlockCopy(readBuf, 0, data, 0, readBufIdx);
			string diskSpecs = "SPT=" + h37Sectors.ToString("D2") + " SSZ=" + h37SectorSize.ToString("D4") + " TRK=" + h37Tracks.ToString("D2") + " SID=" + h37Sides.ToString() + " " + h37Density;
			byte[] specs = System.Text.Encoding.ASCII.GetBytes(diskSpecs);
			System.Buffer.BlockCopy(specs, 0, data, readBufIdx, specs.Length);
			System.IO.File.WriteAllBytes(path, data);
		}
		else
		{
			byte[] data = new byte[readBufIdx];
			System.Buffer.BlockCopy(readBuf, 0, data, 0, readBufIdx);
			System.IO.File.WriteAllBytes(path, data);
		}

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

		yield return new WaitForEndOfFrame();

		if (serialPort != null && serialPort.IsOpen)
		{
			byte[] cmdBuf = new byte[16];
			cmdBuf[0] = (byte)'?';
			serialPort.Write(cmdBuf, 0, 1);

			//SendToLog("SYSTEM QUERY SENT");

			yield return new WaitForEndOfFrame();

			while (true)
			{
				try
				{
					if (serialPort.BytesToRead > 0)
					{
						int c = serialPort.ReadByte();
						if (c == '?')
						{
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
		}
	}

	bool SetDrive()
	{
		byte[] cmdBuf = new byte[16];

		if (driveDropdown.captionText.text.Equals("SY1"))
		{
			drive80TrkToggle.isOn = true;
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

			int cmd = serialPort.ReadByte();
			if (cmd == cmdBuf[0])
			{
				ShowQueryResults();
			}
		}
	}

	public void ReadDiskPressed()
	{
		StartCoroutine(ReadDiskCoroutine());
	}

	IEnumerator ReadDiskCoroutine()
	{
		isReadingDisk = true;
		abortTransfer = false;

		progressBar.value = 0;
		progressValue.text = string.Empty;

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
				drive80TrkToggle.isOn = false;
			}

			// determine if using H8DIMGR2, H37IMGR or H89LDR
			bool h8dImgr = SetDrive();

			int res = 0;
			byte[] cmdBuf = new byte[16];
			byte[] inBuf = new byte[1024];

			/*
			// determine if using H8DIMGR2 or H89LDR
			if (driveDropdown.captionText.text.Equals("SY1"))
			{
				drive80TrkToggle.isOn = true;
				cmdBuf[0] = (byte)'1';
			}
			else
			{
				cmdBuf[0] = (byte)'0';
			}

			// send drive designator (0,1,2)
			serialPort.Write(cmdBuf, 0, 1);
			res = serialPort.ReadByte();

			//Debug.Log(cmdBuf[0].ToString() + " sent reply=" + c.ToString());

			bool h8dImgr = true;
			if (res == '?')
			{
				// client did not understand so using H89LDR
				h8dImgr = false;
				res = '0';
			}
			*/

			string driveDesignator = h37Toggle.isOn ? "DK" : "SY";
			SendToLog("DRIVE " + driveDesignator + (char)res + ": SELECTED");
			
			if (h8dImgr)
            {
				if (driveOverride.isOn)
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
					res = serialPort.ReadByte();

					//Debug.Log(cmdBuf[0].ToString() + " sent reply=" + cmd.ToString());

					if (res == 'Q')
					{
						if (h37Toggle.isOn)
						{
							ShowQueryResults();
							if (drive80TrkToggle.isOn)
                            {
								diskType = driveDSToggle.isOn ? 3 : 2;
                            }
							else
                            {
								diskType = driveDSToggle.isOn ? 1 : 0;
                            }
						}
						else
						{
							diskType = serialPort.ReadByte();

							SendToLog("DISK QUERY RETURNED " + diskType.ToString());

							if (diskType == 1)
							{
								drive80TrkToggle.isOn = false;
								driveDSToggle.isOn = true;
							}
							else if (diskType == 2)
							{
								drive80TrkToggle.isOn = true;
								driveDSToggle.isOn = false;
							}
							else if (diskType == 3)
							{
								drive80TrkToggle.isOn = true;
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
			float trackTime = 0;
			int mins;
			int secs;
			string t = string.Empty;

			readBufIdx = 0;

			trackNumberField.text = track.ToString();

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
						trackNumberField.text = actualTrack.ToString() + "/" + actualSide.ToString() + " [" + n.ToString("D4") + "/" + expectedBytes.ToString() + " " + t + "]";
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
								diskLabelField.text = "NON-HDOS DISK IMAGE";
							}
						}

						// check for handshake byte 'R' or 'r' at 2561
						if (trackBytes == expectedBytes + 1)
						{
							if (readBuf[readBufIdx - 1] == (byte)'R' || readBuf[readBufIdx - 1] == (byte)'r')
							{
								readBufIdx--; // rewind for handshake byte
								SendToLog("RECEIVED TRACK " + actualTrack.ToString() + " SIDE " + actualSide.ToString());
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

	void ShowQueryResults()
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

		driveDSToggle.isOn = (numSides == 2) ? true : false;
		densityToggle.isOn = (density == 0x04) ? true : false;

		int bytes = (hibyte * 256) + lobyte;
		sectorsPerTrackField.text = sectors.ToString() + " / " + h37sectorLen.ToString();
		SendToLog("QUERY RESULTS SECTORS=" + sectors.ToString() + " SIDES=" + numSides.ToString() + " SECTOR SIZE=" + h37sectorLen.ToString() + " TRACK SIZE=" + bytes.ToString() + " DENSITY=" + h37Density);
	}

	public void ReadTrack()
	{
		StartCoroutine(ReadTrackCoroutine());
	}

	IEnumerator ReadTrackCoroutine()
	{
		yield return new WaitForEndOfFrame();

		if (serialPort != null && serialPort.IsOpen)
		{
			//byte[] cmdBuf = new byte[16];
			//cmdBuf[0] = (byte)'Q';
			//serialPort.Write(cmdBuf, 0, 1);
		}
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
			StartCoroutine(SendDiskCoroutine());
		}
	}

	IEnumerator SendDiskCoroutine()
	{
		isWritingDisk = true;

		progressBar.value = 0;
		progressValue.text = string.Empty;

		yield return new WaitForEndOfFrame();

		if (serialPort != null && serialPort.IsOpen)
		{
			DisableButtons();
			clientStatusButton.interactable = false;

			int diskType = 0;
			int imageBytes = sendBuf.Length;

			driveDSToggle.isOn = false;
			drive80TrkToggle.isOn = false;

			int disk1s80t = 2560 * 80;
			int disk2s80t = 2560 * 80 * 2;

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

			byte[] cmdBuf = new byte[16];
			// drive configuration should be: SY0=1S40T, SY1=2S80T
			if (diskType == 0)
			{
				cmdBuf[0] = (byte)'0';
			}
			else
			{
				cmdBuf[0] = (byte)'1';
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

			// set disk volume number on client
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

			int mins = 0;
			int secs = 0;
			string t = string.Empty;

			float trackTime = 0;

			sendBufIdx = 0;
			do
			{
				if (abortTransfer)
				{
					AbortTransferImage();
					yield break;
				}
				cmdBuf[0] = (byte)'W';
				serialPort.Write(cmdBuf, 0, 1);

				yield return new WaitForEndOfFrame();

				sendBufIdx = track * trackSize;
				serialPort.Write(sendBuf, sendBufIdx, trackSize);

				int sides = driveDSToggle.isOn ? 2 : 1;
				int actualSide = (sides == 2) ? (track % sides) + 1 : 1;
				int actualTrack = track / sides;

				while (serialPort.BytesToRead <= 0)
				{
					trackTime += Time.deltaTime;

					mins = (int)trackTime / 60;
					secs = (int)trackTime % 60;
					t = mins.ToString() + ":" + secs.ToString("D2");
					trackNumberField.text = actualTrack.ToString() + "/" + actualSide.ToString() + " [" + t + "]";

					float progress = (float)sendBufIdx / (expectedTracks * 2560);
					progressBar.value = progress;
					progress = Mathf.RoundToInt(progress * 100);
					progressValue.text = progress.ToString("N3") + "%";

					yield return new WaitForEndOfFrame();
				}

				c = serialPort.ReadByte();
				if (c == 'W')
				{
					SendToLog("TRACK " + actualTrack.ToString() + " SIDE " + actualSide.ToString() + " SENT");
					track++;
				}
			} while (track < expectedTracks);

			SendToLog("DISK IMAGE \"" + FilePicker.Instance.GetFileName() + "\" SENT IN " + t);

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
